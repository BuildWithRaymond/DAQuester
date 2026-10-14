using System.Text.Json;
using DAQuester.DarkAges.Protocol;
using Xunit;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using Arbiter.Net.Server.Types;

namespace DAQuester.Tests;

public class CharacterFeedbackTests
{
    static void Observe(StateTracker tracker, ServerMessage message)
    {
        var opcode = ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value;
        var packet = new NetworkPacketBuilder(opcode);
        try
        {
            message.Serialize(ref packet);
            tracker.Observe((byte)opcode, packet.ToPacket().Data);
        }
        finally { packet.Dispose(); }
    }

    static void Message(StateTracker tracker, byte type, string text)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        tracker.Observe(0x0A, [type, (byte)(bytes.Length >> 8), (byte)bytes.Length, .. bytes]);
    }
    [Fact]
    public void Fresh_numeric_active_xp_message_creates_transient_character_feedback()
    {
        var tracker = new StateTracker(1);
        tracker.Identity("Kel", 42);
        Message(tracker, 3, "123 experience!");
        var feedback = Assert.Single(tracker.Snapshot.Feedback);
        Assert.Equal(123u, feedback.Value);
        Assert.Equal("Experience", feedback.Kind);
        Assert.Equal(tracker.Snapshot.Revision, feedback.Revision);
        Message(tracker, 3, "123 experience!");
        Assert.Equal(2, tracker.Snapshot.Feedback.Length);
        Assert.NotEqual(tracker.Snapshot.Feedback[0].Revision, tracker.Snapshot.Feedback[1].Revision);
    }

    [Theory]
    [InlineData(0, "123 experience!")]
    [InlineData(1, "123 experience!")]
    [InlineData(3, "You received 123 experience!")]
    [InlineData(3, "0 experience!")]
    [InlineData(3, "1,23 experience!")]
    [InlineData(3, "99999999999999 experience!")]
    public void Other_messages_do_not_create_xp_feedback(byte type, string text)
    {
        var tracker = new StateTracker(1);
        tracker.Identity("Kel", 42);
        Message(tracker, type, text);
        Assert.Empty(tracker.Snapshot.Feedback);
    }

    [Fact]
    public void Level_feedback_requires_observed_increase_and_clears_on_identity_or_disconnect()
    {
        var tracker = new StateTracker(1);
        tracker.Identity("Kel", 42);
        void Level(byte level) => Observe(tracker, new ServerStatusMessage { Fields = StatsFieldFlags.Stats, Level = level });
        Level(11);
        Assert.Empty(tracker.Snapshot.Feedback);
        Level(12);
        Assert.Equal("Level", Assert.Single(tracker.Snapshot.Feedback).Kind);
        Assert.Equal(12u, tracker.Snapshot.Feedback[0].Value);
        Level(12);
        Assert.Single(tracker.Snapshot.Feedback);
        tracker.Identity("Other", 43);
        Assert.Empty(tracker.Snapshot.Feedback);
        Level(20);
        Assert.Empty(tracker.Snapshot.Feedback);
        Message(tracker, 3, "1,234 experience!");
        Assert.Equal(1234u, Assert.Single(tracker.Snapshot.Feedback).Value);
        tracker.Disconnect();
        Assert.Empty(tracker.Snapshot.Feedback);
    }

    [Theory]
    [InlineData(42u, "Kill")]
    [InlineData(43u, "Experience")]
    public void Kill_wording_requires_our_observed_killing_blow(uint attacker, string expected)
    {
        var tracker = new StateTracker(1);
        tracker.Identity("Kel", 42);
        Observe(tracker, new ServerDrawObjectsMessage { Entities = [new ServerCreatureEntity { Id = 900, X = 6, Y = 6, Sprite = 0x8000 + 53, CreatureType = CreatureType.Monster }] });
        Observe(tracker, new ServerEffectLayerMessage { TargetId = 900, SourceId = attacker, TargetAnimation = 263 });
        Message(tracker, 3, "123 experience!");
        Assert.Equal(expected, Assert.Single(tracker.Snapshot.Feedback).Kind);
    }
}
