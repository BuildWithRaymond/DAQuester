using System.Buffers.Binary;
using Arbiter.Net.Server;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;

namespace DAQuester.DarkAges.Transport;

public readonly record struct MovementWindow(int Pending, bool Recovery, Tile? Projected, Tile? FirstTarget, DateTimeOffset? Oldest, int Capacity);
internal sealed class MovementReplanException : Exception;
public sealed partial class ProxyConnection
{
    readonly object movementGate = new();
    readonly Queue<PendingStep> pendingSteps = new();
    sealed record PendingStep(MovementIntent Intent, bool Presented, DateTimeOffset Sent);
    bool movementRecovery, refreshRequested;
    Tile? failedMovementTarget;
    double? acknowledgmentMilliseconds;
    public event Action? MovementChanged;
    public MovementWindow Movement
    {
        get
        {
            lock (movementGate)
            {
                var first = pendingSteps.FirstOrDefault();
                var last = pendingSteps.LastOrDefault();
                // Excalibur's acknowledged fast walker starts at three and adapts to RTT,
                // capped at four. Projection is transport bookkeeping, never quest evidence.
                int capacity = acknowledgmentMilliseconds is { } rtt ? Math.Clamp((int)Math.Ceiling(rtt / 160), 1, 4) : 3;
                return new(pendingSteps.Count, movementRecovery, last is null ? Tracker.Snapshot.Position : StateTracker.GridStep(last.Intent.Origin, last.Intent.Direction), failedMovementTarget ?? (first is null ? null : StateTracker.GridStep(first.Intent.Origin, first.Intent.Direction)), first?.Sent, capacity);
            }
        }
    }

    public void CancelMovement()
    {
        lock (movementGate)
        {
            if (pendingSteps.TryPeek(out var first))
            {
                movementRecovery = true;
                failedMovementTarget ??= StateTracker.GridStep(first.Intent.Origin, first.Intent.Direction);
            }
        }

        MovementChanged?.Invoke();
    }

    void ResetMovement()
    {
        pendingSteps.Clear();
        movementRecovery = false;
        refreshRequested = false;
        failedMovementTarget = null;
    }

    void RegisterMovement(byte direction, long expectedGeneration, Tile? origin, bool presented, int capacity)
    {
        var s = Tracker.Snapshot;
        if (!Connected || !s.Connected || s.Generation != expectedGeneration)
            throw new IOException("Session generation no longer active.");
        if (s.Id is not { } id || s.Map is not { } map || s.Position is not { } observed || direction > 3)
            throw new QuestBlockedException("Observed movement origin missing.");
        var last = pendingSteps.LastOrDefault();
        var projected = last is null ? observed : StateTracker.GridStep(last.Intent.Origin, last.Intent.Direction);
        if (movementRecovery || pendingSteps.Count >= Math.Clamp(capacity, 1, 4) || origin is not null && origin != projected)
            throw new QuestBlockedException("Movement must settle before another step.");
        pendingSteps.Enqueue(new(new(expectedGeneration, map, id, origin ?? projected, direction), presented, DateTimeOffset.UtcNow));
    }

    // Called under both toClient and movementGate, together with Tracker.Observe.
    // Server packets remain the only source of observed state.
    ServerPacket? ObserveMovement(CharacterSnapshot before, byte opcode, byte[] payload)
    {
        if (opcode == 0x15)
        {
            ResetMovement();
            return null;
        }

        if (opcode == 0x04 && payload.Length >= 4)
        {
            if (refreshRequested)
                ResetMovement();
            else if (pendingSteps.TryPeek(out var first))
            {
                movementRecovery = pendingSteps.Any(p => p.Presented);
                failedMovementTarget = movementRecovery ? StateTracker.GridStep(first.Intent.Origin, first.Intent.Direction) : null;
                pendingSteps.Clear();
            }

            return null; // The real position packet already corrects the client's display.
        }

        if (opcode != 0x0B || payload.Length < 5 || pendingSteps.Count == 0)
            return null;
        var step = pendingSteps.Dequeue();
        var s = before;
        var intent = step.Intent;
        var origin = new Tile(BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(1)), BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(3)));
        bool matched = payload[0] <= 3 && origin == intent.Origin && payload[0] == intent.Direction && s.Connected && s.Generation == intent.Generation && s.Map == intent.Map && s.Id == intent.PlayerId;
        if (matched && !movementRecovery)
        {
            var rtt = (DateTimeOffset.UtcNow - step.Sent).TotalMilliseconds;
            acknowledgmentMilliseconds = acknowledgmentMilliseconds is { } old ? old * .75 + rtt * .25 : rtt;
            Interlocked.Increment(ref visibleMoves);
            return step.Presented ? null : PlayerPresentation.Move(intent);
        }

        movementRecovery = true;
        failedMovementTarget ??= StateTracker.GridStep(intent.Origin, intent.Direction);
        return step.Presented || pendingSteps.Any(p => p.Presented) ? PlayerPresentation.Position(StateTracker.GridStep(origin, payload[0])) : PlayerPresentation.AcknowledgedMove(intent, s, opcode, payload);
    }
}
