using System.Collections.Immutable;
using System.Reflection;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class QueueEligibilityTests
{
    static CharacterSnapshot Observed() => new()
    {
        Name = "Elara",
        Id = 1,
        Connected = true,
        Generation = 1,
        Revision = 2,
        Level = 6,
        LegendsKnown = true,
        LegendsRevision = 2,
        Legends = ["((Unregistered))"]
    };

    static async Task<DesktopRuntime> Runtime(CharacterSnapshot? remembered = null)
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara", "Wren"];
        settings.ClientPath = "";
        settings.Save();
        if (remembered is not null)
            await new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), settings.ServerProfile).ObserveAsync("Elara", remembered);
        return new(preview: true);
    }

    [Theory]
    [InlineData("emerald-dawn", "level 11+", false)]
    [InlineData("emerald-dawn", "level 11+", true)]
    [InlineData("entered-muisir-village", "registered character", false)]
    [InlineData("entered-muisir-village", "registered character", true)]
    public async Task KnownRestrictionsBlockBothQueueControlsAndDirectEdits(string quest, string reason, bool connected)
    {
        var observed = Observed();
        var runtime = await Runtime(connected ? null : observed);
        try
        {
            if (connected)
                runtime.SetPreviewSnapshot(observed);
            await runtime.EditQueueAsync("Elara", queue => queue with { Quests = ["perfect-hairstyle"] });
            var vm = new MainWindowViewModel(runtime);
            vm.Filter = "All";
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == quest);
            Assert.Equal("Blocked", vm.SelectedQuest.Status);
            Assert.Contains(reason, vm.SelectedQuest.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.False(vm.SelectedQuest.CanQueue);
            Assert.False(vm.AddSelectedQuestToQueueCommand.CanExecute(null));
            await vm.ToggleQuestAsync(vm.SelectedQuest);
            var error = await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.EditQueueAsync("Elara", queue => queue with { Quests = queue.Quests.Append(quest).ToArray() }));
            Assert.Contains(reason, error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(["perfect-hairstyle"], runtime.Queue("Elara").Quests);
            Assert.Equal(["perfect-hairstyle"], new FileQuestQueueStore(Path.Combine(AppSettings.DataPath, "queues")).Load("Elara").Quests);
            Assert.Empty(runtime.Queue("Wren").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("emerald-dawn", "inventory slot")]
    [InlineData(SickChildEvidence.Id, "5,000 gold")]
    [InlineData(SickChildEvidence.AnamanId, "inventory slot")]
    public async Task MissingChoicesCannotHideKnownResourceRestrictions(string quest, string reason)
    {
        var runtime = await Runtime();
        try
        {
            runtime.SetPreviewSnapshot(Observed() with
            {
                Level = 11,
                Hp = 10,
                Legends = [],
                InventoryKnown = true,
                Inventory = Enumerable.Range(1, 59).ToImmutableDictionary(i => (byte)i, i => new Item((byte)i, "Synthetic item", 1)),
                Gold = 0,
                Weight = 0,
                MaxWeight = 100,
                Experience = 100
            });
            var availability = runtime.Availability("Elara", quest);
            Assert.Equal("Blocked", availability.Status);
            Assert.False(availability.CanQueue);
            Assert.Contains(reason, availability.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task EligibleEmeraldStillOffersChoicesAfterCheckingRequirements()
    {
        var runtime = await Runtime();
        try
        {
            runtime.SetPreviewSnapshot(Observed() with
            {
                Level = 11,
                Hp = 10,
                Legends = [],
                InventoryKnown = true,
                Weight = 0,
                MaxWeight = 100,
                Experience = 100
            });
            var availability = runtime.Availability("Elara", "emerald-dawn");
            Assert.Equal("Choices", availability.Status);
            Assert.True(availability.CanQueue);
            Assert.True(availability.Ready);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("mundane-birth")]
    [InlineData("emerald-dawn")]
    [InlineData(SickChildEvidence.Id)]
    public async Task MissingChoicesCannotHideADeadCharacter(string quest)
    {
        var runtime = await Runtime();
        try
        {
            runtime.SetPreviewSnapshot(Observed() with { Level = 11, Hp = 0, Legends = [] });
            var availability = runtime.Availability("Elara", quest);
            Assert.Equal("Blocked", availability.Status);
            Assert.False(availability.CanQueue);
            Assert.False(availability.Ready);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task UnknownOfflineRequirementsStillAllowPlanning()
    {
        var runtime = await Runtime();
        try
        {
            foreach (string quest in new[] { "entered-muisir-village", "emerald-dawn" })
            {
                var availability = runtime.Availability("Elara", quest);
                Assert.Equal("Not checked", availability.Status);
                Assert.True(availability.CanQueue);
                Assert.False(availability.Ready);
                await runtime.EditQueueAsync("Elara", queue => queue with { Quests = queue.Quests.Append(quest).ToArray() });
            }
            Assert.False(runtime.Snapshot("Elara").Connected);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("entered-muisir-village")]
    [InlineData("emerald-dawn")]
    public async Task ExistingIneligibleQuestCanStillBeRemoved(string quest)
    {
        var runtime = await Runtime(Observed());
        try
        {
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [quest] });
            var vm = new MainWindowViewModel(runtime);
            vm.Filter = "All";
            var row = vm.Quests.Single(q => q.RecipeId == quest);
            Assert.Equal("Blocked", row.Status);
            Assert.True(row.CanQueue);
            await vm.ToggleQuestAsync(row);
            Assert.Empty(runtime.Queue("Elara").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Starter_dependencies_allow_selection_and_recalculate_after_edits_and_fresh_state()
    {
        var runtime = await Runtime();
        try
        {
            var observed = Observed() with
            {
                Level = 1, Hp = 100, Gold = 50_000, InventoryKnown = true,
                Legends = [], Weight = 0, MaxWeight = 100, Experience = 0
            };
            runtime.SetPreviewSnapshot(observed);
            await runtime.EditQueueAsync("Elara", q => q with { Quests = [NewCharacterPath.FirstStepsId] });
            Assert.True(runtime.PlanningAvailability("Elara", NewCharacterPath.ClimbId).CanQueue);
            Assert.True(runtime.PlanningAvailability("Elara", NewCharacterPath.ClimbId).Warning);
            await runtime.EditQueueAsync("Elara", q => q with { Quests = [.. q.Quests, NewCharacterPath.ClimbId, "emerald-dawn"] });
            Assert.Equal(JourneyPlanningState.Conditional, runtime.PlanQueue("Elara").Entries[1].State);
            Assert.Equal("Not checked", runtime.PlanningAvailability("Elara", "emerald-dawn").Status);
            Assert.False(runtime.Availability("Elara", "emerald-dawn").Ready);
            Assert.Equal("", runtime.QueueStartReason("Elara", runtime.Queue("Elara")));
            Assert.Equal("First quest ready. Later requirements will be checked.", new MainWindowViewModel(runtime).QueueReadiness);
            Assert.Equal(observed, runtime.Snapshot("Elara"));

            await runtime.MoveQueueAsync("Elara", NewCharacterPath.ClimbId, 0);
            Assert.Equal(JourneyPlanningState.Blocked, runtime.PlanQueue("Elara").Entries[0].State);
            Assert.Contains("Missing", runtime.QueueStartReason("Elara", runtime.Queue("Elara")));
            await runtime.EditQueueAsync("Elara", q => q with { Quests = ["emerald-dawn"] });
            Assert.Equal(JourneyPlanningState.Blocked, runtime.PlanQueue("Elara").Entries[0].State);
            runtime.SetPreviewSnapshot(observed with { Level = 11 });
            Assert.True(runtime.Availability("Elara", "emerald-dawn").Ready);
            Assert.True(runtime.PlanQueue("Elara").Entries[0].ChoicesRequired);
            Assert.Equal(JourneyPlanningState.Unknown, runtime.PlanQueue("Elara").Entries[0].State);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Adding_an_earlier_entry_cannot_use_a_prerequisite_that_will_run_later()
    {
        var runtime = await Runtime();
        try
        {
            runtime.SetPreviewSnapshot(Observed() with { Level = 1, Hp = 100, Gold = 50_000, InventoryKnown = true, Legends = [] });
            await runtime.EditQueueAsync("Elara", q => q with { Quests = [NewCharacterPath.FirstStepsId] });
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.EditQueueAsync("Elara", q => q with { Quests = [NewCharacterPath.ClimbId, NewCharacterPath.FirstStepsId] }));
            Assert.Equal([NewCharacterPath.FirstStepsId], runtime.Queue("Elara").Quests);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task A_supported_level_dependency_can_be_planned_while_future_gold_remains_unknown()
    {
        var runtime = await Runtime();
        try
        {
            runtime.SetPreviewSnapshot(Observed() with { Level = 1, Hp = 100, Gold = 50_000, InventoryKnown = true, Legends = [] });
            await runtime.EditQueueAsync("Elara", q => q with { Quests = [NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId, SickChildEvidence.Id] });
            var entry = runtime.PlanQueue("Elara").Entries[^1];
            Assert.Equal(JourneyPlanningState.Unknown, entry.State);
            Assert.Equal(NewCharacterPath.ClimbId, Assert.Single(entry.Conditions).EarlierQuest);
            Assert.True(runtime.PlanningAvailability("Elara", SickChildEvidence.Id).CanQueue);
            Assert.False(runtime.Availability("Elara", SickChildEvidence.Id).Ready);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Dispatch_rechecks_observations_choices_and_cooldowns_without_using_the_plan()
    {
        var runtime = await Runtime();
        try
        {
            var observed = Observed() with
            {
                Level = 1, Hp = 100, Gold = 50_000, InventoryKnown = true, Legends = [],
                Weight = 0, MaxWeight = 100, Experience = 0
            };
            runtime.SetPreviewSnapshot(observed);
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId, "emerald-dawn"] });
            var queue = runtime.Queue("Elara");
            var dispatch = typeof(DesktopRuntime).GetMethod("RequireQueueDispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Check(string quest = "emerald-dawn") => dispatch.Invoke(runtime, ["Elara", quest, false, 1L]);
            string Blocked(string quest = "emerald-dawn") => Assert.IsType<QuestBlockedException>(Assert.Throws<TargetInvocationException>(() => Check(quest)).InnerException).Message;

            Assert.Equal(JourneyPlanningState.Unknown, runtime.PlanQueue("Elara").Entries[^1].State);
            Assert.Contains("level 11", Blocked(), StringComparison.OrdinalIgnoreCase);
            runtime.SetPreviewSnapshot(observed with { Level = 11, Generation = 2 });
            Assert.Contains("connection changed", Blocked(), StringComparison.OrdinalIgnoreCase);
            runtime.SetPreviewSnapshot(observed with { Level = 11, InventoryKnown = false });
            Assert.Contains("inventory", Blocked(), StringComparison.OrdinalIgnoreCase);
            runtime.SetPreviewSnapshot(observed with { Level = 11 });
            Assert.Contains("Choose", Blocked());
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new() { Character = "Elara", Quest = "emerald-dawn", EmeraldOptions = new("emerald-knights") });
            Check();
            await journal.SaveAsync(new() { Character = "Elara", Quest = "perfect-hairstyle", CompletedAt = DateTimeOffset.UtcNow });
            Assert.Contains("Next attempt", Blocked("perfect-hairstyle"));
            Assert.Equal(queue, runtime.Queue("Elara"));
            Assert.Null(journal.Load("Elara", "emerald-dawn").CompletedAt);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Forecast_adapter_reads_current_choices_helpers_and_supplies_without_changing_saved_data()
    {
        var runtime = await Runtime();
        try
        {
            var now = new DateTimeOffset(2026, 10, 13, 15, 0, 0, TimeSpan.Zero);
            var observed = Observed() with { Level = 11, Hp = 100, Gold = 25_000, InventoryKnown = true, Legends = [] };
            runtime.SetPreviewSnapshot(observed);
            runtime.SetPreviewSnapshot(observed with
            {
                Name = "Wren", Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, SickChildEvidence.Blossom, 1))
            });
            runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [SickChildEvidence.Id, AltarWorshipEvidence.Id] });
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new()
            {
                Character = "Elara", Quest = SickChildEvidence.Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["Wren"], 0)
            });
            var before = runtime.Queue("Elara");
            var forecast = runtime.ForecastQueue("Elara", () => now);
            Assert.Equal(now, forecast.CalculatedAt);
            Assert.Equal(32_500, forecast.KnownGoldBudget);
            Assert.Equal("Wren", Assert.Single(forecast.Entries[0].Supplies).Character);
            Assert.Equal(7_500, forecast.KnownGoldShortfall);
            Assert.Equal(before, runtime.Queue("Elara"));
            Assert.Equal(observed, runtime.Snapshot("Elara"));
            Assert.Empty(journal.Load("Elara", SickChildEvidence.Id).SickChildTurnIns);
            Assert.Null(journal.Load("Elara", SickChildEvidence.Id).CompletedAt);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
