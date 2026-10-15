using System.Text.Json;
using Xunit;

namespace DAQuester.Tests;

public class WalkabilityCoverageTests
{
    [Fact]
    public void Every_recorded_map_has_collision_metadata_or_an_explicit_missing_entry()
    {
        string content = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content"));
        using var routes = JsonDocument.Parse(File.ReadAllText(Path.Combine(content, "routes.json")));
        using var asset = JsonDocument.Parse(File.ReadAllText(Path.Combine(content, "maps/datester-7.41.walkability.json")));
        var grids = asset.RootElement.GetProperty("maps").EnumerateArray().ToDictionary(m => m.GetProperty("id").GetInt32());
        var missing = asset.RootElement.GetProperty("missingMapIds").EnumerateArray().Select(m => m.GetInt32()).ToHashSet();
        var recorded = routes.RootElement.EnumerateArray().ToArray();
        Assert.Empty(recorded.Where(r => !grids.ContainsKey(r.GetProperty("id").GetInt32()) &&
            !missing.Contains(r.GetProperty("id").GetInt32())).Select(r => r.GetProperty("id").GetInt32()));
        foreach (var route in recorded.Where(r => grids.ContainsKey(r.GetProperty("id").GetInt32())))
        {
            var grid = grids[route.GetProperty("id").GetInt32()];
            Assert.Equal(route.GetProperty("width").GetInt32(), grid.GetProperty("width").GetInt32());
            Assert.Equal(route.GetProperty("height").GetInt32(), grid.GetProperty("height").GetInt32());
        }
        Assert.Equal(recorded.Length, grids.Count + missing.Count);
    }
}
