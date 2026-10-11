using Xunit;
using DAQuester.DarkAges.Protocol;
using DAQuester.Core;
using System.Text;
using System.Text.Json;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Serialization;

namespace DAQuester.Tests;
public class PacketTests
{
    [Fact]
    public void Fragmented_and_coalesced_frames()
    {
        var b = new FrameBuffer();
        b.Append([0xAA, 0]);
        Assert.Null(b.Read());
        b.Append([2, 8, 9, 0xAA, 0, 1, 7]);
        Assert.Equal(new byte[] { 0xAA, 0, 2, 8, 9 }, b.Read());
        Assert.Equal(new byte[] { 0xAA, 0, 1, 7 }, b.Read());
        Assert.Null(b.Read());
    }

    [Fact]
    public void Give_preserves_excalibur_uint32_quantity_layout()
    {
        var packet = ItemPackets.Give(9, 0x11223344, 1000);
        Assert.Equal(new byte[] { 9, 0x11, 0x22, 0x33, 0x44, 0, 0, 3, 0xE8 }, packet.Data);
    }

    [Fact]
    public void Datester_741_avatar_packet_sets_logged_in_identity()
    {
        var s = new StateTracker(1);
        s.Identity("Nuclear", 0);
        s.Observe(0x05, [0, 0, 0, 42, 0, 0, 1, 0, 0]);
        Assert.Equal(42u, s.Snapshot.Id);
        Assert.True(s.Snapshot.Connected);
    }

    [Fact]
    public void Self_look_sets_known_legend_revision_and_disconnect_invalidates_it()
    {
        var tracker = new StateTracker(88);
        tracker.Identity("Clock", 42);
        Assert.False(tracker.Snapshot.LegendsKnown);
        var message = new ServerSelfLookMessage
        {
            LegendMarks = [new ServerLegendMark
            {
                Text = "Loved by Suomi Mundanes"
            }

            ]
        };
        var builder = new NetworkPacketBuilder(ServerCommand.SelfLook);
        try
        {
            message.Serialize(ref builder);
            tracker.Observe(0x39, builder.ToPacket().Data);
        }
        finally
        {
            builder.Dispose();
        }

        Assert.True(tracker.Snapshot.LegendsKnown);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(tracker.Snapshot));
        Assert.Equal(tracker.Snapshot.Revision, json.RootElement.GetProperty("LegendsRevision").GetInt64());
        tracker.Disconnect();
        Assert.False(tracker.Snapshot.LegendsKnown);
    }

    static byte[] Text8(string s) => new byte[]
    {
        (byte)s.Length
    }.Concat(Encoding.ASCII.GetBytes(s)).ToArray();
    static byte[] Text16(string s) => new byte[]
    {
        0,
        (byte)s.Length
    }.Concat(Encoding.ASCII.GetBytes(s)).ToArray();
    static byte[] Menu(uint npc)
    {
        var b = new List<byte>
        {
            0,
            1,
            0,
            0,
            0,
            (byte)npc,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0
        };
        b.AddRange(Text8("Callough"));
        b.AddRange(Text16("Choose"));
        b.Add(2);
        b.AddRange(Text8("Other"));
        b.AddRange(new byte[] { 0, 99 });
        b.AddRange(Text8("Spare a Stick"));
        b.AddRange(new byte[] { 1, 8 });
        return b.ToArray();
    }

    [Fact]
    public void Reordered_menu_keeps_live_pursuit_and_npc_ids()
    {
        var t = new StateTracker(1);
        t.Observe(0x2F, Menu(42));
        Assert.Equal((ushort)264, t.Snapshot.Dialog!.Choices.Single(x => x.Text == "Spare a Stick").Pursuit);
        t.Observe(0x2F, Menu(81));
        Assert.Equal(81u, t.Snapshot.Dialog!.EntityId);
    }

    [Fact]
    public void Inventory_increment_replaces_slot_quantity()
    {
        var t = new StateTracker(1);
        byte[] Item(int n) => new byte[]
        {
            1,
            0,
            0,
            0
        }.Concat(Text8("Branch")).Concat(new byte[] { 0, 0, 0, (byte)n, 1, 0, 0, 0, 0, 0, 0, 0, 0 }).ToArray();
        t.Observe(0x0F, Item(2));
        t.Observe(0x0F, Item(3));
        Assert.Equal(3, t.Snapshot.Count("Branch"));
        t.Observe(0x10, [1]);
        Assert.Equal(0, t.Snapshot.Count("Branch"));
    }

    [Fact]
    public void Position_correction_is_server_authoritative()
    {
        var t = new StateTracker(9);
        t.Observe(0x04, [0, 10, 0, 20, 0, 0, 0, 0]);
        Assert.Equal(new Tile(10, 20), t.Snapshot.Position);
        t.Observe(0x0B, [1, 0, 10, 0, 20, 0, 0, 0, 0, 0]);
        Assert.Equal(new Tile(11, 20), t.Snapshot.Position);
        t.Observe(0x04, [0, 8, 0, 20, 0, 0, 0, 0]);
        Assert.Equal(new Tile(8, 20), t.Snapshot.Position);
    }

    [Fact]
    public void Unrelated_packet_does_not_replace_dialog()
    {
        var t = new StateTracker(1);
        t.Observe(0x2F, Menu(42));
        var d = t.Snapshot.Dialog;
        t.Observe(0x7E, [1]);
        Assert.Same(d, t.Snapshot.Dialog);
    }
}
