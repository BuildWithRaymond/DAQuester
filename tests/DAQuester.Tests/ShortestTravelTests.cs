using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class ShortestTravelTests
{
    [Fact]
    public async Task Same_map_reload_during_cadence_waits_for_authoritative_position()
    {
        var door = new Tile(4, 1);
        Task reload = Task.CompletedTask;
        await using var f = await MovingObstacleTests.Fixture.Create(150, record: new(500, "Start", 8, 8, [new(door, 501, new(1, 1))]), additional: [new(501, "End", 5, 5, [])]);
        f.Connection.Tracker.Observe(0x33, MovingObstacleTests.Human(99, new(7, 7)).Data);
        try
        {
            await f.Walk(door, async (n, next) =>
            {
                if (n == 1)
                    reload = Task.Run(async () =>
                    {
                        await Task.Delay(30);
                        await f.Map(500, 8, 8);
                        await Task.Delay(250);
                        await f.Position(new(2, 1));
                    });
                if (next != door)
                    return false;
                await f.Map(501, 5, 5);
                await f.Position(new(1, 1));
                return true;
            }, new("navigate", 501, new(1, 1)));
        }
        finally
        {
            await reload;
        }

        Assert.DoesNotContain((byte)0x38, f.Commands);
        Assert.Equal(501, f.Session.Snapshot.Map);
    }

    [Fact]
    public async Task Authoritative_position_correction_reprices_remaining_map_route()
    {
        var alternative = new Tile(1, 5);
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Start", 8, 8, [new(new(4, 1), 501, new(1, 1)), new(alternative, 501, new(1, 1))]), additional: [new(501, "End", 5, 5, [])]);
        await f.Walk(alternative, async (n, next) =>
        {
            if (n == 1)
            {
                Assert.Equal(new Tile(2, 1), next);
                await f.Position(new(1, 4));
                return true;
            }

            Assert.Equal(alternative, next);
            await f.Map(501, 5, 5);
            await f.Position(new(1, 1));
            return true;
        }, new("navigate", 501, new(1, 1)));
        Assert.DoesNotContain((byte)0x38, f.Commands);
        Assert.Equal(2, f.Commands.Count(c => c == 6));
    }

    [Fact]
    public async Task Standing_on_door_leaves_then_reenters_before_waiting_for_map()
    {
        var door = new Tile(2, 1);
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Start", 5, 5, [new(door, 501, new(1, 1))]), origin: door, additional: [new(501, "End", 5, 5, [])]);
        await f.Walk(door, async (n, next) =>
        {
            if (n == 1)
            {
                Assert.NotEqual(door, next);
                return false;
            }

            Assert.Equal(door, next);
            await f.Map(501, 5, 5);
            await f.Position(new(1, 1));
            return true;
        }, new("navigate", 501, new(1, 1)));
        Assert.Equal(2, f.Commands.Count(c => c == 6));
    }

    [Fact]
    public async Task Same_map_shortcut_chain_is_used_even_when_direct_walk_is_valid()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(500, "Shortcuts", 20, 5, []) { InternalTransfers = [new(new(2, 1), new(8, 1)), new(new(9, 1), new(17, 1))] });
        await f.Walk(new(18, 1), async (n, next) =>
        {
            if (n > 2)
                return false;
            Assert.Equal(new Tile(n == 1 ? 2 : 9, 1), next);
            await f.Map(500, 20, 5);
            await Task.Delay(30);
            await f.Position(new(n == 1 ? 8 : 17, 1));
            return true;
        });
        Assert.Equal(new Tile(18, 1), f.Session.Snapshot.Position);
        Assert.Equal(3, f.Commands.Count(c => c == 6));
        Assert.DoesNotContain((byte)0x38, f.Commands);
    }

    [Fact]
    public async Task Player_blocking_exit_during_cadence_changes_selected_door()
    {
        var near = new Tile(4, 1);
        var alternative = new Tile(1, 5);
        Task arrival = Task.CompletedTask;
        await using var f = await MovingObstacleTests.Fixture.Create(150, record: new(500, "Start", 8, 8, [new(near, 501, new(1, 1)), new(alternative, 501, new(1, 1))]), additional: [new(501, "End", 5, 5, [])]);
        try
        {
            await f.Walk(alternative, async (n, next) =>
            {
                if (n == 1)
                {
                    Assert.Equal(new Tile(2, 1), next);
                    arrival = Task.Run(async () =>
                    {
                        await Task.Delay(30);
                        f.Connection.Tracker.Observe(0x33, MovingObstacleTests.Human(99, near).Data);
                    });
                    return false;
                }

                Assert.NotEqual(near, next);
                if (n == 2)
                    Assert.NotEqual(new Tile(3, 1), next);
                if (next != alternative)
                    return false;
                await f.Map(501, 5, 5);
                await f.Position(new(1, 1));
                return true;
            }, new("navigate", 501, new(1, 1)));
        }
        finally
        {
            await arrival;
        }

        Assert.DoesNotContain((byte)0x38, f.Commands);
        Assert.Equal(501, f.Session.Snapshot.Map);
    }

    [Fact]
    public async Task All_occupied_exits_keep_bounded_refresh_recovery()
    {
        var door = new Tile(2, 1);
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Start", 5, 5, [new(door, 501, new(1, 1))]), additional: [new(501, "End", 5, 5, [])]);
        f.Connection.Tracker.Observe(0x33, MovingObstacleTests.Human(99, door).Data);
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Walk(door, (_, _) => Task.FromResult(false), new("navigate", 501, new(1, 1))));
        Assert.Contains("three route refreshes", error.Message);
        Assert.Equal(2, f.Commands.Count(c => c == 0x38));
        Assert.DoesNotContain((byte)6, f.Commands);
    }

    [Fact]
    public async Task Navigation_chooses_near_door_instead_of_first_recorded_door()
    {
        var near = new Tile(2, 1);
        var far = new Tile(18, 1);
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(500, "Start", 20, 5, [new(far, 501, new(1, 1)), new(near, 501, new(4, 1))]), additional: [new(501, "End", 5, 5, [])]);
        await f.Walk(near, async (_, next) =>
        {
            Assert.Equal(near, next);
            await f.Map(501, 5, 5);
            await f.Position(new(4, 1));
            return true;
        }, new("navigate", 501, new(4, 1)));
        Assert.Single(f.Commands, c => c == 6);
        Assert.Equal(501, f.Session.Snapshot.Map);
    }

    [Fact]
    public async Task Two_nearby_doors_beat_one_far_door()
    {
        var near = new Tile(2, 1);
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(500, "Start", 20, 5, [new(new(18, 1), 502, new(1, 1)), new(near, 501, new(1, 1))]), additional: [new(501, "Shortcut", 5, 5, [new(near, 502, new(1, 1))]), new(502, "End", 5, 5, [])]);
        await f.Walk(near, async (_, next) =>
        {
            Assert.Equal(near, next);
            int nextMap = f.Session.Snapshot.Map == 500 ? 501 : 502;
            await f.Map(nextMap, 5, 5);
            await f.Position(new(1, 1));
            return true;
        }, new("navigate", 502, new(1, 1)));
        Assert.Equal(2, f.Commands.Count(c => c == 6));
        Assert.Equal(502, f.Session.Snapshot.Map);
    }

    [Fact]
    public async Task Different_arrivals_to_same_map_are_compared_for_remaining_walk()
    {
        var closeDoor = new Tile(2, 1);
        var betterDoor = new Tile(8, 1);
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(500, "Start", 20, 5, [new(closeDoor, 501, new(18, 1)), new(betterDoor, 501, new(1, 1))]), additional: [new(501, "Middle", 20, 5, [new(new(2, 1), 502, new(1, 1))]), new(502, "End", 5, 5, [])]);
        await f.Walk(betterDoor, async (_, next) =>
        {
            if (f.Session.Snapshot.Map == 500)
            {
                Assert.NotEqual(closeDoor, next);
                if (next != betterDoor)
                    return false;
                await f.Map(501, 20, 5);
                await f.Position(new(1, 1));
            }
            else
            {
                Assert.Equal(new Tile(2, 1), next);
                await f.Map(502, 5, 5);
                await f.Position(new(1, 1));
            }

            return true;
        }, new("navigate", 502, new(1, 1)));
        Assert.Equal(502, f.Session.Snapshot.Map);
    }
}
