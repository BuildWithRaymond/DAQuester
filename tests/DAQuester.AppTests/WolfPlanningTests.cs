using System.Collections.Immutable;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class WolfPlanningTests
{
    const string Id = "wolf-of-west-woodlands";
    const string CatalogId = "wolfofwestwoodlands";
    const string Legend = "Hunted down a wolf of West Woodland";

    static CharacterSnapshot Ready() => new()
    {
        Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 5,
        Level = 70, Hp = 100, MaxHp = 100, Gold = 100000, Experience = 1000,
        Weight = 1, MaxWeight = 127, InventoryKnown = true,
        LegendsKnown = true, LegendsRevision = 5
    };

    static DesktopRuntime Workspace()
    {
        var runtime = DashboardTests.Workspace();
        // Distinct fixture name proves that the catalog mapping uses its stable ID.
        ((Dictionary<string, QuestDefinition>)runtime.Recipes)[Id] = new(Id, 1,
            "Wolf quest integration fixture", "Synthetic integration fixture", [],
            [new("preflight", "wolf-preflight", "accept"), new("accept", "wolf-accept", "enter"),
             new("enter", "wolf-enter", "hunt"), new("hunt", "wolf-hunt", "return"),
             new("return", "wolf-return", "confirm"), new("confirm", "wolf-confirm", "complete")])
        { Repeatable = false };
        runtime.SetPreviewSnapshot(Ready());
        return runtime;
    }

    [Fact]
    public async Task Library_maps_wolf_by_catalog_id_and_describes_observed_reward()
    {
        var runtime = Workspace();
        try
        {
            var vm = new MainWindowViewModel(runtime) { Filter = "All" };
            var wolf = Assert.Single(vm.Quests, q => q.Id == CatalogId);
            Assert.Equal(Id, wolf.RecipeId);
            Assert.False(wolf.IsGuideOnly);
            Assert.Contains("Throttler", wolf.Description);
            Assert.Contains("Vanilla Potion", wolf.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Vanilla Potion", wolf.Rewards);
            Assert.DoesNotContain("Hydele deum", wolf.Rewards);
            Assert.Contains("60,000", wolf.Rewards);
            Assert.DoesNotContain("100,000", wolf.Rewards);
            Assert.False(DesktopRuntime.HasChoices(Id));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Wolf_uses_own_level_seventy_gate_instead_of_stick_gate()
    {
        var runtime = Workspace();
        try
        {
            Assert.Equal("Ready", runtime.Availability("Elara", Id).Status);
            Assert.True(runtime.Availability("Elara", Id).CanQueue);
            runtime.SetPreviewSnapshot(Ready() with { Level = 69 });
            var blocked = runtime.Availability("Elara", Id);
            Assert.Equal("Blocked", blocked.Status);
            Assert.Contains("70+", blocked.Reason);
            Assert.Contains("guide", blocked.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.False(blocked.CanQueue);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Own_exact_wolf_mark_prevents_another_claim()
    {
        var runtime = Workspace();
        try
        {
            runtime.SetPreviewSnapshot(Ready() with { Legends = ImmutableArray.Create(Legend) });
            var completed = runtime.Availability("Elara", Id);
            Assert.Equal("Completed", completed.Status);
            Assert.False(completed.CanQueue);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Similar_wolf_text_does_not_block_new_quest()
    {
        var runtime = Workspace();
        try
        {
            runtime.SetPreviewSnapshot(Ready() with { Legends = ImmutableArray.Create("Never " + Legend, Legend + " apprentice", Legend + " - Deoch 223, Autumn") });
            Assert.Equal("Ready", runtime.Availability("Elara", Id).Status);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
