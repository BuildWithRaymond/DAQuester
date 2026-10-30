using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DAQuester.App.ViewModels;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    static async Task CaptureBagSortingAsync(Window window, MainWindowViewModel vm)
    {
        foreach (string name in vm.Runtime.Characters)
        {
            vm.Runtime.SetPreviewRunning(name, false);
            vm.Runtime.SetPreviewQueueRunning(name, false);
            vm.Runtime.SetPreviewQueue(new() { Character = name, State = "Idle" });
        }
        var owner = vm.Runtime.Snapshot("Agent") with
        {
            Name = "Agent", Id = 1, Connected = true, InventoryKnown = true, Generation = 1,
            Dialog = null, Exchange = null
        };
        vm.Runtime.SetPreviewSnapshot(owner);
        vm.SelectedCharacter = vm.Characters.Single(character => character.OriginalName == "Agent");
        vm.Refresh();
        var button = window.FindControl<Button>("sortBags")!;

        async Task Capture(string name)
        {
            await Task.Delay(180);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/" + name + ".png");
        }

        foreach (bool compact in new[] { false, true })
        {
            window.Width = compact ? 900 : 1420;
            window.Height = compact ? 560 : 900;
            foreach (string page in new[] { "Dashboard", "Planner", "Characters", "Tasks" })
            {
                vm.Page = page;
                await Capture("bag-sort-" + page.ToLowerInvariant() + (compact ? "-compact" : ""));
            }
        }
        vm.Page = "Planner";
        vm.Runtime.SetPreviewRunning("Agent", true);
        vm.Refresh();
        await Capture("bag-sort-busy");
        vm.Runtime.SetPreviewRunning("Agent", false);
        vm.Refresh();

        // Exercise ordinary activation with a temporary synthetic command;
        // this never calls a game operation.
        int activations = 0;
        BagSortRequest? capturedRequest = null;
        bool eligible = true;
        button.Command = new RelayCommand(_ => { activations++; capturedRequest = vm.CaptureBagSortRequest(); }, _ => eligible);
        void Click() => typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
        Click();
        bool clickedOnce = activations == 1 && capturedRequest is not null;
        eligible = false;
        Click();
        bool disabledClickIgnored = activations == 1;
        File.WriteAllText("artifacts/bag-sort-ui-review.json", JsonSerializer.Serialize(new
        {
            isolated = vm.Runtime.Preview, synthetic = true, clickedOnce, disabledClickIgnored,
            capturedRoster = capturedRequest?.Targets.Select(target => target.Character).ToArray()
        }, AppSettings.Json));
        if (!clickedOnce || !disabledClickIgnored)
            throw new InvalidOperationException("Synthetic bag click review failed.");
    }
}
