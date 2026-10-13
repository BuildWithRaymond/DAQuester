using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class PaulinQueueTests
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
    public async Task Direct_edit_cannot_queue_both_but_can_repair_an_old_pair()
    {
        var runtime = Runtime();
        try
        {
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.EditQueueAsync("Elara", q => q with { Quests = [SickChildEvidence.Id, SickChildEvidence.AnamanId] }));
            Assert.Empty(runtime.Queue("Elara").Quests);
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [SickChildEvidence.Id, SickChildEvidence.AnamanId] });
            await runtime.EditQueueAsync("Elara", q => q with { Quests = [SickChildEvidence.Id] });
            Assert.Equal([SickChildEvidence.Id], runtime.Queue("Elara").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData(SickChildEvidence.Id)]
    [InlineData(SickChildEvidence.AnamanId)]
    public async Task Missing_confirmation_does_not_silently_queue_a_permanent_choice(string id)
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            await vm.ToggleQuestAsync(vm.Quests.Single(q => q.RecipeId == id));
            Assert.Empty(runtime.Queue("Elara").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData(SickChildEvidence.Id, SickChildEvidence.AnamanId, true)]
    [InlineData(SickChildEvidence.AnamanId, SickChildEvidence.Id, true)]
    [InlineData(SickChildEvidence.Id, SickChildEvidence.AnamanId, false)]
    public async Task Replacement_requires_confirmation_and_preserves_other_quests(string id, string other, bool approve)
    {
        var runtime = Runtime();
        try
        {
            string[] before = ["perfect-hairstyle", other, "emerald-dawn"];
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = before });
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            vm.ConfirmPaulinQuestAsync = (name, target, replacing) =>
            {
                Assert.Equal("Elara", name);
                Assert.Equal(id, target);
                Assert.Equal(other, replacing);
                Assert.Equal(before, runtime.Queue(name).Quests);
                return Task.FromResult(approve);
            };
            await vm.ToggleQuestAsync(vm.Quests.Single(q => q.RecipeId == id));
            Assert.Equal(approve ? ["perfect-hairstyle", id, "emerald-dawn"] : before, runtime.Queue("Elara").Quests);
            Assert.Empty(runtime.Queue("Wren").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Confirmed_add_and_removal_only_prompt_on_add()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            int prompts = 0;
            vm.ConfirmPaulinQuestAsync = (_, _, replacing) => { prompts++; Assert.Null(replacing); return Task.FromResult(true); };
            await vm.ToggleQuestAsync(vm.Quests.Single(q => q.RecipeId == SickChildEvidence.Id));
            Assert.Equal([SickChildEvidence.Id], runtime.Queue("Elara").Quests);
            await vm.ToggleQuestAsync(vm.Quests.Single(q => q.RecipeId == SickChildEvidence.Id));
            Assert.Empty(runtime.Queue("Elara").Quests);
            Assert.Equal(1, prompts);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Stale_confirmation_cannot_change_another_character_or_changed_queue(bool switchCharacter)
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            vm.ConfirmPaulinQuestAsync = async (_, _, _) =>
            {
                if (switchCharacter)
                    vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
                else
                    await runtime.EditQueueAsync("Elara", q => q with { Quests = ["perfect-hairstyle"] });
                return true;
            };
            await vm.ToggleQuestAsync(vm.Quests.Single(q => q.RecipeId == SickChildEvidence.Id));
            Assert.DoesNotContain(SickChildEvidence.Id, runtime.Queue("Elara").Quests);
            Assert.Empty(runtime.Queue("Wren").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
