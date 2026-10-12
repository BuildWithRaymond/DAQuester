using Xunit;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Navigation;
using System.Collections.Immutable;

namespace DAQuester.Tests;

public class CalibrationRunnerTests
{
    [Fact]
    public async Task Full_capture_validates_replies_patrol_and_actual_rewards()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Experience = 0
        });
        var recorder = new ReplayRecorder();
        session.Changed += s => recorder.Record(0x0F, [], s);
        recorder.Record(0x0F, [], session.Snapshot);
        Dialog D(string text, params DialogChoice[] choices) => new(1, session.Snapshot.Revision + 1, 99, 1, "Callough", text, true, 12, 0, 0, choices.ToImmutableArray());
        session.OnCommand = c =>
        {
            if (c.Kind is "navigate" or "visit")
            {
                if (session.Snapshot.Dialog is not null)
                    throw new QuestBlockedException("Dialog still busy before movement.");
                var s = session.Snapshot with
                {
                    Map = c.Map,
                    Position = c.Target ?? new(6, 10)
                };
                if (c.Map == 500)
                    s = s with
                    {
                        Inventory = s.Inventory.SetItem(1, new(1, "Branch", 6))
                    };
                session.Set(s);
            }

            if (c.Kind == "open")
                session.Set(session.Snapshot with
                {
                    Dialog = D("Welcome", new DialogChoice("Spare a Stick", 12, 1))
                });
            if (c.Kind == "choose" && c.Text == "Spare a Stick")
                session.Set(session.Snapshot with
                {
                    Dialog = D("Can you help?", new("I'll gather them now", 12, 1), new("I have those branches, here", 12, 2))
                });
            if (c.Kind == "choose" && c.Text == "I'll gather them now")
                session.Set(session.Snapshot with
                {
                    Dialog = D("Good luck gathering branches")
                });
            if (c.Kind == "close")
                session.Set(session.Snapshot with
                {
                    Dialog = null
                });
            if (c.Kind == "choose" && c.Text == "I have those branches, here")
                session.Set(session.Snapshot with
                {
                    Dialog = D("Thank you!"),
                    Experience = 3500,
                    Level = 12,
                    Inventory = ImmutableDictionary<byte, Item>.Empty.Add(2, new(2, "Stick", 1)).Add(3, new(3, "Wooden Shield", 1))
                });
        };
        Tile[] rotation = [new(40, 40), new(54, 43), new(64, 44)];
        var run = new AutomaticCalibration(session, Journal(), recorder, new(), rotation);
        var result = await run.RunAsync(Path.Combine(Path.GetTempPath(), "DAQuester-capture-" + Guid.NewGuid() + ".json"), default);
        Assert.True(result.Ready);
        Assert.Contains("Good luck gathering branches", result.Accepted);
        Assert.Contains("Thank you!", result.Completed);
        Assert.NotEmpty(result.Patrol);
        Assert.Equal(1, run.Job.Rewards["item:Stick"]);
        Assert.Equal(3500, run.Job.Rewards["experience"]);
        Assert.NotNull(run.Job.CompletedAt);
        Assert.Contains(session.Commands, c => c.Kind == "close");
        Assert.Equal(rotation, result.Patrol); // Keep player-requested areas even when only one yielded a drop.
    }

    [Fact]
    public async Task Recorded_acceptance_resumes_collection_without_accepting_again()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Experience = 1
        });
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Accepted = true,
            AcceptedGeneration = session.Snapshot.Generation,
            Step = "gather"
        });
        session.OnCommand = c =>
        {
            if (c.Kind == "navigate")
                throw new QuestBlockedException("Collection started");
        };
        var run = new AutomaticCalibration(session, journal, new(), new(), [new(22, 22)]);
        await Assert.ThrowsAsync<QuestBlockedException>(() => run.RunAsync("unused", default));
        Assert.Equal("navigate", Assert.Single(session.Commands).Kind);
    }

    [Fact]
    public async Task Reconnected_calibration_reactivates_branch_collection()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Generation = 2,
            Experience = 1
        });
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Accepted = true,
            AcceptedGeneration = 1,
            Step = "gather"
        });
        session.OnCommand = _ => throw new QuestBlockedException("Test stops at first command");
        var run = new AutomaticCalibration(session, journal, new(), new(), [new(64, 45)]);
        await Assert.ThrowsAsync<QuestBlockedException>(() => run.RunAsync("unused", default));
        Assert.Equal("visit", Assert.Single(session.Commands).Kind);
        Assert.False(run.Job.Accepted);
    }

    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Six_unstacked_branches_require_six_free_slots(bool automatic)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Experience = 1,
            Inventory = Enumerable.Range(1, 55).ToImmutableDictionary(i => (byte)i, i => new Item((byte)i, "Other", 1))
        });
        session.OnCommand = _ => throw new QuestBlockedException("Unexpected command before capacity check");
        var journal = Journal();
        var cal = new Calibration
        {
            Validated = true,
            Patrol = [new(64, 45)],
            Accepted = ["Accepted"],
            Completed = ["Complete"],
            EvidenceFiles = ["simulation"]
        };
        Task Run() => automatic ? new AutomaticCalibration(session, journal, new(), cal, cal.Patrol).RunAsync("unused", default) : new StickActions().ExecuteAsync(new(session, journal, cal, new()
        {
            Character = "Nuclear"
        }), new("preflight", "preflight", null), default);
        var blocked = await Assert.ThrowsAsync<QuestBlockedException>(Run);
        Assert.Contains("slot", blocked.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public void Candidate_patrol_excludes_walls_and_unreachable_tiles()
    {
        var grid = new Grid(15, 15, (x, y) => x == 7);
        var result = TreePatrol.Build(grid, new(1, 1), [new("test", new(5, 5), 4)]);
        Assert.NotEmpty(result);
        Assert.All(result, t => Assert.False(grid.Blocked(t)));
        Assert.All(result, t => Assert.True(t.X < 7));
    }

    [Fact]
    public async Task Unknown_level_never_starts_calibration_packets()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Level = null
        });
        var run = new AutomaticCalibration(session, Journal(), new(), new(), [new(22, 22)]);
        await Assert.ThrowsAsync<QuestBlockedException>(() => run.RunAsync("unused", default));
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task Pending_turnin_never_repeats_during_calibration()
    {
        var session = new SimulatedSession();
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Pending = new("turn-in", 1, 1, 0, 6, 0, DateTimeOffset.UtcNow)
        });
        var run = new AutomaticCalibration(session, journal, new(), new(), [new(22, 22)]);
        await Assert.ThrowsAsync<QuestBlockedException>(() => run.RunAsync("unused", default));
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task No_candidate_patrol_blocks_before_navigation()
    {
        var session = new SimulatedSession();
        var run = new AutomaticCalibration(session, Journal(), new(), new(), []);
        await Assert.ThrowsAsync<QuestBlockedException>(() => run.RunAsync("unused", default));
        Assert.Empty(session.Commands);
    }
}
