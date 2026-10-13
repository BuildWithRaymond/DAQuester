using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class PlannerPresentationTests
{
    static DesktopRuntime Runtime()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara", "Wren"];
        settings.ClientPath = "";
        settings.Save();
        return new(preview: true);
    }

    [Fact]
    public async Task Collapse_survives_refresh_navigation_and_character_change_without_touching_queue()
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["perfect-hairstyle"] });
            var vm = new MainWindowViewModel(runtime);
            var before = runtime.Queue("Elara");
            bool canRun = vm.StartQueueCommand.CanExecute(null);
            Assert.False(vm.JourneyCollapsed);
            vm.ToggleJourneyCommand.Execute(null);
            vm.Refresh();
            vm.Page = "Characters";
            vm.Page = "Planner";
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.True(vm.JourneyCollapsed);
            Assert.Equal("Expand journey and queue", vm.JourneyToggleLabel);
            Assert.Equal(before, runtime.Queue("Elara"));
            Assert.Empty(runtime.Queue("Wren").Quests);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            Assert.Equal(canRun, vm.StartQueueCommand.CanExecute(null));
            vm.ToggleJourneyCommand.Execute(null);
            Assert.False(vm.JourneyCollapsed);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Queue_action_icon_and_compact_controls_follow_actual_owner()
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["perfect-hairstyle"] });
            var vm = new MainWindowViewModel(runtime);
            Assert.Equal("Play", vm.QueuePrimaryIcon);
            Assert.StartsWith("Run queue", vm.QueuePrimaryLabel);
            Assert.Contains("Connect Elara", vm.QueuePrimaryTooltip);
            Assert.True(vm.CompactQueueControls);
            runtime.SetPreviewQueueRunning("Elara", true);
            Assert.Equal("Pause", vm.QueuePrimaryIcon);
            Assert.Equal("Pause queue", vm.QueuePrimaryLabel);
            runtime.SetPreviewQueueRunning("Elara", false);
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["perfect-hairstyle"], State = "Paused", ActiveQuest = "perfect-hairstyle" });
            Assert.Equal("Resume queue", vm.QueuePrimaryLabel);
            Assert.True(vm.CompactQueueControls);
            Assert.False(vm.CompactIndividualControls);
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["perfect-hairstyle"] });
            runtime.SetPreviewRunning("Elara", true);
            Assert.True(vm.CompactIndividualControls);
            Assert.False(vm.CompactQueueControls);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Can_queue_filter_distinguishes_offline_planning_from_readiness_and_temporary_locks()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            vm.Filter = "Can queue";
            Assert.NotEmpty(vm.Quests);
            Assert.All(vm.Quests, quest => Assert.True(quest.QueueEligible));
            var emerald = vm.Quests.Single(q => q.RecipeId == "emerald-dawn");
            Assert.False(emerald.IsReady);
            Assert.Contains("check requirements", emerald.SelectionExplanation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Add to queue", emerald.QueueTooltip);
            int count = vm.Quests.Count;
            runtime.SetPreviewQueue(new() { Character = "Elara", State = "Paused", ActiveQuest = "perfect-hairstyle", Quests = ["perfect-hairstyle"] });
            Assert.Equal(count, vm.Quests.Count);
            Assert.All(vm.Quests, quest => Assert.False(quest.CanQueue));
            Assert.Contains("Stop", emerald.QueueTooltip);
            Assert.Contains("Stop", vm.LibrarySelectionHint);
            vm.Filter = "Ready";
            Assert.Empty(vm.Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Blocked_and_guide_only_rows_explain_disabled_selection_but_queued_completed_rows_can_be_removed()
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 2, Level = 6, Hp = 10, LegendsKnown = true, LegendsRevision = 2 });
            var vm = new MainWindowViewModel(runtime);
            var blocked = vm.Quests.Single(q => q.RecipeId == "emerald-dawn");
            Assert.False(blocked.CanQueue);
            Assert.False(blocked.QueueEligible);
            Assert.Contains("level 11+", blocked.QueueTooltip, StringComparison.OrdinalIgnoreCase);
            Assert.True(blocked.StatusWarning);
            var guide = vm.Quests.First(q => q.RecipeId.Length == 0);
            Assert.Equal("Guide only", guide.StatusLabel);
            Assert.False(guide.CanQueue);
            Assert.Contains("guide", guide.QueueTooltip, StringComparison.OrdinalIgnoreCase);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Legends = [MuisirEvidence.Legend] });
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["entered-muisir-village"] });
            var completed = vm.Quests.Single(q => q.RecipeId == "entered-muisir-village");
            Assert.True(completed.IsCompleted);
            Assert.True(completed.ShowQueueCheckbox);
            Assert.True(completed.CanQueue);
            Assert.StartsWith("Remove from queue", completed.QueueTooltip);
            await vm.ToggleQuestAsync(completed);
            Assert.False(completed.ShowQueueCheckbox);
            Assert.Empty(runtime.Queue("Elara").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Missing_character_explains_why_membership_is_disabled()
    {
        var runtime = Runtime();
        try
        {
            runtime.Settings.Characters = [];
            var vm = new MainWindowViewModel(runtime);
            var quest = vm.Quests.Single(q => q.RecipeId == "perfect-hairstyle");
            Assert.False(quest.CanQueue);
            Assert.Contains("character", quest.QueueTooltip, StringComparison.OrdinalIgnoreCase);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Unavailable_automation_explains_manual_play_without_showing_calibration_diagnostics()
    {
        var runtime = Runtime();
        try
        {
            runtime.SaveCalibration("{\"Validated\":false}");
            runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 2, Level = 6, Hp = 10 });
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["spare-a-stick"] });
            var vm = new MainWindowViewModel(runtime);
            var quest = vm.Quests.Single(q => q.RecipeId == "spare-a-stick");
            Assert.Contains("guide", quest.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("calibration", quest.QueueTooltip, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("control bridge", vm.QueuePrimaryTooltip, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("calibration", vm.QueueReadiness, StringComparison.OrdinalIgnoreCase);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
