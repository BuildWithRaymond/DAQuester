using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class EmeraldDawnStatusTests
{
    static StateTracker Tracker(int map)
    {
        var t = new StateTracker(77);
        t.Identity("SyntheticCharacter", 42);
        var b = new NetworkPacketBuilder(ServerCommand.MapSize);
        try
        {
            new ServerMapSizeMessage
            {
                MapId = (ushort)map,
                Width = 25,
                Height = 25
            }.Serialize(ref b);
            t.Observe((byte)ServerCommand.MapSize, b.ToPacket().Data);
        }
        finally
        {
            b.Dispose();
        }

        return t;
    }

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

    [Theory]
    [InlineData(414)]
    [InlineData(2052)]
    [InlineData(660)]
    [InlineData(425)]
    public void Public_insignia_reply_is_observed_and_recordable_on_quest_map(int map)
    {
        var t = Tracker(map);
        var recorder = new ReplayRecorder();
        t.Observed += (o, p) => recorder.Record(o, p, t.Snapshot);
        t.Observe(0x0A, Packet(3, "Synthetic refusal: bring the Emerald Knight Insignia."));
        Assert.Contains("Insignia", t.Snapshot.Notice?.Text);
        Assert.Equal(1, recorder.Count);
        Assert.Equal(t.Snapshot.Revision, t.Snapshot.Notice!.Revision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(8)]
    public void Private_text_never_becomes_emerald_evidence(byte type)
    {
        var t = Tracker(414);
        var recorder = new ReplayRecorder();
        t.Observed += (o, p) => recorder.Record(o, p, t.Snapshot);
        t.Observe(0x0A, Packet(type, "Emerald Knight Insignia private message"));
        Assert.Null(t.Snapshot.Notice);
        Assert.Equal(0, recorder.Count);
    }

    [Fact]
    public void Skan_sale_message_becomes_evidence_only_in_the_crypt_vestibule()
    {
        var t = Tracker(425);
        t.Observe(0x0A, Packet(3, DAQuester.Core.EmeraldDawnEvidence.RogueSaleNotice));
        Assert.Equal(DAQuester.Core.EmeraldDawnEvidence.RogueSaleNotice, t.Snapshot.Notice?.Text);
        t = Tracker(500);
        t.Observe(0x0A, Packet(3, DAQuester.Core.EmeraldDawnEvidence.RogueSaleNotice));
        Assert.Null(t.Snapshot.Notice);
    }

    // Server messages are always recorded as raw events; they only become quest evidence where the quest expects them.
    [Fact]
    public void Unrelated_notice_and_other_map_cannot_become_emerald_evidence()
    {
        var t = Tracker(500);
        var r = new ReplayRecorder();
        t.Observed += (o, p) => r.Record(o, p, t.Snapshot);
        t.Observe(0x0A, Packet(3, "Emerald Knight Insignia"));
        Assert.Null(t.Snapshot.Notice);
        Assert.Equal(1, r.Count);
        t = Tracker(414);
        t.Observe(0x0A, Packet(3, "Unrelated world announcement"));
        Assert.Null(t.Snapshot.Notice);
    }
}
