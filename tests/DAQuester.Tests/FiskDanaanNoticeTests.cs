using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class FiskDanaanNoticeTests
{
    [Theory]
    [InlineData("AwASNzUwMDAwIGV4cGVyaWVuY2Uh", "750000 experience!")]
    [InlineData("AwAcWW91IGdhaW4gNzUwLDAwMCBleHBlcmllbmNlIQ==", "You gain 750,000 experience!")]
    public void Actual_fisk_reward_messages_reach_the_quest_receipt(string packet, string expected)
    {
        var tracker = new StateTracker(1);
        tracker.Identity("Synthetic", 42);
        var builder = new NetworkPacketBuilder(ServerCommand.MapSize);
        new ServerMapSizeMessage { MapId = 415, Width = 20, Height = 20, Name = "Synthetic shrine" }.Serialize(ref builder);
        tracker.Observe((byte)ServerCommand.MapSize, builder.ToPacket().Data);
        builder.Dispose();
        tracker.Observe(10, Convert.FromBase64String(packet));
        Assert.Equal(expected, tracker.Snapshot.Notice?.Text);
        Assert.Equal((byte)3, tracker.Snapshot.Notice?.Type);
    }
}
