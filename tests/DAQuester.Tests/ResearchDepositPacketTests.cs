using System.Buffers.Binary;
using Arbiter.Net.Client;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class ResearchDepositPacketTests
{
    [Theory]
    [InlineData(1u, 83)]
    [InlineData(25u, 84)]
    public void Dar_deposit_uses_raw_slot_and_string_quantity_without_a_bank_menu(uint quantity, int operation)
    {
        var s = FoundationTests.Ready() with { Map = 401, Position = new(7, 9),
            Inventory = System.Collections.Immutable.ImmutableDictionary<byte, Item>.Empty.Add(7, new(7, "Mileth Song", quantity)) };
        s = s with { Entities = s.Entities.Add(55, new(55, "Dar", new(5, 4), true)) };
        var packet = DialogPackets.DepositSong(s, 55u, 7, "Mileth Song", quantity);
        Assert.Equal(0x39, (byte)packet.Command);
        var bytes = packet.Data;
        Assert.Equal(1, bytes[0]);
        Assert.Equal(55u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)));
        Assert.Equal(operation, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(5)));
        if (quantity == 1) Assert.Equal(7, bytes[7]);
        else Assert.Equal(new byte[] { 1, 7, 2, (byte)'2', (byte)'5' }, bytes[7..]);
    }

    [Theory]
    [InlineData("wrong-npc")]
    [InlineData("wrong-map")]
    [InlineData("far-away")]
    [InlineData("changed-slot")]
    [InlineData("changed-quantity")]
    public void Changed_Dar_or_stack_cannot_authorize_a_deposit(string change)
    {
        var s = FoundationTests.Ready() with { Map = 401, Position = new(7, 9),
            Inventory = System.Collections.Immutable.ImmutableDictionary<byte, Item>.Empty.Add(7, new(7, "Mileth Song", 25)) };
        s = s with { Entities = s.Entities.Add(55, new(55, change == "wrong-npc" ? "Other NPC" : "Dar", change == "far-away" ? new(14, 0) : new(5, 4), true)) };
        if (change == "wrong-map") s = s with { Map = 500 };
        if (change == "changed-slot") s = s with { Inventory = s.Inventory.SetItem(7, new(7, "Other Item", 25)) };
        if (change == "changed-quantity") s = s with { Inventory = s.Inventory.SetItem(7, new(7, "Mileth Song", 24)) };
        Assert.Throws<QuestBlockedException>(() => DialogPackets.DepositSong(s, 55, 7, "Mileth Song", 25));
    }

    [Fact]
    public void Deposit_cleanup_matches_the_reference_close_without_an_argument_byte()
    {
        var s = FoundationTests.Ready() with { Map = 401, Position = new(7, 9) };
        var reply = new Dialog(s.Generation, s.Revision, 55, 1, "Dar", "It will be done.", true, 0, 1, 0, []);
        s = s with { Dialog = reply, Entities = s.Entities.Add(55, new(55, "Dar", new(5, 4), true)) };
        var packet = DialogPackets.CloseSongDeposit(s, reply);
        Assert.Equal(0x3A, (byte)packet.Command);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 55, 0, 0, 0, 1 }, packet.Data);
    }
}
