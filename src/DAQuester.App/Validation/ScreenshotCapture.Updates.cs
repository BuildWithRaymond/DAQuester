using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DAQuester.App.ViewModels;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    static async Task CaptureUpdatesAsync(Window window, MainWindowViewModel vm)
    {
        if (!Program.IsScreenshots || !vm.Runtime.Preview)
            throw new InvalidOperationException("Isolated preview required.");
        vm.Page = "Settings";
        var button = window.FindControl<Button>("updateAndRestart")!;
        var status = window.FindControl<TextBlock>("updateStatus")!;
        var scroll = window.FindControl<ScrollViewer>("settingsPage")!;
        int activations = 0;
        bool eligible = true;
        // Review the actual control with a synthetic command; never invoke an updater.
        button.Command = new RelayCommand(_ => activations++, _ => eligible);
        button.IsEnabled = true;
        void Click() => typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
        Click();
        bool clickedOnce = activations == 1;
        button.Focus();
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        bool keyboardActivated = activations == 2;
        eligible = false;
        button.IsEnabled = false;
        Click();
        bool disabledClickIgnored = activations == 2;

        foreach (bool compact in new[] { false, true })
        {
            window.Width = compact ? 900 : 1420;
            window.Height = compact ? 560 : 900;
            foreach (string state in new[] { "ready", "waiting", "error" })
            {
                eligible = state != "waiting";
                button.IsEnabled = eligible;
                status.Text = state switch
                {
                    "waiting" => "Version 1.8.2 is ready. It will install and restart when quests and game clients are closed.",
                    "error" => "Couldn't start the update. Your download is saved; click Update to try again.",
                    _ => "Version 1.8.2 is available. Click Update and restart when you're ready."
                };
                scroll.Offset = new Vector(0, 10000);
                await Task.Delay(180);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                bitmap.Render(window);
                bitmap.Save("artifacts/screenshots/updates-" + state + (compact ? "-compact" : "") + ".png");
            }
        }
        File.WriteAllText("artifacts/updates-ui-review.json", JsonSerializer.Serialize(new
        {
            isolated = true, synthetic = true, clickedOnce, keyboardActivated, disabledClickIgnored,
            updaterInvoked = false, realClientsLaunched = false
        }));
        if (!clickedOnce || !keyboardActivated || !disabledClickIgnored)
            throw new InvalidOperationException("Synthetic update control review failed.");
    }
}
