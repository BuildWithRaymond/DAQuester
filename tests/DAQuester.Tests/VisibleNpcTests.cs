using System.Collections.Immutable;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class VisibleNpcTests
{
    static void Show(MovingObstacleTests.Fixture f, Tile tile, bool player = false)
    {
        var s = f.Session.Snapshot;
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(f.Connection.Tracker,
            s with { Entities = s.Entities.SetItem(88, new(88, "Merchant", tile, true) { Kind = 2, IsPlayer = player }) });
    }

    [Fact]
    public async Task Visible_mundane_needs_no_closer_approach()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Synthetic shop", 12, 5, []));
        Show(f, new(10, 1));
        await f.Walk(new(9, 1), (_, _) => Task.FromResult(false), new("visit", 500, new(9, 1), "Merchant"));
        Assert.Equal(new Tile(1, 1), f.Session.Snapshot.Position);
        Assert.DoesNotContain((byte)6, f.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approach_stops_when_mundane_first_appears(bool fast)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(160, record: new(500, "Synthetic shop", 12, 5, []));
        f.Session.FastMode = fast;
        await f.Walk(new(9, 1), (moves, _) =>
        {
            if (moves == 1) Show(f, new(10, 1));
            return Task.FromResult(false);
        }, new("visit", 500, new(9, 1), "Merchant"));
        Assert.True(f.Session.Snapshot.Position!.X < 9);
        Assert.Single(f.Commands, opcode => opcode == 6);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Cached_offscreen_mundane_requires_approach_until_screen_range(bool fast, bool speechApproach)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(160, record: new(500, "Synthetic large temple", 30, 5, []));
        f.Session.FastMode = fast;
        Show(f, new(25, 1));
        var command = speechApproach ? new GameCommand("navigate", 500, new(24, 1), StopAtVisibleNpc: "Merchant") : new("visit", 500, new(24, 1), "Merchant");
        await f.Walk(new(24, 1), (_, _) => Task.FromResult(false), command);
        Assert.Equal(new Tile(15, 1), f.Session.Snapshot.Position);
        Assert.Equal(14, f.Commands.Count(opcode => opcode == 6));
    }

    [Fact]
    public async Task Unanchored_visit_stops_at_screen_range()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Synthetic large shop", 30, 5, []));
        Show(f, new(25, 1));
        await f.Walk(new(24, 1), (_, _) => Task.FromResult(false), new("visit", 500, Text: "Merchant"));
        Assert.Equal(new Tile(15, 1), f.Session.Snapshot.Position);
    }

    [Fact]
    public async Task Open_does_not_send_for_cached_offscreen_mundane()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Synthetic large shop", 30, 5, []));
        Show(f, new(25, 1));
        var blocked = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("open", Text: "Merchant"), CancellationToken.None));
        Assert.Contains("visible", blocked.Message);
        Assert.Equal(new Tile(1, 1), f.Session.Snapshot.Position);
    }

    [Fact]
    public async Task Visible_mundane_on_doorway_does_not_skip_departure()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Synthetic doorway", 12, 5, [new(new(1, 1), 501, new(1, 1))]),
            additional: [new(501, "Synthetic outside", 12, 5, [])]);
        Show(f, new(10, 1));
        await f.Walk(new(9, 1), (_, _) => Task.FromResult(false), new("visit", 500, new(9, 1), "Merchant"));
        Assert.Single(f.Commands, opcode => opcode == 6);
        Assert.NotEqual(new Tile(1, 1), f.Session.Snapshot.Position);
        Assert.Equal(500, f.Session.Snapshot.Map);
    }

    [Fact]
    public async Task Wizard_hall_visible_mundane_waits_for_stable_interior()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(398, "Synthetic Wizard Hall", 13, 12, []));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(f.Connection.Tracker,
            f.Session.Snapshot with { Position = new(6, 9) });
        Show(f, new(6, 2));
        await f.Walk(new(6, 8), (_, _) => Task.FromResult(false), new("visit", 398, new(6, 4), "Merchant"));
        Assert.Equal(new Tile(6, 8), f.Session.Snapshot.Position);
        Assert.Single(f.Commands, opcode => opcode == 6);
    }

    [Fact]
    public async Task Source_selection_uses_recorded_travel_cost_instead_of_fixed_garden()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Synthetic routes", 12, 5,
            [new(new(3, 1), 3072, new(1, 1)), new(new(11, 1), 622, new(1, 1))]),
            additional: [new(3072, "Synthetic Mehadi", 5, 5, []), new(622, "Synthetic Garden", 5, 5, [])]);
        var source = QuestSupplySources.Select(f.Session, new("gland", "gather", 622) { Item = "Viper's Gland", Monster = 49, Sprite = 327 });
        Assert.Equal(3072, source.Map);
        Assert.Contains("Guide hypothesis", source.Evidence);
    }

    [Fact]
    public async Task Callough_approach_walks_from_distant_entry_then_stops_at_first_visibility()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(132, "Synthetic weapon shop", 20, 16, []));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(f.Connection.Tracker,
            f.Session.Snapshot with { Position = new(13, 1) });
        await f.Walk(new(6, 10), (moves, _) =>
        {
            if (moves == 3)
            {
                var state = f.Session.Snapshot;
                typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(f.Connection.Tracker,
                    state with { Entities = state.Entities.SetItem(88, new(88, "Callough", new(6, 7), true) { Kind = 2 }) });
            }
            return Task.FromResult(false);
        }, new("visit", 132, new(6, 10), "Callough"));
        Assert.Equal(3, f.Commands.Count(opcode => opcode == 6));
        Assert.NotEqual(new Tile(6, 10), f.Session.Snapshot.Position);
        Assert.NotNull(NpcVisibility.Find(f.Session.Snapshot, 132, "Callough"));
    }
}
