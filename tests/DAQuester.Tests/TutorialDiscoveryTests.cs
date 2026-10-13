using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using Xunit;

namespace DAQuester.Tests;

public sealed class TutorialDiscoveryTests
{
    static byte[] Npc(string name, Tile position)
    {
        var message = new ServerDrawObjectsMessage { Entities = [new ServerCreatureEntity
        {
            Id = 100, X = (ushort)position.X, Y = (ushort)position.Y, Sprite = 28,
            Name = name, CreatureType = CreatureType.Mundane
        }] };
        var builder = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try { message.Serialize(ref builder); return ((ServerPacket)builder.ToPacket()).Data; }
        finally { builder.Dispose(); }
    }

    [Fact]
    public async Task Discovery_walks_until_named_NPC_is_observed_and_stays_on_the_tutorial_map()
    {
        var map = new MapRecord(AwakeningEvidence.TutorialMap, "Tutorial", 20, 20, [new(new(19, 19), 3000, new(2, 3))]);
        await using var fixture = await MovingObstacleTests.Fixture.Create(record: map);
        await fixture.Walk(new(10, 10), (moves, _) =>
        {
            if (moves == 1)
                fixture.Connection.Tracker.Observe(7, Npc(AwakeningEvidence.Bartley, new(13, 13)));
            return Task.FromResult(false);
        }, new("discover-npc", map.Id, Text: AwakeningEvidence.Bartley));
        Assert.Contains((byte)6, fixture.Commands);
        Assert.Equal(map.Id, fixture.Session.Snapshot.Map);
        Assert.Contains(fixture.Session.Snapshot.Entities.Values, e => e.Name == AwakeningEvidence.Bartley);
    }

    [Fact]
    public async Task Discovery_is_rejected_outside_the_tutorial_without_sending_any_movement()
    {
        await using var fixture = await MovingObstacleTests.Fixture.Create();
        await Assert.ThrowsAsync<QuestBlockedException>(() => fixture.Session.ExecuteAsync(new("discover-npc", AwakeningEvidence.TutorialMap, Text: AwakeningEvidence.Bartley), default));
        Assert.Empty(fixture.Commands);
    }

    [Fact]
    public async Task Cancelled_discovery_sends_no_movement()
    {
        var map = new MapRecord(AwakeningEvidence.TutorialMap, "Tutorial", 20, 20, []);
        await using var fixture = await MovingObstacleTests.Fixture.Create(record: map);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Session.ExecuteAsync(new("discover-npc", map.Id, Text: AwakeningEvidence.Bartley), cancelled.Token));
        Assert.Empty(fixture.Commands);
    }

    [Fact]
    public void Discovery_sweep_covers_reachable_map_and_never_uses_a_portal_or_wall()
    {
        var grid = new Grid(20, 20, (x, y) => x == 10 && y != 15);
        var start = new Tile(1, 1);
        var portals = new HashSet<Tile> { new(19, 19) };
        var points = NpcDiscovery.Sweep(grid, start, portals);
        Assert.NotEmpty(points);
        Assert.True(points.Length < 40);
        var previous = start;
        foreach (var point in points)
        {
            Assert.False(grid.Blocked(point));
            Assert.DoesNotContain(point, portals);
            Assert.DoesNotContain(grid.Path(previous, point, portals), p => portals.Contains(p));
            previous = point;
        }
        foreach (var point in Enumerable.Range(0, 20).SelectMany(x => Enumerable.Range(0, 20).Select(y => new Tile(x, y))).Where(p => !grid.Blocked(p) && !portals.Contains(p)))
            Assert.Contains(points.Prepend(start), stop => Math.Max(Math.Abs(point.X - stop.X), Math.Abs(point.Y - stop.Y)) <= 6);
    }
}
