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
    static async Task CaptureResearchAsync(Window window, MainWindowViewModel vm)
    {
        var current = vm.Runtime.Snapshot("Agent");
        vm.Runtime.SetPreviewQueue(vm.Runtime.Queue("Agent") with { State = "Idle", ActiveQuest = null, Quests = [] });
        vm.Runtime.SetPreviewSnapshot(current with { Class = "Wizard", Hp = 1000, MaxHp = 1000, Gold = 50000,
            InventoryKnown = true, Inventory = current.Inventory.SetItem(3, new(3, "Parchment Scroll", 1)) });
        vm.Refresh();
        vm.Page = "Tasks";
        vm.SelectedTask = vm.Tasks.Single(t => t.IsResearch);
        async Task Capture(Window target, string name)
        {
            await Task.Delay(250);
            Dispatcher.UIThread.RunJobs();
            target.UpdateLayout();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)target.ClientSize.Width, (int)target.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(target);
            bitmap.Save("artifacts/screenshots/" + name + ".png");
        }
        window.Width = 1420; window.Height = 900;
        await Capture(window, "tasks");
        await vm.Runtime.SaveSchedulerChoicesAsync("Elara", WizardyResearchEvidence.Id, new(ResearchOptions: new()));
        await vm.Runtime.SaveScheduleAsync("Elara", WizardyResearchEvidence.Id, DateTimeOffset.UtcNow.AddHours(12), true, true);
        vm.Refresh();
        await Capture(window, "tasks-other-character-schedule");
        await vm.Runtime.SaveSchedulerChoicesAsync("Agent", WizardyResearchEvidence.Id, new(ResearchOptions: new()));
        await vm.Runtime.SaveScheduleAsync("Agent", WizardyResearchEvidence.Id, DateTimeOffset.UtcNow.AddHours(12), true, true);
        vm.Refresh();
        await Capture(window, "tasks-scheduled");
        var supplied = vm.Runtime.Snapshot("Agent");
        vm.Runtime.SetPreviewSnapshot(supplied with { Gold = 0 });
        vm.Refresh();
        await Capture(window, "tasks-no-gold");
        vm.Runtime.SetPreviewSnapshot(supplied);
        vm.Refresh();
        window.Width = 900; window.Height = 560;
        await Capture(window, "tasks-compact");
        vm.OpenTaskCommand.Execute(vm.SelectedTask);
        await Capture(window, "tasks-compact-details");
        vm.BackToTasksCommand.Execute(null);
        vm.TaskSearch = "no matching task";
        await Capture(window, "tasks-empty");
        vm.ClearTaskFiltersCommand.Execute(null);
        vm.Runtime.SetPreviewRunning("Agent", true, WizardyResearchEvidence.Id);
        vm.Refresh();
        await Capture(window, "tasks-compact-running");
        vm.OpenRunningTaskCommand.Execute(null);
        await Capture(window, "tasks-running-details");
        vm.Runtime.SetPreviewRunning("Agent", false);
        vm.Refresh();
        var quest = vm.Runtime.Recipes[WizardyResearchEvidence.Id];
        var dialog = new ActivityRunDialog(vm.Runtime, "Agent", quest, vm.Runtime.Job("Agent", quest.Id));
        var closed = dialog.ShowDialog(window);
        await Capture(dialog, "tasks-setup");
        dialog.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "activitySchedule").IsChecked = true;
        await Capture(dialog, "tasks-setup-schedule");
        dialog.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "activityBootConsent").IsChecked = true;
        await Capture(dialog, "tasks-setup-consent");
        dialog.Close();
        await closed;
        window.Width = 1420; window.Height = 900;
        // Additional rows exist only in this isolated preview, never the shipped catalog.
        var samples = new[] { ("Herb gathering", "Gathering", "Abel garden"), ("Parcel delivery", "Errands", "Mileth inn"),
            ("Workshop supplies", "Crafting", "Suomi workshop"), ("Daily provisions", "Errands", "Rucesion market"),
            ("Garden tending", "Gathering", "Suomi garden"), ("Apothecary supplies", "Crafting", "Abel apothecary") };
        for (int i = 0; i < samples.Length; i++)
        {
            var (name, category, location) = samples[i];
            string id = "preview-task-" + i;
            ((Dictionary<string, QuestDefinition>)vm.Runtime.Recipes)[id] = new(id, 1, name + " (sample)", "Isolated layout preview", [], [])
            { IsTask = true, TaskPresentation = new() { Category = category, Location = location, Summary = "Synthetic example for catalog layout review." } };
        }
        vm.Refresh();
        vm.OpenTaskCommand.Execute(vm.Tasks.Single(t => t.IsResearch));
        await Capture(window, "tasks-many-samples");
        vm.TaskSearch = "SUPPLIES";
        await Capture(window, "tasks-filtered-samples");
    }
}
