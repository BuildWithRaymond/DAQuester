using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;

namespace DAQuester.Tests;

public class VisitedMapTests
{
    [Fact]
    public void Observed_dimensions_preserve_existing_routes_and_cannot_invent_connectivity()
    {
        string folder = Path.Combine(Path.GetTempPath(), "DAQuester-visited-map-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        string routeFile = Path.Combine(folder, "routes.json");
        var known = new MapRecord(500, "Synthetic known map", 5, 5, [new(new(1, 0), 501, new(2, 3))]);
        File.WriteAllText(routeFile, JsonSerializer.Serialize(new[] { known }));
        var repository = new MapRepository(folder, routeFile);
        repository.RecordVisited(new(500, "Synthetic observed name", 8, 9, []));
        Assert.Equal(5, repository.Record(500).Width);
        Assert.Single(repository.Record(500).Portals);
        repository.RecordVisited(new(999, "Synthetic newly visited", 8, 9, [new(new(1, 0), 500, new(1, 1))])
        {
            NpcTransfers = [new(new(2, 2), 500, new(1, 1), "Synthetic NPC", "Synthetic transfer", [])],
            InternalTransfers = [new(new(2, 2), new(3, 3))]
        });
        var observed = repository.Record(999);
        Assert.Equal(8, observed.Width);
        Assert.Equal(9, observed.Height);
        Assert.Empty(observed.Portals);
        Assert.Empty(observed.NpcTransfers);
        Assert.Empty(observed.InternalTransfers);
        Assert.Throws<InvalidOperationException>(() => repository.Route(999, 500));
        Assert.Single(JsonSerializer.Deserialize<MapRecord[]>(File.ReadAllText(routeFile))!);
    }
}
