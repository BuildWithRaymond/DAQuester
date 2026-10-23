using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class LionTravelRegressionTests
{
    [Fact]
    public async Task A_faerie_return_continues_after_late_movement_and_an_authoritative_refresh()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(
            record: new(547, "Synthetic Shinewood return", 50, 50, [new(new(29, 49), 546, new(8, 7))]),
            origin: new(29, 43), additional: [new(546, "Synthetic return destination", 50, 50, [])]);
        f.Session.FastMode = true;
        int refreshes = 0;
        await f.Walk(new(8, 7), async (move, next) =>
        {
            if (move <= 3) return true;
            if (next == new Tile(29, 49))
            {
                await f.Map(546, 50, 50);
                await f.Position(new(8, 7));
                return true;
            }
            return false;
        }, new("walk-exit", 546, new(29, 49)), refresh: async () =>
        {
            Assert.Equal(1, ++refreshes);
            await f.Map(547, 50, 50);
            await f.Position(new(29, 46));
        }, projectedServer: true);

        Assert.Equal(1, refreshes);
        Assert.Equal(546, f.Session.Snapshot.Map);
        Assert.Equal(new Tile(8, 7), f.Session.Snapshot.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Glioca_arrival_moves_west_into_the_observed_interior_before_continuing(bool fast)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(
            record: new(3007, "Synthetic Glioca interior", 24, 12, [new(new(22, 6), 3006, new(12, 19))]),
            origin: new(22, 6), wall: (x, y) => x >= 23 || x == 22 && y == 5,
            additional: [new(3006, "Synthetic temple exterior", 25, 25, [])]);
        f.Session.FastMode = fast;
        var visited = new List<Tile>();
        await f.Walk(new(20, 6), async (_, next) =>
        {
            visited.Add(next);
            if (next.X == 22)
            {
                await f.Map(3006, 25, 25);
                await f.Position(new(12, 19));
                return true;
            }
            Assert.Equal(6, next.Y);
            return false;
        }, new("navigate", 3007));

        Assert.Equal(new[] { new Tile(21, 6), new(20, 6) }, visited);
        Assert.Equal(3007, f.Session.Snapshot.Map);
        Assert.Equal(new Tile(20, 6), f.Session.Snapshot.Position);
    }
}
