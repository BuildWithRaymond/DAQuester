namespace DAQuester.Core;

public sealed partial class QuestSupplyIntegration(IQuestSupplyProvider provider, IQuestSupplyConsent consent,
    IQuestSupplyJourneyStore store, IQuestSupplyParcelOperations? parcels = null, Guid? journeyId = null)
{
    public IQuestSupplyProvider Provider { get; } = provider;
    public IQuestSupplyConsent Consent { get; } = consent;
    public IQuestSupplyJourneyStore Store { get; } = store;
    public IQuestSupplyParcelOperations Parcels { get; } = parcels ?? UnsupportedQuestSupplyParcelOperations.Instance;
    public Guid? JourneyId { get; private set; } = journeyId;
    public Func<QuestSupplyReturnReserves>? ReturnReserves { get; init; }
    public Func<IDisposable>? ReturnDispatchLease { get; init; }

    static Dictionary<string, int> Counts(CharacterSnapshot snapshot) => Counts(snapshot.Inventory);
    static Dictionary<string, int> Counts(IReadOnlyDictionary<byte, Item> inventory) => inventory.Values
        .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => checked((int)g.Sum(i => (long)i.Quantity)), StringComparer.OrdinalIgnoreCase);

    static int Count(IReadOnlyDictionary<string, int> values, string item)
        => values.FirstOrDefault(p => p.Key.Equals(item, StringComparison.OrdinalIgnoreCase)).Value;

    static string PreparationKey(IReadOnlyList<QuestSupply> supplies) => string.Join("|", supplies
        .OrderBy(s => s.Quest, StringComparer.Ordinal).ThenBy(s => s.Task.Id, StringComparer.Ordinal)
        .Select(s => s.Quest + ":" + s.Task.Id + ":" + s.Task.Item + ":" + s.Task.Quantity));

    static QuestSupplyLine[] Missing(CharacterSnapshot snapshot, IReadOnlyList<QuestSupply> supplies) => supplies
        .GroupBy(s => s.Task.Item!, StringComparer.OrdinalIgnoreCase)
        .Select(g => new QuestSupplyLine(g.Key, Math.Max(0, checked(g.Sum(s => s.Task.Quantity)) - snapshot.Count(g.Key))))
        .Where(i => i.Quantity > 0).ToArray();

    static void Identity(CharacterSnapshot current, string character, long generation)
    {
        if (!current.Connected || !current.InventoryKnown || current.Generation != generation ||
            !string.Equals(current.Name, character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("AislingExchange supplies need fresh own-character state before continuing.");
    }

    AislingExchangeSupplyJourney? Load(string character, Guid? id)
    {
        if (id is null) return null;
        var value = Store.Load(character, id.Value);
        if (value is not null && (value.Id != id || !value.Character.Equals(character, StringComparison.OrdinalIgnoreCase)))
            throw new QuestBlockedException("AislingExchange supply checkpoint identity does not match this journey.");
        return value;
    }

    public async Task PrepareAsync(QuestContext context, IReadOnlyList<QuestSupply> supplies, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var snapshot = context.Session.Snapshot;
        string character = context.Job.Character;
        string profile = context.Calibration.ServerProfile;
        var id = JourneyId ?? context.Job.SupplyJourneyId;
        AislingExchangeSupplyJourney? previous = null;
        if (JourneyId is not null && context.Job.SupplyJourneyId is { } previousId && previousId != JourneyId)
        {
            previous = Load(character, previousId);
            if (previous is not null && (!previous.Ended || previous.Operations.Any(o => !o.Resolved || CollectionReportsPending(o))))
                throw new QuestBlockedException("An earlier AislingExchange supply journey still owns a saved continuation.");
        }
        var ledger = Load(character, id);
        if (ledger is not null && ledger.ServerProfile != profile)
            throw new QuestBlockedException("AislingExchange supply checkpoint belongs to another game server.");
        bool pendingNormalWork = context.Job.Pending is not null;
        if (pendingNormalWork && ledger?.Operations.Any(o => !o.Resolved) == true)
            throw new QuestBlockedException("Reconcile the saved quest and supply transactions before collection.");
        var priorReturns = Store.PendingReturns(character, profile).Concat(new[] { previous, ledger }
            .OfType<AislingExchangeSupplyJourney>().Where(value => value.Ended && value.ReturnManifest.Length > 0)).ToArray();
        // Validate every discovered identity before applying readiness filters.
        // A fresh queue ID does not erase duties referenced by an older job.
        foreach (var prior in priorReturns)
            if (prior.Character != character || prior.ServerProfile != profile || !prior.Ended || prior.Id == Guid.Empty)
                throw new QuestBlockedException("AislingExchange saved return identity needs reconciliation.");
        priorReturns = priorReturns.DistinctBy(value => value.Id).ToArray();
        if (pendingNormalWork)
        {
            foreach (var prior in priorReturns.Where(value => value.ReturnOperations.Length == 0 && !value.AttributionUncertain))
                await Store.SaveAsync(prior with { AttributionUncertain = true, ReturnStatus = "ReconciliationRequired" }, cancellation);
            return;
        }
        if (ledger is not null)
        {
            JourneyId = ledger.Id;
            if (context.Job.SupplyJourneyId != ledger.Id)
                await context.SaveAsync(context.Job with { SupplyJourneyId = ledger.Id });
            foreach (var operation in ledger.Operations.Where(o => !o.Resolved || CollectionReportsPending(o)).ToArray())
            {
                QuestSupplyRequestStatus? status;
                try { status = await Provider.FindAsync(operation.Submission.ClientOperationId, cancellation); }
                catch (OperationCanceledException) { throw; }
                catch { throw new QuestBlockedException("AislingExchange supplies await request reconciliation; no replacement supplies acquired."); }
                if (status is null)
                    throw new QuestBlockedException("AislingExchange supply request remains uncertain; no replacement supplies acquired.");
                ledger = await ReconcileAsync(context, ledger, operation, status, cancellation);
            }
        }
        // A later quest cannot erase a prior committed return. Recovery runs
        // under this runner's existing owner and never retries a physical send.
        var reserves = ReturnReserves?.Invoke() ?? new QuestSupplyReturnReserves(supplies
            .GroupBy(supply => supply.Task.Item!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => checked(group.Sum(supply => supply.Task.Quantity)), StringComparer.OrdinalIgnoreCase), context.SupplyGoldReserve);
        foreach (var prior in priorReturns)
        {
            if (prior.ReturnOperations.Length == 0 && (prior.AttributionUncertain || !Provider.Enabled
                || Parcels is not IQuestSupplyReturnParcelOperations returnPort
                || !returnPort.SupportsSend(profile)))
            {
                // Normal work may consume or replace old surplus even when the
                // old ID is only discoverable, or readiness currently blocks it.
                if (!prior.AttributionUncertain)
                    await Store.SaveAsync(prior with { AttributionUncertain = true, ReturnStatus = "ReconciliationRequired" }, cancellation);
                continue;
            }
            if (prior.ReturnOperations.Length > 0 && !prior.ReturnOperations.Any(ReturnCheckpointPending)) continue;
            var recovery = new QuestSupplyIntegration(Provider, Consent, Store, Parcels, prior.Id)
            { ReturnReserves = ReturnReserves, ReturnDispatchLease = ReturnDispatchLease };
            await recovery.ProcessReturnsAsync(context.Session, reserves.Items, reserves.Gold, cancellation);
        }
        // Recovery may have registered and sent from the current ended journey.
        // Never overwrite those new durable checkpoints with this older object.
        ledger = Load(character, id);
        if (ledger is { Ended: true })
        {
            if (ledger.ReturnManifest.Length > 0 && ledger.ReturnOperations.Length == 0 && !ledger.AttributionUncertain)
            {
                // New normal work may consume or replace these items. A later
                // equal count cannot reconstruct the old unregistered surplus.
                ledger = ledger with { AttributionUncertain = true, ReturnStatus = "ReconciliationRequired" };
                await Store.SaveAsync(ledger, cancellation);
            }
            // The old record remains discoverable for AE collection/bank updates.
            // Only the next material offer creates the new journey checkpoint.
            JourneyId = id = Guid.NewGuid();
            ledger = null;
        }
        snapshot = context.Session.Snapshot;
        if (!Provider.Enabled || !Parcels.SupportsReceiveAll(profile)) return;
        Identity(snapshot, character, snapshot.Generation);
        var missing = Missing(context.Session.Snapshot, supplies);
        if (missing.Length == 0) return;
        if (ledger is null)
        {
            JourneyId = id ?? Guid.NewGuid();
            ledger = new(JourneyId.Value, character, profile, Counts(snapshot))
            {
                LastInventory = Counts(snapshot), Generation = snapshot.Generation
            };
            await Store.SaveAsync(ledger, cancellation);
            await context.SaveAsync(context.Job with { SupplyJourneyId = ledger.Id });
        }
        if (ledger.Ended)
            throw new QuestBlockedException("The previous AislingExchange supply journey has ended; retain its return checkpoint.");
        string preparation = PreparationKey(supplies);
        if (ledger.ConsideredPreparations.Contains(preparation, StringComparer.Ordinal)) return;
        var offer = ledger.AwaitingConsent;
        if (offer is not null && (offer.Generation != snapshot.Generation || offer.Quote.ExpiresAt <= DateTimeOffset.UtcNow ||
            ledger.OfferedInventory is null || !SameCounts(ledger.OfferedInventory, Counts(snapshot))))
        {
            ledger = ledger with { AwaitingConsent = null, OfferedInventory = null };
            await Store.SaveAsync(ledger, cancellation);
            offer = null;
        }
        if (offer is null)
        {
            QuestSupplyQuote? quote;
            try { quote = await Provider.QuoteAsync(new(character, profile, ledger.Id, missing), cancellation); }
            catch (OperationCanceledException) { throw; }
            catch { return; }
            if (!ValidQuote(quote, character, profile, missing)) return;
            offer = new(quote!, snapshot.Generation);
            ledger = ledger with { AwaitingConsent = offer, OfferedInventory = Counts(snapshot) };
            await Store.SaveAsync(ledger, cancellation);
        }
        bool? accepted = await Consent.AcceptAsync(offer, cancellation);
        if (accepted is null)
            throw new QuestBlockedException("AislingExchange supplies await your decision. Use supplies or gather/buy normally, then Resume.");
        if (!accepted.Value)
        {
            await Store.SaveAsync(ledger with { AwaitingConsent = null, OfferedInventory = null,
                DeclinedDemand = missing, ConsideredPreparations = [.. ledger.ConsideredPreparations, preparation] }, cancellation);
            return;
        }
        if (!Provider.Enabled)
        {
            await Store.SaveAsync(ledger with { AwaitingConsent = null, OfferedInventory = null,
                ConsideredPreparations = [.. ledger.ConsideredPreparations, preparation] }, cancellation);
            return;
        }
        Identity(context.Session.Snapshot, character, offer.Generation);
        if (offer.Quote.ExpiresAt <= DateTimeOffset.UtcNow || !SameCounts(ledger.OfferedInventory!, Counts(context.Session.Snapshot)) ||
            !ValidQuote(offer.Quote, character, profile, Missing(context.Session.Snapshot, supplies)))
            throw new QuestBlockedException("AislingExchange supply offer changed. Resume for a fresh offer before shipment.");
        var submission = new QuestSupplySubmission(Guid.NewGuid(), ledger.Id, character, profile, offer.Quote.Id,
            offer.Quote.Items.Select(i => i with { }).ToArray());
        var checkpoint = new AislingExchangeSupplyCheckpoint(submission);
        ledger = ledger with { AwaitingConsent = null, OfferedInventory = null,
            ConsideredPreparations = [.. ledger.ConsideredPreparations, preparation], Operations = [.. ledger.Operations, checkpoint] };
        // This durable boundary precedes the first HTTP mutation, including a lost response.
        await Store.SaveAsync(ledger, cancellation);
        if (!Provider.Enabled)
        {
            await Store.SaveAsync(Replace(ledger, checkpoint with { State = "cancelled", Resolved = true }), cancellation);
            return;
        }
        QuestSupplyRequestStatus submitted;
        try { submitted = await Provider.SubmitAsync(submission, cancellation); }
        catch (OperationCanceledException) { throw; }
        catch { throw new QuestBlockedException("AislingExchange supply request may be committed. Resume to reconcile the saved request."); }
        await ReconcileAsync(context, ledger, checkpoint, submitted, cancellation);
    }

    static bool SameCounts(IReadOnlyDictionary<string, int> first, IReadOnlyDictionary<string, int> second)
        => first.Count == second.Count && first.All(p => Count(second, p.Key) == p.Value);

    static bool ValidQuote(QuestSupplyQuote? quote, string character, string profile, QuestSupplyLine[] missing)
        => quote is not null && !string.IsNullOrWhiteSpace(quote.Id) && quote.Character.Equals(character, StringComparison.OrdinalIgnoreCase) &&
            quote.ServerProfile == profile && quote.ExpiresAt > DateTimeOffset.UtcNow && quote.Items.Length is > 0 and <= 64 &&
            quote.Items.Select(i => i.Item).Distinct(StringComparer.OrdinalIgnoreCase).Count() == quote.Items.Length &&
            quote.Items.All(i => i.Quantity > 0 && missing.Any(m => m.Item.Equals(i.Item, StringComparison.OrdinalIgnoreCase) && i.Quantity <= m.Quantity));

    static AislingExchangeSupplyJourney Replace(AislingExchangeSupplyJourney ledger, AislingExchangeSupplyCheckpoint operation)
        => ledger with { Operations = ledger.Operations.Select(o => o.Submission.ClientOperationId == operation.Submission.ClientOperationId ? operation : o).ToArray() };

    async Task<AislingExchangeSupplyJourney> ReconcileAsync(QuestContext context, AislingExchangeSupplyJourney ledger,
        AislingExchangeSupplyCheckpoint operation, QuestSupplyRequestStatus status, CancellationToken cancellation)
    {
        var submission = operation.Submission;
        ValidateSupplyStatus(operation, status);
        operation = operation with { RequestId = status.Id, State = status.State, Units = status.Units };
        ledger = Replace(ledger, operation);
        await Store.SaveAsync(ledger, cancellation);
        (ledger, operation) = await FlushCollectionReportsAsync(ledger, operation, cancellation);
        var collect = operation.Units.Where(u => u.SourceAccepted && !operation.ReceivedUnits.Contains(u.Id, StringComparer.Ordinal)).ToArray();
        if (collect.Length > 0)
        {
            if (!Parcels.SupportsReceiveAll(ledger.ServerProfile))
                throw new QuestBlockedException("AislingExchange supplies await qualified parcel collection; the shipment remains saved.");
            var snapshot = context.Session.Snapshot;
            Identity(snapshot, ledger.Character, snapshot.Generation);
            bool reconcileOnly = operation.ReceiptIntent is not null;
            var intent = operation.ReceiptIntent ?? new(submission.ClientOperationId, ledger.Character, ledger.ServerProfile,
                snapshot.Generation, snapshot.Revision, Counts(snapshot), collect);
            operation = operation with { ReceiptIntent = intent };
            ledger = Replace(ledger, operation);
            await Store.SaveAsync(ledger, cancellation);
            QuestSupplyParcelReceipt receipt;
            try
            {
                receipt = reconcileOnly
                    ? await Parcels.ReconcileAsync(context.Session, intent, cancellation)
                    : await Parcels.ReceiveAsync(context.Session, intent, cancellation);
            }
            catch (OperationCanceledException) { throw; }
            catch { throw new QuestBlockedException("AislingExchange parcel response is uncertain. Reconcile the saved collection; do not collect again."); }
            var after = context.Session.Snapshot;
            Identity(after, ledger.Character, receipt.ObservingGeneration ?? intent.Generation);
            if (receipt.Ambiguous || receipt.Outcome == QuestSupplyCollectionOutcome.Uncertain || receipt.Character != ledger.Character
                || receipt.Generation != intent.Generation || receipt.Units is null || receipt.PersonalReceived is null ||
                receipt.Units.Length == 0 && receipt.Outcome is not (QuestSupplyCollectionOutcome.Empty or QuestSupplyCollectionOutcome.CapacityBlocked or QuestSupplyCollectionOutcome.Refused) ||
                receipt.Units.Select(u => u.UnitId).Distinct(StringComparer.Ordinal).Count() != receipt.Units.Length ||
                receipt.Units.Any(r => !r.Sender.Equals("Exchanger", StringComparison.OrdinalIgnoreCase) || !intent.Units.Any(u => u.Id == r.UnitId && u.Item == r.Item)) ||
                receipt.PersonalReceived.Any(item => string.IsNullOrWhiteSpace(item.Item) || item.Quantity <= 0)
                || !ReceiptCountsMatch(after, intent, receipt))
                throw new QuestBlockedException("AislingExchange parcel arrival is not proven. The collection checkpoint remains saved.");
            var credited = new Dictionary<string, int>(ledger.ProviderReceived, StringComparer.OrdinalIgnoreCase);
            foreach (var item in receipt.Units.GroupBy(u => u.Item, StringComparer.OrdinalIgnoreCase))
                credited[item.Key] = checked(Count(credited, item.Key) + item.Count());
            var personal = new Dictionary<string, int>(ledger.PersonalBaseline, StringComparer.OrdinalIgnoreCase);
            foreach (var item in receipt.PersonalReceived)
                personal[item.Item] = checked(Count(personal, item.Item) + item.Quantity);
            operation = operation with { ReceivedUnits = [.. operation.ReceivedUnits, .. receipt.Units.Select(u => u.UnitId)], ReceiptIntent = null };
            ledger = Replace(ledger with { ProviderReceived = credited, PersonalBaseline = personal,
                AttributionUncertain = ledger.AttributionUncertain || ledger.Generation != after.Generation
                    || !SameCounts(ledger.LastInventory, intent.InventoryBaseline),
                LastInventory = Counts(after), Generation = after.Generation }, operation);
            await Store.SaveAsync(ledger, cancellation);
            (ledger, operation) = await FlushCollectionReportsAsync(ledger, operation, cancellation);
            if (receipt.Outcome is QuestSupplyCollectionOutcome.Empty or QuestSupplyCollectionOutcome.CapacityBlocked or QuestSupplyCollectionOutcome.Refused)
                throw new QuestBlockedException(receipt.Outcome == QuestSupplyCollectionOutcome.Empty
                    ? "AislingExchange parcels have not arrived yet. Resume for a new mailbox check."
                    : "AislingExchange collection stopped at the mailbox. Make room or resolve its refusal, then Resume.");
        }
        int expected = submission.Items.Sum(i => i.Quantity);
        bool settled = operation.Units.Length == expected && operation.Units.All(u => u.Cancelled || operation.ReceivedUnits.Contains(u.Id, StringComparer.Ordinal));
        bool rejected = operation.State == "rejected" && operation.Units.All(u => !u.SourceAccepted) && operation.ReceivedUnits.Length == 0;
        if (!settled && !rejected)
            throw new QuestBlockedException("AislingExchange supplies are still pending. Resume to reconcile before gathering replacements.");
        operation = operation with { Resolved = true };
        ledger = Replace(ledger, operation);
        await Store.SaveAsync(ledger, cancellation);
        return ledger;
    }

    public async Task ObserveInventoryAsync(QuestContext context, bool acquisition, CancellationToken cancellation, string? acquiredItem = null)
    {
        var ledger = Load(context.Job.Character, JourneyId ?? context.Job.SupplyJourneyId);
        if (ledger is null) return;
        var snapshot = context.Session.Snapshot;
        Identity(snapshot, ledger.Character, snapshot.Generation);
        var current = Counts(snapshot);
        var consumed = new Dictionary<string, int>(ledger.Consumed, StringComparer.OrdinalIgnoreCase);
        var acquired = new Dictionary<string, int>(ledger.JourneyAcquired, StringComparer.OrdinalIgnoreCase);
        var personal = new Dictionary<string, int>(ledger.PersonalBaseline, StringComparer.OrdinalIgnoreCase);
        foreach (string item in current.Keys.Concat(ledger.LastInventory.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            int delta = Count(current, item) - Count(ledger.LastInventory, item);
            if (delta < 0) consumed[item] = checked(Count(consumed, item) - delta);
            if (delta > 0)
            {
                var destination = acquisition && item.Equals(acquiredItem, StringComparison.OrdinalIgnoreCase) ? acquired : personal;
                destination[item] = checked(Count(destination, item) + delta);
            }
        }
        await Store.SaveAsync(ledger with { PersonalBaseline = personal, JourneyAcquired = acquired, Consumed = consumed,
            LastInventory = current, AttributionUncertain = ledger.AttributionUncertain || snapshot.Generation != ledger.Generation,
            Generation = snapshot.Generation }, cancellation);
    }

    public async Task EndJourneyAsync(CharacterSnapshot snapshot, IReadOnlyDictionary<string, int> reservations, CancellationToken cancellation)
    {
        var ledger = Load(snapshot.Name ?? "", JourneyId);
        if (ledger is null) return;
        // An ended journey owns its immutable return manifest and registrations.
        // Re-entry only resumes those operations; it must never recompute them.
        if (ledger.Ended) return;
        Identity(snapshot, ledger.Character, snapshot.Generation);
        if (ledger.Operations.Any(o => !o.Resolved || CollectionReportsPending(o)) || ledger.AttributionUncertain || ledger.Generation != snapshot.Generation)
        {
            await Store.SaveAsync(ledger with { ReturnStatus = "ReconciliationRequired" }, cancellation);
            return;
        }
        var manifest = new List<QuestSupplyReturnLine>();
        foreach (string item in ledger.ProviderReceived.Keys.Concat(ledger.JourneyAcquired.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            int capacity = Math.Max(0, snapshot.Count(item) - Count(ledger.PersonalBaseline, item) - Count(reservations, item));
            int supplied = Math.Max(0, Count(ledger.ProviderReceived, item) - Count(ledger.Consumed, item) - Count(ledger.Returned, item));
            int providerReturn = Math.Min(capacity, supplied);
            if (providerReturn > 0) manifest.Add(new(item, providerReturn, "exchanger_supply"));
            int remainingConsumption = Math.Max(0, Count(ledger.Consumed, item) - Count(ledger.ProviderReceived, item));
            int gathered = Math.Max(0, Count(ledger.JourneyAcquired, item) - remainingConsumption - Math.Max(0, Count(ledger.Returned, item) - Count(ledger.ProviderReceived, item)));
            int surplusReturn = Math.Min(capacity - providerReturn, gathered);
            if (surplusReturn > 0) manifest.Add(new(item, surplusReturn, "journey_surplus"));
        }
        await Store.SaveAsync(ledger with { ReturnManifest = manifest.ToArray(), Reserved = new(reservations, StringComparer.OrdinalIgnoreCase),
            ReturnStatus = manifest.Count == 0 ? "Complete" : "AwaitingRegistration", Ended = true }, cancellation);
    }
}
