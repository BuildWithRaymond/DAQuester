using System.Collections.Immutable;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class HuntingPresentationTests
{
    static DesktopRuntime Runtime()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara", "Wren"];
        settings.ClientPath = "";
        settings.Save();
        var runtime = new DesktopRuntime(preview: true);
        runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 2, Level = 5, ToNextLevel = 1234, Map = 1, MapName = "Crypt 1-1", Hp = 100, MaxHp = 100 });
        runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [NewCharacterPath.FirstStepsId], State = "Running", ActiveQuest = NewCharacterPath.FirstStepsId });
        runtime.SetPreviewRunning("Elara", true);
        return runtime;
    }

    [Fact]
    public async Task Starter_journey_shows_current_level_final_goal_and_observed_next_level_xp()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            Assert.Equal("Level 5 → 7 · 1,234 XP to level 6", vm.JourneyLevelSummary);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Level = 7, ToNextLevel = 16000 });
            Assert.Equal("Level 7 → 7 · goal reached", vm.JourneyLevelSummary);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Connected = false });
            Assert.Contains("Last observed", vm.JourneyLevelSummary);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Activity_follows_actual_target_and_never_leaks_across_pause_generation_or_character()
    {
        var runtime = Runtime();
        try
        {
            var target = new Entity(55, "", new(6, 5), true) { Sprite = 53, Kind = 0 };
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Entities = ImmutableDictionary<uint, Entity>.Empty.Add(target.Id, target) });
            var vm = new MainWindowViewModel(runtime);
            runtime.RecordHuntingActivity("Elara", NewCharacterPath.FirstStepsId, 1, new("Attacking", target));
            Assert.Equal("Attacking Spider · Crypt 1-1", vm.JourneyMessage);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Map = 421, MapName = "Rucesion Armor Shop" });
            Assert.DoesNotContain("Attacking", vm.JourneyMessage);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Map = 1, MapName = "Crypt 1-1" });
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Entities = ImmutableDictionary<uint, Entity>.Empty });
            Assert.DoesNotContain("Attacking", vm.JourneyMessage);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Entities = ImmutableDictionary<uint, Entity>.Empty.Add(target.Id, target), Generation = 2 });
            Assert.DoesNotContain("Attacking", vm.JourneyMessage);
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Generation = 1 });
            runtime.SetPreviewRunning("Elara", false);
            Assert.DoesNotContain("Attacking", vm.JourneyMessage);
            runtime.SetPreviewRunning("Elara", true);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.DoesNotContain("Attacking", vm.JourneyMessage);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
