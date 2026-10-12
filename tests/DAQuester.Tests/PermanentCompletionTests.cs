using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using System.Collections.Immutable;
using System.Text.Json;
using Xunit;

namespace DAQuester.Tests;

public class PermanentCompletionTests
{
    static readonly Calibration Cal = new()
    {
        Validated = true,
        Patrol = [new(54, 43), new(40, 40), new(64, 44)],
        Accepted = ["Accepted"],
        Completed = ["Done"],
        EvidenceFiles = ["simulation"]
    };
    static QuestDefinition Recipe() => new("spare-a-stick", 1, "Spare a Stick", "", [], [new("preflight", "preflight", "accept"), new("accept", "accept", null)]);
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    static Dialog Reply(SimulatedSession s, string text, string name = "Callough", long? generation = null) => new(generation ?? s.Snapshot.Generation, s.Snapshot.Revision + 1, 99, 1, name, text, true, 177, 82, 0, []);
    [Fact]
    public async Task Child_reply_stops_forever_preserves_exact_evidence_and_disables_repeat_after_restart()
    {
        var session = new SimulatedSession();
        var journal = Journal();
        const string text = "You remind me of a child I once knew.";
        session.OnCommand = c =>
        {
            if (c.Kind == "open")
                session.Set(session.Snapshot with
                {
                    Dialog = Reply(session, "Welcome") with
                    {
                        Choices = [new("Spare a Stick", 177, 1)]
                    }
                });
            if (c.Kind == "choose")
                session.Set(session.Snapshot with
                {
                    Dialog = Reply(session, text)
                });
        };
        var runner = new QuestRunner(session, journal, Recipe(), Cal);
        await runner.RunAsync(repeat: true);
        Assert.Equal("PermanentlyCompleted", runner.Job.Status);
        Assert.False(runner.Job.Repeat);
        Assert.Null(runner.Job.NextRun);
        Assert.DoesNotContain(session.Commands, c => c.Text == "I'll gather them now");
        using var stored = JsonDocument.Parse(JsonSerializer.Serialize(journal.Load("Nuclear", "spare-a-stick")));
        Assert.Equal(text, stored.RootElement.GetProperty("PermanentCompletion").GetProperty("Dialog").GetProperty("Text").GetString());
        session.Commands.Clear();
        var restored = new QuestRunner(session, journal, Recipe(), new());
        await restored.RunAsync(repeat: true);
        await restored.PauseAsync();
        Assert.Equal("PermanentlyCompleted", restored.Job.Status);
        Assert.Empty(session.Commands);
    }

    [Theory]
    [InlineData("Bring those branches, child.", "Callough", 1)]
    [InlineData("You remind me of a child.", "Cassidy", 1)]
    [InlineData("You remind me of a child.", "Callough", 999)]
    public async Task Other_npc_vague_child_word_and_stale_generation_do_not_mark_permanent(string text, string name, long generation)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Dialog = Reply(session, text, name, generation)
        });
        session.OnCommand = _ => throw new QuestBlockedException("Fixture ends before network interaction");
        var runner = new QuestRunner(session, Journal(), Recipe(), Cal);
        await runner.RunAsync();
        Assert.Equal("Paused", runner.Job.Status);
    }

    [Fact]
    public async Task Permanent_reply_overrides_level_gate_and_keeps_ambiguous_transaction_for_audit()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Level = 12,
            Dialog = Reply(session, "You remind me of a child.")
        });
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Step = "confirm",
            Pending = new("turn-in", 1, 0, 0, 6, 0, DateTimeOffset.UtcNow)
        });
        var runner = new QuestRunner(session, journal, new("spare-a-stick", 1, "Spare a Stick", "", [], [new("confirm", "confirm", null)]), Cal);
        await runner.RunAsync();
        Assert.Equal("PermanentlyCompleted", runner.Job.Status);
        Assert.NotNull(runner.Job.Pending);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task Calibration_records_permanent_reply_without_gathering_or_reward_claims()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Experience = 1,
            Dialog = Reply(session, "You remind me of a child.")
        });
        var runner = new AutomaticCalibration(session, Journal(), new(), Cal, Cal.Patrol);
        await runner.RunAsync(Path.Combine(Path.GetTempPath(), "DAQuester-permanent-" + Guid.NewGuid() + ".json"), default);
        Assert.Equal("PermanentlyCompleted", runner.Job.Status);
        Assert.Empty(session.Commands);
        Assert.Empty(runner.Job.Rewards);
    }

    [Fact]
    public async Task Real_child_terminal_also_records_correlated_optional_rewards()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Level = 3,
            Experience = 3851,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Wooden Shield", 1)).Add(2, new(2, "Oak Stick", 1)).Add(7, new(7, "Branch", 1)),
            Dialog = Reply(session, "You remind me of my own little child. I thank you so much.")
        });
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Step = "confirm",
            Pending = new("turn-in", 1, 0, 0, 7, 351, DateTimeOffset.UtcNow)
            {
                InventoryBaselineKnown = true,
                InventoryCounts = new() { { "Branch", 7 } }
            }
        });
        var runner = new QuestRunner(session, journal, new("spare-a-stick", 1, "Spare a Stick", "", [], [new("confirm", "confirm", null)]), Cal);
        await runner.RunAsync();
        Assert.Equal("PermanentlyCompleted", runner.Job.Status);
        Assert.Equal(3500, runner.Job.Rewards["experience"]);
        Assert.Equal(6, runner.Job.Rewards["branchesConsumed"]);
        Assert.Equal(1, runner.Job.Rewards["item:Oak Stick"]);
        Assert.Equal(1, runner.Job.Rewards["item:Wooden Shield"]);
        Assert.Null(runner.Job.Pending);
    }

    [Fact]
    public async Task Passive_child_reply_waits_for_owner_checkpoint_before_saving_permanent_state()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Experience = 3500,
            Dialog = Reply(session, "You remind me of my own little child. I thank you so much.")
        });
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Status = "Running",
            Step = "turn-in"
        });
        var observed = await PermanentQuestCompletion.ObserveAsync(journal, session.Snapshot, async () =>
        {
            // The canceled owner saves its pending baseline before releasing ownership.
            await journal.SaveAsync(new()
            {
                Character = "Nuclear",
                Step = "confirm",
                Pending = new("turn-in", 1, 0, 0, 6, 0, DateTimeOffset.UtcNow)
            });
        });
        Assert.NotNull(observed);
        Assert.Equal("PermanentlyCompleted", observed.Status);
        Assert.Equal(3500, observed.Rewards["experience"]);
        Assert.Null(observed.Pending);
        Assert.Equal("PermanentlyCompleted", journal.Load("Nuclear", "spare-a-stick").Status);
    }
}
