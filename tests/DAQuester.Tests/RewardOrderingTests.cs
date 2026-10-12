using Xunit;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using System.Collections.Immutable;

namespace DAQuester.Tests;

public class RewardOrderingTests
{
    sealed class HistorySession : IGameSession
    {
        public readonly SimulatedSession Inner = new();
        readonly EvidenceHistory history = new();
        public HistorySession()
        {
            history.Record(Inner.Snapshot);
            Inner.Changed += history.Record;
        }

        public CharacterSnapshot Snapshot => Inner.Snapshot;
        public IReadOnlySet<string> Capabilities => Inner.Capabilities;

        public event Action<CharacterSnapshot>? Changed { add => Inner.Changed += value; remove => Inner.Changed -= value; }

        public event Action<string>? Interrupted { add => Inner.Interrupted += value; remove => Inner.Interrupted -= value; }

        public Task ExecuteAsync(GameCommand c, CancellationToken ct) => Inner.ExecuteAsync(c, ct);
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken ct) => history.Find(Snapshot.Generation, after, test) is { } found ? Task.FromResult(found) : Inner.WaitAsync(test, after, timeout, ct);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rewards_use_the_terminal_reply_snapshot_after_split_xp_and_item_packets(bool calibration)
    {
        var session = new HistorySession();
        var sim = session.Inner;
        sim.Set(sim.Snapshot with
        {
            Experience = 301,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Branch", 6))
        });
        Dialog D(string text, params DialogChoice[] choices) => new(1, sim.Snapshot.Revision + 1, 99, 1, "Callough", text, true, 177, 17, 0, choices.ToImmutableArray());
        sim.OnCommand = c =>
        {
            if (c.Kind == "open")
                sim.Set(sim.Snapshot with
                {
                    Dialog = D("Welcome", new DialogChoice("Spare a Stick", 177, 1))
                });
            if (c.Kind == "choose" && c.Text == "Spare a Stick")
                sim.Set(sim.Snapshot with
                {
                    Dialog = D("Bring branches", new DialogChoice("I have those branches, here", 177, 5))
                });
            if (c.Kind == "choose" && c.Text == "I have those branches, here")
            {
                sim.Set(sim.Snapshot with
                {
                    Inventory = ImmutableDictionary<byte, Item>.Empty,
                    Experience = 2301
                });
                sim.Set(sim.Snapshot with
                {
                    Inventory = sim.Snapshot.Inventory.Add(1, new(1, "Stick", 1))
                });
                sim.Set(sim.Snapshot with
                {
                    Inventory = sim.Snapshot.Inventory.Add(2, new(2, "Wooden Shield", 1))
                });
                sim.Set(sim.Snapshot with
                {
                    Experience = 3801,
                    Level = 3
                });
                sim.Set(sim.Snapshot with
                {
                    Dialog = D("Complete")
                });
            }
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        var cal = new Calibration
        {
            Validated = true,
            Patrol = [new(64, 45)],
            Accepted = ["Accepted"],
            Completed = ["Complete"],
            EvidenceFiles = ["simulation"]
        };
        var job = new QuestJob
        {
            Character = "Nuclear",
            Accepted = true,
            AcceptedGeneration = session.Snapshot.Generation
        };
        QuestJob result;
        if (calibration)
        {
            await journal.SaveAsync(job);
            var recorder = new ReplayRecorder();
            session.Changed += s => recorder.Record(0x0F, [], s);
            var run = new AutomaticCalibration(session, journal, recorder, cal, [new(64, 45)]);
            await run.RunAsync(Path.Combine(Path.GetTempPath(), "DAQuester-ordering-" + Guid.NewGuid() + ".json"), default);
            result = run.Job;
        }
        else
        {
            var context = new QuestContext(session, journal, cal, job);
            var actions = new StickActions();
            await actions.ExecuteAsync(context, new("turn-in", "turn-in", null), default);
            await actions.ExecuteAsync(context, new("confirm", "confirm", null), default);
            result = context.Job;
        }

        Assert.Equal(3500, result.Rewards["experience"]);
        Assert.Equal(1, result.Rewards["item:Stick"]);
        Assert.Equal(1, result.Rewards["item:Wooden Shield"]);
        Assert.Equal(6, result.Rewards["branchesConsumed"]);
    }
}
