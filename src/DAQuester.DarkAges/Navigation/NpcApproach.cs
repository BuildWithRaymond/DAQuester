using DAQuester.Core;

namespace DAQuester.DarkAges.Navigation;

public static class NpcApproach
{
    public static Tile Find(Grid grid, Tile start, Tile npc, ISet<Tile> obstacles, int range = 3)
    {
        var distance = Reachable(grid, start, obstacles);
        return distance.Keys.Where(t => t != npc && Manhattan(t, npc) <= range).OrderBy(t => distance[t]).ThenBy(t => Manhattan(t, npc)).FirstOrDefault() ?? throw new QuestBlockedException("NPC has no reachable interaction tile within " + range + " tiles.");
    }

    // The wanted tile when it is free and reachable; otherwise the free reachable tile nearest to it within tolerance.
    public static Tile Resolve(Grid grid, Tile start, Tile wanted, ISet<Tile> obstacles, int tolerance)
    {
        var distance = Reachable(grid, start, obstacles);
        if (distance.ContainsKey(wanted) && !obstacles.Contains(wanted))
            return wanted;
        return distance.Keys.Where(t => Manhattan(t, wanted) <= tolerance).OrderBy(t => Manhattan(t, wanted)).ThenBy(t => distance[t]).FirstOrDefault() ?? throw new QuestBlockedException($"No free tile within {tolerance} of ({wanted.X},{wanted.Y}) can be reached.");
    }

    static int Manhattan(Tile a, Tile b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    static Dictionary<Tile, int> Reachable(Grid grid, Tile start, ISet<Tile> obstacles)
    {
        var distance = new Dictionary<Tile, int>
        {
            {
                start,
                0
            }
        };
        var queue = new Queue<Tile>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var tile))
            foreach (var next in Grid.Neighbors(tile))
            {
                if (grid.Blocked(next) || obstacles.Contains(next) || distance.ContainsKey(next))
                    continue;
                distance[next] = distance[tile] + 1;
                queue.Enqueue(next);
            }

        return distance;
    }
}
