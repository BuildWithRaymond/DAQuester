using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using Xunit;

namespace DAQuester.Tests;

public class BranchDroughtTests
{
    static readonly Tile[] patrol = [new(40, 40), new(54, 43), new(64, 44)];
    static readonly Calibration calibration = new()
    {
        Validated = true,
        Patrol = patrol,
        Accepted = ["Accepted"],
        Completed = ["Done"],
        EvidenceFiles = ["simulated"]
    };
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    // Exercise repeated empty laps, then partial inventory progress. Neither runner
    // has an elapsed-time cutoff; a real three-minute sleep adds no coverage here.
    [Fact]
    public async Task Both_runners_keep_rotating_empty_patrols_until_inventory_goal_is_met()
    {
        await Task.WhenAll(CheckQuest(), CheckCalibration());
    }

    static async Task CheckQuest()
    {
        var session = new DroughtSession();
        var context = new QuestContext(session, Journal(), calibration, new()
        {
            Character = "Nuclear",
            Accepted = true,
            AcceptedGeneration = 1,
            Step = "gather"
        });
        await new StickActions().ExecuteAsync(context, new("gather", "gather", null), default);
        Assert.Equal(6, session.Snapshot.Count("Branch"));
        AssertPatrol(session);
    }

    static async Task CheckCalibration()
    {
        var session = new DroughtSession();
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Accepted = true,
            AcceptedGeneration = 1,
            Step = "gather"
        });
        session.Inner.OnCommand = c =>
        {
            if (c.Kind == "visit")
                throw new QuestBlockedException("Test reached turn-in with six branches");
        };
        var runner = new AutomaticCalibration(session, journal, new(), calibration, patrol);
        var end = await Assert.ThrowsAsync<QuestBlockedException>(() => runner.RunAsync("unused", default));
        Assert.Equal("Test reached turn-in with six branches", end.Message);
        Assert.Equal(6, session.Snapshot.Count("Branch"));
        AssertPatrol(session);
    }

    static void AssertPatrol(DroughtSession session)
    {
        var moves = session.Inner.Commands.Where(c => c.Kind == "navigate").ToArray();
        Assert.Equal(13, moves.Length);
        Assert.Equal(patrol.Concat(patrol), moves.Take(6).Select(c => c.Target));
        Assert.All(moves, c => Assert.Equal(new InventoryGoal("Branch", 6), c.StopWhen));
    }

    sealed class DroughtSession : IGameSession
    {
        public SimulatedSession Inner { get; } = new();

        int moves;
        public DroughtSession()
        {
            Inner.Set(Inner.Snapshot with
            {
                Experience = 0
            });
        }

        public CharacterSnapshot Snapshot => Inner.Snapshot;
        public IReadOnlySet<string> Capabilities => Inner.Capabilities;

        public event Action<CharacterSnapshot>? Changed { add => Inner.Changed += value; remove => Inner.Changed -= value; }

        public event Action<string>? Interrupted { add => Inner.Interrupted += value; remove => Inner.Interrupted -= value; }

        public async Task ExecuteAsync(GameCommand command, CancellationToken cancellation)
        {
            await Inner.ExecuteAsync(command, cancellation);
            if (command.Kind != "navigate")
                return;
            moves++;
            var snapshot = Snapshot with
            {
                Map = 500,
                Position = command.Target
            };
            if (moves >= 8)
                snapshot = snapshot with
                {
                    Inventory = snapshot.Inventory.SetItem(1, new(1, "Branch", (uint)(moves - 7)))
                };
            Inner.Set(snapshot);
        }

        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation) => Inner.WaitAsync(predicate, after, timeout, cancellation);
    }
}
