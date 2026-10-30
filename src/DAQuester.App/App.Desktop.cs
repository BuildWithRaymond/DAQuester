using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.App.Views;

namespace DAQuester.App;

public partial class App
{
    internal BackgroundLifetime? Background { get; private set; }
    internal ResidentTray? Tray { get; private set; }
    internal int PreviewExitCode { get; set; }

    void ConfigureResidentLifetime(IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow window, DesktopRuntime runtime, MainWindowViewModel viewModel)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ResidentTray? tray = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        void Show()
        {
            desktop.MainWindow = window;
            if (!window.IsVisible) window.Show();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            foreach (var dialog in window.OwnedWindows.Where(w => w.IsVisible)) dialog.Activate();
        }
        var lifetime = new BackgroundLifetime(runtime.StartAsync, async () =>
        {
            if (runtime.TryBeginUpdateExit()) return;
            if (window.IsVisible) _ = runtime.RefreshPresentationAsync();
            await runtime.TickSchedulerAsync();
            await runtime.TickDanaanContinuationsAsync();
        }, async install =>
        {
            // Dismiss authorizations as cancellations before waiting for owners
            // that may be awaiting those dialogs to finish saving.
            foreach (var dialog in window.OwnedWindows.ToArray()) dialog.Close();
            await runtime.ShutdownAsync(install);
        }, () => runtime.Settings.KeepRunningInTray && tray?.Available == true,
        window.Hide, Show, () =>
        {
            timer.Stop();
            tray?.Dispose();
            desktop.Shutdown(PreviewExitCode);
        }, viewModel.ReportError);
        Background = lifetime;

        runtime.ShowWindowRequested = lifetime.Show;
        viewModel.RequestClose += async () => await lifetime.ExitAsync(viewModel.InstallRequested);
        window.Closing += async (_, e) =>
        {
            if (lifetime.IsExited) return;
            e.Cancel = true;
            if (e.CloseReason is WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown)
                await lifetime.ExitAsync();
            else
                await lifetime.CloseWindowAsync();
        };
        desktop.ShutdownRequested += async (_, e) =>
        {
            if (lifetime.IsExited) return;
            e.Cancel = true;
            await lifetime.ExitAsync();
        };
        desktop.Exit += (_, _) => { timer.Stop(); tray?.Dispose(); };
        try { tray = new ResidentTray(runtime, viewModel, lifetime); }
        catch (Exception error) { viewModel.ReportError(error); }
        Tray = tray;

        bool background = Program.Arguments.Contains("--background") && runtime.Settings.KeepRunningInTray && tray?.Available == true;
        desktop.MainWindow = background ? null : window;
        timer.Tick += async (_, _) => await lifetime.TickAsync();
        Dispatcher.UIThread.Post(async () =>
        {
            timer.Start();
            await lifetime.StartAsync();
            if (!runtime.Preview && !runtime.Ready) lifetime.Show();
            if (Program.IsValidation)
                await Validation.ScreenshotCapture.RunAsync(window, viewModel);
        });
    }
}
