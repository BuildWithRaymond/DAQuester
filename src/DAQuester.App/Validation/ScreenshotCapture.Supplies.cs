using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.ViewModels;
using DAQuester.App.Views;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    static async Task CaptureSuppliesAsync(Window window, MainWindowViewModel vm)
    {
        async Task Capture(Window target, string name)
        {
            await Task.Delay(150);
            Dispatcher.UIThread.RunJobs();
            target.UpdateLayout();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)target.ClientSize.Width, (int)target.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(target);
            bitmap.Save("artifacts/screenshots/" + name + ".png");
        }
        window.Width = 1100;
        window.Height = 760;
        vm.Page = "Settings";
        await Task.Delay(100);
        window.UpdateLayout();
        window.FindControl<CheckBox>("aislingExchangeSupplies")!.BringIntoView();
        window.FindControl<ScrollViewer>("settingsPage")!.Offset = new Vector(0, 10000);
        await Capture(window, "aislingexchange-settings");
        var offer = new QuestSupplyOffer(new("synthetic-offer", "ExamplePlayer", "datester-7.41", DateTimeOffset.UtcNow.AddMinutes(5),
            [new("Mold", 2), new("Viper's Gland", 3), new("Oren Brandy", 1)]), 1);
        var dialog = new AislingExchangeSupplyDialog(offer);
        dialog.Show(window);
        await Capture(dialog, "aislingexchange-offer");
        dialog.Close();
    }
}
