using DAQuester.App;
using DAQuester.App.Controls;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class DanaanLibraryTests
{
    static CharacterSnapshot Character(string name, byte? body) => new()
    {
        Name = name,
        Id = 1,
        Connected = true,
        Generation = 1,
        Revision = 5,
        Level = 99,
        Hp = 100,
        InventoryKnown = true,
        LegendsKnown = true,
        LegendsRevision = 5,
        Appearance = body is { } known ? CharacterPreviewAppearance.From(null).Look with
        {
            Body = known
        } : null
    };

    [Theory]
    [InlineData(0x10, "capofdanaan")]
    [InlineData(0x20, "bandofdanaan")]
    [InlineData(0x30, "capofdanaan")]
    [InlineData(0x40, "bandofdanaan")]
    [InlineData(0x50, "capofdanaan")]
    [InlineData(0x60, "bandofdanaan")]
    [InlineData(0x70, "capofdanaan")]
    [InlineData(0x80, "capofdanaan")]
    [InlineData(0x90, "bandofdanaan")]
    [InlineData(0xA0, "capofdanaan")]
    [InlineData(0xB0, "bandofdanaan")]
    public async Task Known_character_sees_only_matching_Danaan_reward(byte body, string expected)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(Character("Elara", body));
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            var reward = Assert.Single(vm.Quests, q => q.RecipeId == DanaanEvidence.Id);
            Assert.Equal(expected, reward.Id);
            Assert.Equal(vm.Quests.Count, vm.AllCount);
            vm.LegendMarksOnly = true;
            Assert.Equal(expected, Assert.Single(vm.Quests, q => q.RecipeId == DanaanEvidence.Id).Id);
            Assert.Equal(vm.Quests.Count, vm.AllCount);
            vm.Filter = "Available";
            Assert.Equal(expected, Assert.Single(vm.Quests, q => q.RecipeId == DanaanEvidence.Id).Id);
            Assert.Equal(vm.Quests.Count, vm.AvailableCount);
            runtime.SetPreviewSnapshot(Character("Elara", body) with
            {
                Legends = ["Worthy of the Cap of Danaan, Deoch 223, Summer"]
            });
            vm.Filter = "Completed";
            Assert.Equal(expected, Assert.Single(vm.Quests, q => q.RecipeId == DanaanEvidence.Id).Id);
            Assert.Equal(vm.Quests.Count, vm.CompletedCount);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData((byte)0)]
    [InlineData((byte)0xC0)]
    public async Task Unknown_character_sex_hides_both_rewards(byte? body)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(Character("Elara", body));
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            Assert.DoesNotContain(vm.Quests, q => q.RecipeId == DanaanEvidence.Id);
            Assert.Equal(vm.Quests.Count, vm.AllCount);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.DoesNotContain(vm.Quests, q => q.RecipeId == DanaanEvidence.Id);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData(0x10, "capofdanaan", 0x20, "bandofdanaan")]
    [InlineData(0x20, "bandofdanaan", 0x10, "capofdanaan")]
    public async Task Saved_appearance_filters_offline_until_own_live_appearance_arrives(byte savedBody, string savedReward, byte liveBody, string liveReward)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            await new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), runtime.Settings.ServerProfile)
                .ObserveAsync("Elara", Character("Elara", savedBody));
            runtime.SetPreviewSnapshot(Character("Elara", liveBody) with
            {
                Connected = false
            });
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            Assert.Equal(savedReward, Assert.Single(vm.Quests, q => q.RecipeId == DanaanEvidence.Id).Id);
            runtime.SetPreviewSnapshot(Character("Elara", null));
            Assert.Equal(savedReward, Assert.Single(vm.Quests, q => q.RecipeId == DanaanEvidence.Id).Id);
            runtime.SetPreviewSnapshot(Character("Elara", liveBody));
            Assert.Equal(liveReward, Assert.Single(vm.Quests, q => q.RecipeId == DanaanEvidence.Id).Id);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Character_switch_updates_Danaan_details_and_preserves_each_queue()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(Character("Elara", 0x10));
            runtime.SetPreviewSnapshot(Character("Wren", 0x20));
            runtime.SetPreviewQueue(new()
            {
                Character = "Elara",
                Quests = [DanaanEvidence.Id]
            });
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            vm.SelectedQuest = vm.Quests.Single(q => q.Id == "capofdanaan");
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.Equal("bandofdanaan", vm.SelectedQuest?.Id);
            Assert.Equal([DanaanEvidence.Id], runtime.Queue("Elara").Quests);
            Assert.Empty(runtime.Queue("Wren").Quests);
            runtime.SetPreviewSnapshot(Character("Wren", null));
            Assert.Null(vm.SelectedQuest);
            Assert.DoesNotContain(vm.Quests, q => q.RecipeId == DanaanEvidence.Id);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
