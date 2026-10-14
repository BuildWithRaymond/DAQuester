using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using Xunit;
using DAQuester.Core;

namespace DAQuester.AppTests;

public class DashboardTests
{
    internal static DesktopRuntime Workspace()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara", "Wren"];
        settings.ClientPath = "";
        settings.Save();
        var runtime = new DesktopRuntime(preview: true);
        runtime.SetPreviewSnapshot(new()
        {
            Name = "Elara",
            Id = 1,
            Connected = true,
            Generation = 1,
            Revision = 2,
            Level = 9
        });
        return runtime;
    }

    [Fact]
    public async Task Application_opens_on_character_dashboard()
    {
        var runtime = Workspace();
        try
        {
            Assert.Equal("Dashboard", new MainWindowViewModel(runtime).Page);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Search_filters_real_characters_and_row_selection_leaves_planner_character_alone()
    {
        var runtime = Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            Assert.True(vm.DashboardCardsView);
            Assert.Equal(2, vm.DashboardCount);
            Assert.Equal(1, vm.DashboardIdleCount);
            Assert.Equal(1, vm.DashboardOfflineCount);
            var original = vm.SelectedCharacter;
            vm.SelectedDashboardCharacter = vm.DashboardCharacters.Single(c => c.Name == "Wren");
            Assert.Same(original, vm.SelectedCharacter);
            vm.DashboardFilter = "Offline";
            Assert.Equal("Wren", Assert.Single(vm.DashboardCharacters).Name);
            vm.DashboardFilter = "All";
            vm.DashboardSearch = " Elara ";
            var row = Assert.Single(vm.DashboardCharacters);
            Assert.Equal("Elara", row.Name);
            vm.DashboardListView = true;
            Assert.True(vm.DashboardShowList);
            vm.DashboardSearch = "missing";
            Assert.True(vm.DashboardEmpty);
            Assert.False(vm.DashboardShowList);
            vm.DashboardSearch = "";
            Assert.True(vm.DashboardShowList);
            vm.SelectedDashboardCharacter = vm.DashboardCharacters.Single(c => c.Name == "Wren");
            vm.SelectedDashboardCharacter.PlannerCommand.Execute(null);
            Assert.Equal("Wren", vm.CharacterName);
            Assert.Equal("Planner", vm.Page);
            vm.DashboardViewCommand.Execute("List");
            Assert.True(vm.DashboardListView);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Dashboard_journey_uses_actual_run_and_never_the_selected_library_quest()
    {
        var runtime = Workspace();
        try
        {
            runtime.SetPreviewQueue(new()
            {
                Character = "Elara",
                Quests = [NewCharacterPath.ClimbId, "spare-a-stick"],
                State = "Running",
                ActiveQuest = NewCharacterPath.ClimbId
            });
            runtime.SetPreviewRunning("Elara", true);
            var vm = new MainWindowViewModel(runtime);
            var row = vm.DashboardCharacters.Single(c => c.Name == "Elara");
            Assert.Equal("Running", row.Status);
            Assert.Equal("Crypt Climb", row.JourneyTitle);
            Assert.Equal(2, row.QueueEntries.Count);
            vm.Filter = "All";
            vm.SelectedQuest = vm.Quests.First(q => q.RecipeId == "spare-a-stick");
            Assert.Equal("Crypt Climb", row.JourneyTitle);
            Assert.False(vm.DashboardCharacters.Single(c => c.Name == "Wren").PrimaryCommand.CanExecute(null));
            Assert.DoesNotContain("Warrior", row.LevelAndClass);
            Assert.Equal(1, vm.DashboardRunningCount);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Pause_remains_available_while_the_same_dashboard_command_is_awaiting_owned_work()
    {
        var runtime = Workspace();
        Task? pending = null;
        var busy = (HashSet<string>)typeof(DesktopRuntime).GetField("queueSaving", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        try
        {
            runtime.SetPreviewRunning("Elara", true);
            var vm = new MainWindowViewModel(runtime);
            var row = vm.DashboardCharacters.Single(c => c.Name == "Elara");
            busy.Add("Elara"); // Hold the real pause path at its queue-save wait; no game command runs.
            pending = row.PrimaryCommand.ExecuteAsync();
            Assert.False(pending.IsCompleted);
            Assert.True(row.PrimaryCommand.CanExecute(null), "Pause must stay available during a long-running dashboard command.");
        }
        finally
        {
            busy.Remove("Elara");
            if (pending is not null)
                await pending;
            await runtime.ShutdownAsync();
        }
    }
}
