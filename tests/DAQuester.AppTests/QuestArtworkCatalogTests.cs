using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Controls;
using Xunit;

namespace DAQuester.AppTests;

public sealed class QuestArtworkCatalogTests
{
    [Fact]
    public void Approved_game_artwork_covers_existing_quests_and_custom_journeys_use_app_artwork()
    {
        var catalog = new QuestArtworkCatalog(Path.Combine(AppSettings.Content, "quest-artwork.json"));
        // Custom journeys use the app medallion, so they need no installed sprite selection.
        string[] custom = ["awakening", "peasantsfirststeps", "cryptclimb", "peasants-first-steps", "crypt-climb"];
        string[] approvedTaskArtwork = ["wizardy-research"];
        void Check(string id)
        {
            if (custom.Contains(id)) Assert.Null(catalog.Find(id));
            else Assert.NotNull(catalog.Find(id));
        }
        using var quests = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.Content, "catalog.json")));
        foreach (var quest in quests.RootElement.GetProperty("entries").EnumerateArray())
            Check(quest.GetProperty("id").GetString()!);
        foreach (string path in Directory.EnumerateFiles(Path.Combine(AppSettings.Content, "recipes"), "*.json"))
        {
            using var recipe = JsonDocument.Parse(File.ReadAllText(path));
            string id = recipe.RootElement.EnumerateObject().Single(p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase)).Value.GetString()!;
            // Tasks without approved game artwork keep the scroll fallback.
            bool questEntry = recipe.RootElement.TryGetProperty("QuestClasses", out var classes) && classes.GetArrayLength() > 0;
            if (recipe.RootElement.TryGetProperty("IsTask", out var task) && task.GetBoolean() && !questEntry && !approvedTaskArtwork.Contains(id)) Assert.Null(catalog.Find(id));
            else Check(id);
        }
        Assert.Equal(72, catalog.Entries.Count);
        Assert.Null(catalog.Find("unknown-quest"));
        Assert.Equal(70, catalog.Find("emerald-dawn")!.Sprite);
        Assert.Equal(2, catalog.Find("wolfofwestwoodlands")!.Sprite);
        Assert.Equal((byte)58, catalog.Find("yowienterritory")!.Dye);
        Assert.Equal("a", catalog.Find("arsaidhaon")!.Variant);
        Assert.Same(catalog.Find("terroralley"), catalog.Find("terroralleygarden"));
    }
}
