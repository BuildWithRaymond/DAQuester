using System.Collections.Immutable;
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
}
