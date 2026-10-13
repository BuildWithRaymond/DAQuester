using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class JourneyPresentationTests
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
    public async Task Browsing_and_planning_never_create_a_current_journey()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            foreach (var id in new[] { "perfect-hairstyle", "spare-a-stick", "mundane-birth" })
            {
                vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == id);
                Assert.Equal("No journey running", vm.JourneyTitle);
                Assert.Equal("Idle", vm.JourneyState);
                Assert.Equal(0, vm.JourneySteps.Total);
                Assert.Empty(vm.JourneyRewards);
                Assert.Empty(vm.JourneyNext);
            }
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["perfect-hairstyle"] });
            vm.Refresh();
            Assert.Equal("No journey running", vm.JourneyTitle);
            Assert.Equal("Idle", vm.JourneyState);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("Running")]
    [InlineData("Paused")]
    public async Task Started_queue_owns_the_card_until_stopped_or_completed(string state)
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", State = state,
                Quests = ["spare-a-stick", "mundane-birth"], ActiveQuest = "spare-a-stick" });
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "mundane-birth");
            Assert.Equal("Spare a Stick", vm.JourneyTitle);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.Equal("No journey running", vm.JourneyTitle);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            Assert.Equal("Spare a Stick", vm.JourneyTitle);
            // Stop retains its checkpoint for reconciliation, but ends the current journey.
            runtime.SetPreviewQueue(runtime.Queue("Elara") with { State = "Idle" });
            vm.Refresh();
            Assert.Equal("No journey running", vm.JourneyTitle);
            runtime.SetPreviewQueue(new() { Character = "Elara", Reason = "Queue completed" });
            vm.Refresh();
            Assert.Equal("Idle", vm.JourneyState);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Queue_transition_does_not_fall_back_to_library_selection()
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", State = "Running", Quests = ["spare-a-stick"] });
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "mundane-birth");
            Assert.True(vm.HasCurrentJourney);
            Assert.Equal("Journey in progress", vm.JourneyTitle);
            Assert.Empty(vm.JourneyIcon);
            Assert.Equal(0, vm.JourneySteps.Total);
            runtime.SetPreviewQueue(runtime.Queue("Elara") with { ActiveQuest = "spare-a-stick" });
            Assert.Equal("Spare a Stick", vm.JourneyTitle);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Active_scheduled_journey_follows_its_character_and_ends_with_ownership()
    {
        var runtime = Runtime();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var character = typeof(DesktopRuntime).GetField("schedulerCharacter", flags)!;
        var quest = typeof(DesktopRuntime).GetField("schedulerQuest", flags)!;
        try
        {
            var vm = new MainWindowViewModel(runtime);
            runtime.SetPreviewQueue(new() { Character = "Elara", Reason = "Stopped; progress saved", Quests = ["mundane-birth"] });
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "mundane-birth");
            // Synthetic ownership only: no launcher, network or game actions.
            character.SetValue(runtime, "Elara");
            quest.SetValue(runtime, "spare-a-stick");
            vm.Refresh();
            Assert.Equal("Spare a Stick", vm.JourneyTitle);
            Assert.DoesNotContain("Stopped", vm.JourneyMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Mundane Birth", vm.JourneyNext);
            Assert.Equal("spare-a-stick", runtime.SchedulerQuestId("elara"));
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.False(vm.HasCurrentJourney);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            Assert.True(vm.HasCurrentJourney);
            character.SetValue(runtime, null);
            quest.SetValue(runtime, null);
            vm.Refresh();
            Assert.Equal("No journey running", vm.JourneyTitle);
        }
        finally
        {
            character.SetValue(runtime, null);
            quest.SetValue(runtime, null);
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Scheduler_indicator_distinguishes_armed_paused_and_disabled_schedules()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            Assert.True(vm.SchedulerIndicatorVisible);
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
            Assert.Equal("Off", vm.SchedulerIndicatorLabel);
            await runtime.SetSchedulerEnabledAsync(true, true);
            Assert.True(vm.SchedulerIndicatorVisible);
            Assert.Equal("Active", vm.SchedulerIndicatorLabel);
            Assert.True(vm.SchedulerIndicatorReady);
            await runtime.PauseScheduleAsync(Assert.Single(runtime.SchedulerState.Entries).Id);
            Assert.Equal("Paused", vm.SchedulerIndicatorLabel);
            Assert.False(vm.SchedulerIndicatorReady);
            await runtime.SetSchedulerEnabledAsync(false, false);
            Assert.Equal("Off", vm.SchedulerIndicatorLabel);
            await runtime.RemoveScheduleAsync(Assert.Single(runtime.SchedulerState.Entries).Id);
            Assert.True(vm.SchedulerIndicatorVisible);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("Launching", "Launch in progress", "launching")]
    [InlineData("Preparing", "Preparing scheduled quest", "checking fresh")]
    [InlineData("Running", "Quest in progress", "scheduled quest is in progress")]
    [InlineData("Closing", "Closing game window", "closing its game window")]
    public async Task Scheduled_phases_ignore_an_unrelated_saved_queue(string phase, string state, string message)
    {
        var runtime = Runtime();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        void Set(string field, object? value) => typeof(DesktopRuntime).GetField(field, flags)!.SetValue(runtime, value);
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", State = "Paused", ActiveQuest = "mundane-birth",
                Quests = ["mundane-birth"], Reason = "Stopped; progress saved" });
            Set("schedulerCharacter", "Elara");
            Set("schedulerQuest", "spare-a-stick");
            Set("schedulerPhase", phase);
            var vm = new MainWindowViewModel(runtime);
            Assert.Equal("Spare a Stick", vm.JourneyTitle);
            Assert.Equal(state, vm.JourneyState);
            Assert.Contains(message, vm.JourneyMessage);
            Assert.DoesNotContain("Mundane Birth", vm.JourneyNext);
            Assert.Equal(phase, runtime.SchedulerRunPhase("elara"));
            Assert.Null(runtime.SchedulerRunPhase("Wren"));
        }
        finally
        {
            Set("schedulerCharacter", null);
            Set("schedulerQuest", null);
            Set("schedulerPhase", null);
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Scheduler_login_block_is_shown_as_needing_attention_until_disabled()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
            await runtime.SetSchedulerEnabledAsync(true, true);
            var scheduler = (QuestScheduler)typeof(DesktopRuntime).GetField("scheduler",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
            await scheduler.BlockLoginAsync("Elara", "Synthetic login denied");
            Assert.Equal("Needs attention", vm.SchedulerIndicatorLabel);
            Assert.True(vm.SchedulerIndicatorWarning);
            Assert.False(vm.SchedulerIndicatorReady);
            await runtime.SetSchedulerEnabledAsync(false, false);
            Assert.Equal("Off", vm.SchedulerIndicatorLabel);
            Assert.False(vm.SchedulerIndicatorWarning);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
