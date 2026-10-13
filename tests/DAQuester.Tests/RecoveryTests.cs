using Xunit;
using DAQuester.Core;
using System.Collections.Immutable;

namespace DAQuester.Tests;

public class RecoveryTests
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString());
    static Calibration Cal() => new()
    {
        Validated = true,
        Patrol = [new(22, 22)],
        Accepted = ["Accepted"],
        Completed = ["Done"],
        Refused = ["No"],
        Cooldown = ["Later"],
        EvidenceFiles = ["simulation"]
    };
    [Fact]
    public async Task Acceptance_follows_server_next_before_selecting_option_and_closes_popup()
    {
        var s = new SimulatedSession();
        Dialog D(string text, bool next = false, params DialogChoice[] choices) => new(1, s.Snapshot.Revision + 1, 99, 1, "Callough", text, true, 177, 16, 0, choices.ToImmutableArray(), HasNext: next);
        s.OnCommand = c =>
        {
            if (c.Kind == "open")
                s.Set(s.Snapshot with
                {
                    Dialog = D("Welcome", false, new DialogChoice("Spare a Stick", 177, 1))
                });
            if (c.Kind == "choose" && c.Text == "Spare a Stick")
                s.Set(s.Snapshot with
                {
                    Dialog = D("Introduction", true)
                });
            if (c.Kind == "next")
                s.Set(s.Snapshot with
                {
                    Dialog = D("Bring branches", false, new DialogChoice("I'll gather them now", 177, 4))
                });
            if (c.Kind == "choose" && c.Text == "I'll gather them now")
                s.Set(s.Snapshot with
                {
                    Dialog = D("Accepted")
                });
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
        };
        var c = new QuestContext(s, new FileQuestJournal(dir), Cal(), new()
        {
            Character = "Nuclear"
        });
        await new StickActions().ExecuteAsync(c, new("accept", "accept", null), default);
        Assert.True(c.Job.Accepted);
        Assert.Null(c.Job.Pending);
        Assert.Null(s.Snapshot.Dialog);
        Assert.Contains(s.Commands, x => x.Kind == "next");
    }

    [Theory]
    [InlineData(1000u, 1u)]
    [InlineData(2000u, 1001u)]
    public async Task Deposit_only_excess_and_verify_gold(uint start, uint expected)
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Gold = start
        });
        s.OnCommand = x =>
        {
            if (x.Kind == "prepare-deposit")
                s.Set(s.Snapshot with
                {
                    Dialog = new(1, s.Snapshot.Revision + 1, 7015, 1, "Cassidy", "Deposit amount?", false, 82, 0, 2, [])
                });
            if (x.Kind == "deposit")
                s.Set(s.Snapshot with
                {
                    Gold = 999
                });
        };
        var c = new QuestContext(s, new FileQuestJournal(dir), Cal(), new()
        {
            Character = "Nuclear"
        });
        await new StickActions().ExecuteAsync(c, new("bank", "bank", null), default);
        Assert.Equal(expected, s.Commands.Single(x => x.Kind == "deposit").Amount);
        Assert.Null(c.Job.Pending);
    }

    [Fact]
    public async Task Reconnect_with_pending_deposit_requires_intervention()
    {
        var j = new FileQuestJournal(dir);
        await j.SaveAsync(new()
        {
            Character = "Nuclear",
            Step = "bank",
            Pending = new("bank", 1, 0, 1000, 0, 0, DateTimeOffset.UtcNow)
        });
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Generation = 2,
            Gold = 1000
        });
        var c = new QuestContext(s, j, Cal(), j.Load("Nuclear", "spare-a-stick"));
        await Assert.ThrowsAsync<QuestBlockedException>(() => new StickActions().ExecuteAsync(c, new("bank", "bank", null), default));
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task Already_accepted_does_not_accept_twice()
    {
        var s = new SimulatedSession();
        var c = new QuestContext(s, new FileQuestJournal(dir), Cal(), new()
        {
            Character = "Nuclear",
            Accepted = true,
            AcceptedGeneration = s.Snapshot.Generation
        });
        await new StickActions().ExecuteAsync(c, new("accept", "accept", null), default);
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task Unrelated_dialog_cannot_confirm_turn_in()
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Experience = 7000,
            Dialog = new(1, 99, 42, 1, "Cassidy", "Done", true, 8, 2, 0, [])
        });
        var c = new QuestContext(s, new FileQuestJournal(dir), Cal(), new()
        {
            Character = "Nuclear",
            Pending = new("turn-in", 1, 0, 999, 6, 3500, DateTimeOffset.UtcNow)
        });
        await Assert.ThrowsAsync<TimeoutException>(() => new StickActions().ExecuteAsync(c, new("confirm", "confirm", null), default));
        Assert.Null(c.Job.CompletedAt);
    }

    [Fact]
    public async Task Session_interruption_cancels_patrol_and_checkpoints()
    {
        var s = new SimulatedSession();
        var j = new FileQuestJournal(dir);
        await j.SaveAsync(new()
        {
            Character = "Nuclear",
            Step = "gather",
            Accepted = true
        });
        var def = new QuestDefinition("spare-a-stick", 1, "", "", ["navigate"], [new("gather", "gather", null)]);
        var r = new QuestRunner(s, j, def, Cal());
        s.OnCommand = _ => s.Interrupt();
        await r.RunAsync();
        Assert.Equal("Paused", r.Job.Status);
        Assert.Equal("gather", r.Job.Step);
        Assert.Single(s.Commands);
    }
}
