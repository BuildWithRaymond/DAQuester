using DAQuester.App.ViewModels;
using Xunit;

namespace DAQuester.AppTests;

public class CharacterDisclosureTests
{
    [Fact]
    public async Task Saved_characters_start_collapsed_and_refresh_preserves_each_disclosure()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            Assert.All(vm.Characters, row => Assert.False(row.IsExpanded));
            var elara = vm.Characters.Single(row => row.Name == "Elara");
            elara.IsExpanded = true;
            vm.Page = "Characters";
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with
            {
                Revision = 3,
                Level = 10
            });
            vm.Refresh();
            vm.Page = "Dashboard";
            vm.Page = "Characters";
            Assert.Same(elara, vm.Characters.Single(row => row.Name == "Elara"));
            Assert.True(elara.IsExpanded);
            Assert.Contains("10", elara.LevelText);
            Assert.False(vm.Characters.Single(row => row.Name == "Wren").IsExpanded);
            elara.IsExpanded = false;
            vm.Refresh();
            Assert.False(elara.IsExpanded);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task New_account_opens_for_entry_and_saving_preserves_its_disclosure()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            vm.AddCharacterCommand.Execute(null);
            var draft = vm.SelectedCharacter!;
            Assert.True(draft.IsExpanded);
            draft.Name = "Cato";
            await vm.SaveAccountsCommand.ExecuteAsync();
            Assert.False(vm.HasError);
            Assert.Same(draft, vm.Characters.Single(row => row.Name == "Cato"));
            Assert.True(draft.IsExpanded);
            vm.DiscardAccountsCommand.Execute(null);
            Assert.All(vm.Characters, row => Assert.False(row.IsExpanded));
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
