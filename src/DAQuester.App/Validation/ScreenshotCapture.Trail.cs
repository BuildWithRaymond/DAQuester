using Avalonia.Controls;
using DAQuester.App.ViewModels;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    // Reproduces a long journey entirely in the isolated preview journal. No quest
    // runner, connection or game action is used to populate these display states.
    static async Task CaptureLongTrailAsync(Window window, MainWindowViewModel vm, List<string> renders)
    {
        if (!vm.Runtime.Preview)
            throw new InvalidOperationException("Trail capture requires isolated preview data.");
        var runtime = vm.Runtime;
        var originalQueue = runtime.Queue("Agent");
        var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
        var originalJob = journal.Load("Agent", DanaanEvidence.Id);
        await journal.SaveAsync(new()
        {
            Character = "Agent",
            Quest = DanaanEvidence.Id,
            Step = "run",
            Status = "Running",
            DanaanOptions = new("Cail", true),
            Route = new()
            {
                Task = 1
            }
        });
        runtime.SetPreviewQueue(new()
        {
            Character = "Agent",
            State = "Running",
            ActiveQuest = DanaanEvidence.Id,
            Quests = [DanaanEvidence.Id]
        });
        runtime.SetPreviewQueueRunning("Agent", true);
        vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Agent");
        vm.Refresh();

        foreach (bool compact in new[] { false, true })
        {
            window.Width = compact ? 900 : 1450;
            window.Height = compact ? 560 : 930;
            string suffix = compact ? "-compact" : "";
            vm.Page = "Planner";
            await CaptureJourneyAsync(window, "danaan-trail" + suffix, renders);
            var overview = window.FindControl<ScrollViewer>("journeyOverview")!;
            overview.ScrollToEnd();
            await CaptureJourneyAsync(window, "danaan-trail-details" + suffix, renders);
            overview.Offset = default;
            vm.Page = "Dashboard";
            vm.DashboardListView = false;
            await CaptureJourneyAsync(window, "danaan-trail-cards" + suffix, renders);
            vm.DashboardListView = true;
            vm.SelectedDashboardCharacter = vm.DashboardCharacters.Single(c => c.Name == "Agent");
            await SettleAsync(window);
            await CaptureJourneyAsync(window, "danaan-trail-list" + suffix, renders);
        }

        // Row-boundary and completion captures also check the highlighted stage and
        // the final partial row without inventing live completion evidence.
        window.Width = 1450;
        window.Height = 930;
        vm.Page = "Planner";
        var sample = journal.Load("Agent", DanaanEvidence.Id);
        await journal.SaveAsync(sample with
        {
            Route = new()
            {
                Task = 10
            }
        });
        vm.Refresh();
        await CaptureJourneyAsync(window, "danaan-trail-wrapped-stage", renders);
        await journal.SaveAsync(sample with
        {
            Step = "complete",
            Status = "Complete"
        });
        vm.Refresh();
        await CaptureJourneyAsync(window, "danaan-trail-complete", renders);

        runtime.SetPreviewQueueRunning("Agent", false);
        runtime.SetPreviewQueue(originalQueue);
        await journal.SaveAsync(originalJob);
        vm.DashboardListView = false;
        vm.Refresh();
    }
}
