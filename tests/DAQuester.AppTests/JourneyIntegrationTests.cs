using System.Collections.Immutable;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class JourneyIntegrationTests
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
            Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 5,
            Level = 1, Hp = 100, Gold = 50_000, InventoryKnown = true,
            LegendsKnown = true, LegendsRevision = 5, Weight = 0, MaxWeight = 100, Experience = 0,
            Class = "Priest"
        });
        return runtime;
    }

    [Fact]
    public async Task Queue_rows_explain_starter_dependencies_and_recalculate_after_reorder_and_removal()
    {
        var runtime = Workspace();
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId, "emerald-dawn"] });
            var observed = runtime.Snapshot("Elara");
            var vm = new MainWindowViewModel(runtime);
            Assert.Contains("Conditional", vm.QueueItems[1].PlanningSummary);
            Assert.Contains(NewCharacterPath.FirstStepsName, vm.QueueItems[1].PlanningSummary);
            Assert.Contains("Unknown", vm.QueueItems[2].PlanningSummary);
            Assert.Contains(NewCharacterPath.ClimbName, vm.QueueItems[2].PlanningSummary);
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new() { Character = "Elara", Quest = "emerald-dawn", EmeraldOptions = new("emerald-knights") });
            vm.Refresh();
            Assert.Contains("Conditional", vm.QueueItems[2].PlanningSummary);
            Assert.Contains(NewCharacterPath.ClimbName, vm.QueueItems[2].PlanningSummary);
            await runtime.MoveQueueAsync("Elara", NewCharacterPath.ClimbId, 0);
            vm.Refresh();
            Assert.Contains("Blocked", vm.QueueItems[0].PlanningSummary);
            await runtime.EditQueueAsync("Elara", q => q with { Quests = ["emerald-dawn"] });
            vm.Refresh();
            Assert.Contains("Blocked", Assert.Single(vm.QueueItems).PlanningSummary);
            Assert.Equal(observed, runtime.Snapshot("Elara"));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Whole_journey_separates_next_action_from_consumption_and_reserves_gold_once()
    {
        var runtime = Workspace();
        try
        {
            var observed = runtime.Snapshot("Elara") with { Level = 11, Gold = 25_000,
                Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, SickChildEvidence.Blossom, 5)) };
            runtime.SetPreviewSnapshot(observed);
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [SickChildEvidence.Id, AltarWorshipEvidence.Id] });
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new() { Character = "Elara", Quest = SickChildEvidence.Id, SickChildOptions = new(SickChildOptions.Daily, [], 0) });
            var vm = new MainWindowViewModel(runtime);
            var overview = vm.JourneyOverview;
            Assert.Contains("32,500", overview.Consumption);
            Assert.Contains("32,500", overview.StartingBalance);
            Assert.Contains("5,000", overview.NextActionBalance);
            Assert.Contains("25,000", overview.VerifiedGold);
            Assert.Contains("7,500", overview.Shortfall);
            Assert.Contains("25,000", overview.Rows[0].Gold);
            Assert.Contains("7,500", overview.Rows[1].Gold);
            Assert.Contains("27h", overview.Rows[1].Wait);
            Assert.Contains("5 White Blossom reserved", overview.SupplySummary);
            Assert.Same(overview, vm.Characters.Single(c => c.Name == "Elara").JourneyOverview);
            Assert.Same(overview, vm.DashboardCharacters.Single(c => c.Name == "Elara").JourneyOverview);
            Assert.Equal(observed, runtime.Snapshot("Elara"));
            Assert.Empty(journal.Load("Elara", SickChildEvidence.Id).SickChildTurnIns);
            Assert.Null(journal.Load("Elara", AltarWorshipEvidence.Id).CompletedAt);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Existing_supplies_and_partial_progress_remain_explicit_in_the_view()
    {
        var runtime = Workspace();
        try
        {
            var now = DateTimeOffset.UtcNow;
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Level = 11,
                Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, SickChildEvidence.Blossom, 1)).Add(2, new(2, AltarWorshipEvidence.Shield, 3)) });
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [SickChildEvidence.Id, AltarWorshipEvidence.Id] });
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new() { Character = "Elara", Quest = SickChildEvidence.Id,
                SickChildOptions = new(SickChildOptions.Daily, [], 1),
                SickChildTurnIns = [new(now.AddDays(-2), 1, 25_000, 20_000, "", null), new(now.AddDays(-1), 1, 20_000, 15_000, "", null)] });
            await journal.SaveAsync(new() { Character = "Elara", Quest = AltarWorshipEvidence.Id,
                AltarOfferings = [new(now.AddHours(-4), 1, 750, "", 0), new(now.AddHours(-1), 1, 750, "", 0)] });
            var vm = new MainWindowViewModel(runtime, () => now);
            Assert.Contains("2 White Blossom needed", vm.JourneyOverview.Rows[0].Items);
            Assert.Contains("2 recorded receipts", vm.JourneyOverview.Rows[0].Progress);
            Assert.Contains("1 player-declared", vm.JourneyOverview.Rows[0].Progress);
            Assert.Contains("10,000", vm.JourneyOverview.Rows[0].Gold);
            Assert.Contains("8 Leather Shield needed", vm.JourneyOverview.Rows[1].Items);
            Assert.Contains("3 verified/reserved", vm.JourneyOverview.Rows[1].Items);
            Assert.Contains("5 to obtain", vm.JourneyOverview.Rows[1].Items);
            Assert.Contains("3,750", vm.JourneyOverview.Rows[1].Gold);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Fresh_choices_inventory_and_clock_refresh_all_character_surfaces()
    {
        var runtime = Workspace();
        try
        {
            var now = DateTimeOffset.UtcNow;
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Level = 11 });
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [SickChildEvidence.Id] });
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            var vm = new MainWindowViewModel(runtime, () => now);
            Assert.Contains("Unknown", Assert.Single(vm.QueueItems).PlanningSummary);
            Assert.Contains("unavailable", vm.JourneyOverview.Wait);
            await journal.SaveAsync(new() { Character = "Elara", Quest = SickChildEvidence.Id,
                SickChildOptions = new(SickChildOptions.Daily, [], 0), SickChildPicks = [new("Elara", now.AddHours(-23), false)] });
            vm.Refresh();
            Assert.Contains("97h", vm.JourneyOverview.Wait);
            now = now.AddHours(2);
            vm.Refresh();
            Assert.Contains("96h", vm.JourneyOverview.Wait);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Revision = 6, Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, SickChildEvidence.Blossom, 5)) });
            vm.Refresh();
            Assert.Contains("0m", vm.JourneyOverview.Wait);
            Assert.Contains("0 to obtain", vm.JourneyOverview.Rows[0].Items);
            var original = vm.SelectedCharacter;
            vm.SelectedDashboardCharacter = vm.DashboardCharacters.Single(c => c.Name == "Wren");
            Assert.Same(original, vm.SelectedCharacter);
            Assert.False(vm.SelectedDashboardCharacter.JourneyOverview.HasQueue);
            Assert.Contains("25,000", vm.Characters.Single(c => c.Name == "Elara").JourneyOverview.Consumption);
            Assert.Null(journal.Load("Elara", SickChildEvidence.Id).CompletedAt);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Unknown_facts_and_uncovered_costs_are_not_rendered_as_zero_cost()
    {
        var runtime = Workspace();
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["perfect-hairstyle"] });
            var vm = new MainWindowViewModel(runtime);
            Assert.Contains("unavailable", vm.JourneyOverview.Consumption);
            Assert.Contains("unavailable", vm.JourneyOverview.StartingBalance);
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [SickChildEvidence.Id] });
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Level = 11, Revision = 0, InventoryKnown = false, Gold = null });
            vm.Refresh();
            Assert.Contains("Unknown", Assert.Single(vm.QueueItems).PlanningSummary);
            Assert.Contains("unavailable", vm.JourneyOverview.VerifiedGold);
            Assert.Contains("unknown", vm.JourneyOverview.Shortfall);
            Assert.Contains("owned unknown", vm.JourneyOverview.Rows[0].Items);
            Assert.False(vm.DashboardCharacters.Single(c => c.Name == "Elara").PrimaryCommand.CanExecute(null));
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(runtime.Job("Elara", SickChildEvidence.Id) with
            {
                SickChildOptions = new(SickChildOptions.Daily, [], 0),
                Pending = new("sick-turnin", 1, 10, 1, 0, 0, DateTimeOffset.UtcNow)
            });
            vm.Refresh();
            Assert.StartsWith("Up to 25,000", vm.JourneyOverview.Consumption);
            Assert.Contains("reconcile", Assert.Single(vm.QueueItems).PlanningExplanation);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Sidebar_does_not_call_an_unchosen_head_ready()
    {
        var runtime = Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Level = 11 });
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [SickChildEvidence.Id] });
            var vm = new MainWindowViewModel(runtime);
            Assert.Contains("choices", vm.QueueReadiness, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("All selected quests are ready", vm.QueueReadiness);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
