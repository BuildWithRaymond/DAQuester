using System.Collections.Immutable;
using System.Reflection;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class StickRewardCleanupTests
{
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
    static QuestJob Completed(SimulatedSession s) => new() { Character = s.Snapshot.Name!, Quest = "spare-a-stick", Step = "complete", Status = "Completed", CompletedAt = DateTimeOffset.UtcNow.AddHours(-1) };
    static SimulatedSession WithStick()
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Stick", 1)) });
        return s;
    }

    [Fact]
    public async Task Timed_out_drop_is_saved_before_submission_and_late_debit_reconciles_without_resending()
    {
        var s = WithStick();
        var journal = Journal();
        var job = Completed(s);
        await journal.SaveAsync(job);
        bool savedBeforeSend = false;
        s.OnCommand = c => { if (c.Kind == "drop") savedBeforeSend = journal.Load(job.Character, job.Quest).StickCleanup?.Pending is not null; };
        var c = new QuestContext(s, journal, new(), job);
        await Assert.ThrowsAsync<QuestBlockedException>(() => StickRewardCleanup.RunAsync(c, default));
        Assert.True(savedBeforeSend);
        Assert.Null(QueueAttemptOutcome.Evaluate(c.Job));
        await Assert.ThrowsAsync<QuestBlockedException>(() => StickRewardCleanup.RunAsync(c, default));
        Assert.Single(s.Commands, x => x.Kind == "drop");
        s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(1) });
        await StickRewardCleanup.RunAsync(c, default);
        Assert.Single(s.Commands, x => x.Kind == "drop");
        Assert.True(c.Job.StickCleanup!.Finished);
        Assert.Equal(job.CompletedAt, c.Job.CompletedAt);
        Assert.NotNull(QueueAttemptOutcome.Evaluate(c.Job));
    }

    [Theory]
    [InlineData("new-generation")]
    [InlineData("unknown-bag")]
    [InlineData("slot-move")]
    public async Task Unrelated_inventory_changes_never_acknowledge_or_resubmit_a_pending_drop(string scenario)
    {
        var s = WithStick();
        var journal = Journal();
        var c = new QuestContext(s, journal, new(), Completed(s));
        await Assert.ThrowsAsync<QuestBlockedException>(() => StickRewardCleanup.RunAsync(c, default));
        var next = scenario switch
        {
            "new-generation" => s.Snapshot with { Generation = s.Snapshot.Generation + 1, Inventory = s.Snapshot.Inventory.Clear() },
            "unknown-bag" => s.Snapshot with { InventoryKnown = false, Inventory = s.Snapshot.Inventory.Clear() },
            _ => s.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty.Add(2, new(2, "Stick", 1)) }
        };
        s.Set(next);
        await Assert.ThrowsAsync<QuestBlockedException>(() => StickRewardCleanup.RunAsync(c, default));
        Assert.Single(s.Commands, x => x.Kind == "drop");
        Assert.NotNull(c.Job.StickCleanup!.Pending);
    }

    [Fact]
    public async Task A_failed_cleanup_checkpoint_never_drops_any_item()
    {
        var s = WithStick();
        var journal = Journal();
        var job = Completed(s);
        await journal.SaveAsync(job);
        var c = new QuestContext(s, journal, new(), job);
        // Obtain the real hashed checkpoint through the journal's own path rule.
        var checkpoint = (string)typeof(FileQuestJournal).GetMethod("PathFor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(journal, [job.Character, job.Quest])!;
        using (var locked = new FileStream(checkpoint, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => StickRewardCleanup.RunAsync(c, default));
        Assert.Empty(s.Commands);
        Assert.Null(journal.Load(job.Character, job.Quest).StickCleanup);
    }

    [Fact]
    public async Task An_unfinished_turn_in_never_authorizes_disposal()
    {
        var s = WithStick();
        var c = new QuestContext(s, Journal(), new(), Completed(s) with { Pending = new("turn-in", s.Snapshot.Generation, s.Snapshot.Revision, 0, 6, 0, DateTimeOffset.UtcNow) });
        await StickRewardCleanup.RunAsync(c, default);
        Assert.Empty(s.Commands);
        Assert.Null(c.Job.StickCleanup);
    }

    [Theory]
    [InlineData("changed-item")]
    [InlineData("moved")]
    public async Task Bound_drop_rejects_a_changed_item_or_location_before_sending(string scenario)
    {
        await using var fixture = await MovingObstacleTests.Fixture.Create();
        var snapshot = fixture.Session.Snapshot with { InventoryKnown = true,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, scenario == "changed-item" ? "Oak Stick" : "Stick", 1)) };
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(fixture.Connection.Tracker, snapshot);
        int sends = 0;
        fixture.Connection.ClientObserved += (_, _, _) => sends++;
        var tile = scenario == "moved" ? new Tile(snapshot.Position!.X + 1, snapshot.Position.Y) : snapshot.Position;
        await Assert.ThrowsAsync<QuestBlockedException>(() => fixture.Session.ExecuteAsync(new("drop", Map: snapshot.Map, Target: tile,
            Slot: 1, Amount: 1, ExpectedGeneration: snapshot.Generation, ExpectedResource: "Stick"), default));
        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task Verified_session_drops_exact_Stick_and_Wooden_Shield_leftovers_before_yielding()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty
            .Add(1, new(1, "Stick", 1)).Add(2, new(2, "Wooden Shield", 1))
            .Add(3, new(3, "Oak Stick", 1)).Add(4, new(4, "Branch", 6)).Add(5, new(5, "Wooden Shield", 1)) });
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new() { Character = session.Snapshot.Name!, Quest = "spare-a-stick", Step = "complete", Status = "Completed", CompletedAt = DateTimeOffset.UtcNow.AddHours(-1) });
        session.OnCommand = command =>
        {
            if (command.Kind == "drop")
                session.Set(session.Snapshot with { Inventory = session.Snapshot.Inventory.Remove(command.Slot!.Value) });
        };
        var recipe = new QuestDefinition("spare-a-stick", 1, "Stick", "Synthetic", [], [] ) { Repeatable = true };
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync(singleAttempt: true);
        Assert.Equal(3, session.Commands.Count(c => c.Kind == "drop"));
        Assert.Equal(1, session.Snapshot.Count("Oak Stick"));
        Assert.Equal(6, session.Snapshot.Count("Branch"));
        Assert.DoesNotContain(session.Commands, c => c.Kind is "unequip" or "open" or "choose");
        Assert.Equal("Completed", runner.Job.Status);
    }
}
