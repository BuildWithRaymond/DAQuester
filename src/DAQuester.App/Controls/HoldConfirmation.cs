namespace DAQuester.App.Controls;

// Keeps confirmation independent of timer frequency and keyboard repeat events.
public sealed class HoldConfirmation
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(3);
    TimeSpan started;
    object? context;
    bool pressed;
    public bool IsHolding { get; private set; }

    public bool Press(TimeSpan now, object? scope)
    {
        if (pressed)
            return false;
        pressed = true;
        IsHolding = true;
        started = now;
        context = scope;
        return true;
    }

    public bool Advance(TimeSpan now, object? scope, bool enabled)
    {
        if (!IsHolding)
            return false;
        if (!enabled || !Equals(context, scope))
        {
            Cancel();
            return false;
        }
        if (now - started < Duration)
            return false;
        IsHolding = false;
        return true;
    }

    public double Progress(TimeSpan now) => IsHolding
        ? Math.Clamp((now - started).TotalMilliseconds / Duration.TotalMilliseconds, 0, 1)
        : 0;

    public void Cancel() => IsHolding = false;

    public void Release()
    {
        pressed = false;
        IsHolding = false;
        context = null;
    }
}
