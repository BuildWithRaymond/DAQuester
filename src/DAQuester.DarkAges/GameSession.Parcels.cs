using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession : IQuestParcelSession
{
    sealed record ParcelAuthority(CharacterSnapshot Owner, long Manual, long BagInput);
    QuestParcelCheckpoint? pendingParcelCleanup;
    public QuestParcelInputStamp InputStamp => new(connection.ManualOperationRevision, connection.ManualBagOperationRevision);

    public Task<QuestParcelCheckpoint> ReceiveParcelAsync(QuestParcelIntent intent, IQuestParcelJournal journal,
        IQuestParcelReceiptPolicy policy, CancellationToken cancellation)
    {
        if (intent.Direction != QuestParcelDirection.Receive) throw new QuestBlockedException("A receive intent is required.");
        return ExecuteParcel(intent, journal, policy, cancellation);
    }

    public Task<QuestParcelCheckpoint> SendParcelAsync(QuestParcelIntent intent, IQuestParcelJournal journal,
        IQuestParcelReceiptPolicy policy, CancellationToken cancellation)
    {
        if (intent.Direction != QuestParcelDirection.Send) throw new QuestBlockedException("A send intent is required.");
        return ExecuteParcel(intent, journal, policy, cancellation);
    }

    void RequireParcelOwner(QuestParcelIntent intent, ParcelAuthority authority, QuestParcelRoute route,
        uint npcId, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var state = Snapshot;
        var owner = authority.Owner;
        var movement = connection.Movement;
        if (!state.Connected || !state.InventoryKnown || state.Id is null || state.Name != intent.Character
            || state.Generation != intent.Generation || state.Generation != owner.Generation || state.Id != owner.Id
            || state.Map != route.Map || state.Position is null || state.Position != owner.Position
            || state.Gold is null || state.Weight is null || state.MaxWeight is null
            || state.Exchange is { Cancelled: false } || state.Exchange != owner.Exchange
            || movement.Pending != 0 || movement.Recovery || pendingEquipmentRemoval is not null
            || entryBlock is not null || townWeaponRemovalRequired
            || !state.Entities.TryGetValue(npcId, out var npc) || npc.IsPlayer || npc.Kind != 2
            || !npc.Name.Equals(route.Npc, StringComparison.OrdinalIgnoreCase)
            || Math.Abs(state.Position.X - npc.Position.X) + Math.Abs(state.Position.Y - npc.Position.Y) > 3
            || connection.ManualOperationRevision != authority.Manual || connection.ManualBagOperationRevision != authority.BagInput)
            throw new QuestBlockedException("The parcel character, NPC, inventory or another action changed. Reconcile before another request.");
    }

    static bool SameCounts(IReadOnlyDictionary<byte, Item> before, IReadOnlyDictionary<byte, Item> after, string? item = null, int delta = 0)
    {
        var names = before.Values.Select(i => i.Name).Concat(after.Values.Select(i => i.Name)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (string name in names)
        {
            long first = before.Values.Where(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(i => (long)i.Quantity);
            long last = after.Values.Where(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(i => (long)i.Quantity);
            if (last != first + (name.Equals(item, StringComparison.OrdinalIgnoreCase) ? delta : 0)) return false;
        }
        return item is null || after.Values.Any(i => i.Name.Equals(item, StringComparison.OrdinalIgnoreCase)) || delta < 0;
    }

    static QuestParcelReply? ProveParcel(QuestParcelCheckpoint checkpoint, IQuestParcelReceiptPolicy policy)
    {
        if (!checkpoint.WriteIntent || checkpoint.Interference || checkpoint.Observations.Length == 0) return null;
        var latest = checkpoint.Observations[^1].State;
        var before = checkpoint.Before;
        if (latest.Generation != before.Generation || latest.Character != before.Character || latest.Id != before.Id ||
            latest.Map != before.Map || latest.Position != before.Position || latest.Gold is null || before.Gold is null) return null;
        foreach (var observation in checkpoint.Observations)
        {
            if (observation.State.Generation != before.Generation || observation.State.Character != before.Character ||
                observation.State.Id != before.Id || observation.State.Revision <= checkpoint.WriteRevision) continue;
            var reply = policy.Interpret(observation, checkpoint);
            if (reply is null || reply.Revision <= checkpoint.WriteRevision) continue;
            bool trusted = reply.Source switch
            {
                QuestParcelReplySource.ActiveStatus => observation.ActiveStatus is { Type: 3 } status &&
                    status.Generation == before.Generation && status.Revision == reply.Revision,
                QuestParcelReplySource.NpcDialog => observation.State.Dialog is { } dialog && dialog.Generation == before.Generation &&
                    dialog.EntityId == checkpoint.NpcId && dialog.Name == checkpoint.NpcName && dialog.Revision == reply.Revision,
                _ => false
            };
            if (!trusted) continue;
            if (reply.Outcome is QuestParcelOutcome.Empty or QuestParcelOutcome.InventoryFull or QuestParcelOutcome.TooHeavy or QuestParcelOutcome.Rejected)
            {
                if (BagSortPlanner.Matches(before.Inventory, latest.Inventory) && latest.Gold == before.Gold) return reply;
            }
            else if (checkpoint.Intent.Direction == QuestParcelDirection.Receive && checkpoint.Stage == "Receive" &&
                reply.Outcome == QuestParcelOutcome.Received && reply.Quantity == 1 && !string.IsNullOrWhiteSpace(reply.Sender) &&
                !string.IsNullOrWhiteSpace(reply.Item) && reply.FeeGold == checkpoint.Intent.QuotedFeeGold &&
                SameCounts(before.Inventory, latest.Inventory, reply.Item, 1) &&
                (ulong)latest.Gold.Value + checkpoint.Intent.QuotedFeeGold == before.Gold.Value)
                return reply;
            else if (checkpoint.Intent.Direction == QuestParcelDirection.Send && checkpoint.Stage == "Recipient" &&
                reply.Outcome == QuestParcelOutcome.SourceAccepted && checkpoint.SelectedSlot is { } slot &&
                before.Inventory.TryGetValue(slot, out var selected) && checkpoint.Intent.Item == selected.Name)
            {
                uint actual;
                if (checkpoint.Intent.FeeMode == QuestParcelFeeMode.ServerCalculated)
                {
                    // A bag/dialog event carries cached gold. Even a free send
                    // needs a new independently received gold-bearing status.
                    if (latest.GoldRevision <= checkpoint.WriteRevision || latest.Gold.Value > before.Gold.Value) continue;
                    actual = before.Gold.Value - latest.Gold.Value;
                    if (reply.FeeGold is { } stated && stated != actual) continue;
                }
                else if (checkpoint.Intent.FeeMode == QuestParcelFeeMode.LegacyQuoted &&
                    (reply.FeeGold is null || reply.FeeGold == checkpoint.Intent.QuotedFeeGold) &&
                    (ulong)latest.Gold.Value + checkpoint.Intent.QuotedFeeGold == before.Gold.Value)
                    actual = checkpoint.Intent.QuotedFeeGold;
                else continue;
                var expected = before.Inventory.ToBuilder();
                if (selected.Quantity == 1) expected.Remove(slot); else expected[slot] = selected with { Quantity = selected.Quantity - 1 };
                if (BagSortPlanner.Matches(expected, latest.Inventory)) return reply with { FeeGold = actual };
            }
        }
        return null;
    }

    static ClientPacket ParcelPacket(uint npc, ushort action, byte? slot = null)
    {
        var builder = new NetworkPacketBuilder(ClientCommand.Merchant);
        try
        {
            builder.AppendByte(1); builder.AppendUInt32(npc); builder.AppendUInt16(action);
            if (slot is { } itemSlot)
            {
                builder.AppendByte(2); builder.AppendByte(itemSlot); builder.AppendByte(0x31); builder.AppendString8("Exchanger");
            }
            return (ClientPacket)builder.ToPacket();
        }
        finally { builder.Dispose(); }
    }

    async Task<QuestParcelCheckpoint> ExecuteParcel(QuestParcelIntent intent, IQuestParcelJournal journal,
        IQuestParcelReceiptPolicy policy, CancellationToken cancellation)
    {
        var authority = new ParcelAuthority(Snapshot, connection.ManualOperationRevision, connection.ManualBagOperationRevision);
        if (intent.Quantity != 1 || intent.ClientOperationId == Guid.Empty || string.IsNullOrWhiteSpace(intent.AttemptId)
            || !Enum.IsDefined(intent.FeeMode)
            || intent.ServerProfile != maps.ServerProfile || policy.Route(intent.ServerProfile) is not { } route ||
            intent.Direction == QuestParcelDirection.Receive && !policy.SupportsReceiveAll(intent.ServerProfile))
            throw new QuestBlockedException("Parcel operations need exact-profile qualification and a stable one-unit intent.");
        await actions.WaitAsync(cancellation);
        QuestParcelCheckpoint? checkpoint = null;
        QuestParcelCheckpoint? provenSend = null;
        var observed = new ConcurrentQueue<QuestParcelObservation>();
        var pulse = new SemaphoreSlim(0);
        using var conflict = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, conflict.Token);
        bool attached = false, financialWriteStarted = false, interference = false;
        void Observe(CharacterSnapshot state) { observed.Enqueue(QuestParcelObservation.From(state)); pulse.Release(); }
        void Manual(byte opcode, byte[] payload, bool bot)
        {
            if (!bot && (connection.ManualOperationRevision != authority.Manual || connection.ManualBagOperationRevision != authority.BagInput))
            {
                interference = true;
                try { conflict.Cancel(); } catch (ObjectDisposedException) { }
            }
        }
        void InterruptParcel(string reason)
        {
            try { conflict.Cancel(); } catch (ObjectDisposedException) { }
        }
        async Task Save(CancellationToken token)
        {
            if (checkpoint is null) return;
            var observations = checkpoint.Observations.ToList();
            while (observed.TryDequeue(out var next)) observations.Add(next);
            checkpoint = checkpoint with { Observations = observations.ToArray(), Interference = checkpoint.Interference || interference };
            await journal.SaveAsync(checkpoint, token);
        }
        async Task Write(string stage, ClientPacket packet, Dialog expectedDialog)
        {
            RequireParcelOwner(intent, authority, route, checkpoint!.NpcId, linked.Token);
            var expected = Snapshot;
            if (!BagSortPlanner.Matches(expected.Inventory, checkpoint.Before.Inventory) || expected.Gold != checkpoint.Before.Gold)
                throw new QuestBlockedException("The reserved parcel inventory or gold changed before submission.");
            if (checkpoint.WrittenStages.Contains(stage, StringComparer.Ordinal))
                throw new QuestBlockedException("This parcel stage was already submitted. Reconcile without repeating it.");
            checkpoint = checkpoint with { Stage = stage, WriteIntent = true, Submitted = false, WriteRevision = expected.Revision,
                WrittenStages = [.. checkpoint.WrittenStages, stage] };
            await Save(linked.Token);
            void Validate()
            {
                RequireParcelOwner(intent, authority, route, checkpoint.NpcId, linked.Token);
                var current = Snapshot;
                if (current.Revision != expected.Revision || current.Dialog != expectedDialog || current.Gold != expected.Gold ||
                    !BagSortPlanner.Matches(current.Inventory, expected.Inventory))
                    throw new QuestBlockedException("The parcel page or reserved inventory changed before its request.");
                if (intent.Direction == QuestParcelDirection.Send && (checkpoint.SelectedSlot is not { } slot
                    || !checkpoint.Before.Inventory.TryGetValue(slot, out var selectedItem)
                    || !policy.SupportsSend(intent.ServerProfile, selectedItem.Quantity > 1)
                    || intent.FeeMode == QuestParcelFeeMode.LegacyQuoted && policy.SendFeeGold(intent.ServerProfile, selectedItem.Name) != intent.QuotedFeeGold))
                    throw new QuestBlockedException("The selected item's qualified parcel fee changed before submission.");
            }
            await connection.SendAsync(packet, intent.Generation, linked.Token, validateSend: Validate, beforeWrite: () =>
            {
                Validate();
                if (stage is "Receive" or "Recipient") financialWriteStarted = true;
            });
            checkpoint = checkpoint with { Submitted = true };
            await Save(linked.Token);
        }
        try
        {
            var prior = (await journal.LoadAsync(intent.ClientOperationId, linked.Token)).SingleOrDefault(c => c.Intent.AttemptId == intent.AttemptId);
            if (prior is not null)
            {
                if (prior.Intent != intent)
                    throw new QuestBlockedException("The saved parcel intent cannot be relabelled. Reconcile its original identity and fee mode.");
                return await ReconcileParcelAsync(prior, journal, policy, linked.Token);
            }
            if (pendingParcelCleanup is not null)
                throw new QuestBlockedException("The proven parcel's popup cleanup needs read-only reconciliation before another parcel action.");
            var state = Snapshot;
            if (state.Dialog is not { IsPursuit: false } menu || menu.Generation != state.Generation || menu.EntityType != 1 ||
                !menu.Name.Equals(route.Npc, StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException("Open the observed parcel NPC's current menu first.");
            RequireParcelOwner(intent, authority, route, menu.EntityId, linked.Token);
            var selected = intent.Direction == QuestParcelDirection.Send
                ? state.Inventory.Values.Where(i => i.Name == intent.Item).OrderBy(i => i.Quantity == 1 ? 0 : 1).ThenBy(i => i.Slot).FirstOrDefault() : null;
            if (intent.Direction == QuestParcelDirection.Send && (selected is null || !policy.SupportsSend(intent.ServerProfile, selected.Quantity > 1)
                || (intent.FeeMode == QuestParcelFeeMode.LegacyQuoted
                    ? policy.SendFeeGold(intent.ServerProfile, selected.Name) is not { } fee || fee != intent.QuotedFeeGold
                        || (ulong)state.Gold!.Value < (ulong)fee + intent.ReservedGold
                    : state.Gold!.Value <= intent.ReservedGold)
                || selected.Slot is < 1 or > 59))
                throw new QuestBlockedException("The return item, qualified parcel handling or available postage funds need reconciliation.");
            checkpoint = new(intent, QuestParcelState.From(state), menu.EntityId, menu.Name) { SelectedSlot = selected?.Slot };
            await journal.SaveAsync(checkpoint, linked.Token);
            Changed += Observe; connection.ClientObserved += Manual; Interrupted += InterruptParcel; attached = true;
            var started = Stopwatch.StartNew();
            var timeout = policy.ReplyTimeout > TimeSpan.Zero && policy.ReplyTimeout <= TimeSpan.FromSeconds(30) ? policy.ReplyTimeout : TimeSpan.FromSeconds(30);
            if (intent.Direction == QuestParcelDirection.Receive) await Write("Receive", ParcelPacket(menu.EntityId, 0x64), menu);
            else await Write("Recipient", ParcelPacket(menu.EntityId, 0x63, selected!.Slot), menu);
            while (true)
            {
                RequireParcelOwner(intent, authority, route, menu.EntityId, linked.Token);
                await Save(linked.Token);
                if (ProveParcel(checkpoint, policy) is { } proof)
                {
                    checkpoint = checkpoint with { Result = proof, Uncertain = false };
                    if (intent.Direction == QuestParcelDirection.Send && intent.FeeMode == QuestParcelFeeMode.ServerCalculated
                        && proof.Outcome == QuestParcelOutcome.SourceAccepted)
                    {
                        // Freeze the financial proof before optional UI cleanup.
                        // Later manual input/cancellation must never replace it.
                        provenSend = checkpoint = checkpoint with { CleanupPending = true };
                        pendingParcelCleanup = provenSend;
                        await journal.SaveAsync(checkpoint, linked.Token);
                        RequireParcelOwner(intent, authority, route, menu.EntityId, linked.Token);
                        if (Snapshot.Dialog is { } parcelDialog && parcelDialog.EntityId == menu.EntityId && parcelDialog.Name == menu.Name)
                            await CloseDialog(parcelDialog, linked.Token);
                        var cleaned = checkpoint with { CleanupPending = false };
                        await journal.SaveAsync(cleaned, linked.Token);
                        pendingParcelCleanup = null;
                        return cleaned;
                    }
                    await journal.SaveAsync(checkpoint, linked.Token);
                    if (Snapshot.Dialog is { } current && current.EntityId == menu.EntityId && current.Name == menu.Name)
                        await CloseDialog(current, linked.Token);
                    await Save(linked.Token);
                    return checkpoint;
                }
                var remaining = timeout - started.Elapsed;
                if (remaining <= TimeSpan.Zero || !await pulse.WaitAsync(remaining, linked.Token))
                {
                    checkpoint = checkpoint with { Uncertain = true, Failure = "Parcel receipt timed out; no request was repeated." };
                    await Save(CancellationToken.None);
                    return checkpoint;
                }
            }
        }
        catch
        {
            if (provenSend is not null)
            {
                var settled = provenSend with { Failure = "Parcel payment is proven; popup cleanup needs reconciliation." };
                await journal.SaveAsync(settled, CancellationToken.None);
                return settled;
            }
            if (checkpoint is not null)
            {
                bool knownUnsent = !financialWriteStarted && !interference;
                checkpoint = checkpoint with
                {
                    WriteIntent = knownUnsent ? false : checkpoint.WriteIntent,
                    Submitted = knownUnsent ? false : checkpoint.Submitted,
                    Result = knownUnsent ? new(QuestParcelOutcome.NotSubmitted, QuestParcelReplySource.ActiveStatus, Snapshot.Revision) : checkpoint.Result,
                    Uncertain = !knownUnsent, Interference = checkpoint.Interference || interference,
                    Failure = "Parcel exchange stopped. Reconcile its journal before another request."
                };
                await Save(CancellationToken.None);
            }
            throw;
        }
        finally
        {
            if (attached) { Changed -= Observe; connection.ClientObserved -= Manual; Interrupted -= InterruptParcel; }
            actions.Release();
        }
    }

    public async Task<QuestParcelCheckpoint> ReconcileParcelAsync(QuestParcelCheckpoint checkpoint, IQuestParcelJournal journal,
        IQuestParcelReceiptPolicy policy, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var current = Snapshot;
        if (!current.Connected || !current.InventoryKnown || current.Name != checkpoint.Intent.Character ||
            maps.ServerProfile != checkpoint.Intent.ServerProfile)
            throw new QuestBlockedException("Parcel reconciliation needs fresh own-character state on its original server profile.");
        if (checkpoint is { WriteIntent: false, Submitted: false, Uncertain: false, Interference: false, Result.Outcome: QuestParcelOutcome.NotSubmitted })
            return checkpoint;
        // Server-calculated proof uses only its original saved observations.
        // A later gold update cannot supply a missing action-specific receipt.
        // Legacy fixed-quote reconciliation retains its original behavior.
        var result = ProveParcel(checkpoint, policy);
        if (result is null && checkpoint.Intent.FeeMode == QuestParcelFeeMode.LegacyQuoted
            && current.Generation == checkpoint.Before.Generation && checkpoint.WriteIntent)
        {
            checkpoint = checkpoint with { Observations = [.. checkpoint.Observations, QuestParcelObservation.From(current)] };
            result = ProveParcel(checkpoint, policy);
        }
        checkpoint = checkpoint with { Result = result, Uncertain = result is null };
        if (checkpoint.Intent.FeeMode == QuestParcelFeeMode.ServerCalculated
            && result?.Outcome == QuestParcelOutcome.SourceAccepted && checkpoint.CleanupPending)
        {
            bool absent = current.Dialog is null || current.Generation == checkpoint.Before.Generation
                && current.Dialog is { IsPursuit: false } page && dismissedMerchantRevision == page.Revision;
            if (absent)
                checkpoint = checkpoint with { CleanupPending = false };
            else pendingParcelCleanup ??= checkpoint;
        }
        await journal.SaveAsync(checkpoint, cancellation);
        // Release the in-memory gate only after the same cleanup metadata has
        // been acknowledged durably, including a retry after a lost save ACK.
        if (checkpoint.Intent.FeeMode == QuestParcelFeeMode.ServerCalculated
            && result?.Outcome == QuestParcelOutcome.SourceAccepted && !checkpoint.CleanupPending
            && pendingParcelCleanup?.Intent.AttemptId == checkpoint.Intent.AttemptId
            && pendingParcelCleanup.Intent.ClientOperationId == checkpoint.Intent.ClientOperationId)
            pendingParcelCleanup = null;
        return checkpoint;
    }
}
