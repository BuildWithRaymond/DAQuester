using System.Globalization;
using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using DAQuester.Core;

namespace DAQuester.DarkAges.Protocol;

public static partial class DialogPackets
{
    // Excalibur DialogueRespond source reference: operation 84 takes a raw
    // argument marker and slot, followed by a String8 quantity, not two strings.
    public static ClientPacket DepositSong(CharacterSnapshot s, uint npc, byte slot, string name, uint quantity)
    {
        if (NpcVisibility.Find(s, WizardyResearchEvidence.Map, "Dar") is not { } dar || dar.Id != npc ||
            !s.InventoryKnown || !WizardyResearchEvidence.IsSong(name) || !s.Inventory.TryGetValue(slot, out var item) ||
            item.Name != name || item.Quantity != quantity || quantity == 0 ||
            s.Dialog is { IsPursuit: true } || s.Dialog is { Name: not "Dar" })
            throw new QuestBlockedException("Dar or the observed Song stack changed; no deposit sent.");
        var b = new NetworkPacketBuilder(ClientCommand.Merchant);
        try
        {
            b.AppendByte(1);
            b.AppendUInt32(npc);
            b.AppendUInt16(quantity == 1 ? (ushort)83 : (ushort)84);
            if (quantity > 1) b.AppendByte(1);
            b.AppendByte(slot);
            if (quantity > 1) b.AppendString8(quantity.ToString(CultureInfo.InvariantCulture));
            return (ClientPacket)b.ToPacket();
        }
        finally { b.Dispose(); }
    }

    public static ClientPacket CloseSongDeposit(CharacterSnapshot s, Dialog d)
    {
        if (d.Name != "Dar" || d.Generation != s.Generation || s.Dialog != d ||
            NpcVisibility.Find(s, WizardyResearchEvidence.Map, "Dar")?.Id != d.EntityId)
            throw new QuestBlockedException("Dar's deposit reply changed; cleanup remains saved.");
        var b = new NetworkPacketBuilder(ClientCommand.Pursuit);
        try
        {
            b.AppendByte(1);
            b.AppendUInt32(d.EntityId);
            b.AppendUInt16(0);
            b.AppendUInt16(1);
            return (ClientPacket)b.ToPacket();
        }
        finally { b.Dispose(); }
    }
}
