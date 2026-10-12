using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    volatile bool fastMode;
    TaskCompletionSource movementChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool FastMode
    {
        get => fastMode;
        set
        {
            if (fastMode == value)
                return;
            fastMode = value;
            PulseMovement();
        }
    }

    void PulseMovement() => Interlocked.Exchange(ref movementChanged, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    public string FastModeDescription => !FastMode ? "slows near players" : !Snapshot.Connected ? "ready when connected" : Snapshot.Position is null || Snapshot.Id is null ? "waiting for location" : Snapshot.Map is 10056 or 10180 ? "normal pace in this area" : MovementCadence(Snapshot) == 160 ? "fast pace" : "normal pace near players";

    internal int MovementCadence(CharacterSnapshot state, Tile? projected = null)
    {
        if (!FastMode)
            return cadence;
        // Excalibur Fast is 160ms; unsafe ordinary walking is 430ms. Every other
        // observed player counts here, without friend or alt exemptions.
        if (!state.Connected || state.Id is null || state.Position is not { } origin || state.Map is null or 10056 or 10180)
            return 430;
        return state.Entities.Values.Any(e => e.IsPlayer && e.Id != state.Id && (Distance(origin, e.Position) <= 20 || projected is not null && Distance(projected, e.Position) <= 20)) ? 430 : 160;
    }

    static int Distance(Tile a, Tile b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    async Task WaitForMovementCadence(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var changed = Volatile.Read(ref movementChanged).Task;
            var delay = lastMove.AddMilliseconds(MovementCadence(Snapshot)) - DateTimeOffset.UtcNow;
            if (delay <= TimeSpan.Zero)
                return;
            try
            {
                await changed.WaitAsync(delay, ct);
            }
            catch (TimeoutException)
            {
            } // Recheck players and checkbox before sending.
        }
    }
}
