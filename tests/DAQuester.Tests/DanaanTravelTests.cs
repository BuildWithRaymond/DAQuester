using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Synthetic server arrivals use observed routes, including variable Aoife landings.
public class DanaanTravelTests
{
    sealed class Session : SimulatedSession, ICombatMap
    {
        public (int Width, int Height)? Size => (100, 100);
        public bool IsWall(Tile t) => false;
        public bool IsWarp(Tile t) => false;
        public bool IsWalkable(Tile t) => t.X >= 0 && t.Y >= 0;
        public int PathLength(Tile a, Tile b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
    static async Task<Session> Probe(int source, Tile position, string strategy, int target, Tile approach, bool pending = false,
        Tile? coliseumLanding = null, int coliseumMap = 5231, bool expectClose = true)
    {
        var s = new Session();
        s.Set(s.Snapshot with
        {
            Map = source,
            Position = position,
            Level = 99,
            Hp = 100,
            MaxHp = 100,
            Gold = 100000,
            Experience = 1000,
            LegendsKnown = true,
            LegendsRevision = 1
        });
        Dialog Page(string name, string text, bool root = false, string? choice = null) => new(1, s.Snapshot.Revision + 1, 55, 1, name, text, !root, 111, 2, 0,
            choice is null ? [] : [new(choice, 3, 1)]);
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate" && c.Map == 5231 && coliseumLanding is { } landing)
            {
                coliseumLanding = null;
                s.Set(s.Snapshot with { Map = coliseumMap, Position = landing });
                throw new InvalidOperationException($"No recorded walkable route from {coliseumMap} to 5231");
            }
            if (c.Kind == "navigate")
                s.Set(s.Snapshot with
                {
                    Map = c.Map,
                    Position = c.Target
                });
            if (c.Kind == "enter")
                s.Set(s.Snapshot with
                {
                    Map = c.Map,
                    Position = c.InwardTarget
                });
            if (c.Kind == "step")
            {
                s.Set(s.Snapshot with
                {
                    Position = c.Target
                });
                if (c.Map == 5220)
                    s.Set(s.Snapshot with
                    {
                        Dialog = Page("", "Will you be respectful of the monastery?", choice: "Yes") with
                        {
                            EntityType = 4
                        }
                    });
            }
            if (c.Kind == "open")
                s.Set(s.Snapshot with
                {
                    Dialog = Page("Aoife", "Synthetic root", true, "Guide Reception")
                });
            if (c.Kind == "choose" && c.Text == "Guide Reception")
                s.Set(s.Snapshot with
                {
                    Dialog = Page("Aoife", "Come to give guidance to a new Aisling who is choosing his or her Craft?", choice: "Yes")
                });
            if (c.Kind == "choose" && c.Text == "Yes")
                s.Set(s.Snapshot with
                {
                    Map = strategy == "cedar-grove" ? 5210 : 394,
                    Position = strategy == "cedar-grove" ? new(14, 13) : new(7, 7),
                    Dialog = null
                });
            if (c.Kind == "interact")
                s.Set(s.Snapshot with
                {
                    Dialog = Page("Target", "Synthetic terminal")
                });
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        await journal.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true),
            Pending = pending ? new("observed-access:" + source, 1, 4, 100000, 0, 1000, DateTimeOffset.UtcNow) : null
        });
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("travel-probe","dialog",target,"Target",approach)
            { ApproachStrategy=strategy,Pages=[new("Target","Synthetic terminal","close")] }]
        };
        var runner = new QuestRunner(s, journal, recipe, new());
        await runner.RunAsync();
        Assert.Equal(expectClose ? 1 : 0, s.Commands.Count(c => c.Kind == "close"));
        return s;
    }
    [Fact]
    public async Task Upper_coliseum_arrival_continues_through_the_known_door_after_same_map_planner_failure()
    {
        var s = await Probe(500, new(50, 50), "coliseum", 5232, new(4, 9), coliseumLanding: new(0, 1));
        var entry = Assert.Single(s.Commands, c => c.Kind == "enter");
        Assert.Equal(5232, entry.Map);
        Assert.Equal(new Tile(0, 0), entry.Target);
        Assert.Equal(new Tile(4, 11), entry.InwardTarget);
    }
    [Theory]
    [InlineData(5231, 5)]
    [InlineData(5230, 1)]
    public async Task Other_coliseum_landings_keep_the_route_failure_without_guessing_an_entry(int actualMap, int y)
    {
        var s = await Probe(500, new(50, 50), "coliseum", 5232, new(4, 9),
            coliseumLanding: new(0, y), coliseumMap: actualMap, expectClose: false);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "enter" or "interact");
    }
    [Fact]
    public async Task Room_seven_entry_moves_inward_then_to_the_stable_interior()
    {
        var s = await Probe(205, new(3, 3), "inn-room7", 177, new(2, 3));
        var enter = Assert.Single(s.Commands, c => c.Kind == "enter");
        Assert.Equal(new Tile(4, 5), enter.InwardTarget);
        Assert.Equal(new Tile(2, 3), s.Snapshot.Position);
    }
    [Fact]
    public async Task Monk_exit_uses_north_away_step_then_stable_corridor()
    {
        var s = await Probe(348, new(4, 5), "", 3007, new(6, 4));
        Assert.Equal(new Tile(2, 6), s.Commands.First(c => c.Kind == "enter").InwardTarget);
        Assert.Contains(s.Commands, c => c.Kind == "navigate" && c.Map == 344 && c.Target == new Tile(6, 8));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fresh_or_interrupted_guidance_moves_away_before_path_hall_travel(bool interrupted)
    {
        var s = await Probe(interrupted ? 394 : 393, interrupted ? new(7, 7) : new(3, 5), "path-hall", 348, new(4, 5), interrupted);
        Assert.Contains(s.Commands, c => c.Kind == "step" && c.Map == 394 && c.Target == new Tile(6, 7));
        Assert.Equal(interrupted ? 0 : 1, s.Commands.Count(c => c.Kind == "choose" && c.Text == "Yes"));
    }
    [Fact]
    public async Task Monastery_current_consent_moves_north_from_actual_arrival()
    {
        var s = await Probe(5220, new(1, 7), "cedar-grove", 5210, new(4, 6));
        Assert.Single(s.Commands, c => c.Kind == "choose" && c.Text == "Yes");
        Assert.Contains(s.Commands, c => c.Kind == "step" && c.Map == 5210 && c.Target == new Tile(14, 12));
    }
    [Fact]
    public async Task Coast_bar_uses_actual_door_landing_and_public_counter_approach()
    {
        var s = await Probe(185, new(8, 29), "coast-bar", 195, new(7, 2));
        var enter = Assert.Single(s.Commands, c => c.Kind == "enter");
        Assert.Equal(new Tile(7, 29), enter.Target);
        Assert.Equal(new Tile(7, 5), enter.InwardTarget);
        Assert.Equal(new Tile(7, 2), s.Snapshot.Position);
    }
}
