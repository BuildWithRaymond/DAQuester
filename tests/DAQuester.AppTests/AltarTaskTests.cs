using DAQuester.App;
using DAQuester.App.Controls;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class AltarTaskTests
{
    [Theory]
    [InlineData("Bard", true)]
    [InlineData("Master Priest", true)]
    [InlineData("Warrior", false)]
    public async Task Offline_character_uses_saved_class_for_quest_placement(string characterClass, bool quest)
    {
        var initial = DashboardTests.Workspace();
        var known = initial.Snapshot("Elara") with { Class = characterClass };
        await initial.ShutdownAsync();
        await new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), initial.Settings.ServerProfile).ObserveAsync("Elara", known);
        var runtime = new DesktopRuntime(preview: true);
        try
        {
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            Assert.False(runtime.Snapshot("Elara").Connected);
            Assert.Equal(quest, vm.Quests.Any(q => q.RecipeId == AltarWorshipEvidence.Id));
            Assert.Contains(vm.Tasks, t => t.Id == AltarWorshipEvidence.Id);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("Priest", true)]
    [InlineData("Bard", true)]
    [InlineData("Master Priest", true)]
    [InlineData("priest", true)]
    [InlineData("Warrior", false)]
    [InlineData("Wizard", false)]
    [InlineData("Master Wizard", false)]
    [InlineData(null, false)]
    public async Task Altar_is_always_a_task_and_only_priest_family_gets_a_quest(string? characterClass, bool quest)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = characterClass, Hp = 100, Gold = 7500, InventoryKnown = true });
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            Assert.Contains(vm.Tasks, t => t.Id == AltarWorshipEvidence.Id);
            Assert.Equal(quest, vm.Quests.Any(q => q.RecipeId == AltarWorshipEvidence.Id));
            if (quest)
            {
                var row = Assert.Single(vm.Quests, q => q.RecipeId == AltarWorshipEvidence.Id);
                var rewardDisplay = string.Concat(LegendRewardParts.Split(row.Rewards, row.LegendEvidence).Select(p => p.Text));
                Assert.DoesNotContain("Priests only", rewardDisplay);
                Assert.Contains(AltarWorshipEvidence.Legend, rewardDisplay);
            }
            Assert.False(runtime.IsRunning("Elara"));
            Assert.False(runtime.SchedulerEnabled);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Changing_class_hides_altar_quest_selection_without_changing_its_queue_or_progress()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = "Priest", Hp = 100, Gold = 7500, InventoryKnown = true });
            runtime.SetPreviewQueue(runtime.Queue("Elara") with { Quests = [AltarWorshipEvidence.Id] });
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == AltarWorshipEvidence.Id);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = "Wizard" });
            vm.Refresh();
            Assert.DoesNotContain(vm.Quests, q => q.RecipeId == AltarWorshipEvidence.Id);
            Assert.NotEqual(AltarWorshipEvidence.Id, vm.SelectedQuest?.RecipeId);
            Assert.Contains(AltarWorshipEvidence.Id, runtime.Queue("Elara").Quests);
            Assert.Contains(vm.Tasks, t => t.Id == AltarWorshipEvidence.Id);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Saved_offering_wait_is_shared_by_quest_and_task_and_keeps_its_schedule()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = "Bard", Hp = 100, Gold = 7500, InventoryKnown = true });
            var at = DateTimeOffset.UtcNow.AddHours(-1);
            await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(new()
            { Character = "Elara", Quest = AltarWorshipEvidence.Id, Step = "run", AltarOfferings = [new(at, 1, 750, "", 0)] });
            await runtime.SaveScheduleAsync("Elara", AltarWorshipEvidence.Id, DateTimeOffset.UtcNow, false, true);
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            var task = Assert.Single(vm.Tasks, t => t.Id == AltarWorshipEvidence.Id);
            Assert.Contains(vm.Quests, q => q.RecipeId == task.Id);
            Assert.Equal("Resume", task.PrimaryAction);
            Assert.Contains("Offering 1 of 10", task.Progress);
            Assert.Contains(AltarWorshipEvidence.Legend, task.Rewards);
            Assert.Equal("Waiting for next offering", task.Status);
            Assert.False(task.NeedsAttention);
            Assert.Contains(at.AddHours(3).ToLocalTime().ToString("MMM d, h:mm tt"), task.NextCheck);
            Assert.True(task.IsScheduled);
            Assert.Equal(at.AddHours(3), Assert.Single(runtime.SchedulerState.Entries).Due);
            Assert.False(runtime.SchedulerNeedsChoices("Elara", task.Id));
            Assert.Single(runtime.Job("Elara", task.Id).AltarOfferings);
            Assert.False(runtime.SchedulerEnabled);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
