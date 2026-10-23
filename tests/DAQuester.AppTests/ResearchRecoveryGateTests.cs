using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class ResearchRecoveryGateTests
{
    [Theory]
    [InlineData("healing")]
    [InlineData("health")]
    [InlineData("purchase")]
    [InlineData("shop-departure")]
    public async Task Saved_recovery_keeps_resume_available_and_choices_locked_at_stage_zero(string recovery)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            await SaveRecovery(runtime, recovery);
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            var task = Assert.Single(vm.Tasks, t => t.IsResearch);

            Assert.True(vm.ResumeResearchCommand.CanExecute(null));
            Assert.False(vm.CanChangeResearchChoices);
            Assert.True(task.NeedsReview);
            Assert.Equal("Resume", task.PrimaryAction);
            Assert.True(vm.TaskPrimaryCommand.CanExecute(task));
            Assert.False(task.CanChangeSettings);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("healing")]
    [InlineData("health")]
    [InlineData("purchase")]
    [InlineData("shop-departure")]
    public async Task Saved_recovery_cannot_replace_assistant_choices(string recovery)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            await SaveRecovery(runtime, recovery);

            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SaveSchedulerChoicesAsync("Elara", WizardyResearchEvidence.Id,
                new(ResearchOptions: new("Wren"))));

            Assert.Null(runtime.Job("Elara", WizardyResearchEvidence.Id).ResearchOptions!.Assistant);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("healing")]
    [InlineData("health")]
    [InlineData("purchase")]
    [InlineData("shop-departure")]
    public async Task Saved_recovery_bypasses_new_setup_and_is_due_now(string recovery)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            await SaveRecovery(runtime, recovery);
            bool setupOpened = false;
            runtime.ChooseActivityRunAsync = (_, _, _) => { setupOpened = true; return Task.FromResult<ActivityRunChoice?>(null); };
            var method = typeof(DesktopRuntime).GetMethod("ConfigureActivityRunAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var recipe = runtime.Recipes[WizardyResearchEvidence.Id];

            Assert.True(await (Task<bool>)method.Invoke(runtime, ["Elara", recipe, runtime.Job("Elara", recipe.Id), false, true])!);
            Assert.False(setupOpened);
            var now = DateTimeOffset.UtcNow;
            Assert.Equal(now, runtime.SchedulerDue("Elara", recipe.Id, now));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    static async Task SaveRecovery(DesktopRuntime runtime, string recovery)
    {
        var progress = JsonSerializer.Deserialize<WizardyResearchProgress>(recovery switch
        {
            "healing" => """{"Healing":{"Generation":1,"Revision":2,"BeforeCount":30,"BeforeHp":30,"Slot":2}}""",
            "health" => """{"HealthCheckRequired":true}""",
            "shop-departure" => """{"PotionShopDeparture":{"Generation":1,"Revision":2,"EntityId":55,"EntityType":1,"Name":"Jana","Text":"Synthetic dismissed stock","IsPursuit":false,"Pursuit":74,"Step":0,"Type":4,"Choices":[]}}""",
            _ => """{"PotionPurchase":{"Generation":1,"Revision":2,"BeforeCount":0,"BeforeGold":5000,"Quantity":30,"QuotedGold":3000,"Stock":{"Generation":1,"Revision":2,"EntityId":55,"EntityType":1,"Name":"Potion seller","Text":"Synthetic stock","IsPursuit":false,"Pursuit":64,"Step":0,"Type":4,"Choices":[]}}}"""
        })!;
        await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(new()
        {
            Character = "Elara", Quest = WizardyResearchEvidence.Id, ResearchOptions = new(), Research = progress,
            NextRun = DateTimeOffset.UtcNow.AddHours(12), Reason = "Research supplies and health need reconciliation"
        });
    }
}
