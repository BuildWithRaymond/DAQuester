using System.Text.Json;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Diagnostics;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Xunit;

namespace DAQuester.Tests;

public class BirthStatusTests
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

    [Fact]
    public void Status_message_is_server_observed_with_generation_revision_and_time()
    {
        var tracker = new StateTracker(42);
        tracker.Identity("Awesome", 1179711);
        string text = "She winks, \"It's on your legend now\"";
        var start = DateTimeOffset.UtcNow;
        tracker.Observe(0x0A, Packet(3, text));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(tracker.Snapshot));
        var n = json.RootElement.GetProperty("Notice");
        Assert.Equal(42, n.GetProperty("Generation").GetInt64());
        Assert.Equal(tracker.Snapshot.Revision, n.GetProperty("Revision").GetInt64());
        Assert.Equal(text, n.GetProperty("Text").GetString());
        Assert.True(n.GetProperty("ObservedAt").GetDateTimeOffset() >= start);
    }

    [Fact]
    public void Recorder_exports_birth_status_without_arbitrary_whisper_payloads()
    {
        var recorder = new ReplayRecorder();
        var tracker = new StateTracker(42);
        tracker.Identity("Awesome", 1179711);
        tracker.Observed += (o, p) => recorder.Record(o, p, tracker.Snapshot);
        var logged = new List<string>();
        tracker.ServerMessage += (_, text) => logged.Add(text);
        tracker.Observe(0x0A, Packet(0, "private message"));
        Assert.Equal(0, recorder.Count);
        Assert.Empty(logged);
        tracker.Observe(0x0A, Packet(3, "She winks, \"It's on your legend now\""));
        Assert.Equal(1, recorder.Count);
        Assert.Equal(["She winks, \"It's on your legend now\""], logged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void Only_active_messages_are_recorded_or_logged(byte type)
    {
        var recorder = new ReplayRecorder();
        var tracker = new StateTracker(42);
        tracker.Identity("Awesome", 1179711);
        tracker.Observed += (o, p) => recorder.Record(o, p, tracker.Snapshot);
        var logged = 0;
        tracker.ServerMessage += (_, _) => logged++;
        tracker.Observe(0x0A, Packet(type, "She winks, \"It's on your legend now\""));
        Assert.Equal(0, recorder.Count);
        Assert.Equal(0, logged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void Chat_and_popup_text_cannot_become_birth_status(byte type)
    {
        var tracker = new StateTracker(42);
        tracker.Identity("Awesome", 1179711);
        tracker.Observe(0x0A, Packet(type, "She winks, \"It's on your legend now\""));
        Assert.Null(tracker.Snapshot.Notice is { } n && DAQuester.Core.MundaneBirthEvidence.Matches(n) ? n : null);
    }
}
