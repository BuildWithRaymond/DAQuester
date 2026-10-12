using System.Text.Json;
using Xunit;

namespace DAQuester.Tests;

public class LegendCatalogTests
{
    [Fact]
    public void Muisir_requires_level_one_and_registration()
    {
        using var doc = Catalog();
        var quest = doc.RootElement.GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetString() == "muisirvillageentrance");
        Assert.Equal("1+", quest.GetProperty("level").GetString());
        Assert.Contains("registered", quest.GetProperty("requiredItems").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    static JsonDocument Catalog() => JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/catalog.json"))));
    [Fact]
    public void Catalog_contains_sourced_legend_rewards_and_explicitly_requested_pet_exception()
    {
        using var doc = Catalog();
        var entries = doc.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.DoesNotContain(entries, e => e.GetProperty("id").GetString() == "rionasgreeting");
        Assert.DoesNotContain(entries, e => e.GetProperty("id").GetString() == "harvestgrapes");
        Assert.All(entries, e =>
        {
            if (e.GetProperty("id").GetString() == "shinewoodfaerie")
            {
                Assert.False(e.GetProperty("givesLegend").GetBoolean());
                Assert.Equal("requested-pet-and-spells", e.GetProperty("catalogException").GetString());
            }
            else
            {
                Assert.True(e.GetProperty("givesLegend").GetBoolean());
                Assert.NotEmpty(e.GetProperty("legendEvidence").GetString()!);
            }
        });
        foreach (var id in new[]
        {
            "spareastick",
            "mundanebirth",
            "perfecthairstyle",
            "altarworship",
            "amanjungle",
            "defeatedlaw"
        }

        )
            Assert.Contains(entries, e => e.GetProperty("id").GetString() == id);
        Assert.Contains("Defeated Law", entries.Single(e => e.GetProperty("id").GetString() == "defeatedlaw").GetProperty("rewards").GetString());
    }
}
