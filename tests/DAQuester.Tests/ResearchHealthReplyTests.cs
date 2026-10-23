using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Local synthetic server packets; no character or game client is operated.
[Collection("Walking cadence")]
public class ResearchHealthReplyTests
{
    static byte[] Payload(ServerMessage message)
    {
        var builder = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try { message.Serialize(ref builder); return builder.ToPacket().Data; }
        finally { builder.Dispose(); }
    }

    static byte[] Health(uint hp) => Payload(new ServerStatusMessage { Fields = StatsFieldFlags.Vitals, Health = hp });

    static void ObservePage(MovingObstacleTests.Fixture fixture, string kind)
    {
        fixture.Connection.Tracker.Observe(8, Health(100));
        fixture.Connection.Tracker.Observe(0x30, Payload(new ServerPursuitMessage
        {
            DialogType = kind == "input" ? DialogType.TextInput : DialogType.Popup,
            EntityType = EntityTypeFlags.Reactor,
            EntityId = 77,
            PursuitId = 315,
            StepId = 2,
            Content = "Synthetic research result",
            HasNextButton = kind == "next",
            InputPrompt = kind == "input" ? "Assistant" : null
        }));
    }

    static GameCommand Reply(MovingObstacleTests.Fixture fixture, string kind, bool earlyHealth)
    {
        var before = fixture.Session.Snapshot;
        return new GameCommand(kind, Text: kind == "input" ? "Synthetic assistant" : null,
            Dialog: before.Dialog, ExpectedGeneration: before.Generation,
            ExpectedDialogRevision: before.Dialog!.Revision, ExpectedPageText: before.Dialog.Text,
            ReplyTimeout: TimeSpan.FromMilliseconds(500), ReturnOnHealthLoss: earlyHealth);
    }

    static TaskCompletionSource Sent(MovingObstacleTests.Fixture fixture, Action? observed = null)
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode != 0x3A) return;
            observed?.Invoke();
            sent.TrySetResult();
        };
        return sent;
    }

    [Theory]
    [InlineData("next", 60u)]
    [InlineData("next", 0u)]
    [InlineData("input", 60u)]
    [InlineData("input", 0u)]
    public async Task Guarded_research_reply_returns_on_health_loss_before_dialogue_arrives(string kind, uint hp)
    {
        await using var fixture = await MovingObstacleTests.Fixture.Create();
        ObservePage(fixture, kind);
        var original = fixture.Session.Snapshot.Dialog;
        int submissions = 0;
        var sent = Sent(fixture, () => submissions++);
        var reply = fixture.Session.ExecuteAsync(Reply(fixture, kind, true), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.Send(8, Health(hp));

        await reply;

        Assert.Equal(hp, fixture.Session.Snapshot.Hp);
        Assert.Equal(original, fixture.Session.Snapshot.Dialog);
        Assert.Equal(1, submissions);
    }

    [Theory]
    [InlineData("next")]
    [InlineData("input")]
    public async Task Default_reply_still_waits_for_dialogue_after_health_loss(string kind)
    {
        await using var fixture = await MovingObstacleTests.Fixture.Create();
        ObservePage(fixture, kind);
        var sent = Sent(fixture);
        var reply = fixture.Session.ExecuteAsync(Reply(fixture, kind, false), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.Send(8, Health(60));

        await Assert.ThrowsAsync<TimeoutException>(() => reply);
        Assert.Equal(60u, fixture.Session.Snapshot.Hp);
    }

    [Theory]
    [InlineData("next")]
    [InlineData("input")]
    public async Task Old_health_loss_cannot_complete_a_new_research_reply(string kind)
    {
        await using var fixture = await MovingObstacleTests.Fixture.Create();
        fixture.Connection.Tracker.Observe(8, Health(40));
        ObservePage(fixture, kind);
        var sent = Sent(fixture);
        var reply = fixture.Session.ExecuteAsync(Reply(fixture, kind, true), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.Send(8, Payload(new ServerStatusMessage { Fields = StatsFieldFlags.ExperienceGold, Gold = 300 }));

        await Assert.ThrowsAsync<TimeoutException>(() => reply);
        Assert.Equal(100u, fixture.Session.Snapshot.Hp);
    }

    [Theory]
    [InlineData("next")]
    [InlineData("input")]
    public async Task Another_character_health_cannot_complete_research_reply(string kind)
    {
        await using var fixture = await MovingObstacleTests.Fixture.Create();
        ObservePage(fixture, kind);
        var sent = Sent(fixture);
        var reply = fixture.Session.ExecuteAsync(Reply(fixture, kind, true), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        fixture.Connection.Tracker.Identity("Other synthetic character", 43);
        await fixture.Send(8, Health(60));

        await Assert.ThrowsAsync<TimeoutException>(() => reply);
    }

    [Fact]
    public async Task Health_loss_on_another_map_cannot_complete_research_input()
    {
        await using var fixture = await MovingObstacleTests.Fixture.Create();
        ObservePage(fixture, "input");
        var sent = Sent(fixture);
        var reply = fixture.Session.ExecuteAsync(Reply(fixture, "input", true), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.Map(501, 5, 5);
        await fixture.Send(8, Health(60));

        await Assert.ThrowsAsync<TimeoutException>(() => reply);
    }
}
