namespace DAQuester.App.Controls;

// One reversible clock keeps layout and visual layers together without touching runtime state.
internal sealed class JourneyMotion
{
    double from, target, started, duration;

    public void Target(bool collapsed, double milliseconds, bool enabled = true)
    {
        double next = collapsed ? 1 : 0;
        if (!enabled)
        {
            from = target = next;
            duration = 0;
            return;
        }
        if (next == target)
            return;
        from = Position(milliseconds);
        target = next;
        started = milliseconds;
        duration = 520 * Math.Sqrt(Math.Abs(target - from));
    }

    public double Position(double milliseconds)
    {
        if (duration == 0)
            return target;
        double t = Math.Clamp((milliseconds - started) / duration, 0, 1);
        double eased = 1 - Math.Pow(1 - t, 4);
        return from + (target - from) * eased;
    }

    public bool Active(double milliseconds) => duration > 0 && milliseconds < started + duration;
    public double Activity(double milliseconds) => Active(milliseconds)
        ? Math.Sin(Math.PI * Math.Clamp((milliseconds - started) / duration, 0, 1)) : 0;
}
