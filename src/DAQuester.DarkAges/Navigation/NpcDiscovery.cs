using DAQuester.Core;

namespace DAQuester.DarkAges.Navigation;

public static class NpcDiscovery
{
    // Authored search coverage, not an assertion that an unseen NPC is absent.
    const int Radius = 6;

    public static Tile[] Sweep(Grid grid, Tile start, ISet<Tile> excluded)
    {
        var remaining = Distances(grid, start, excluded).Keys.ToHashSet();
        var points = new List<Tile>();
        var current = start;
        while (remaining.Count > 0)
        {
            remaining.RemoveWhere(t => Math.Max(Math.Abs(t.X - current.X), Math.Abs(t.Y - current.Y)) <= Radius);
            if (remaining.Count == 0)
                break;
            var distance = Distances(grid, current, excluded);
            current = remaining.OrderBy(t => distance[t]).ThenBy(t => t.Y).ThenBy(t => t.X).First();
            points.Add(current);
        }
        return points.ToArray();
    }

    static Dictionary<Tile, int> Distances(Grid grid, Tile start, ISet<Tile> excluded)
    {
        var distance = new Dictionary<Tile, int> { [start] = 0 };
        var queue = new Queue<Tile>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var tile))
            foreach (var next in Grid.Neighbors(tile))
            {
                if (grid.Blocked(next) || excluded.Contains(next) || distance.ContainsKey(next))
                    continue;
                distance[next] = distance[tile] + 1;
                queue.Enqueue(next);
            }
        return distance;
    }
}
