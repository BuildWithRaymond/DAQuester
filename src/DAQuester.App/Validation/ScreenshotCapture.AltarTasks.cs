using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DAQuester.App.ViewModels;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    static async Task CaptureAltarTasksAsync(Window window, MainWindowViewModel vm)
    {
        var runtime = vm.Runtime;
        runtime.SetPreviewQueue(runtime.Queue("Agent") with { State = "Idle", ActiveQuest = null, Quests = [] });
        var current = runtime.Snapshot("Agent") with { Class = "Warrior", Hp = 1000, MaxHp = 1000, Gold = 7500, InventoryKnown = true };
        runtime.SetPreviewSnapshot(current);
        vm.Filter = "All";
        vm.Refresh();
        async Task Capture(string name)
        {
            await Task.Delay(250);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/" + name + ".png");
        }
        window.Width = 1420; window.Height = 900;
        vm.Page = "Tasks";
        vm.OpenTaskCommand.Execute(vm.Tasks.Single(t => t.Id == AltarWorshipEvidence.Id));
        await Capture("altar-task-warrior");
        vm.Page = "Planner";
        vm.Search = "Altar";
        await Capture("altar-quest-warrior-hidden");
        foreach (string characterClass in new[] { "Priest", "Bard", "Master Priest" })
        {
            runtime.SetPreviewSnapshot(current with { Class = characterClass });
            vm.Refresh();
            vm.Page = "Planner";
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == AltarWorshipEvidence.Id);
            await Capture("altar-quest-" + characterClass.ToLowerInvariant().Replace(' ', '-'));
        }
        vm.Page = "Tasks";
        await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(new()
        {
            Character = "Agent", Quest = AltarWorshipEvidence.Id, Step = "run",
            AltarOfferings = [new(DateTimeOffset.UtcNow.AddHours(-1), 1, 750, "", 0)]
        });
        await runtime.SaveScheduleAsync("Agent", AltarWorshipEvidence.Id, DateTimeOffset.UtcNow, false, true);
        vm.Refresh();
        vm.OpenTaskCommand.Execute(vm.Tasks.Single(t => t.Id == AltarWorshipEvidence.Id));
        await Capture("altar-task-wait");
        window.Width = 900; window.Height = 560;
        await Capture("altar-task-compact-list");
        vm.OpenTaskCommand.Execute(vm.SelectedTask);
        await Capture("altar-task-compact-details");
    }
}
