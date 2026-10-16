using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.ViewModels;
using DAQuester.App.Views;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    static async Task CaptureLegendMarksAsync(Window owner, MainWindowViewModel vm, List<string> renders)
    {
        vm.Page = "Characters";
        vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
        var sample = vm.Runtime.Snapshot("Agent");
        var agent = vm.Characters.Single(c => c.Name == "Agent");
        async Task<LegendMarksDialog> Open(CharacterItemVM character)
        {
            character.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            owner.UpdateLayout();
            var button = owner.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "legendMarks" && b.DataContext == character);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(300);
            return owner.OwnedWindows.OfType<LegendMarksDialog>().Single();
        }
        async Task Capture(LegendMarksDialog dialog, string name)
        {
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            dialog.UpdateLayout();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)dialog.ClientSize.Width, (int)dialog.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(dialog);
            string path = "artifacts/screenshots/legend-marks-" + name + ".png";
            bitmap.Save(path);
            renders.Add(path);
        }
        var popup = await Open(agent);
        await Capture(popup, "observed");
        popup.Width = 480;
        popup.Height = 320;
        await Capture(popup, "compact");
        var longMarks = Enumerable.Range(1, 40).Select(i => new LegendMarkEntry(
            i == 40 ? "Completed a very long remembered journey with friends from across the kingdom - Deoch 223, Summer" :
            "Remembered journey " + i + " - Deoch 223, Summer", (byte)(i % 8))).ToImmutableArray();
        vm.Runtime.SetPreviewSnapshot(sample with { Revision = 14, LegendsRevision = 14, Legends = longMarks.Select(e => e.Text).ToImmutableArray(), LegendEntries = longMarks });
        vm.Refresh();
        await Task.Delay(200);
        popup.FindControl<ScrollViewer>("legendScroll")!.Offset = new Vector(0, 10000);
        await Capture(popup, "scrolled");
        popup.FindControl<Button>("legendClose")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(100);

        vm.Runtime.SetPreviewSnapshot(sample with { Connected = false, Legends = [], LegendEntries = [] });
        vm.Refresh();
        popup = await Open(agent);
        await Capture(popup, "remembered");
        popup.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        await Task.Delay(100);
        popup = await Open(vm.Characters.Single(c => c.Name == "Elara"));
        await Capture(popup, "empty");
        popup.Close();
        await Task.Delay(100);
        popup = new LegendMarksDialog(new CharacterItemVM(vm, "Unobserved"));
        popup.Show(owner);
        await Capture(popup, "unknown");
        popup.Close();
        vm.Runtime.SetPreviewSnapshot(sample);
        vm.Refresh();
        vm.SelectedCharacter = agent;
    }
}
