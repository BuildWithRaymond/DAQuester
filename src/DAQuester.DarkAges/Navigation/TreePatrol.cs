using DAQuester.Core;
using System.Text.Json;

namespace DAQuester.DarkAges.Navigation;

public record TreeRegion(string Id, Tile Center, int Radius, string Basis = "image projection; not yet drop-validated");
public static class TreePatrol
{
    public static readonly TreeRegion[] Candidates = [new("west", new(18, 56), 4, "Guide's armor-shop tree area; doorway (18,62), actual collision footprint; drop tiles remain chance-based"), new("central", new(54, 43), 3, "Tutorial Book area; live branches nearby"), new("east", new(64, 44), 3, "Tutorial Book area; live branches nearby")];
    public static TreeRegion[] Load(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        if (doc.RootElement.GetProperty("map").GetInt32() != 500)
            throw new InvalidDataException("Tree candidate profile must describe Mileth map 500.");
        var regions = doc.RootElement.GetProperty("regions").Deserialize<TreeRegion[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        if (regions.Length == 0 || regions.Any(r => r.Radius is < 1 or > 15))
            throw new InvalidDataException("Invalid tree candidate regions.");
        return regions;
    }

    public static Tile[] Build(Grid grid, Tile start, IEnumerable<TreeRegion>? regions = null)
    {
        if (grid.Blocked(start))
            throw new QuestBlockedException("Patrol origin is blocked in client collision data.");
        var reachable = new HashSet<Tile>
        {
            start
        };
        var queue = new Queue<Tile>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var t))
            foreach (var next in Grid.Neighbors(t))
                if (!grid.Blocked(next) && reachable.Add(next))
                    queue.Enqueue(next);
        var result = new List<Tile>();
        var position = start;
        foreach (var region in regions ?? Candidates)
        {
            var available = reachable.Where(t => (t.X - region.Center.X) * (t.X - region.Center.X) + (t.Y - region.Center.Y) * (t.Y - region.Center.Y) <= region.Radius * region.Radius).ToHashSet();
            while (available.Count > 0)
            {
                var nearest = available.OrderBy(t => Math.Abs(t.X - position.X) + Math.Abs(t.Y - position.Y)).ThenBy(t => t.Y).ThenBy(t => t.X).First();
                available.Remove(nearest);
                if (!result.Contains(nearest))
                    result.Add(nearest);
                position = nearest;
            }
        }

        return result.ToArray();
    }
}
