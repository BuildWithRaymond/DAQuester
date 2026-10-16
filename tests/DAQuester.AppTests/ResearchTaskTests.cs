using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class ResearchTaskTests
{
    [Fact]
    public async Task Tasks_navigation_keeps_research_out_of_quest_library_and_schedules_off()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            vm.NavigateCommand.Execute("Tasks");
            Assert.True(vm.IsTasks);
            Assert.True(runtime.Recipes[WizardyResearchEvidence.Id].IsTask);
            Assert.DoesNotContain(vm.Quests, q => q.RecipeId == WizardyResearchEvidence.Id);
            Assert.False(runtime.SchedulerEnabled);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Scheduler_requires_and_saves_explicit_assistant_choices()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            Assert.True(runtime.SchedulerNeedsChoices("Elara", WizardyResearchEvidence.Id));
            var choice = JsonSerializer.Deserialize<QuestChoiceResult>("{\"ResearchOptions\":{\"Assistant\":null}}")!;
            await runtime.SaveSchedulerChoicesAsync("Elara", WizardyResearchEvidence.Id, choice);
            Assert.NotNull(runtime.Job("Elara", WizardyResearchEvidence.Id).ResearchOptions);
            Assert.False(runtime.SchedulerNeedsChoices("Elara", WizardyResearchEvidence.Id));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Repeat_prompt_saves_one_schedule_and_keeps_global_scheduler_off()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.ChooseActivityRunAsync = (_, _, _) => Task.FromResult<ActivityRunChoice?>(new(new(), true, true));
            var method = typeof(DesktopRuntime).GetMethod("ConfigureActivityRunAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var quest = runtime.Recipes[WizardyResearchEvidence.Id];
            for (int i = 0; i < 2; i++)
                Assert.True(await (Task<bool>)method.Invoke(runtime, ["Elara", quest, runtime.Job("Elara", quest.Id), false, false])!);
            var saved = Assert.Single(runtime.SchedulerState.Entries);
            Assert.True(saved.Recurring);
            Assert.Equal(quest.Id, saved.Quest);
            Assert.False(runtime.SchedulerEnabled);
            Assert.NotNull(runtime.Job("Elara", quest.Id).ResearchOptions);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Connected_character_with_unread_class_can_start_fresh_inspection()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            Assert.Null(runtime.Snapshot("Elara").Class);
            Assert.True(vm.RunResearchCommand.CanExecute(null));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Wizard")]
    public async Task No_gold_disables_research_start_and_explains_parchment_cost(string? characterClass)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Gold = 0, Class = characterClass });
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            Assert.False(vm.RunResearchCommand.CanExecute(null));
            Assert.Contains("gold", vm.ResearchReason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("500", vm.ResearchReason);
            bool setupOpened = false;
            runtime.ChooseActivityRunAsync = (_, _, _) => { setupOpened = true; return Task.FromResult<ActivityRunChoice?>(null); };
            var method = typeof(DesktopRuntime).GetMethod("ConfigureActivityRunAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var quest = runtime.Recipes[WizardyResearchEvidence.Id];
            var blocked = await Assert.ThrowsAsync<QuestBlockedException>(async () =>
                await (Task<bool>)method.Invoke(runtime, ["Elara", quest, runtime.Job("Elara", quest.Id), false, false])!);
            Assert.Contains("500", blocked.Message);
            Assert.False(setupOpened);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
