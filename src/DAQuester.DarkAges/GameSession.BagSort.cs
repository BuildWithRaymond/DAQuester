using System.Collections.Immutable;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Client.Types;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    sealed record BagSortAuthority(CharacterSnapshot Owner, long Manual, long BagInput);
    sealed record PendingBagSwap(long Generation, long Baseline, BagSlotSwap Swap,
        ImmutableDictionary<byte, Item> Before, ImmutableDictionary<byte, Item> After);
    sealed record BagSwapReceipt(PendingBagSwap Pending)
    {
        public TaskCompletionSource Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    PendingBagSwap? pendingBagSwap;

    BagSortAuthority CaptureBagSort(GameCommand command)
    {
        var owner = Snapshot;
        if (command.ExpectedGeneration is not { } generation || generation != owner.Generation)
            throw new QuestBlockedException("Bag sorting requires the current character connection.");
        return new(owner, connection.ManualOperationRevision, connection.ManualBagOperationRevision);
    }

    void RequireBagSortOwner(BagSortAuthority authority, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var current = Snapshot;
        var owner = authority.Owner;
        var movement = connection.Movement;
        if (!current.Connected || current.Name is null || current.Id is null || !current.InventoryKnown
            || current.Generation != owner.Generation || current.Id != owner.Id
            || !string.Equals(current.Name, owner.Name, StringComparison.OrdinalIgnoreCase)
            || current.Dialog is not null || current.Exchange is { Cancelled: false }
            || current.Exchange != owner.Exchange || movement.Pending != 0 || movement.Recovery
            || pendingEquipmentRemoval is not null || entryBlock is not null || townWeaponRemovalRequired
            || connection.ManualOperationRevision != authority.Manual
            || connection.ManualBagOperationRevision != authority.BagInput)
            throw new QuestBlockedException("Bag sorting stopped because the character, bag or another action changed.");
    }

    void RequireExpectedBag(BagSortAuthority authority, IReadOnlyDictionary<byte, Item> expected, CancellationToken cancellation)
    {
        RequireBagSortOwner(authority, cancellation);
        if (!BagSortPlanner.Matches(Snapshot.Inventory, expected))
            throw new QuestBlockedException("The bag changed before its next move. Sort again after fresh inventory.");
    }

    static bool SameBagItem(Item? left, Item? right) => left is null ? right is null : right is not null
        && left.Name == right.Name && left.Quantity == right.Quantity && left.Sprite == right.Sprite && left.Stackable == right.Stackable;

    static bool ExpectedSwapTransition(IReadOnlyDictionary<byte, Item> current, PendingBagSwap pending)
    {
        foreach (byte slot in current.Keys.Concat(pending.Before.Keys).Distinct())
        {
            current.TryGetValue(slot, out var item);
            pending.Before.TryGetValue(slot, out var before);
            if (slot != pending.Swap.Source && slot != pending.Swap.Target)
            {
                if (!SameBagItem(item, before)) return false;
                continue;
            }
            pending.After.TryGetValue(slot, out var after);
            // A swap is published as multiple remove/add packets. Either old item,
            // new item or an empty affected slot is an intermediate, never receipt.
            if (item is not null && !SameBagItem(item, before) && !SameBagItem(item, after)) return false;
        }
        return true;
    }

    async Task SortBag(BagSortAuthority authority, CancellationToken cancellation)
    {
        RequireBagSortOwner(authority, cancellation);
        if (pendingBagSwap is { } unresolved)
        {
            if (unresolved.Generation != Snapshot.Generation || Snapshot.Revision <= unresolved.Baseline
                || !BagSortPlanner.Matches(Snapshot.Inventory, unresolved.After))
                throw new QuestBlockedException("A previous bag move is unconfirmed. Wait for its inventory receipt or reconnect before sorting again.");
            pendingBagSwap = null;
        }
        RequireExpectedBag(authority, authority.Owner.Inventory, cancellation);
        var plan = BagSortPlanner.Create(authority.Owner.Inventory);
        if (plan.Swaps.IsEmpty) return;

        using var conflict = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, conflict.Token);
        BagSwapReceipt? awaiting = null;
        void ManualInput(byte opcode, byte[] payload, bool bot)
        {
            if (!bot && (connection.ManualOperationRevision != authority.Manual
                || connection.ManualBagOperationRevision != authority.BagInput))
            {
                try { conflict.Cancel(); }
                // Unsubscribing cannot revoke an event delegate already captured
                // by the packet pump. A completed sort must not fail forwarding.
                catch (ObjectDisposedException) { }
            }
        }
        void Observe(CharacterSnapshot state)
        {
            var receipt = Volatile.Read(ref awaiting);
            if (receipt is null || state.Revision <= receipt.Pending.Baseline) return;
            var pending = receipt.Pending;
            try
            {
                RequireBagSortOwner(authority, cancellation);
                if (BagSortPlanner.Matches(state.Inventory, pending.After)) receipt.Received.TrySetResult();
                else if (!ExpectedSwapTransition(state.Inventory, pending))
                    receipt.Received.TrySetException(new QuestBlockedException("An unexpected inventory change stopped bag sorting. No move was repeated."));
            }
            catch (Exception error) when (error is QuestBlockedException or OperationCanceledException)
            {
                receipt.Received.TrySetException(error);
            }
        }
        connection.ClientObserved += ManualInput;
        Changed += Observe;
        try
        {
            var expected = plan.Original;
            bool first = true;
            foreach (var swap in plan.Swaps)
            {
                if (!first) await Task.Delay(150, linked.Token);
                first = false;
                RequireExpectedBag(authority, expected, linked.Token);
                var after = BagSortPlanner.ApplySwap(expected, swap);
                BagSwapReceipt? submitted = null;
                void Validate() => RequireExpectedBag(authority, expected, linked.Token);
                await connection.SendAsync(Packet(new ClientChangeSlotMessage
                {
                    Pane = ClientSlotSwapType.Inventory, SourceSlot = swap.Source, TargetSlot = swap.Target
                }), authority.Owner.Generation, linked.Token, validateSend: Validate, beforeWrite: () =>
                {
                    Validate();
                    var pending = new PendingBagSwap(authority.Owner.Generation, Snapshot.Revision, swap, expected, after);
                    submitted = new(pending);
                    Volatile.Write(ref awaiting, submitted);
                    pendingBagSwap = pending;
                });
                try { await submitted!.Received.Task.WaitAsync(TimeSpan.FromSeconds(4), linked.Token); }
                catch (TimeoutException)
                {
                    throw new QuestBlockedException("The bag move is unconfirmed. Wait for its inventory receipt or reconnect; no move was repeated.");
                }
                RequireExpectedBag(authority, after, linked.Token);
                pendingBagSwap = null;
                Volatile.Write(ref awaiting, null);
                expected = after;
            }
        }
        catch (OperationCanceledException) when (conflict.IsCancellationRequested && !cancellation.IsCancellationRequested)
        {
            throw new QuestBlockedException("Player input stopped bag sorting. Wait for fresh inventory before sorting again.");
        }
        finally
        {
            Changed -= Observe;
            connection.ClientObserved -= ManualInput;
        }
    }
}
