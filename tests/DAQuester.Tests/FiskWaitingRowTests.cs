using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class FiskWaitingRowTests
{
    sealed class WaitingSession : IGameSession, ICombatMap
    {
        readonly SimulatedSession session = new();
        public CharacterSnapshot Snapshot => session.Snapshot;
        public IReadOnlySet<string> Capabilities => session.Capabilities;
        public List<GameCommand> Commands => session.Commands;
        public event Action<CharacterSnapshot>? Changed { add => session.Changed += value; remove => session.Changed -= value; }
        public event Action<string>? Interrupted { add => session.Interrupted += value; remove => session.Interrupted -= value; }
        public Task ExecuteAsync(GameCommand c, CancellationToken ct) => session.ExecuteAsync(c, ct);
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> p, long r, TimeSpan t, CancellationToken ct) => session.WaitAsync(p, r, t, ct);
        public (int Width, int Height)? Size => (16, 16);
        public bool IsWall(Tile t) => false;
        public bool IsWarp(Tile t) => t == new Tile(15, 1);
        public bool IsWalkable(Tile t) => !Snapshot.Entities.Values.Any(e => e.Blocks && e.Position == t);
        public int PathLength(Tile a, Tile b) => IsWalkable(b) ? Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) : 0;
        public WaitingSession(IEnumerable<int> occupied)
        {
            session.Set(session.Snapshot with
            {
                Map = 413,
                Position = new(10, 8),
                Level = 99,
                Gold = 50000,
                Experience = 1000,
                LegendsKnown = true,
                MaxWeight = 127,
                Entities = occupied.Select(x => new Entity((uint)(100 + x), "Synthetic blocker", new(x, 1), true) { IsPlayer = true, Kind = 4 }).ToImmutableDictionary(e => e.Id)
            });
            session.OnCommand = c =>
            {
                if (c.Kind == "navigate")
                    session.Set(Snapshot with
                    {
                        Map = c.Map,
                        Position = c.Target
                    });
                if (c.Kind == "turn")
                {
                    session.Set(Snapshot with
                    {
                        Motion = new((byte)c.Amount!.Value, 0, DateTimeOffset.UtcNow, 0, false)
                    });
                    session.Interrupt();
                }
            };
        }
        public void MoveAway() => session.Set(Snapshot with { Position = new(12, 8) });
    }

    static async Task Run(WaitingSession s)
    {
        var at = DateTimeOffset.UtcNow;
        var d = new Dialog(s.Snapshot.Generation, 10, 22, 1, "Viveka", FiskEvidence.BouquetAccepted, true, 0, 1, 0, []) { ObservedAt = at };
        var j = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        await j.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = FiskEvidence.Id,
            Step = "run",
            Accepted = true,
            FiskWait = new(at, d, 50000, 30000, true)
        });
        var recipe = FiskDanaanAutomationTests.Recipe(FiskEvidence.Id) with
        {
            Journey = [new("bouquet-wait", "wait")]
        };
        var r = new QuestRunner(s, j, recipe, new());
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await r.RunAsync(cancellation: cancel.Token);
    }

    [Fact]
    public async Task Occupied_first_spot_is_skipped_and_character_faces_down()
    {
        var s = new WaitingSession([10]);
        await Run(s);
        Assert.Equal(new Tile(11, 1), s.Commands.Single(c => c.Kind == "navigate").Target);
        Assert.Equal(2u, s.Commands.Single(c => c.Kind == "turn").Amount);
        Assert.Equal(new Tile(11, 1), s.Snapshot.Position);
    }

    [Fact]
    public async Task Full_row_never_displaces_players_or_uses_a_doorway()
    {
        var s = new WaitingSession([10, 11, 12, 13, 14]);
        await Run(s);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "navigate" or "turn");
        Assert.Equal(new Tile(10, 8), s.Snapshot.Position);
    }
    [Fact]
    public async Task Shared_allocator_releases_a_spot_after_character_moves_away()
    {
        var area = new FiskWaitingArea();
        var first = new WaitingSession([]);
        var second = new WaitingSession([]);
        Assert.Equal(new Tile(10, 1), await area.ParkAsync(first, default));
        first.MoveAway();
        Assert.Equal(new Tile(10, 1), await area.ParkAsync(second, default));
    }
}
