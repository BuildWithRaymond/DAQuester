using System.Collections.Immutable;
using DAQuester.App.Services;
using DAQuester.App;
using DAQuester.App.ViewModels;
using DAQuester.App.Controls;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class FiskDanaanAppTests
{
    static CharacterSnapshot Ready() => new()
    {
        Name = "Elara",
        Id = 1,
        Connected = true,
        Generation = 1,
        Revision = 5,
        Level = 99,
        Hp = 100,
        Gold = 100000,
        Experience = 1000,
        Weight = 1,
        MaxWeight = 127,
        InventoryKnown = true,
        LegendsKnown = true,
        LegendsRevision = 5
    };
    static async Task SaveWait()
    {
        var at = DateTimeOffset.UtcNow;
        var d = new Dialog(1, 4, 55, 1, "Viveka", FiskEvidence.BouquetAccepted, true, 0, 1, 0, []) { ObservedAt = at };
        await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(new()
        {
            Character = "Elara",
            Quest = FiskEvidence.Id,
            Step = "run",
            Status = "Deferred",
            NextRun = at.AddHours(1),
            FiskWait = new(at, d, 50000, 30000, true)
        });
    }
    [Fact]
    public async Task Saved_paid_wait_never_makes_a_dead_character_ready()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            await SaveWait();
            runtime.SetPreviewSnapshot(Ready() with
            {
                Hp = 0
            });
            Assert.False(runtime.Availability("Elara", FiskEvidence.Id).Ready);
            Assert.Equal("Blocked", runtime.Availability("Elara", FiskEvidence.Id).Status);
        }
        finally { await runtime.ShutdownAsync(); }
    }
    [Fact]
    public async Task Resuming_paid_wait_does_not_require_later_unready_quests()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            await SaveWait();
            runtime.SetPreviewSnapshot(Ready());
            var queue = new QuestQueueState
            {
                Character = "Elara",
                State = "Paused",
                Quests = [FiskEvidence.Id, "spare-a-stick"],
                Deferred = [new(FiskEvidence.Id, DateTimeOffset.UtcNow.AddHours(1))]
            };
            Assert.Equal("Blocked", runtime.Availability("Elara", "spare-a-stick").Status);
            Assert.Equal("", runtime.QueueStartReason("Elara", queue));
        }
        finally { await runtime.ShutdownAsync(); }
    }
    [Fact]
    public async Task Waiting_journey_explains_the_paid_continuation_instead_of_a_generic_restored_message()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            await SaveWait();
            runtime.SetPreviewSnapshot(Ready());
            runtime.SetPreviewQueue(new()
            {
                Character = "Elara",
                State = "Running",
                Quests = [FiskEvidence.Id],
                Deferred = [new(FiskEvidence.Id, DateTimeOffset.UtcNow.AddHours(1))]
            });
            runtime.SetPreviewQueueRunning("Elara", true);
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
            Assert.Equal("Bouquet waiting", vm.JourneyState);
            Assert.Contains("paid", vm.JourneyMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Restored", vm.JourneyMessage);
        }
        finally { await runtime.ShutdownAsync(); }
    }
    [Fact]
    public async Task Cap_and_band_library_entries_share_one_automatic_family()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var look = CharacterPreviewAppearance.From(null).Look;
            runtime.SetPreviewSnapshot(Ready() with
            {
                Appearance = look with
                {
                    Body = 0x10
                }
            });
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            Assert.Equal(DanaanEvidence.Id, vm.Quests.Single(q => q.Id == "capofdanaan").RecipeId);
            runtime.SetPreviewSnapshot(Ready() with
            {
                Appearance = look with
                {
                    Body = 0x20
                }
            });
            Assert.Equal(DanaanEvidence.Id, vm.Quests.Single(q => q.Id == "bandofdanaan").RecipeId);
            Assert.True(DesktopRuntime.HasChoices(DanaanEvidence.Id));
        }
        finally { await runtime.ShutdownAsync(); }
    }
    [Fact]
    public async Task Fisk_and_family_check_own_character_requirements_instead_of_stick_level_gate()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(new()
            {
                Name = "Elara",
                Id = 1,
                Connected = true,
                Generation = 1,
                Revision = 5,
                Level = 99,
                Hp = 100,
                MaxHp = 100,
                Gold = 100000,
                Experience = 1000,
                Weight = 1,
                MaxWeight = 127,
                InventoryKnown = true,
                LegendsKnown = true,
                LegendsRevision = 5
            });
            Assert.True(runtime.Availability("Elara", FiskEvidence.Id).Ready);
            Assert.Equal("Choices", runtime.Availability("Elara", DanaanEvidence.Id).Status);
        }
        finally { await runtime.ShutdownAsync(); }
    }
    [Theory]
    [InlineData(FiskEvidence.Id, "Kept Fisk's Secret - Deoch 223, Summer")]
    [InlineData(DanaanEvidence.Id, "Worthy of the Cap of Danaan, Deoch 223, Summer")]
    public async Task Existing_own_completion_is_unqueueable_without_new_claim(string id, string mark)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(new()
            {
                Name = "Elara",
                Id = 1,
                Connected = true,
                Generation = 1,
                Revision = 5,
                Level = 99,
                Hp = 100,
                LegendsKnown = true,
                LegendsRevision = 5,
                Legends = ImmutableArray.Create(mark)
            });
            var availability = runtime.Availability("Elara", id);
            Assert.Equal("Completed", availability.Status);
            Assert.False(availability.CanQueue);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
