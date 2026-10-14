using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    sealed record EquipmentRemoval(long Generation, long Revision, byte Slot, string Name, int InventoryCount);
    EquipmentRemoval? pendingEquipmentRemoval;

    static bool RemovalReceived(CharacterSnapshot snapshot, EquipmentRemoval removal) =>
        snapshot.Connected && snapshot.InventoryKnown && snapshot.Generation == removal.Generation
        && !snapshot.Equipment.ContainsKey(removal.Slot)
        && snapshot.Count(removal.Name) == removal.InventoryCount + 1;

    async Task UnequipObserved(GameCommand command, CancellationToken ct)
    {
        var before = Snapshot;
        if (pendingEquipmentRemoval is { } pending)
        {
            if (!RemovalReceived(before, pending))
                throw new QuestBlockedException("Equipment removal is pending. Reconcile equipment and inventory before another removal.");
            pendingEquipmentRemoval = null;
        }

        if (command.Slot is not >= 1 or > 18)
            throw new QuestBlockedException("An observed equipment slot from 1 to 18 is required.");
        byte slot = command.Slot.Value;
        if (!before.InventoryKnown)
            throw new QuestBlockedException("Equipment removal requires known inventory.");
        if (before.Inventory.Count >= 59)
            throw new QuestBlockedException("Free an inventory slot before removing equipment.");
        if (!before.Equipment.TryGetValue(slot, out var name) || string.IsNullOrWhiteSpace(name))
            throw new QuestBlockedException("No item is observed in that equipment slot.");
        if (command.Text is { } expected && !name.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Equipped item changed. Inspect equipment before removal.");

        var removal = new EquipmentRemoval(before.Generation, before.Revision, slot, name, before.Count(name));
        // Keep the baseline before sending: an interrupted reply must not authorize a second request.
        pendingEquipmentRemoval = removal;
        await Send(new ClientRemoveEquipmentMessage { Slot = (EquipmentSlot)slot }, ct);
        try
        {
            await WaitAsync(s => RemovalReceived(s, removal), before.Revision, TimeSpan.FromSeconds(5), ct);
            pendingEquipmentRemoval = null;
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("Equipment removal is unconfirmed. Inspect equipment and inventory; do not repeat the request.");
        }
    }
}
