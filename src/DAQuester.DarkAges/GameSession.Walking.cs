using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    async Task<CharacterSnapshot> RefreshView(CancellationToken ct)
    {
        // RefreshUser is F5. Look (0x09) can interact with the facing tile/sign.
        // Excalibur's acknowledged walker blocks new movement until authoritative
        // position reanchors recovery. A fixed sleep can race a delayed refresh.
        var before = Snapshot;
        bool Refreshed(CharacterSnapshot s) => s.Revision > before.Revision &&
            (connection.Tracker.WorldMap is not null || s.Position is not null &&
                (s.Map != before.Map || connection.Tracker.LastPositionRevision > before.Revision
                    && s.Revision >= connection.Tracker.LastPositionRevision));
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var current = Snapshot;
            if (!current.Connected || current.Generation != before.Generation)
                throw new IOException("Session disconnected or replaced.");
            // A delayed reply to an earlier F5 still proves the refresh. Keep
            // the original baseline instead of discarding that late receipt.
            if (Refreshed(current))
                return current;
            await connection.SendAsync(new ClientPacket((byte)ClientCommand.RefreshUser, []), before.Generation, ct);
            try
            {
                return await WaitAsync(Refreshed, before.Revision, TimeSpan.FromSeconds(5), ct);
            }
            catch (TimeoutException)
            {
                connection.Report((before.Name ?? "Walker") + ": server position refresh delayed; retrying F5 while movement waits for authoritative position.");
            }
        }
    }

    async Task WalkTo(Tile target, CancellationToken ct, int? expectedMap = null, InventoryGoal? stopWhen = null,
        Tile? transferLanding = null, Func<bool>? replanWhen = null)
    {
        expectedMap ??= Snapshot.Map;
        int retries = 0;
        var avoided = new HashSet<Tile>();
        while (Snapshot.Position != target)
        {
            if (replanWhen?.Invoke() == true)
                return;
            if (GoalReached(stopWhen))
                return;
            if (await DismissTravelNotice(ct))
            {
                await SettleMovement(ct);
                retries = 0;
                avoided.Clear();
                if (Snapshot.Map != expectedMap || connection.Tracker.WorldMap is not null)
                    return;
            }

            if (FastMode && MovementCadence(Snapshot) == 160)
            {
                var fastOrigin = Snapshot.Position;
                long confirmed = VisibleMoves;
                var fast = await WalkFast(target, expectedMap, stopWhen, transferLanding, replanWhen, avoided, ct);
                if (fast.Done)
                    return;
                if (fast.Failed is { } failed)
                {
                    if (VisibleMoves > confirmed)
                        retries = 0;
                    if (++retries >= 3)
                    {
                        var route = maps.Record(expectedMap!.Value);
                        if (stopWhen is not null && fast.Corrected && fast.Origin == fastOrigin && VisibleMoves == confirmed
                            && !route.Portals.Any(p => p.From == target) && !route.InternalTransfers.Any(t => t.From == target))
                        {
                            await SettleMovement(ct);
                            return;
                        }

                        throw new QuestBlockedException("Fast movement corrected or unanswered three times. Inspect route.");
                    }

                    CharacterSnapshot fastRefresh;
                    if (connection.Movement is { Pending: 0, CanReanchor: true })
                    {
                        connection.AcceptServerPosition();
                        fastRefresh = Snapshot;
                    }
                    else
                        fastRefresh = await RefreshView(ct);
                    if (connection.Tracker.WorldMap is not null || fastRefresh.Map != expectedMap)
                        return;
                    if (fastRefresh.Position != fast.Origin)
                    {
                        retries = 0;
                        avoided.Clear();
                        if (replanWhen?.Invoke() == true)
                            return;
                    }
                    else
                        avoided.Add(failed);
                    continue;
                }
            }

            ct.ThrowIfCancellationRequested();
            var before = Snapshot;
            if (before.Map is null || before.Position is null)
                before = await WaitAsync(x => x.Map is not null && x.Position is not null, before.Revision - 1, TimeSpan.FromSeconds(2), ct);
            if (before.Map is null || before.Position is null)
                throw new QuestBlockedException("Waiting for refreshed map and position.");
            if (before.Map != expectedMap)
                throw new QuestBlockedException("Map changed while navigating; inspect doorway pushback.");
            var grid = maps.Load(before.Map.Value);
            var record = maps.Record(before.Map.Value);
            var permanentObstacles = record.ExitTiles.Concat(record.InternalTransfers.Select(t => t.From)).Where(p => p != target).ToHashSet();
            var blocked = before.Entities.Values.Where(x => x.Blocks && x.Id != before.Id).Select(x => x.Position).ToHashSet();
            blocked.UnionWith(permanentObstacles);
            blocked.UnionWith(avoided);
            bool portalGoal = record.ExitTiles.Contains(target);
            IReadOnlyList<Tile> path;
            try
            {
                path = grid.Path(before.Position, target, blocked, allowBlockedGoal: portalGoal);
            }
            catch (InvalidOperationException) when (blocked.Except(permanentObstacles).Any())
            {
                // Only a route blocked by transient occupancy may be skipped. Bad terrain
                // or a missing static route still fails here before any movement is sent.
                grid.Path(before.Position, target, permanentObstacles, allowBlockedGoal: portalGoal);
                if (stopWhen is not null && !portalGoal)
                    return; // Try the next patrol tile; revisit on the next lap.
                if (++retries >= 3)
                    throw new QuestBlockedException("Destination remains occupied after three route refreshes.");
                await RefreshView(ct);
                await Task.Delay(cadence, ct);
                continue;
            }

            if (path.Count == 0)
                return;
            var next = path[0];
            if (bounce.Bounced(before.Map.Value, next))
            {
                connection.Report((before.Name ?? "Walker") + ": walk bounced between ("
                    + before.Position.X + "," + before.Position.Y + ") and (" + next.X + "," + next.Y
                    + ") toward (" + target.X + "," + target.Y + "); path " + path.Count + " steps: "
                    + string.Join(" ", path.Take(4).Select(p => "(" + p.X + "," + p.Y + ")"))
                    + "; " + blocked.Count + " tiles blocked; avoiding (" + next.X + "," + next.Y + ")");
                avoided.Add(next);
                continue;
            }
            byte dir = (byte)(next.Y < before.Position.Y ? 0 : next.X > before.Position.X ? 1 : next.Y > before.Position.Y ? 2 : 3);
            await WaitForMovementCadence(ct);
            if (replanWhen?.Invoke() == true)
                return;
            if (GoalReached(stopWhen))
                return; // Inventory often arrives just after the movement acknowledgment.
            var current = Snapshot;
            if (lastMove.AddMilliseconds(MovementCadence(current)) > DateTimeOffset.UtcNow)
                continue;
            if (IsTravelNotice(current))
                continue;
            if (replanWhen is not null && current.Map == before.Map && current.Position is not null && current.Position != before.Position)
                return;
            if (current.Map != before.Map || current.Position != before.Position
                || current.Entities.Values.Any(e => e.Blocks && e.Id != current.Id && e.Position == next))
                continue;
            try
            {
                await connection.SendAsync(Packet(new ClientMoveMessage { Direction = (WorldDirection)dir }),
                    current.Generation, ct, movementAllowed: () => !IsTravelNotice(Snapshot) && replanWhen?.Invoke() != true);
            }
            catch (MovementReplanException)
            {
                continue;
            }

            lastMove = DateTimeOffset.UtcNow;
            CharacterSnapshot? ack = null;
            try
            {
                ack = await WaitAsync(x => IsTravelNotice(x) || connection.Tracker.WorldMap is not null || x.Map != before.Map
                    || x.Position is not null && (x.Position == next || x.Position != before.Position
                        || connection.Tracker.LastMovementRevision > before.Revision && x.Revision >= connection.Tracker.LastMovementRevision
                        || connection.Tracker.LastPositionRevision > before.Revision && x.Revision >= connection.Tracker.LastPositionRevision),
                    before.Revision, TimeSpan.FromSeconds(2), ct);
            }
            catch (TimeoutException)
            {
            } // A delayed reply must drain through refresh before another step.

            if (ack is not null && portalGoal && next == target && Snapshot.EntranceRefusedRevision > before.Revision && Snapshot.Map == before.Map)
            {
                connection.AcceptServerPosition();
                throw new EntranceRefusedException(Snapshot.EntranceRefusedText ?? "Entrance refused.", target);
            }

            if (ack is not null)
            {
                if (await DismissTravelNotice(ct))
                {
                    await SettleMovement(ct);
                    retries = 0;
                    avoided.Clear();
                    if (Snapshot.Map != expectedMap || connection.Tracker.WorldMap is not null)
                        return;
                    continue;
                }

                if (connection.Tracker.WorldMap is not null)
                    return;
                if (ack.Map != before.Map)
                    return;
                if (transferLanding is not null && ack.Position == transferLanding)
                    return;
                if (ack.Position == next)
                {
                    retries = 0;
                    bounce.Stepped(before.Map.Value, before.Position);
                    continue;
                }

                // A rejected entrance step still needs its recorded trigger. Excluding
                // that goal makes every refreshed route fail without another attempt.
                // Keep the bounded retry below; live occupancy and entry refusals
                // are checked independently before any subsequent movement.
                if (!portalGoal || next != target)
                    avoided.Add(next);
            }

            if (++retries >= 3)
            {
                if (stopWhen is not null && !portalGoal && ack?.Position == before.Position)
                    return;
                throw new QuestBlockedException(ack is null
                    ? "Movement unacknowledged after three resynchronized attempts."
                    : "Position corrected three times without walking progress. Inspect route.");
            }

            if (ack is not null && connection.Movement is { Pending: 0, CanReanchor: true })
            {
                connection.AcceptServerPosition();
                if (Snapshot.Position != before.Position)
                {
                    retries = 0;
                    avoided.Clear();
                    if (replanWhen is not null)
                        return; // A displacement can make a different doorway the shortest route.
                }
                continue; // Replan from the real rejection position, without an unnecessary F5.
            }
            var refreshed = await RefreshView(ct);
            if (connection.Tracker.WorldMap is not null || refreshed.Map != before.Map)
                return;
            if (refreshed.Position != before.Position)
            {
                retries = 0;
                avoided.Clear();
                if (replanWhen is not null)
                    return;
            }
            else if (!portalGoal || next != target)
                avoided.Add(next);
        }
    }
}

// Walking straight back onto the tile just left, again and again, is a bounce.
internal sealed class BounceGuard(int limit)
{
    (int Map, Tile Tile)? lastLeft;
    int count;

    public void Stepped(int map, Tile from) => lastLeft = (map, from);

    public bool Bounced(int map, Tile next)
    {
        if (lastLeft != (map, next))
        {
            count = 0;
            return false;
        }

        if (++count < limit)
            return false;
        count = 0;
        lastLeft = null;
        return true;
    }
}
