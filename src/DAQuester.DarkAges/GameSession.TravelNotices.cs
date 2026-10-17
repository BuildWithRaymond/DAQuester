using Arbiter.Net.Types;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    static bool IsTownNotice(Dialog? d) => d is { IsPursuit: true, EntityType: 4, Type: 0, Prompt: null }
        && d.Choices.Length == 0 && d.Text.StartsWith("((You are entering a role-playing", StringComparison.Ordinal);

    static bool IsWeaponNotice(CharacterSnapshot s) => s.Map is 504 or 3008
        && s.Dialog is { IsPursuit: true, EntityType: 1 or 4, Type: 0, Prompt: null } d
        && d.Generation == s.Generation && d.Choices.Length == 0
        && d.Text.StartsWith("You can't bring weapons here!", StringComparison.Ordinal);

    static bool IsWeaponStatus(CharacterSnapshot s) => s.Map is 504 or 3008 && s.ActiveStatus is { Type: 3 } notice
        && notice.Generation == s.Generation && notice.Revision == s.Revision
        && notice.Text.StartsWith("You can't bring weapons here!", StringComparison.Ordinal);

    bool IsTravelNotice(CharacterSnapshot s) => IsTownNotice(s.Dialog) || IsWeaponNotice(s) || EventNotices.Recognized(s)
        || townWeaponRemovalRequired || entryBlock is not null;

    async Task RemoveTownWeapon(CancellationToken ct)
    {
        if (pendingEquipmentRemoval is { } pending)
        {
            if (!RemovalReceived(Snapshot, pending))
                throw new QuestBlockedException("Town weapon removal is pending. Reconcile equipment and its bag receipt; no removal repeated.");
            pendingEquipmentRemoval = null;
            townWeaponRemovalRequired = false;
            return;
        }
        var equipment = Snapshot;
        if (equipment.Equipment.ContainsKey((byte)EquipmentSlot.Weapon))
        {
            if (!equipment.InventoryKnown)
                throw new QuestBlockedException("Waiting for inventory before removing weapon. Resume when inventory is known.");
            if (equipment.Inventory.Count >= 59)
                throw new QuestBlockedException("Free an inventory slot for the weapon, then resume travel.");
            await UnequipObserved(new("unequip", Slot: (byte)EquipmentSlot.Weapon, Text: equipment.Equipment[(byte)EquipmentSlot.Weapon]), ct);
        }

        townWeaponRemovalRequired = false;
    }

    async Task<bool> DismissTravelNotice(CancellationToken ct)
    {
        bool handled = false;
        for (int page = 0; page < 8; page++)
        {
            RequireEntryAllowed();
            var before = Snapshot;
            var notice = before.Dialog;
            if (!IsTravelNotice(before))
                return handled;
            if (EventNotices.Recognized(before))
                await CloseDialog(notice, ct);
            else if (IsWeaponNotice(before))
            {
                townWeaponRemovalRequired = true;
                await CloseDialog(notice, ct);
                await RemoveTownWeapon(ct);
            }
            else if (townWeaponRemovalRequired)
                await RemoveTownWeapon(ct);
            else
            {
                if (notice!.HasNext)
                {
                    await connection.SendAsync(Protocol.DialogPackets.Continue(before, notice), before.Generation, ct);
                    await WaitAsync(s => s.Dialog is null || s.Dialog.Revision > notice.Revision, before.Revision, TimeSpan.FromSeconds(5), ct);
                }
                else
                    await CloseDialog(notice, ct);
            }

            handled = true;
        }

        throw new QuestBlockedException("Town notice repeated eight times. Inspect the popup before resuming travel.");
    }
}
