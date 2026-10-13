using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public sealed class LibraryFilterTests
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
    public async Task Library_follows_starter_then_level_progression_without_reordering_saved_queue()
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["perfect-hairstyle", "mundane-birth"] });
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            Assert.Equal(new[] { "awakening", "mundane-birth", "spare-a-stick", "peasants-first-steps" }, vm.Quests.Take(4).Select(q => q.RecipeId));
            var ordered = vm.Quests.Select(q => q.Id).ToList();
            Assert.True(ordered.IndexOf("terroralleygarden") < ordered.IndexOf("cryptclimb"));
            Assert.True(ordered.IndexOf("ciad") < ordered.IndexOf("tiota"));
            Assert.True(ordered.IndexOf("tiota") < ordered.IndexOf("treas"));
            Assert.Equal(new[] { "perfect-hairstyle", "mundane-birth" }, runtime.Queue("Elara").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Outside_level_three_starter_can_queue_Awakening_but_completed_journal_stays_finished()
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 2, Map = 500, Position = new(10, 10), Level = 3, Hp = 10 });
            var vm = new MainWindowViewModel(runtime);
            Assert.Contains(vm.Quests, q => q.RecipeId == AwakeningEvidence.Id && q.QueueEligible);
            await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(new() { Character = "Elara", Quest = AwakeningEvidence.Id, Step = "complete", CompletedAt = DateTimeOffset.UtcNow });
            vm.Refresh();
            Assert.DoesNotContain(vm.Quests, q => q.RecipeId == AwakeningEvidence.Id);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Available_is_default_and_queue_locks_do_not_hide_plannable_quests()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            Assert.Equal("Available", vm.Filter);
            Assert.NotEmpty(vm.Quests);
            Assert.All(vm.Quests, q => Assert.True(q.QueueEligible));
            string[] initial = vm.Quests.Select(q => q.Id).ToArray();
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["perfect-hairstyle"], ActiveQuest = "perfect-hairstyle", State = "Paused" });
            Assert.Equal(initial, vm.Quests.Select(q => q.Id));
            Assert.All(vm.Quests, q => Assert.False(q.CanQueue));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Guides_excludes_automatic_quests_and_search_still_applies()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime) { Filter = "Guides" };
            Assert.NotEmpty(vm.Quests);
            Assert.All(vm.Quests, q => Assert.Empty(q.RecipeId));
            string name = vm.Quests.First().Name;
            vm.Search = name;
            Assert.All(vm.Quests, q => Assert.Contains(name, string.Join(' ', q.Name, q.Rewards, q.Requirements, q.Level, q.Reason), StringComparison.OrdinalIgnoreCase));
            vm.Search = "";
            vm.Filter = "All";
            Assert.Contains(vm.Quests, q => q.RecipeId == AwakeningEvidence.Id);
            Assert.Contains(vm.Quests, q => q.RecipeId == NewCharacterPath.FirstStepsId);
            Assert.Contains(vm.Quests, q => q.RecipeId == NewCharacterPath.ClimbId);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Completed_excludes_repeatable_attempts_until_goal_is_permanent()
    {
        var runtime = Runtime();
        try
        {
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new() { Character = "Elara", Quest = "spare-a-stick", Step = "complete", CompletedAt = DateTimeOffset.UtcNow });
            runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 2, Level = 6, Hp = 10, LegendsKnown = true, LegendsRevision = 2, Legends = [MuisirEvidence.Legend] });
            var vm = new MainWindowViewModel(runtime) { Filter = "Completed" };
            Assert.Contains(vm.Quests, q => q.RecipeId == "entered-muisir-village");
            Assert.DoesNotContain(vm.Quests, q => q.RecipeId == "spare-a-stick");
            Assert.All(vm.Quests, q => Assert.True(q.IsCompleted));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("Available")]
    [InlineData("Completed")]
    [InlineData("Guides")]
    [InlineData("All")]
    public async Task Legend_toggle_combines_with_every_category_search_and_character(string category)
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 2,
                Level = 6, Hp = 10, LegendsKnown = true, LegendsRevision = 2, Legends = [MuisirEvidence.Legend] });
            var vm = new MainWindowViewModel(runtime) { Filter = category };
            string[] all = vm.Quests.Select(q => q.Id).ToArray();
            string[] expected = vm.Quests.Where(q => q.HasLegendReward).Select(q => q.Id).ToArray();
            vm.LegendMarksOnly = true;
            Assert.Equal(category, vm.Filter);
            Assert.Equal(expected, vm.Quests.Select(q => q.Id));
            Assert.All(vm.Quests, q => Assert.True(q.HasLegendReward));
            int count = category switch { "Available" => vm.AvailableCount, "Completed" => vm.CompletedCount, "Guides" => vm.GuidesCount, _ => vm.AllCount };
            Assert.Equal(expected.Length, count);
            vm.Search = "no matching quest anywhere";
            Assert.Empty(vm.Quests);
            Assert.Equal(count, category switch { "Available" => vm.AvailableCount, "Completed" => vm.CompletedCount, "Guides" => vm.GuidesCount, _ => vm.AllCount });
            vm.Search = "";
            vm.LegendMarksOnly = false;
            Assert.Equal(all, vm.Quests.Select(q => q.Id));
            vm.LegendMarksOnly = true;
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.Equal(category, vm.Filter);
            Assert.True(vm.LegendMarksOnly);
            Assert.All(vm.Quests, q => Assert.True(q.HasLegendReward));
            Assert.Empty(runtime.Queue("Elara").Quests);
            Assert.Empty(runtime.Queue("Wren").Quests);
            vm.Filter = "All";
            vm.LegendMarksOnly = false;
            Assert.False(vm.Quests.Single(q => q.RecipeId == ShinewoodFaerieEvidence.Id).HasLegendReward);
            Assert.True(vm.Quests.Single(q => q.RecipeId == "mundane-birth").HasLegendReward);
            Assert.False(vm.Quests.Single(q => q.RecipeId == NewCharacterPath.FirstStepsId).HasLegendReward);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Character_legend_chips_keep_observed_and_remembered_information_distinct()
    {
        var runtime = Runtime();
        try
        {
            var memory = new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), runtime.Settings.ServerProfile);
            await memory.ObserveAsync("Elara", new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 2,
                LegendsKnown = true, LegendsRevision = 2, Legends = ["Remembered first mark", "Remembered second mark"] });
            var vm = new MainWindowViewModel(runtime);
            var character = vm.Characters.Single(c => c.Name == "Elara");
            Assert.Equal(new[] { "Remembered first mark", "Remembered second mark" }, character.LegendMarks);
            Assert.Contains("last observed", character.LegendSummary);
            Assert.False(character.PortraitCharacter.Connected);
            var same = character.LegendMarks;
            vm.Refresh();
            Assert.Same(same, character.LegendMarks);
            runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 2, Revision = 4,
                LegendsKnown = true, LegendsRevision = 4, Legends = ["Current mark"] });
            Assert.Equal("Current mark", Assert.Single(character.LegendMarks));
            Assert.Equal("1 · observed", character.LegendSummary);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Legends = [] });
            Assert.False(character.HasLegendMarks);
            Assert.Equal("0 · observed", character.LegendSummary);
            Assert.Equal("No legend marks observed", character.LegendEmptyText);
            var unknown = vm.Characters.Single(c => c.Name == "Wren");
            Assert.False(unknown.HasLegendMarks);
            Assert.Equal("Not observed", unknown.LegendSummary);
            Assert.Contains("Connect", unknown.LegendEmptyText);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Scheduler_warning_overrides_healthy_schedules_and_off_remains_visible()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            Assert.True(vm.SchedulerIndicatorVisible);
            Assert.Equal("Off", vm.SchedulerIndicatorLabel);
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
            await runtime.SaveScheduleAsync("Wren", "perfect-hairstyle", DateTimeOffset.UtcNow.AddHours(1), true, true);
            await runtime.SetSchedulerEnabledAsync(true, true);
            Assert.True(vm.SchedulerIndicatorReady);
            Assert.Equal("Active", vm.SchedulerIndicatorLabel);
            await runtime.PauseScheduleAsync(runtime.SchedulerState.Entries.First().Id);
            Assert.True(vm.SchedulerIndicatorWarning);
            Assert.False(vm.SchedulerIndicatorReady);
            await runtime.SetSchedulerEnabledAsync(false, false);
            Assert.Equal("Off", vm.SchedulerIndicatorLabel);
            Assert.False(vm.SchedulerIndicatorWarning);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData(NewCharacterPath.FirstStepsId)]
    [InlineData(NewCharacterPath.ClimbId)]
    public async Task New_character_schedules_require_saved_acknowledgements(string id)
    {
        var runtime = Runtime();
        try
        {
            Assert.True(runtime.SchedulerNeedsChoices("Elara", id));
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SaveScheduleAsync("Elara", id, DateTimeOffset.UtcNow.AddHours(1), false, true));
            await runtime.SaveSchedulerChoicesAsync("Elara", id, new(FirstStepsBriefed: id == NewCharacterPath.FirstStepsId, TerrorWindowAccepted: id == NewCharacterPath.ClimbId));
            Assert.False(runtime.SchedulerNeedsChoices("Elara", id));
            await runtime.SaveScheduleAsync("Elara", id, DateTimeOffset.UtcNow.AddHours(1), false, true);
            Assert.Equal(id, Assert.Single(runtime.SchedulerState.Entries).Quest);
            Assert.False(runtime.SchedulerEnabled);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Unavailable_connection_pauses_indicator_without_exposing_diagnostics()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            await runtime.SetSchedulerEnabledAsync(true, true);
            typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.ConnectionError))!.SetValue(runtime, "Synthetic private transport details");
            vm.Refresh();
            Assert.True(vm.SchedulerIndicatorWarning);
            Assert.False(vm.SchedulerIndicatorReady);
            Assert.Equal("Paused", vm.SchedulerIndicatorLabel);
            Assert.Contains("connection", vm.SchedulerIndicatorTooltip, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Synthetic private", vm.SchedulerIndicatorTooltip);
            await runtime.SetSchedulerEnabledAsync(false, false);
            Assert.True(vm.SchedulerIndicatorOff);
            Assert.False(vm.SchedulerIndicatorWarning);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
