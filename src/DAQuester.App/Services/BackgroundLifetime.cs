namespace DAQuester.App.Services;

internal sealed class BackgroundLifetime(Func<Task> start, Func<Task> tick,
    Func<bool, Task> shutdown, Func<bool> keepRunning, Action hide, Action show,
    Action exit, Action<Exception> report)
{
    Task? starting, exiting;
    bool started;
    public bool IsExited { get; private set; }
    public bool IsExiting { get; private set; }

    // All calls are serialized on the application's UI dispatcher.
    public Task StartAsync() => IsExiting || IsExited ? Task.CompletedTask : starting ??= StartCoreAsync();
    async Task StartCoreAsync()
    {
        try { await start(); started = true; }
        catch (Exception error) { report(error); Show(); }
    }

    public async Task TickAsync()
    {
        if (!started || IsExiting || IsExited) return;
        // The scheduler and continuations have their own ownership gates. A
        // long scheduled run must not suppress another character's later tick.
        try { await tick(); }
        catch (Exception error) { report(error); }
    }

    public Task CloseWindowAsync()
    {
        if (IsExiting || IsExited) return Task.CompletedTask;
        if (keepRunning()) { hide(); return Task.CompletedTask; }
        return ExitAsync();
    }

    public Task ExitAsync(bool install = false)
    {
        if (IsExited) return Task.CompletedTask;
        return exiting is { IsCompleted: false } ? exiting : exiting = ExitCoreAsync(install);
    }

    async Task ExitCoreAsync(bool install)
    {
        IsExiting = true;
        try
        {
            if (starting is not null) await starting;
            await shutdown(install);
            IsExited = true;
            exit();
        }
        catch (Exception error) { report(error); show(); }
        finally { IsExiting = false; }
    }

    public void Show()
    {
        if (!IsExiting && !IsExited) show();
    }
}
