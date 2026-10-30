using DAQuester.App.ViewModels;
using Xunit;

namespace DAQuester.AppTests;

public sealed class BagSortViewModelTests
{
    [Fact]
    public async Task Toolbar_scope_follows_individual_and_multi_character_pages()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            foreach (string page in new[] { "Dashboard", "Characters", "Scheduler", "History" })
            {
                vm.Page = page;
                Assert.True(vm.BagSortVisible);
                Assert.Equal("Sort All Bags", vm.BagSortLabel);
            }
            foreach (string page in new[] { "Planner", "Tasks" })
            {
                vm.Page = page;
                Assert.True(vm.BagSortVisible);
                Assert.Equal("Sort Bag", vm.BagSortLabel);
            }
            vm.Page = "Settings";
            Assert.False(vm.BagSortVisible);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task All_scope_ignores_dashboard_filters_and_freezes_connection_identities()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { InventoryKnown = true });
            runtime.SetPreviewSnapshot(new() { Name = "UnsavedSession", Connected = true, Id = 88, Generation = 8, InventoryKnown = true });
            var vm = new MainWindowViewModel(runtime) { DashboardSearch = "Wren", DashboardFilter = "Offline" };
            var request = vm.CaptureBagSortRequest();
            Assert.Equal(new[] { "Elara", "Wren" }, request.Targets.Select(target => target.Character));
            Assert.True(vm.SortBagsCommand.CanExecute(request));
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Generation = 99 });
            Assert.Equal(1, request.Targets[0].Generation);
            Assert.False(vm.SortBagsCommand.CanExecute(request));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Single_scope_targets_only_selected_character_and_navigation_invalidates_confirmation()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { InventoryKnown = true });
            var vm = new MainWindowViewModel(runtime) { Page = "Planner" };
            var request = vm.CaptureBagSortRequest();
            Assert.Equal("Elara", Assert.Single(request.Targets).Character);
            Assert.True(vm.SortBagsCommand.CanExecute(request));
            vm.Page = "Tasks";
            Assert.False(vm.SortBagsCommand.CanExecute(request));
            vm.Page = "Planner";
            Assert.False(vm.SortBagsCommand.CanExecute(request));
            vm.SelectedCharacter = vm.Characters.Single(character => character.OriginalName == "Wren");
            Assert.False(vm.SortBagsCommand.CanExecute(request));
            Assert.Equal("Wren", Assert.Single(vm.CaptureBagSortRequest().Targets).Character);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Unknown_or_busy_bags_disable_confirmation_without_launching_characters()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            Assert.False(vm.SortBagsCommand.CanExecute(null));
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { InventoryKnown = true });
            Assert.True(vm.SortBagsCommand.CanExecute(null));
            runtime.SetPreviewRunning("Elara", true);
            Assert.False(vm.SortBagsCommand.CanExecute(null));
            Assert.False(runtime.LaunchPending("Wren"));
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
