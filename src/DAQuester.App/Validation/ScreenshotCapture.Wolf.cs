using Avalonia.Controls;
using DAQuester.App.ViewModels;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    // Optional manual review of the real planner with synthetic state only.
    static async Task CaptureWolfAsync(Window window, MainWindowViewModel vm)
    {
        var runtime = vm.Runtime;
        if (!runtime.Preview)
            throw new InvalidOperationException("Wolf capture requires isolated preview data.");
        runtime.SetPreviewSnapshot(new()
        {
            Name = "Elara",
            Id = 43,
            Connected = true,
            Generation = 2,
            Revision = 30,
            Level = 70,
            Class = "Warrior",
            Hp = 1000,
            MaxHp = 1000,
            Gold = 50000,
            Experience = 100000,
            Weight = 1,
            MaxWeight = 100,
            InventoryKnown = true,
            LegendsKnown = true,
            LegendsRevision = 30,
            Map = WolfQuestEvidence.NpcMap,
            MapName = "West Woodland 12-1",
            Position = new(20, 20)
        });
        runtime.SetPreviewQueue(new() { Character = "Elara" });
        vm.Refresh();
        vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
        vm.Page = "Planner";
        vm.Filter = "All";
        vm.Search = "Wolf";
        vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == WolfQuestEvidence.Id);
        window.FindControl<Expander>("questDetails")!.IsExpanded = true;
        window.FindControl<ScrollViewer>("questDetailsScroll")!.Offset = default;

        var renders = new List<string>();
        window.Width = 1450;
        window.Height = 930;
        await CaptureJourneyAsync(window, "wolf", renders);
        window.Width = 900;
        window.Height = 560;
        await CaptureJourneyAsync(window, "wolf-compact", renders);
    }
}
