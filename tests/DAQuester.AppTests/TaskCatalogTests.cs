using DAQuester.App.Services;
using DAQuester.App;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class TaskCatalogTests
{
    [Theory]
    [InlineData("Armed", true)]
    [InlineData("Paused", true)]
    [InlineData("Needs attention", true)]
    [InlineData("Completed", false)]
    public async Task Saved_schedule_status_and_due_time_remain_visible_with_scheduler_off(string status, bool scheduled)
    {
        var original = DashboardTests.Workspace();
        await original.ShutdownAsync();
        var due = DateTimeOffset.UtcNow.AddHours(12);
        await new FileQuestSchedulerStore(Path.Combine(AppSettings.DataPath, "scheduler.json")).SaveAsync(new()
        {
            Entries = [new() { Character = "Elara", Quest = WizardyResearchEvidence.Id, Profile = original.Settings.ServerProfile,
                Status = status, Due = due, Recurring = true, Reason = status == "Needs attention" ? "Review the last research result." : "" }]
        });
        var runtime = new DesktopRuntime(preview: true);
        try
        {
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            var task = Assert.Single(vm.Tasks, t => t.IsResearch);
            Assert.Equal(scheduled, task.IsScheduled);
            Assert.Contains(status, task.Schedule, StringComparison.OrdinalIgnoreCase);
            if (scheduled) Assert.Contains(due.ToLocalTime().ToString("MMM d, h:mm tt"), task.Schedule);
            Assert.Contains("Scheduler is off", task.Schedule);
            if (status == "Needs attention") Assert.Contains("Review the last research result.", task.Schedule);
            Assert.False(runtime.SchedulerEnabled);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Pending_research_offers_reconciliation_despite_empty_purse()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = "Wizard", Gold = 0 });
            await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(new()
            {
                Character = "Elara", Quest = WizardyResearchEvidence.Id,
                Pending = new("research:attempt", 1, 1, 0, 0, 0, DateTimeOffset.UtcNow)
            });
            var vm = new MainWindowViewModel(runtime);
            var task = Assert.Single(vm.Tasks, t => t.IsResearch);
            Assert.True(task.NeedsReview);
            Assert.Equal("Resume", task.PrimaryAction);
            Assert.True(vm.TaskPrimaryCommand.CanExecute(task));
            Assert.False(task.CanChangeSettings);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Large_catalog_filters_ready_tasks_and_keeps_unchecked_research_out()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Gold = 500, Hp = 100, InventoryKnown = true, Weight = 0, MaxWeight = 100 });
            for (int i = 0; i < 30; i++) AddTask(runtime, "sample-" + i, "Sample " + i, i % 2 == 0 ? "Gathering" : "Errands", "Abel");
            var vm = new MainWindowViewModel(runtime);
            Assert.Equal(32, vm.Tasks.Count);
            Assert.False(vm.Tasks.Single(t => t.IsResearch).IsReady);
            vm.TaskFilter = "Ready";
            Assert.DoesNotContain(vm.Tasks, t => t.IsResearch);
            vm.TaskCategory = "Errands";
            Assert.Equal(15, vm.Tasks.Count);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = "Wizard" });
            vm.ClearTaskFiltersCommand.Execute(null);
            vm.Refresh();
            vm.TaskFilter = "Ready";
            Assert.Contains(vm.Tasks, t => t.IsResearch);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    static void AddTask(DesktopRuntime runtime, string id, string name, string category, string location)
    {
        ((Dictionary<string, QuestDefinition>)runtime.Recipes)[id] = new(id, 1, name, "Synthetic catalog fixture", [], [])
        {
            IsTask = true,
            TaskPresentation = new() { Category = category, Location = location, Summary = "Synthetic sample task", Requirements = "Synthetic supplies" }
        };
    }

    [Fact]
    public async Task Catalog_combines_search_category_and_state_without_changing_planner_selection()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            AddTask(runtime, "sample-garden", "Garden work", "Gathering", "Abel garden");
            AddTask(runtime, "sample-delivery", "Parcel delivery", "Errands", "Mileth inn");
            var vm = new MainWindowViewModel(runtime);
            var planner = vm.SelectedQuest;
            Assert.Equal(4, vm.Tasks.Count);
            Assert.All(vm.Tasks, t => Assert.True(runtime.Recipes[t.Id].IsTask));
            Assert.Equal("Black Magic Master", vm.Tasks.Single(t => t.IsResearch).Location.Split(" · ")[1]);
            vm.TaskSearch = "MILETH";
            Assert.Equal(3, vm.Tasks.Count);
            vm.TaskCategory = "Errands";
            var delivery = Assert.Single(vm.Tasks);
            vm.SelectedTask = delivery;
            vm.TaskSearch = "nothing matching";
            Assert.Empty(vm.Tasks);
            Assert.True(vm.TasksEmpty);
            vm.ClearTaskFiltersCommand.Execute(null);
            Assert.Equal(4, vm.Tasks.Count);
            Assert.Same(delivery, vm.SelectedTask);
            Assert.Same(planner, vm.SelectedQuest);
            Assert.False(runtime.SchedulerEnabled);
            Assert.False(runtime.IsRunning("Elara"));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Compact_navigation_preserves_search_and_selection()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime) { TasksCompact = true, TaskSearch = "research" };
            var task = Assert.Single(vm.Tasks);
            Assert.True(vm.ShowTaskCatalog);
            Assert.False(vm.ShowTaskDetails);
            vm.OpenTaskCommand.Execute(task);
            Assert.True(vm.ShowTaskDetails);
            Assert.False(vm.ShowTaskCatalog);
            vm.BackToTasksCommand.Execute(null);
            Assert.True(vm.ShowTaskCatalog);
            Assert.Equal("research", vm.TaskSearch);
            Assert.Same(task, vm.SelectedTask);
            Assert.False(runtime.IsRunning("Elara"));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Character_switch_updates_supply_blockers_and_schedule_filters()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = "Wizard", Gold = 0, InventoryKnown = true });
            await runtime.SaveSchedulerChoicesAsync("Wren", WizardyResearchEvidence.Id, new(ResearchOptions: new()));
            await runtime.SaveScheduleAsync("Wren", WizardyResearchEvidence.Id, DateTimeOffset.UtcNow.AddHours(12), true, true);
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            var research = Assert.Single(vm.Tasks, t => t.IsResearch);
            Assert.False(vm.TaskPrimaryCommand.CanExecute(research));
            Assert.Contains("500", research.Reason);
            vm.TaskFilter = "Needs attention";
            Assert.Contains(research, vm.Tasks);
            vm.TaskFilter = "Scheduled";
            Assert.Empty(vm.Tasks);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.Single(vm.Tasks);
            Assert.Contains("Wren", research.Schedule);
            Assert.Contains("Armed", research.Schedule);
            Assert.False(runtime.SchedulerEnabled);
            await runtime.PauseScheduleAsync(Assert.Single(runtime.SchedulerState.Entries).Id);
            vm.Refresh();
            Assert.Contains("Paused", research.Schedule);
            Assert.Contains("Scheduler is off", research.Schedule);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Saved_research_progress_offers_resume_and_running_task_stays_accessible_when_filtered()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = "Wizard", Gold = 0 });
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new() { Character = "Elara", Quest = WizardyResearchEvidence.Id, Research = new() { Stage = 2 } });
            var vm = new MainWindowViewModel(runtime);
            var research = Assert.Single(vm.Tasks, t => t.IsResearch);
            Assert.Equal("Resume", research.PrimaryAction);
            Assert.True(vm.TaskPrimaryCommand.CanExecute(research));
            runtime.SetPreviewRunning("Elara", true, research.Id);
            vm.Refresh();
            Assert.Equal("Pause", research.PrimaryAction);
            vm.TaskSearch = "nothing matching";
            Assert.Empty(vm.Tasks);
            Assert.Same(research, vm.RunningTask);
            Assert.True(vm.ShowRunningTask);
            Assert.True(vm.TaskStopCommand.CanExecute(research));
            vm.OpenRunningTaskCommand.Execute(null);
            Assert.Same(research, vm.SelectedTask);
            Assert.Equal(2, vm.Tasks.Count);
            await vm.TaskStopCommand.ExecuteAsync(research);
            Assert.False(runtime.IsRunning("Elara"));
            Assert.False(vm.ShowRunningTask);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Research_schedule_identifies_other_character_instead_of_claiming_none_saved()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            await runtime.SaveSchedulerChoicesAsync("Wren", WizardyResearchEvidence.Id, new(ResearchOptions: new()));
            await runtime.SaveScheduleAsync("Wren", WizardyResearchEvidence.Id, DateTimeOffset.UtcNow.AddHours(12), true, true);
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            Assert.Contains("Elara", vm.ResearchSchedule);
            Assert.Contains("Wren", vm.ResearchSchedule);
            Assert.False(runtime.SchedulerEnabled);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.Contains("Wren", vm.ResearchSchedule);
            Assert.Contains("Armed", vm.ResearchSchedule);
            Assert.Contains("Scheduler is off", vm.ResearchSchedule);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Auto_tasks_heading_retains_existing_navigation_key()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime) { Page = "Tasks" };
            Assert.True(vm.IsTasks);
            Assert.Equal("Auto Tasks", vm.PageTitle);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
