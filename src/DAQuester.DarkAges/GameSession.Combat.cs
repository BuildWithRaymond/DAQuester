using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession : ICombatMap
{
    // Talos sends the assail (0x13) with a single zero byte.
    Task Assail(CancellationToken ct) => connection.SendAsync(new ClientPacket(0x13, [0]), Snapshot.Generation, ct);

    Task Turn(byte direction, CancellationToken ct) => direction <= 3
        ? Send(new ClientChangeDirectionMessage { Direction = (WorldDirection)direction }, ct)
        : throw new QuestBlockedException("Turn direction must be 0-3.");

    // One step along the route, onto the tile itself as Talos's NavPathfind does; a tile something stands on
    // (a monster) can't be entered, so next to it is as far as the step goes.
    async Task StepToward(Tile target, CancellationToken ct)
    {
        var s = Snapshot;
        if (s.Map is not { } map || s.Position is not { } origin)
            throw new QuestBlockedException("Waiting for server map and position.");
        IReadOnlyList<Tile> path;
        try
        {
            path = maps.Load(map).Path(origin, target, Obstacles(s, map, target), allowBlockedGoal: true);
        }
        catch (InvalidOperationException)
        {
            throw new QuestBlockedException("No walkable route to the monster.");
        }

        bool occupied = s.Entities.Values.Any(e => e.Blocks && e.Id != s.Id && e.Position == target);
        if (path.Count > 1 || path.Count == 1 && !occupied)
            await WalkTo(path[0], ct, map);
    }

    HashSet<Tile> Obstacles(CharacterSnapshot s, int map, Tile goal)
    {
        var record = maps.Record(map);
        var blocked = s.Entities.Values.Where(e => e.Blocks && e.Id != s.Id).Select(e => e.Position).ToHashSet();
        blocked.UnionWith(record.ExitTiles);
        blocked.UnionWith(record.InternalTransfers.Select(t => t.From));
        blocked.Remove(goal);
        return blocked;
    }

    public bool IsWalkable(Tile tile)
    {
        var s = Snapshot;
        if (s.Map is not { } map)
            return false;
        try
        {
            // Talos counts monsters, merchants and players as standing in the way, never walk-through creatures.
            return !maps.Load(map).Blocked(tile) && !s.Entities.Values.Any(e => e.Position == tile && e.Id != s.Id && (e.IsPlayer || e.Kind is 0 or 2));
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or KeyNotFoundException)
        {
            return false;
        }
    }

    public bool IsWarp(Tile tile)
    {
        if (Snapshot.Map is not { } map)
            return false;
        try
        {
            var record = maps.Record(map);
            return record.ExitTiles.Contains(tile) || record.InternalTransfers.Any(t => t.From == tile);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or KeyNotFoundException)
        {
            return false;
        }
    }

    public (int Width, int Height)? Size => connection.Tracker.Width > 0 && connection.Tracker.Height > 0 ? (connection.Tracker.Width, connection.Tracker.Height) : null;

    public bool IsWall(Tile tile)
    {
        if (Snapshot.Map is not { } map)
            return true;
        try
        {
            return maps.Load(map).Blocked(tile);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or KeyNotFoundException)
        {
            return true;
        }
    }

    public int PathLength(Tile from, Tile to)
    {
        var s = Snapshot;
        if (s.Map is not { } map)
            return 0;
        try
        {
            return maps.Load(map).Path(from, to, Obstacles(s, map, to), allowBlockedGoal: true).Count;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or KeyNotFoundException)
        {
            return 0;
        }
    }
}
