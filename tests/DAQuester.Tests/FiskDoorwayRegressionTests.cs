using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class FiskDoorwayRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bertil_doorway_rejection_retries_from_confirmed_position_without_excluding_the_only_entrance(bool fast)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(160,
            record: new(503, "Synthetic Suomi entrance", 30, 90, [new(new(15, 86), 416, new(13, 7))]),
            origin: new(16, 86), additional: [new(416, "Synthetic Bertil interior", 15, 15, [])]);
        f.Session.FastMode = fast;
        int attempts = 0;
        await f.Walk(new(7, 2), async (_, next) =>
        {
            if (f.Session.Snapshot.Map != 503) return false;
            Assert.Equal(new Tile(15, 86), next);
            if (++attempts == 1)
                await f.Send(11, [4, 0, 16, 0, 86, 0, 11, 0, 11, 1]);
            else
            {
                await f.Map(416, 15, 15);
                await f.Position(new(13, 7));
            }
            return true;
        }, new("navigate", 416, new(7, 2), StopAtVisibleNpc: "Bertil"));

        Assert.Equal(2, attempts);
        Assert.Equal(416, f.Session.Snapshot.Map);
        Assert.Equal(new Tile(7, 2), f.Session.Snapshot.Position);
        Assert.DoesNotContain((byte)0x3A, f.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repeated_doorway_rejections_still_stop_after_three_confirmed_attempts(bool fast)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(160,
            record: new(503, "Synthetic Suomi entrance", 30, 90, [new(new(15, 86), 416, new(13, 7))]),
            origin: new(16, 86), additional: [new(416, "Synthetic Bertil interior", 15, 15, [])]);
        f.Session.FastMode = fast;
        int attempts = 0;
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Walk(new(7, 2), async (_, next) =>
        {
            Assert.Equal(new Tile(15, 86), next);
            attempts++;
            await f.Send(11, [4, 0, 16, 0, 86, 0, 11, 0, 11, 1]);
            return true;
        }, new("navigate", 416, new(7, 2), StopAtVisibleNpc: "Bertil")));

        Assert.Contains("Position corrected three times", error.Message);
        Assert.Equal(3, attempts);
        Assert.Equal(503, f.Session.Snapshot.Map);
        Assert.Equal(new Tile(16, 86), f.Session.Snapshot.Position);
        Assert.DoesNotContain((byte)0x3A, f.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Occupancy_observed_after_a_rejection_still_blocks_the_doorway(bool fast)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(160,
            record: new(503, "Synthetic Suomi entrance", 30, 90, [new(new(15, 86), 416, new(13, 7))]),
            origin: new(16, 86), additional: [new(416, "Synthetic Bertil interior", 15, 15, [])]);
        f.Session.FastMode = fast;
        int attempts = 0;
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Walk(new(7, 2), async (_, next) =>
        {
            Assert.Equal(new Tile(15, 86), next);
            attempts++;
            await f.Send(0x33, MovingObstacleTests.Human(99, next).Data);
            await f.Send(11, [4, 0, 16, 0, 86, 0, 11, 0, 11, 1]);
            return true;
        }, new("navigate", 416, new(7, 2), StopAtVisibleNpc: "Bertil")));

        Assert.Contains("Destination remains occupied", error.Message);
        Assert.Equal(1, attempts);
        Assert.Equal(new Tile(16, 86), f.Session.Snapshot.Position);
    }
}
