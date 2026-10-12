using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Diagnostics;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Xunit;

namespace DAQuester.Tests;

public class MuisirStatusTests
{
    static byte[] Packet(byte type, string text)
    {
        var b = new NetworkPacketBuilder(ServerCommand.Message);
        try
        {
            b.AppendByte(type);
            b.AppendString16(text);
            return b.ToPacket().Data;
        }
        finally
        {
            b.Dispose();
        }
    }

    static StateTracker Tracker()
    {
        var t = new StateTracker(77);
        t.Identity("Clock", 42);
        var b = new NetworkPacketBuilder(ServerCommand.MapSize);
        try
        {
            new Arbiter.Net.Server.Messages.ServerMapSizeMessage
            {
                MapId = 306,
                Width = 15,
                Height = 15,
                Name = "Undine Black Magic Master"
            }.Serialize(ref b);
            t.Observe((byte)ServerCommand.MapSize, b.ToPacket().Data);
        }
        finally
        {
            b.Dispose();
        }

        Assert.Equal(306, t.Snapshot.Map);
        return t;
    }

    [Fact]
    public void Kiril_registration_refusal_is_observed_and_exportable()
    {
        var t = Tracker();
        var r = new ReplayRecorder();
        t.Observed += (o, p) => r.Record(o, p, t.Snapshot);
        t.Observe(0x0A, Packet(3, "You must be registered."));
        Assert.Equal("You must be registered.", t.Snapshot.Notice?.Text);
        Assert.Equal(1, r.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(8)]
    public void Private_text_never_becomes_quest_evidence(byte type)
    {
        var t = Tracker();
        var r = new ReplayRecorder();
        t.Observed += (o, p) => r.Record(o, p, t.Snapshot);
        t.Observe(0x0A, Packet(type, "You must be registered."));
        Assert.Null(t.Snapshot.Notice);
        Assert.Equal(0, r.Count);
    }
}
