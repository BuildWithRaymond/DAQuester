using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    sealed record FastWalkResult(bool Done, Tile? Failed = null, Tile? Origin = null, bool Corrected = false);
    async Task SettleMovement(CancellationToken ct)
    {
        while (true)
        {
            var changed = Volatile.Read(ref movementChanged).Task;
            var window = connection.Movement;
            ct.ThrowIfCancellationRequested();
            if (!Snapshot.Connected)
                throw new IOException("Session disconnected or replaced.");
            if (await DismissTravelNotice(ct))
                continue;
            if (window.Recovery)
            {
                await RefreshView(ct);
                return;
            }

            if (window.Pending == 0)
                return;
            var delay = window.Oldest!.Value.AddSeconds(2) - DateTimeOffset.UtcNow;
            if (delay <= TimeSpan.Zero)
            {
                connection.CancelMovement();
                continue;
            }

            try
            {
                await changed.WaitAsync(delay, ct);
            }
            catch (TimeoutException)
            {
            }
        }
    }

    async Task<FastWalkResult> WalkFast(Tile target, int? expectedMap, InventoryGoal? stopWhen, Tile? transferLanding, Func<bool>? replanWhen, HashSet<Tile> avoided, CancellationToken ct)
    {
        bool draining = false, done = false;
        long generation = Snapshot.Generation;
        while (true)
        {
            // Capture the pulse before inspecting state so an acknowledgment cannot be lost.
            var changed = Volatile.Read(ref movementChanged).Task;
            var window = connection.Movement;
            var state = Snapshot;
            ct.ThrowIfCancellationRequested();
            if (!state.Connected || state.Generation != generation)
                throw new IOException("Session disconnected or replaced.");
            // A town popup may reject a projected step. Close it before refresh or
            // draining pending moves, then reanchor from authoritative server position.
            if (await DismissTravelNotice(ct))
            {
                await SettleMovement(ct);
                return new(Snapshot.Map != expectedMap || connection.Tracker.WorldMap is not null);
            }

            if (window.Recovery)
                return new(false, window.FirstTarget ?? target, state.Position, Corrected: true);
            if (connection.Tracker.WorldMap is not null || state.Map != expectedMap)
                return new(true);
            if (state.Position is null)
            {
                await changed.WaitAsync(TimeSpan.FromSeconds(2), ct);
                continue;
            }

            if (GoalReached(stopWhen) || replanWhen?.Invoke() == true || transferLanding is not null && state.Position == transferLanding)
            {
                draining = true;
                done = true;
            }

            if (!FastMode || MovementCadence(state, window.Projected) != 160 || IsTravelNotice(state))
                draining = true;
            var origin = window.Projected ?? state.Position;
            if (origin == target)
                draining = true;
            if (draining && window.Pending == 0)
                return new(done || state.Position == target);
            Tile? next = null;
            if (!draining && window.Pending < window.Capacity)
            {
                var record = maps.Record(state.Map!.Value);
                var triggers = record.ExitTiles.Concat(record.InternalTransfers.Select(t => t.From)).ToHashSet();
                var blocked = state.Entities.Values.Where(e => e.Blocks && e.Id != state.Id).Select(e => e.Position).ToHashSet();
                blocked.UnionWith(triggers.Where(t => t != target));
                blocked.UnionWith(avoided);
                try
                {
                    next = maps.Load(state.Map.Value).Path(origin, target, blocked, allowBlockedGoal: record.ExitTiles.Contains(target)).FirstOrDefault();
                }
                catch (InvalidOperationException)
                {
                    draining = true;
                }

                // Doors and internal transfers use the ordinary acknowledged path. Drain
                // the approach first; never project a step through a map transition.
                if (next is null || triggers.Contains(next))
                {
                    draining = true;
                    if (window.Pending == 0)
                        return new(false);
                }
            }

            if (window.Pending > 0 && window.Oldest!.Value.AddSeconds(2) <= DateTimeOffset.UtcNow)
            {
                connection.CancelMovement();
                return new(false, window.FirstTarget, state.Position);
            }

            var due = lastMove.AddMilliseconds(160) - DateTimeOffset.UtcNow;
            if (!draining && next is not null && due <= TimeSpan.Zero)
            {
                // Recheck player safety and collision immediately before writing the step.
                var current = Snapshot;
                var latest = connection.Movement;
                if (current.Generation != generation || current.Map != state.Map || current.Position != state.Position || !ReferenceEquals(current.Entities, state.Entities) || latest.Projected != origin)
                    continue;
                if (!FastMode || GoalReached(stopWhen) || replanWhen?.Invoke() == true || MovementCadence(current, origin) != 160)
                    continue;
                byte direction = (byte)(next.Y < origin.Y ? 0 : next.X > origin.X ? 1 : next.Y > origin.Y ? 2 : 3);
                bool Allowed()
                {
                    var observed = Snapshot;
                    var pending = connection.Movement;
                    return FastMode && !pending.Recovery && pending.Pending < pending.Capacity && pending.Projected == origin && observed.Generation == generation && observed.Map == state.Map && observed.Position == state.Position && ReferenceEquals(observed.Entities, state.Entities) && !IsTravelNotice(observed) && !GoalReached(stopWhen) && replanWhen?.Invoke() != true && MovementCadence(observed, origin) == 160;
                }

                // The transport checks the guard under the same lock used to observe
                // players and acknowledgments, before consuming a movement counter.
                try
                {
                    await connection.SendAsync(Packet(new ClientMoveMessage { Direction = (WorldDirection)direction }), generation, ct, origin, presentMovement: true, movementCapacity: latest.Capacity, movementAllowed: Allowed);
                }
                catch (MovementReplanException)
                {
                    continue;
                }

                lastMove = DateTimeOffset.UtcNow;
                continue;
            }

            var delay = window.Oldest is { } sent ? sent.AddSeconds(2) - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(2);
            if (!draining && next is not null && due > TimeSpan.Zero && due < delay)
                delay = due;
            if (delay <= TimeSpan.Zero)
                continue;
            try
            {
                await changed.WaitAsync(delay, ct);
            }
            catch (TimeoutException)
            {
            }
        }
    }
}
