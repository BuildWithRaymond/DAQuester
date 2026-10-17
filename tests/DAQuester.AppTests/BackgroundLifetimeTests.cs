using DAQuester.App.Services;
using Xunit;

namespace DAQuester.AppTests;

public class BackgroundLifetimeTests
{
    sealed class Harness
    {
        public int Starts, Ticks, Stops, Hides, Shows, Exits, Errors;
        public bool Installed;
        public bool KeepRunning = true;
        public bool TrayAvailable = true;
        public Func<Task>? Starting;
        public Func<Task>? Stopping;
        public Func<int, Task>? Ticking;
        public BackgroundLifetime Create() => new(
            async () => { Starts++; if (Starting is not null) await Starting(); },
            async () => { Ticks++; if (Ticking is not null) await Ticking(Ticks); },
            async install => { Stops++; Installed = install; if (Stopping is not null) await Stopping(); },
            () => KeepRunning && TrayAvailable,
            () => Hides++, () => Shows++, () => Exits++, _ => Errors++);
    }

    [Fact]
    public async Task A_long_scheduled_run_does_not_suppress_independent_continuation_ticks()
    {
        var firstRun = new TaskCompletionSource();
        var h = new Harness { Ticking = count => count == 1 ? firstRun.Task : Task.CompletedTask };
        var lifetime = h.Create();
        await lifetime.StartAsync();
        var first = lifetime.TickAsync();
        await lifetime.TickAsync();
        Assert.Equal(2, h.Ticks);
        firstRun.SetResult();
        await first;
    }

    [Fact]
    public async Task Hidden_start_initializes_once_and_ticks_without_showing_a_window()
    {
        var h = new Harness();
        var lifetime = h.Create();
        await lifetime.StartAsync();
        await lifetime.StartAsync();
        await lifetime.TickAsync();
        Assert.Equal(1, h.Starts);
        Assert.Equal(1, h.Ticks);
        Assert.Equal(0, h.Shows);
    }

    [Fact]
    public async Task Closing_to_tray_keeps_runtime_and_ticks_alive()
    {
        var h = new Harness();
        var lifetime = h.Create();
        await lifetime.StartAsync();
        await lifetime.CloseWindowAsync();
        await lifetime.TickAsync();
        Assert.Equal(1, h.Hides);
        Assert.Equal(0, h.Stops);
        Assert.Equal(1, h.Ticks);
        lifetime.Show();
        Assert.Equal(1, h.Shows);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Close_exits_when_background_is_disabled_or_no_tray_is_available(bool keep, bool tray)
    {
        var h = new Harness { KeepRunning = keep, TrayAvailable = tray };
        var lifetime = h.Create();
        await lifetime.CloseWindowAsync();
        Assert.Equal(1, h.Stops);
        Assert.Equal(1, h.Exits);
        Assert.Equal(0, h.Starts);
        Assert.Equal(0, h.Hides);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_exit_and_install_wait_for_saving_and_never_hide(bool install)
    {
        var saved = new TaskCompletionSource();
        var h = new Harness { Stopping = () => saved.Task };
        var lifetime = h.Create();
        var first = lifetime.ExitAsync(install);
        var second = lifetime.ExitAsync(install);
        await lifetime.TickAsync();
        Assert.Equal(1, h.Stops);
        Assert.Equal(0, h.Exits);
        Assert.Equal(0, h.Ticks);
        saved.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, h.Exits);
        Assert.Equal(install, h.Installed);
        Assert.True(lifetime.IsExited);
    }

    [Fact]
    public async Task Exit_waits_for_in_flight_startup_and_prevents_a_late_restart()
    {
        var started = new TaskCompletionSource();
        var h = new Harness { Starting = () => started.Task };
        var lifetime = h.Create();
        var start = lifetime.StartAsync();
        var exit = lifetime.ExitAsync();
        Assert.Equal(0, h.Stops);
        started.SetResult();
        await Task.WhenAll(start, exit);
        await lifetime.StartAsync();
        await lifetime.TickAsync();
        Assert.Equal(1, h.Starts);
        Assert.Equal(1, h.Stops);
        Assert.Equal(0, h.Ticks);
    }

    [Fact]
    public async Task Failed_shutdown_restores_access_and_allows_retry()
    {
        var h = new Harness { Stopping = () => throw new IOException("Synthetic checkpoint failure") };
        var lifetime = h.Create();
        await lifetime.ExitAsync();
        Assert.False(lifetime.IsExited);
        Assert.Equal(1, h.Shows);
        Assert.Equal(1, h.Errors);
        Assert.Equal(0, h.Exits);
        h.Stopping = null;
        await lifetime.ExitAsync();
        Assert.True(lifetime.IsExited);
    }
}
