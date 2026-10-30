namespace DAQuester.Core;

public sealed partial class QuestSupplyIntegration
{
    internal static bool ReturnCheckpointPending(AislingExchangeReturnCheckpoint operation)
        => !operation.Completed || operation.PendingSend is not null
            || operation.SourceReceipts.Any(receipt => receipt.CleanupPending)
            || operation.SentReports.Any(report => !operation.ConfirmedReports.Contains(report.ClientOperationId))
            || operation.SentUnits.Any(id => !operation.SentReports.Any(report => report.UnitIds.Contains(id, StringComparer.Ordinal)));

    static AislingExchangeSupplyJourney ReplaceReturn(AislingExchangeSupplyJourney ledger, AislingExchangeReturnCheckpoint operation)
        => ledger with { ReturnOperations = ledger.ReturnOperations.Select(value => value.Submission.ClientOperationId == operation.Submission.ClientOperationId ? operation : value).ToArray() };

    static bool OriginalReturnProof(QuestSupplyReturnSendCheckpoint pending, QuestSupplyReturnParcelReceipt receipt)
    {
        var intent = pending.Intent;
        if (receipt.SourceBefore is not { } before || receipt.SourceAfter is not { } after
            || receipt.SourceWriteRevision is not { } write || receipt.SelectedSlot is not { } slot
            || before.Character != intent.Character || before.Generation != intent.Generation || before.Id is null
            || after.Character != before.Character || after.Generation != before.Generation || after.Id != before.Id
            || after.Map != before.Map || after.Position != before.Position || before.Revision > write || after.Revision <= write
            || after.GoldRevision <= write || after.GoldRevision > after.Revision
            || before.Gold != pending.GoldBefore || receipt.GoldBefore != before.Gold || receipt.GoldAfter != after.Gold
            || !SameCounts(pending.InventoryBaseline, Counts(before.Inventory))
            || !before.Inventory.TryGetValue(slot, out var selected) || selected.Quantity == 0
            || !selected.Name.Equals(intent.Item, StringComparison.OrdinalIgnoreCase)) return false;
        var expected = before.Inventory.ToBuilder();
        if (selected.Quantity == 1) expected.Remove(slot);
        else expected[slot] = selected with { Quantity = selected.Quantity - 1 };
        return BagSortPlanner.Matches(expected, after.Inventory);
    }

    async Task<(AislingExchangeSupplyJourney Journey, AislingExchangeReturnCheckpoint Operation)> ReconcileReturnCleanupAsync(
        AislingExchangeSupplyJourney ledger, AislingExchangeReturnCheckpoint operation,
        IQuestSupplyReturnParcelOperations parcels, IGameSession session, CancellationToken cancellation)
    {
        foreach (var saved in operation.SourceReceipts.Where(receipt => receipt.CleanupPending).ToArray())
        {
            if (saved.Intent is not { FeeMode: QuestParcelFeeMode.ServerCalculated } intent || saved.SourceBefore is null)
                throw new QuestBlockedException("The proven parcel popup needs its original saved intent before reconciliation.");
            var receipt = await parcels.ReconcileSendAsync(session, intent, cancellation);
            Identity(session.Snapshot, ledger.Character, receipt.ObservingGeneration);
            bool SameState(QuestParcelState? first, QuestParcelState? second) => System.Text.Json.JsonElement.DeepEquals(
                System.Text.Json.JsonSerializer.SerializeToElement(first), System.Text.Json.JsonSerializer.SerializeToElement(second));
            if (!receipt.SourceAccepted || receipt.NotSubmitted || receipt.Ambiguous || receipt.Character != ledger.Character
                || receipt.OriginalGeneration != saved.Generation || receipt.UnitId != saved.UnitId || receipt.Item != saved.Item
                || receipt.AttemptId != saved.AttemptId || receipt.ActualFeeGold != saved.ActualFeeGold
                || receipt.GoldBefore != saved.GoldBefore || receipt.GoldAfter != saved.GoldAfter
                || receipt.SourceWriteRevision != saved.SourceWriteRevision || receipt.SelectedSlot != saved.SelectedSlot
                || !SameState(saved.SourceBefore, receipt.SourceBefore) || !SameState(saved.SourceAfter, receipt.SourceAfter)
                || !OriginalReturnProof(new(intent, Counts(saved.SourceBefore.Inventory), saved.GoldBefore), receipt))
                throw new QuestBlockedException("The saved paid parcel proof changed during popup reconciliation; no new parcel action authorized.");
            if (receipt.CleanupPending)
                throw new QuestBlockedException("The parcel payment is reported; resolve its current popup before continuing automated work.");
            operation = operation with { SourceReceipts = operation.SourceReceipts.Select(value => value.UnitId == saved.UnitId
                ? value with { CleanupPending = false } : value).ToArray() };
            ledger = ReplaceReturn(ledger, operation);
            await Store.SaveAsync(ledger, cancellation);
        }
        return (ledger, operation);
    }

    static AislingExchangeReturnCheckpoint[] ReturnBatches(AislingExchangeSupplyJourney ledger)
    {
        var requests = ledger.Operations.Select(value => value.RequestId).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        if (requests.Length > 32 || ledger.ReturnManifest.Any(line => string.IsNullOrWhiteSpace(line.Item) || line.Quantity <= 0
            || line.Origin is not ("exchanger_supply" or "journey_surplus") || line.Origin == "exchanger_supply" && requests.Length == 0))
            throw new QuestBlockedException("AislingExchange return manifest needs reconciliation before registration.");
        var batches = new List<AislingExchangeReturnCheckpoint>();
        foreach (var origin in ledger.ReturnManifest.GroupBy(line => line.Origin).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var items = new List<QuestSupplyReturnLine>();
            int count = 0;
            void Flush()
            {
                if (items.Count == 0) return;
                batches.Add(new(new(Guid.NewGuid(), ledger.Id, ledger.Character, ledger.ServerProfile, requests, items.ToArray())));
                items.Clear(); count = 0;
            }
            foreach (var line in origin.GroupBy(line => line.Item, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                int remaining = checked(line.Sum(value => value.Quantity));
                while (remaining > 0)
                {
                    if (count == 100 || items.Count == 32) Flush();
                    int quantity = Math.Min(remaining, 100 - count);
                    items.Add(new(line.Key, quantity, origin.Key));
                    count += quantity; remaining -= quantity;
                    if (remaining > 0) Flush();
                }
            }
            Flush();
        }
        return batches.ToArray();
    }

    static void ValidateReturnStatus(AislingExchangeReturnCheckpoint operation, QuestSupplyRequestStatus status)
    {
        var submission = operation.Submission;
        if (status.ClientOperationId != submission.ClientOperationId || status.Character != submission.Character
            || status.ServerProfile != submission.ServerProfile || string.IsNullOrWhiteSpace(status.Id)
            || operation.OperationId is not null && status.Id != operation.OperationId
            || status.State is not ("submitted" or "registered" or "awaiting_player_send" or "awaiting_exchanger_receive" or "collecting" or "banking" or "complete" or "partial" or "review" or "cancelled" or "rejected")
            || status.Units is null || status.Units.Any(unit => unit is null || string.IsNullOrWhiteSpace(unit.Id)
                || unit.Cancelled && (unit.SourceAccepted || unit.RecipientObserved || unit.Collected || unit.Banked || unit.ClientReported)
                || unit.Banked && !unit.Collected || unit.RecipientObserved && !unit.SourceAccepted
                || !submission.Items.Any(line => line.Item.Equals(unit.Item, StringComparison.OrdinalIgnoreCase)))
            || status.Units.Select(unit => unit.Id).Distinct(StringComparer.Ordinal).Count() != status.Units.Length
            || status.Units.GroupBy(unit => unit.Item, StringComparer.OrdinalIgnoreCase).Any(group =>
                group.Count() > submission.Items.Single(line => line.Item.Equals(group.Key, StringComparison.OrdinalIgnoreCase)).Quantity)
            || operation.Units.Any(old => !status.Units.Any(unit => unit.Id == old.Id && unit.Item == old.Item
                && (!old.SourceAccepted || unit.SourceAccepted) && (!old.Cancelled || unit.Cancelled)
                && (!old.RecipientObserved || unit.RecipientObserved) && (!old.Collected || unit.Collected)
                && (!old.Banked || unit.Banked) && (!old.ClientReported || unit.ClientReported)))
            || operation.SentUnits.Any(id => !status.Units.Any(unit => unit.Id == id && !unit.Cancelled))
            || operation.PendingSend is { } pending && status.Units.Any(unit => unit.Id == pending.Intent.UnitId && unit.Cancelled)
            || status.State == "complete" && (status.Units.Length != submission.Items.Sum(line => line.Quantity) || status.Units.Any(unit => !unit.Banked && !unit.Cancelled))
            || status.State == "rejected" && (operation.SentUnits.Length > 0 || operation.PendingSend is not null || status.Units.Any(unit => unit.SourceAccepted || unit.Collected || unit.Banked)))
            throw new QuestBlockedException("AislingExchange return status does not match its saved manifest; reconciliation required.");
    }

    async Task<(AislingExchangeSupplyJourney Journey, AislingExchangeReturnCheckpoint Operation)> FlushSentReportsAsync(
        AislingExchangeSupplyJourney ledger, AislingExchangeReturnCheckpoint operation, IQuestSupplyReturnProvider returns, CancellationToken cancellation)
    {
        var uncovered = operation.SentUnits.Where(id => !operation.SentReports.Any(report => report.UnitIds.Contains(id, StringComparer.Ordinal))).ToArray();
        if (uncovered.Length > 0)
        {
            var report = new QuestSupplyUnitReport(Guid.NewGuid(), operation.Submission.ClientOperationId,
                operation.OperationId ?? throw new QuestBlockedException("AislingExchange returns need their original registration identity."),
                ledger.Character, ledger.ServerProfile, uncovered);
            operation = operation with { SentReports = [.. operation.SentReports, report] };
            ledger = ReplaceReturn(ledger, operation);
            await Store.SaveAsync(ledger, cancellation);
        }
        foreach (var report in operation.SentReports.Where(report => !operation.ConfirmedReports.Contains(report.ClientOperationId)).ToArray())
        {
            if (report.ClientOperationId == Guid.Empty || report.OperationClientId != operation.Submission.ClientOperationId
                || report.OperationId != operation.OperationId || report.Character != ledger.Character || report.ServerProfile != ledger.ServerProfile
                || report.UnitIds is null || report.UnitIds.Length == 0 || report.UnitIds.Distinct(StringComparer.Ordinal).Count() != report.UnitIds.Length
                || report.UnitIds.Any(id => !operation.SentUnits.Contains(id, StringComparer.Ordinal)))
                throw new QuestBlockedException("AislingExchange saved return report needs reconciliation.");
            QuestSupplyRequestStatus status;
            try { status = await returns.ReportReturnSentAsync(report, cancellation); }
            catch (OperationCanceledException) { throw; }
            catch { throw new QuestBlockedException("AislingExchange return report awaits confirmation. Resume retries its saved report without sending again."); }
            ValidateReturnStatus(operation with { Units = [] }, status);
            if (status.State == "submitted" || operation.Units.Any(old => !status.Units.Any(unit => unit.Id == old.Id && unit.Item == old.Item))
                || report.UnitIds.Any(id => !status.Units.Any(unit => unit.Id == id && unit.ClientReported)))
                throw new QuestBlockedException("AislingExchange has not confirmed the saved return report; no parcel will be sent again.");
            // Keep the latest validated worker status: an idempotent report
            // acknowledgement may be older than Find's collection/bank proof.
            operation = operation with { Units = operation.Units.Select(unit => report.UnitIds.Contains(unit.Id, StringComparer.Ordinal)
                ? unit with { ClientReported = true } : unit).ToArray(), ConfirmedReports = [.. operation.ConfirmedReports, report.ClientOperationId] };
            ledger = ReplaceReturn(ledger, operation);
            await Store.SaveAsync(ledger, cancellation);
        }
        return (ledger, operation);
    }

    public async Task ProcessReturnsAsync(IGameSession session, IReadOnlyDictionary<string, int> reservations,
        uint reservedGold, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var ledger = Load(session.Snapshot.Name ?? "", JourneyId);
        if (ledger is null || !ledger.Ended || ledger.ReturnManifest.Length == 0) return;
        using var dispatchLease = ReturnDispatchLease?.Invoke();
        if (Provider is not IQuestSupplyReturnProvider returns || Parcels is not IQuestSupplyReturnParcelOperations parcelReturns)
            throw new QuestBlockedException("AislingExchange returns await supported registration and parcel handling; leftovers remain saved.");
        async Task Ready()
        {
            if (!Provider.Enabled || !parcelReturns.SupportsSend(ledger.ServerProfile))
                throw new QuestBlockedException("AislingExchange returns await opt-in and qualified parcel handling; no item sent.");
            bool ready;
            try { ready = await returns.ReturnsAvailableAsync(cancellation); }
            catch (OperationCanceledException) { throw; }
            catch { throw new QuestBlockedException("AislingExchange return readiness is unavailable; no item sent."); }
            if (!ready) throw new QuestBlockedException("AislingExchange returns are not ready; leftovers remain saved.");
        }
        if (ledger.ReturnOperations.Length == 0)
        {
            Identity(session.Snapshot, ledger.Character, ledger.Generation);
            if (ledger.AttributionUncertain || !SameCounts(ledger.LastInventory, Counts(session.Snapshot)))
                throw new QuestBlockedException("AislingExchange return attribution changed before registration; leftovers need reconciliation.");
            await Ready();
            ledger = ledger with { ReturnOperations = ReturnBatches(ledger) };
            // Freeze every registration identity and manifest before the first POST.
            await Store.SaveAsync(ledger, cancellation);
        }
        foreach (var saved in ledger.ReturnOperations.Where(ReturnCheckpointPending).ToArray())
        {
            var operation = saved;
            QuestSupplyRequestStatus? status;
            try { status = await returns.FindReturnAsync(operation.Submission.ClientOperationId, cancellation); }
            catch (OperationCanceledException) { throw; }
            catch { throw new QuestBlockedException("AislingExchange returns await their original registration; no replacement return created."); }
            if (status is null)
            {
                if (operation.OperationId is not null || operation.SentUnits.Length > 0 || operation.PendingSend is not null)
                    throw new QuestBlockedException("AislingExchange return registration remains uncertain; its checkpoint is retained.");
                await Ready();
                try { status = await returns.RegisterReturnAsync(operation.Submission, cancellation); }
                catch (OperationCanceledException) { throw; }
                catch { throw new QuestBlockedException("AislingExchange return registration may be committed. Resume finds the saved registration before sending."); }
            }
            ValidateReturnStatus(operation, status);
            operation = operation with { OperationId = status.Id, State = status.State, Units = status.Units };
            ledger = ReplaceReturn(ledger, operation);
            await Store.SaveAsync(ledger, cancellation);
            (ledger, operation) = await FlushSentReportsAsync(ledger, operation, returns, cancellation);
            (ledger, operation) = await ReconcileReturnCleanupAsync(ledger, operation, parcelReturns, session, cancellation);
            if (operation.State is "submitted" or "review")
                throw new QuestBlockedException("AislingExchange worker has not authorized this return; no item sent.");
            if (operation.State is "rejected" or "cancelled")
            {
                if (operation.PendingSend is not null || operation.Units.Any(unit => !unit.Cancelled && !unit.Banked) && operation.State != "rejected")
                    throw new QuestBlockedException("AislingExchange return cancellation needs reconciliation.");
                operation = operation with { Completed = true };
                ledger = ReplaceReturn(ledger, operation);
                await Store.SaveAsync(ledger, cancellation);
                continue;
            }
            if (operation.Units.Length != operation.Submission.Items.Sum(line => line.Quantity))
                throw new QuestBlockedException("AislingExchange worker has not reserved the complete return manifest.");
            foreach (var unit in operation.Units.Where(unit => !unit.Cancelled && !operation.SentUnits.Contains(unit.Id, StringComparer.Ordinal))
                .OrderBy(unit => unit.Id == operation.PendingSend?.Intent.UnitId ? 0 : 1).ToArray())
            {
                bool reconcile = operation.PendingSend is not null;
                if (reconcile && operation.PendingSend!.Intent.UnitId != unit.Id)
                    throw new QuestBlockedException("Reconcile the saved return parcel before sending another unit.");
                if (!reconcile)
                {
                    if (unit.SourceAccepted || unit.Collected || unit.Banked || unit.ClientReported)
                        throw new QuestBlockedException("AislingExchange return already has remote receipts without matching local proof; reconcile before any send.");
                    await Ready();
                    var before = session.Snapshot;
                    Identity(before, ledger.Character, ledger.Generation);
                    if (!Provider.Enabled || !parcelReturns.SupportsSend(ledger.ServerProfile))
                        throw new QuestBlockedException("AislingExchange return sending is no longer enabled or qualified; no item sent.");
                    var liveReserves = ReturnReserves?.Invoke();
                    uint goldReserve = Math.Max(reservedGold, liveReserves?.Gold ?? 0);
                    int reserve = Math.Max(Math.Max(Count(ledger.Reserved, unit.Item), Count(reservations, unit.Item)),
                        liveReserves is null ? 0 : Count(liveReserves.Items, unit.Item));
                    if (ledger.AttributionUncertain || !SameCounts(ledger.LastInventory, Counts(before))
                        || before.Count(unit.Item) <= checked(Count(ledger.PersonalBaseline, unit.Item) + reserve)
                        || before.Gold is not { } gold || gold <= goldReserve)
                        throw new QuestBlockedException("AislingExchange return would touch reserved or uncertain items or gold; no item sent.");
                    var intent = new QuestSupplyReturnParcelIntent(operation.Submission.ClientOperationId, unit.Id, ledger.Character,
                        ledger.ServerProfile, before.Generation, unit.Item, ReservedGold: goldReserve)
                        { FeeMode = QuestParcelFeeMode.ServerCalculated };
                    operation = operation with { PendingSend = new(intent, Counts(before), gold) };
                    ledger = ReplaceReturn(ledger, operation);
                    await Store.SaveAsync(ledger, cancellation);
                }
                if (!reconcile)
                {
                    var pendingBeforeWrite = operation.PendingSend!;
                    var current = session.Snapshot;
                    var latestReserves = ReturnReserves?.Invoke();
                    int reserve = Math.Max(Math.Max(Count(ledger.Reserved, unit.Item), Count(reservations, unit.Item)),
                        latestReserves is null ? 0 : Count(latestReserves.Items, unit.Item));
                    uint goldReserve = Math.Max(pendingBeforeWrite.Intent.ReservedGold, latestReserves?.Gold ?? 0);
                    if (!Provider.Enabled || !parcelReturns.SupportsSend(ledger.ServerProfile)
                        || !current.Connected || !current.InventoryKnown || current.Name != ledger.Character
                        || current.Generation != pendingBeforeWrite.Intent.Generation || !SameCounts(pendingBeforeWrite.InventoryBaseline, Counts(current))
                        || current.Gold != pendingBeforeWrite.GoldBefore || current.Count(unit.Item) <= checked(Count(ledger.PersonalBaseline, unit.Item) + reserve)
                        || pendingBeforeWrite.GoldBefore <= goldReserve)
                    {
                        // No physical port was invoked for this new intent.
                        // Release it instead of acting on changed reservations.
                        operation = operation with { PendingSend = null };
                        ledger = ReplaceReturn(ledger, operation);
                        await Store.SaveAsync(ledger, cancellation);
                        throw new QuestBlockedException("AislingExchange return authorization or reserves changed before submission; no item sent.");
                    }
                }
                var pending = operation.PendingSend!;
                QuestSupplyReturnParcelReceipt receipt;
                try
                {
                    receipt = reconcile
                        ? await parcelReturns.ReconcileSendAsync(session, pending.Intent, cancellation)
                        : await parcelReturns.SendAsync(session, pending.Intent, cancellation);
                }
                catch (OperationCanceledException) { throw; }
                catch { throw new QuestBlockedException("AislingExchange return send is uncertain. Resume reconciles its saved intent without sending again."); }
                var after = session.Snapshot;
                Identity(after, ledger.Character, receipt.ObservingGeneration);
                bool matchingReceipt = receipt.Character == ledger.Character && receipt.OriginalGeneration == pending.Intent.Generation
                    && receipt.UnitId == unit.Id && receipt.Item == unit.Item && !string.IsNullOrWhiteSpace(receipt.AttemptId);
                if (receipt.NotSubmitted && !receipt.Ambiguous && !receipt.SourceAccepted && matchingReceipt
                    && SameCounts(pending.InventoryBaseline, Counts(after)) && after.Gold == pending.GoldBefore)
                {
                    operation = operation with { PendingSend = null };
                    ledger = ReplaceReturn(ledger, operation);
                    await Store.SaveAsync(ledger, cancellation);
                    throw new QuestBlockedException("AislingExchange return was not submitted. Resolve the refusal, then Resume for a new send.");
                }
                var expected = new Dictionary<string, int>(pending.InventoryBaseline, StringComparer.OrdinalIgnoreCase);
                int remaining = Count(expected, unit.Item) - 1;
                if (remaining == 0) expected.Remove(unit.Item); else expected[unit.Item] = remaining;
                uint? actualFee = pending.Intent.FeeMode switch
                {
                    QuestParcelFeeMode.ServerCalculated => receipt.ActualFeeGold,
                    QuestParcelFeeMode.LegacyQuoted => pending.Intent.QuotedFeeGold,
                    _ => null
                };
                bool serverCalculated = pending.Intent.FeeMode == QuestParcelFeeMode.ServerCalculated;
                uint? originalGoldAfter = serverCalculated ? receipt.GoldAfter : after.Gold;
                bool actualGoldProof = actualFee is { } cost && pending.GoldBefore >= cost
                    && originalGoldAfter == pending.GoldBefore - cost
                    && (receipt.ActualFeeGold is null || receipt.ActualFeeGold == cost)
                    && (receipt.GoldBefore is null || receipt.GoldBefore == pending.GoldBefore)
                    && (receipt.GoldAfter is null || receipt.GoldAfter == originalGoldAfter)
                    && (!serverCalculated || OriginalReturnProof(pending, receipt));
                bool currentMatches = after.Generation == pending.Intent.Generation
                    && SameCounts(expected, Counts(after)) && after.Gold == originalGoldAfter;
                if (receipt.Ambiguous || receipt.NotSubmitted || !receipt.SourceAccepted || !matchingReceipt
                    || (!serverCalculated && !SameCounts(expected, Counts(after)))
                    || !actualGoldProof)
                    throw new QuestBlockedException("AislingExchange return source acceptance is not proven. The send checkpoint remains saved.");
                var returned = new Dictionary<string, int>(ledger.Returned, StringComparer.OrdinalIgnoreCase);
                returned[unit.Item] = checked(Count(returned, unit.Item) + 1);
                var sourceReceipt = new QuestSupplyReturnSourceReceipt(unit.Id, unit.Item, receipt.AttemptId,
                    pending.Intent.Generation, pending.GoldBefore, originalGoldAfter!.Value, actualFee!.Value)
                {
                    Intent = pending.Intent,
                    SourceBefore = receipt.SourceBefore, SourceAfter = receipt.SourceAfter,
                    SourceWriteRevision = receipt.SourceWriteRevision, SelectedSlot = receipt.SelectedSlot, CleanupPending = receipt.CleanupPending
                };
                operation = operation with { SentUnits = [.. operation.SentUnits, unit.Id],
                    SourceReceipts = [.. operation.SourceReceipts, sourceReceipt], PendingSend = null };
                bool historical = serverCalculated && (!currentMatches || receipt.CleanupPending);
                ledger = ReplaceReturn(ledger with
                {
                    Returned = returned, LastInventory = historical ? ledger.LastInventory : Counts(after),
                    Generation = historical ? ledger.Generation : after.Generation,
                    AttributionUncertain = ledger.AttributionUncertain || historical,
                    ReturnStatus = historical ? "ReconciliationRequired" : ledger.ReturnStatus
                }, operation);
                // Persist committed item and actual postage before reporting,
                // including a charge crossing the stop-before-next-send reserve.
                await Store.SaveAsync(ledger, serverCalculated ? CancellationToken.None : cancellation);
                (ledger, operation) = await FlushSentReportsAsync(ledger, operation, returns, cancellation);
                (ledger, operation) = await ReconcileReturnCleanupAsync(ledger, operation, parcelReturns, session, cancellation);
            }
            operation = operation with { Completed = operation.PendingSend is null && operation.Units.All(unit => unit.Cancelled || unit.Banked) };
            ledger = ReplaceReturn(ledger, operation);
            await Store.SaveAsync(ledger, cancellation);
        }
        bool complete = ledger.ReturnOperations.All(operation => !ReturnCheckpointPending(operation));
        string state = complete && ledger.ReturnOperations.All(operation => operation.State == "rejected") ? "Rejected"
            : complete && ledger.ReturnOperations.All(operation => operation.State == "cancelled") ? "Cancelled"
            : complete && ledger.ReturnOperations.Any(operation => operation.State is "rejected" or "cancelled" || operation.Units.Any(unit => unit.Cancelled)) ? "Partial"
            : complete ? "Complete"
            : ledger.ReturnOperations.Any(operation => !operation.Completed && operation.Units.Any(unit => !unit.Cancelled && !operation.SentUnits.Contains(unit.Id, StringComparer.Ordinal))) ? "AwaitingSend"
            : ledger.ReturnOperations.Any(operation => !operation.Completed && operation.Units.Any(unit => !unit.Cancelled && !unit.Collected)) ? "AwaitingCollection"
            : "AwaitingBankDeposit";
        await Store.SaveAsync(ledger with { ReturnStatus = state }, cancellation);
    }
}
