namespace DAQuester.Core;

public sealed partial class QuestSupplyIntegration
{
    static bool CollectionReportsPending(AislingExchangeSupplyCheckpoint operation)
        => operation.ReceiptReports.Any(report => !operation.ConfirmedReports.Contains(report.ClientOperationId))
            || operation.ReceivedUnits.Any(id => !operation.ReceiptReports.Any(report => report.UnitIds.Contains(id, StringComparer.Ordinal)));

    static void ValidateSupplyStatus(AislingExchangeSupplyCheckpoint operation, QuestSupplyRequestStatus status)
    {
        var submission = operation.Submission;
        if (status.ClientOperationId != submission.ClientOperationId || status.Character != submission.Character
            || status.ServerProfile != submission.ServerProfile || string.IsNullOrWhiteSpace(status.Id)
            || operation.RequestId is not null && status.Id != operation.RequestId
            || status.State is not ("submitted" or "reserved" or "processing" or "sent" or "received" or "partial" or "cancelled" or "rejected" or "review")
            || status.Units is null || status.Units.Any(unit => unit is null || string.IsNullOrWhiteSpace(unit.Id)
                || unit.SourceAccepted && unit.Cancelled || unit.Collected || unit.Banked
                || unit.RecipientObserved && !unit.SourceAccepted
                || !submission.Items.Any(item => item.Item.Equals(unit.Item, StringComparison.OrdinalIgnoreCase)))
            || status.Units.Select(unit => unit.Id).Distinct(StringComparer.Ordinal).Count() != status.Units.Length
            || status.Units.GroupBy(unit => unit.Item, StringComparer.OrdinalIgnoreCase).Any(group =>
                group.Count() > submission.Items.Single(item => item.Item.Equals(group.Key, StringComparison.OrdinalIgnoreCase)).Quantity)
            || operation.Units.Any(old => !status.Units.Any(unit => unit.Id == old.Id && unit.Item == old.Item
                && (!old.SourceAccepted || unit.SourceAccepted) && (!old.Cancelled || unit.Cancelled)
                && (!old.ClientReported || unit.ClientReported) && (!old.RecipientObserved || unit.RecipientObserved)))
            || operation.ReceivedUnits.Any(id => !status.Units.Any(unit => unit.Id == id && unit.SourceAccepted))
            || status.State == "rejected" && (operation.ReceivedUnits.Length != 0 || status.Units.Any(unit => unit.SourceAccepted)))
            throw new QuestBlockedException("AislingExchange supply status does not match the saved request; reconciliation required.");
    }

    static bool ReceiptCountsMatch(CharacterSnapshot after, QuestSupplyReceiptIntent intent, QuestSupplyParcelReceipt receipt)
    {
        var expected = new Dictionary<string, int>(intent.InventoryBaseline, StringComparer.OrdinalIgnoreCase);
        foreach (var unit in receipt.Units)
            expected[unit.Item] = checked(Count(expected, unit.Item) + 1);
        foreach (var item in receipt.PersonalReceived)
            expected[item.Item] = checked(Count(expected, item.Item) + item.Quantity);
        return SameCounts(expected, Counts(after));
    }

    async Task<(AislingExchangeSupplyJourney Journey, AislingExchangeSupplyCheckpoint Operation)> FlushCollectionReportsAsync(
        AislingExchangeSupplyJourney ledger, AislingExchangeSupplyCheckpoint operation, CancellationToken cancellation)
    {
        var uncovered = operation.ReceivedUnits.Where(id => !operation.ReceiptReports.Any(report => report.UnitIds.Contains(id, StringComparer.Ordinal))).ToArray();
        if (uncovered.Length > 0)
        {
            var report = new QuestSupplyUnitReport(Guid.NewGuid(), operation.Submission.ClientOperationId,
                operation.RequestId ?? throw new QuestBlockedException("AislingExchange collection needs its original request identity."),
                ledger.Character, ledger.ServerProfile, uncovered);
            operation = operation with { ReceiptReports = [.. operation.ReceiptReports, report], Resolved = false };
            ledger = Replace(ledger, operation);
            // Persist both local receipt credit and the immutable HTTP mutation
            // identity before any report transmission, including migration.
            await Store.SaveAsync(ledger, cancellation);
        }
        foreach (var report in operation.ReceiptReports.Where(report => !operation.ConfirmedReports.Contains(report.ClientOperationId)).ToArray())
        {
            if (report.ClientOperationId == Guid.Empty || report.OperationClientId != operation.Submission.ClientOperationId
                || report.OperationId != operation.RequestId || report.Character != ledger.Character || report.ServerProfile != ledger.ServerProfile
                || report.UnitIds is null || report.UnitIds.Length == 0 || report.UnitIds.Distinct(StringComparer.Ordinal).Count() != report.UnitIds.Length
                || report.UnitIds.Any(id => !operation.ReceivedUnits.Contains(id, StringComparer.Ordinal)))
                throw new QuestBlockedException("AislingExchange saved collection report needs reconciliation.");
            QuestSupplyRequestStatus confirmed;
            try { confirmed = await Provider.ReportReceiptsAsync(report, cancellation); }
            catch (OperationCanceledException) { throw; }
            catch { throw new QuestBlockedException("AislingExchange collection report is awaiting confirmation. Its saved report will be reconciled without collecting again."); }
            // Mutation replay returns the projection cached when this report
            // committed. It can predate a newer Find response. Validate its
            // identity/manifest and acknowledgement without rolling that newer
            // worker evidence back to the historical projection.
            ValidateSupplyStatus(operation with { Units = [] }, confirmed);
            if (operation.Units.Any(old => !confirmed.Units.Any(unit => unit.Id == old.Id && unit.Item == old.Item))
                || report.UnitIds.Any(id => !confirmed.Units.Any(unit => unit.Id == id && unit.ClientReported && unit.SourceAccepted)))
                throw new QuestBlockedException("AislingExchange has not confirmed the saved collection report; no parcel will be received again.");
            operation = operation with { Units = operation.Units.Select(unit => report.UnitIds.Contains(unit.Id, StringComparer.Ordinal)
                    ? unit with { ClientReported = true } : unit).ToArray(),
                ConfirmedReports = [.. operation.ConfirmedReports, report.ClientOperationId] };
            ledger = Replace(ledger, operation);
            await Store.SaveAsync(ledger, cancellation);
        }
        return (ledger, operation);
    }
}
