using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    static async Task CaptureTrayAsync(Window window, MainWindowViewModel vm)
    {
        if (!Program.IsScreenshots || !vm.Runtime.Preview) throw new InvalidOperationException("Isolated preview required.");
        var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        var app = (App)Application.Current!;
        var tray = app.Tray ?? throw new InvalidOperationException("Resident tray not initialized.");
        var lifetime = app.Background!;
        bool startedHidden = !window.IsVisible;
        if (Program.Arguments.Contains("--background") && !startedHidden)
            throw new InvalidOperationException("Background startup showed the main window.");
        lifetime.Show();
        async Task Capture(Window target, string name)
        {
            await Task.Delay(250);
            Dispatcher.UIThread.RunJobs();
            target.UpdateLayout();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)target.ClientSize.Width, (int)target.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(target);
            bitmap.Save("artifacts/screenshots/" + name + ".png");
        }
        // Exercise the actual pinned Win32 tray menu renderer. Reflection stays
        // inside this opt-in isolated visual-review path, never production logic.
        async Task<Window> OpenMenu(string name)
        {
            var platform = typeof(TrayIcon).GetField("_impl", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray.Icon)!;
            platform.GetType().GetMethod("OnRightClicked", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(platform, null);
            await Task.Delay(150);
            var menu = desktop.Windows.Single(w => w.Name?.StartsWith("AvaloniaTrayPopupRoot_", StringComparison.Ordinal) == true);
            await Capture(menu, name);
            return menu;
        }
        vm.Page = "Settings";
        window.Width = 1420; window.Height = 900;
        await Capture(window, "tray-settings");
        window.FindControl<ScrollViewer>("settingsPage")!.Offset = new Vector(0, 10000);
        await Capture(window, "tray-settings-save");
        window.Width = 900; window.Height = 560;
        window.FindControl<ScrollViewer>("settingsPage")!.Offset = new Vector(0, 150);
        await Capture(window, "tray-settings-compact");
        window.FindControl<ScrollViewer>("settingsPage")!.Offset = new Vector(0, 10000);
        await Capture(window, "tray-settings-compact-save");
        window.Close();
        if (window.IsVisible || lifetime.IsExited)
            throw new InvalidOperationException("Closing the window did not retain the background runtime.");
        var off = await OpenMenu("tray-menu-off");
        off.GetVisualDescendants().OfType<MenuItem>().Single(m => m.Header?.ToString() == "Open DAQuester")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(150);
        if (!window.IsVisible) throw new InvalidOperationException("The tray could not reopen the main window.");
        if (off.IsVisible) off.Close();
        window.Close();
        await vm.Runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
        await vm.Runtime.SetSchedulerEnabledAsync(true, true);
        var enabled = await OpenMenu("tray-menu-on");
        var pause = enabled.GetVisualDescendants().OfType<MenuItem>().Single(m => m.Header?.ToString() == "Pause scheduling");
        pause.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        for (int wait = 0; vm.Runtime.SchedulerEnabled && wait < 100; wait++) await Task.Delay(20);
        if (vm.Runtime.SchedulerEnabled) throw new InvalidOperationException("The tray did not pause scheduling.");
        if (enabled.IsVisible) enabled.Close();
        var paused = await OpenMenu("tray-menu-paused");
        paused.Close();
        lifetime.Show();
        File.WriteAllText("artifacts/screenshots/tray-review.json", JsonSerializer.Serialize(new
        {
            isolated = true, startedHidden, closedToTray = true, reopenedFromTray = true,
            schedulerPaused = !vm.Runtime.SchedulerEnabled, startupRegistryChanged = false,
            realClientsLaunched = false
        }));
    }
}
