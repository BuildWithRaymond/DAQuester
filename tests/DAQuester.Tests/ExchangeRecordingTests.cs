using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Diagnostics;
using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Xunit;
namespace DAQuester.Tests;

public class ExchangeRecordingTests
{
    [Fact]
    public void Server_exchange_events_are_recorded()
    {
        var recorder = new ReplayRecorder();
        var tracker = new StateTracker(42);
        tracker.Identity("valadriel", 1655120);
        tracker.Observed += (o, p) => recorder.Record(o, p, tracker.Snapshot);
        var b = new NetworkPacketBuilder(ServerCommand.Exchange);
        byte[] started;
        try
        {
            b.AppendByte(0);
            b.AppendUInt32(1670123);
            b.AppendString8("jackharkness");
            started = b.ToPacket().Data;
        }
        finally { b.Dispose(); }
        tracker.Observe(0x42, started);
        Assert.Equal(1, recorder.Count);
    }
    [Fact]
    public void Client_exchange_actions_are_recorded_with_their_fields()
    {
        var recorder = new ReplayRecorder();
        var tracker = new StateTracker(42);
        tracker.Identity("jackharkness", 1670123);
        recorder.RecordExchangePacket(new ClientPacket(0x4A, [1, 0, 0x19, 0x48, 0x10, 1, 1]), tracker.Snapshot);
        recorder.RecordExchangePacket(new ClientPacket(0x4A, [5, 0, 0x19, 0x48, 0x10]), tracker.Snapshot);
        Assert.Equal(2, recorder.ActionCount);
    }
}
