using Avalonia.Controls;
using Avalonia.Platform;
using DAQuester.App.ViewModels;

namespace DAQuester.App.Services;

internal sealed class ResidentTray : IDisposable
{
    readonly DesktopRuntime runtime;
    readonly MainWindowViewModel viewModel;
    readonly BackgroundLifetime lifetime;
    readonly NativeMenuItem status = new() { IsEnabled = false };
    readonly NativeMenuItem activity = new() { IsEnabled = false };
    readonly NativeMenuItem next = new() { IsEnabled = false };
    readonly NativeMenuItem pause = new() { Header = "Pause scheduling" };
    bool disposed;
    internal TrayIcon Icon { get; }
    internal NativeMenu Menu { get; } = new();
    public bool Available => !disposed && Icon.NativeMenuExporter is not null;

    public ResidentTray(DesktopRuntime runtime, MainWindowViewModel viewModel, BackgroundLifetime lifetime)
    {
        this.runtime = runtime;
        this.viewModel = viewModel;
        this.lifetime = lifetime;
        using var image = AssetLoader.Open(new Uri("avares://DAQuester/Assets/daquester.ico"));
        Icon = new TrayIcon { Icon = new WindowIcon(image), IsVisible = true };
        var open = new NativeMenuItem { Header = "Open DAQuester" };
        open.Click += (_, _) => lifetime.Show();
        var schedules = new NativeMenuItem { Header = "Open scheduler" };
        schedules.Click += (_, _) => { viewModel.Page = "Scheduler"; lifetime.Show(); };
        pause.Click += async (_, _) =>
        {
            try { await runtime.SetSchedulerEnabledAsync(false, false); }
            catch (Exception error) { viewModel.ReportError(error); lifetime.Show(); }
        };
        var exit = new NativeMenuItem { Header = "Exit DAQuester" };
        exit.Click += async (_, _) => await lifetime.ExitAsync();
        foreach (var item in new NativeMenuItemBase[] { status, activity, next, new NativeMenuItemSeparator(),
                     open, schedules, pause, new NativeMenuItemSeparator(), exit })
            Menu.Items.Add(item);
        Icon.Menu = Menu;
        Icon.Clicked += (_, _) => lifetime.Show();
        runtime.Changed += Refresh;
        Refresh();
    }

    void Refresh()
    {
        if (disposed) return;
        var state = runtime.SchedulerState;
        bool attention = runtime.ConnectionError is not null || state.LoginBlocks.Length > 0
            || state.Entries.Any(e => e.Status == "Needs attention");
        string scheduler = !runtime.SchedulerEnabled ? "Scheduler off" : attention ? "Scheduler needs attention" : "Scheduler on";
        status.Header = scheduler;
        var running = state.Entries.FirstOrDefault(e => e.Status == "Running");
        activity.Header = running is null ? attention ? "Open DAQuester to review" : "No scheduled run active"
            : running.Character + " · " + (runtime.SchedulerRunPhase(running.Character) ?? "Running");
        var due = state.Entries.Where(e => e.Status == "Armed" && e.Profile == runtime.Settings.ServerProfile)
            .OrderBy(e => e.Due).FirstOrDefault();
        next.Header = due is null ? "No scheduled checks" : "Next check: " + due.Character + " · " + due.Due.ToLocalTime().ToString("MMM d, h:mm tt");
        pause.IsEnabled = runtime.SchedulerEnabled && !lifetime.IsExiting;
        string tooltip = "DAQuester · " + scheduler + (running is null ? "" : " · " + running.Character);
        Icon.ToolTipText = tooltip.Length <= 120 ? tooltip : tooltip[..119] + "…";
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        runtime.Changed -= Refresh;
        Icon.Dispose();
    }
}
