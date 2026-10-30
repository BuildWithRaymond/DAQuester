using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DAQuester.Core;

public sealed class QuestSupplyParcelOperations(IQuestParcelJournal journal, IQuestParcelReceiptPolicy policy)
    : IQuestSupplyParcelOperations, IQuestSupplyReturnParcelOperations
{
    public bool SupportsReceiveAll(string serverProfile) => policy.SupportsReceiveAll(serverProfile) && policy.Route(serverProfile) is not null;
    public bool SupportsSend(string serverProfile) => policy.SupportsSend(serverProfile, false) && policy.Route(serverProfile) is not null;
    public uint? SendFeeGold(string serverProfile, string item)
        => policy.SupportsSend(serverProfile, false) ? policy.SendFeeGold(serverProfile, item) : null;

    static void Identity(IGameSession session, string character)
    {
        var state = session.Snapshot;
        if (!state.Connected || !state.InventoryKnown || state.Id is null ||
            !string.Equals(state.Name, character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Parcel operations require fresh own-character inventory.");
    }

    async Task OpenAsync(IGameSession session, string profile, CancellationToken cancellation)
    {
        var route = policy.Route(profile) ?? throw new QuestBlockedException("Parcel operations need an observed server profile.");
        var state = session.Snapshot;
        if (state.Map == route.Map && state.Dialog is { } dialog && dialog.Name.Equals(route.Npc, StringComparison.OrdinalIgnoreCase)
            && dialog.Generation == state.Generation && state.Entities.TryGetValue(dialog.EntityId, out var npc) && !npc.IsPlayer)
            return;
        await session.ExecuteAsync(new("interact", Map: route.Map, Text: route.Npc, Target: route.Approach), cancellation);
    }

    public Task<QuestSupplyParcelReceipt> ReceiveAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation)
        => Receive(session, intent, true, cancellation);
    public Task<QuestSupplyParcelReceipt> ReconcileAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation)
        => Receive(session, intent, false, cancellation);

    static string CollectionId(QuestSupplyReceiptIntent intent)
    {
        string claim = JsonSerializer.Serialize(new
        {
            intent.ClientOperationId, intent.Character, intent.ServerProfile, intent.Generation, intent.Revision,
            Inventory = intent.InventoryBaseline.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => new { Item = p.Key.ToUpperInvariant(), p.Value }),
            Units = intent.Units.OrderBy(u => u.Id, StringComparer.Ordinal).Select(u => new { u.Id, u.Item })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(claim)));
    }

    async Task<QuestSupplyParcelReceipt> Receive(IGameSession session, QuestSupplyReceiptIntent intent, bool allowWrites, CancellationToken cancellation)
    {
        Identity(session, intent.Character);
        if (intent.ClientOperationId == Guid.Empty || intent.Units.Length == 0 ||
            intent.Units.Any(u => !u.SourceAccepted || u.Cancelled || string.IsNullOrWhiteSpace(u.Id)) ||
            intent.Units.Select(u => u.Id).Distinct(StringComparer.Ordinal).Count() != intent.Units.Length)
            throw new QuestBlockedException("The parcel manifest does not authorize collection.");
        if (session is not IQuestParcelSession port || !SupportsReceiveAll(intent.ServerProfile))
            throw new QuestBlockedException("Parcel collection needs an observed server profile.");
        await using var lease = await journal.AcquireAsync(intent.ClientOperationId, cancellation);
        string collection = CollectionId(intent);
        var checkpoints = (await journal.LoadAsync(intent.ClientOperationId, cancellation))
            .Where(c => c.Intent.CollectionId == collection).ToList();
        foreach (var saved in checkpoints.ToArray())
        {
            if (saved.Intent.Direction != QuestParcelDirection.Receive || saved.Intent.Character != intent.Character || saved.Intent.ServerProfile != intent.ServerProfile)
                throw new QuestBlockedException("A different parcel operation owns this checkpoint.");
            var restored = await port.ReconcileParcelAsync(saved, journal, policy, cancellation);
            checkpoints[checkpoints.IndexOf(saved)] = restored;
        }
        bool unresolved = checkpoints.Any(c => c.Result is null || c.Uncertain);
        if (!allowWrites || unresolved)
        {
            // Every receive writes this exact claim before its first packet.
            // No inner journal therefore proves that no receive was submitted.
            if (checkpoints.Count == 0)
                return Receipt(session, intent, checkpoints, false) with { Outcome = QuestSupplyCollectionOutcome.Refused };
            return Receipt(session, intent, checkpoints, unresolved);
        }
        if (session.Snapshot.Generation != intent.Generation)
            return Receipt(session, intent, checkpoints, true);

        int maximum = Math.Clamp(policy.MaximumReceiveAttempts, 1, 100);
        for (int attempt = 0; attempt < maximum; attempt++)
        {
            var collected = Receipt(session, intent, checkpoints, false);
            if (collected.Units.Length == intent.Units.Length) return collected;
            if (attempt > 0 && policy.ReceiveSpacing > TimeSpan.Zero)
                await Task.Delay(policy.ReceiveSpacing, cancellation);
            var input = port.InputStamp;
            await OpenAsync(session, intent.ServerProfile, cancellation);
            if (port.InputStamp != input)
                throw new QuestBlockedException("Manual input changed during parcel navigation; no parcel request was submitted.");
            Identity(session, intent.Character);
            if (session.Snapshot.Generation != intent.Generation)
                return Receipt(session, intent, checkpoints, true);
            string id = intent.ClientOperationId.ToString("N") + ":receive:" + collection + ":" + checkpoints.Count;
            var action = new QuestParcelIntent(id, intent.ClientOperationId, "", QuestParcelDirection.Receive,
                intent.Character, intent.ServerProfile, session.Snapshot.Generation) { CollectionId = collection };
            var checkpoint = await port.ReceiveParcelAsync(action, journal, policy, cancellation);
            if (checkpoint.Result is { Outcome: QuestParcelOutcome.Received, Sender: { } sender, Item: { } item } &&
                sender.Equals("Exchanger", StringComparison.OrdinalIgnoreCase))
            {
                var receivedIds = collected.Units.Select(u => u.UnitId).ToHashSet(StringComparer.Ordinal);
                var unit = intent.Units.FirstOrDefault(u => !receivedIds.Contains(u.Id) && u.Item.Equals(item, StringComparison.OrdinalIgnoreCase));
                if (unit is not null)
                {
                    checkpoint = checkpoint with { Intent = checkpoint.Intent with { UnitId = unit.Id, Item = unit.Item } };
                    await journal.SaveAsync(checkpoint, cancellation);
                }
            }
            checkpoints.Add(checkpoint);
            if (checkpoint.Result is not { Outcome: QuestParcelOutcome.Received } || checkpoint.Uncertain)
                return Receipt(session, intent, checkpoints, checkpoint.Result is null || checkpoint.Uncertain);
        }
        return Receipt(session, intent, checkpoints, false);
    }

    static QuestSupplyParcelReceipt Receipt(IGameSession session, QuestSupplyReceiptIntent intent,
        IEnumerable<QuestParcelCheckpoint> checkpoints, bool ambiguous)
    {
        var received = new List<QuestSupplyUnitReceipt>();
        var personal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outcome = QuestSupplyCollectionOutcome.Partial;
        foreach (var checkpoint in checkpoints)
        {
            if (checkpoint.Result is not { } result) { ambiguous = true; continue; }
            if (result.Outcome == QuestParcelOutcome.Received && result.Quantity == 1 && result.Item is { } item)
            {
                var unit = intent.Units.SingleOrDefault(u => u.Id == checkpoint.Intent.UnitId && u.Item.Equals(item, StringComparison.OrdinalIgnoreCase));
                if (unit is not null && result.Sender?.Equals("Exchanger", StringComparison.OrdinalIgnoreCase) == true &&
                    !received.Any(r => r.UnitId == unit.Id)) received.Add(new(unit.Id, unit.Item, "Exchanger"));
                else personal[item] = personal.GetValueOrDefault(item) + 1;
            }
            outcome = result.Outcome switch
            {
                QuestParcelOutcome.Empty => QuestSupplyCollectionOutcome.Empty,
                QuestParcelOutcome.InventoryFull or QuestParcelOutcome.TooHeavy => QuestSupplyCollectionOutcome.CapacityBlocked,
                QuestParcelOutcome.Rejected or QuestParcelOutcome.NotSubmitted => QuestSupplyCollectionOutcome.Refused,
                QuestParcelOutcome.Pending or QuestParcelOutcome.Unknown => QuestSupplyCollectionOutcome.Uncertain,
                _ => outcome
            };
        }
        if (received.Count == intent.Units.Length && !ambiguous) outcome = QuestSupplyCollectionOutcome.Received;
        if (ambiguous) outcome = QuestSupplyCollectionOutcome.Uncertain;
        return new(intent.Character, intent.Generation, received.ToArray(), ambiguous)
        {
            ObservingGeneration = session.Snapshot.Generation, Outcome = outcome,
            PersonalReceived = personal.Select(p => new QuestSupplyLine(p.Key, p.Value)).ToArray()
        };
    }

    public Task<QuestSupplyReturnParcelReceipt> SendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation)
        => Send(session, intent, true, cancellation);
    public Task<QuestSupplyReturnParcelReceipt> ReconcileSendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation)
        => Send(session, intent, false, cancellation);

    async Task<QuestSupplyReturnParcelReceipt> Send(IGameSession session, QuestSupplyReturnParcelIntent intent, bool allowWrites, CancellationToken cancellation)
    {
        Identity(session, intent.Character);
        if (intent.Quantity != 1 || intent.ClientOperationId == Guid.Empty || string.IsNullOrWhiteSpace(intent.UnitId) || string.IsNullOrWhiteSpace(intent.Item))
            throw new QuestBlockedException("A return parcel requires one registered item and a stable unit identity.");
        if (session is not IQuestParcelSession port)
            throw new QuestBlockedException("This session has no parcel capability.");
        await using var lease = await journal.AcquireAsync(intent.ClientOperationId, cancellation);
        string prefix = intent.ClientOperationId.ToString("N") + ":return:" + intent.UnitId + ":";
        var attempts = (await journal.LoadAsync(intent.ClientOperationId, cancellation))
            .Where(c => c.Intent.Direction == QuestParcelDirection.Send && c.Intent.UnitId == intent.UnitId).ToArray();
        var prior = attempts.LastOrDefault();
        string attemptId = prior?.Intent.AttemptId ?? prefix + "0";
        QuestParcelCheckpoint? result = null;
        if (prior is not null)
        {
            if (prior.Intent.Character != intent.Character || prior.Intent.ServerProfile != intent.ServerProfile ||
                prior.Intent.Item != intent.Item || prior.Intent.UnitId != intent.UnitId || prior.Intent.Direction != QuestParcelDirection.Send)
                throw new QuestBlockedException("The saved return parcel belongs to a different manifest.");
            result = await port.ReconcileParcelAsync(prior, journal, policy, cancellation);
        }
        bool notSubmitted = prior is null || result is { Uncertain: false, Result.Outcome: QuestParcelOutcome.NotSubmitted };
        if (!notSubmitted && prior is not null && (prior.Intent.Generation != intent.Generation ||
            prior.Intent.FeeMode != intent.FeeMode || prior.Intent.QuotedFeeGold != intent.QuotedFeeGold || prior.Intent.ReservedGold != intent.ReservedGold))
            throw new QuestBlockedException("The saved return parcel financial intent must be reconciled with its original generation, fee and reserve.");
        if (allowWrites && notSubmitted)
        {
            attemptId = prefix + attempts.Length;
            var item = session.Snapshot.Inventory.Values.Where(i => i.Name.Equals(intent.Item, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Quantity == 1 ? 0 : 1).ThenBy(i => i.Slot).FirstOrDefault();
            if (item is null || !policy.SupportsSend(intent.ServerProfile, item.Quantity > 1) || policy.Route(intent.ServerProfile) is null ||
                !Enum.IsDefined(intent.FeeMode) || session.Snapshot.Generation != intent.Generation
                || intent.FeeMode == QuestParcelFeeMode.LegacyQuoted &&
                    (policy.SendFeeGold(intent.ServerProfile, item.Name) is not { } fee || intent.QuotedFeeGold != fee))
                throw new QuestBlockedException("Return sending needs qualified parcel handling and the original character connection.");
            var input = port.InputStamp;
            await OpenAsync(session, intent.ServerProfile, cancellation);
            if (port.InputStamp != input)
                throw new QuestBlockedException("Manual input changed during parcel navigation; no parcel request was submitted.");
            result = await port.SendParcelAsync(new(attemptId, intent.ClientOperationId, intent.UnitId, QuestParcelDirection.Send,
                intent.Character, intent.ServerProfile, intent.Generation, intent.Item, 1, intent.QuotedFeeGold, intent.ReservedGold)
                { FeeMode = intent.FeeMode }, journal, policy, cancellation);
            notSubmitted = result is { Uncertain: false, Result.Outcome: QuestParcelOutcome.NotSubmitted };
        }
        bool accepted = result is { Uncertain: false, Result.Outcome: QuestParcelOutcome.SourceAccepted };
        return new(intent.Character, intent.Generation, session.Snapshot.Generation, intent.UnitId, intent.Item,
            accepted, !accepted && !notSubmitted, attemptId)
        {
            NotSubmitted = notSubmitted,
            ActualFeeGold = accepted ? result?.Result?.FeeGold : null,
            GoldBefore = accepted ? result?.Before.Gold : null,
            GoldAfter = accepted ? result?.Observations.LastOrDefault()?.State.Gold : null,
            SourceBefore = accepted ? result?.Before : null,
            SourceAfter = accepted ? result?.Observations.LastOrDefault()?.State : null,
            SourceWriteRevision = accepted ? result?.WriteRevision : null,
            SelectedSlot = accepted ? result?.SelectedSlot : null,
            CleanupPending = result?.CleanupPending == true
        };
    }
}
