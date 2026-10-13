using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Transport;

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
        bool fast = FastFightStep(Snapshot);
        if (!fast)
            await SettleMovement(ct);
        var s = Snapshot;
        var window = connection.Movement;
        if (s.Map is not { } map || s.Position is not { } position)
            throw new QuestBlockedException("Waiting for server map and position.");
        // An unanswered step goes through the refresh path on the next call.
        if (fast && window.Oldest is { } sent && sent.AddSeconds(2) <= DateTimeOffset.UtcNow)
        {
            connection.CancelMovement();
            return;
        }

        var origin = fast ? window.Projected ?? position : position;
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
        if (!(path.Count > 1 || path.Count == 1 && !occupied))
            return;
        if (!fast)
        {
            await WalkTo(path[0], ct, map);
            return;
        }

        // Sent at the 160 ms pace without waiting for the server's reply; a full window or an early tick sends on the next call.
        if (window.Pending >= window.Capacity || lastMove.AddMilliseconds(160) > DateTimeOffset.UtcNow)
            return;
        var next = path[0];
        byte direction = (byte)(next.Y < origin.Y ? 0 : next.X > origin.X ? 1 : next.Y > origin.Y ? 2 : 3);
        bool Allowed()
        {
            var o = Snapshot;
            var p = connection.Movement;
            return FastMode && !p.Recovery && p.Pending < p.Capacity && p.Projected == origin && o.Generation == s.Generation && o.Map == map && !IsTravelNotice(o) && MovementCadence(o, origin) == 160 && !o.Entities.Values.Any(e => e.Blocks && e.Id != o.Id && e.Position == next);
        }

        try
        {
            await connection.SendAsync(Packet(new ClientMoveMessage { Direction = (WorldDirection)direction }), s.Generation, ct, origin, presentMovement: true, movementCapacity: window.Capacity, movementAllowed: Allowed);
        }
        catch (MovementReplanException)
        {
            return;
        }

        lastMove = DateTimeOffset.UtcNow;
    }

    bool FastFightStep(CharacterSnapshot s)
    {
        var window = connection.Movement;
        return FastMode && !window.Recovery && !IsTravelNotice(s) && MovementCadence(s, window.Projected) == 160;
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
