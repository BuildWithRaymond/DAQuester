using DAQuester.Core;

namespace DAQuester.DarkAges.Navigation;

public class Grid(int width, int height, Func<int, int, bool> blocked)
{
    public bool Blocked(Tile p) => p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height || blocked(p.X, p.Y);
    public IReadOnlyList<Tile> Path(Tile start, Tile goal, ISet<Tile>? obstacles = null, bool allowBlockedGoal = false)
    {
        bool GoalOverride(Tile p) => allowBlockedGoal && p == goal && p.X >= 0 && p.Y >= 0 && p.X < width && p.Y < height;
        if (Blocked(goal) && !GoalOverride(goal))
            throw new InvalidOperationException("Destination is blocked.");
        var open = new PriorityQueue<Tile, int>();
        var prev = new Dictionary<Tile, Tile>();
        var costs = new Dictionary<Tile, int>
        {
            {
                start,
                0
            }
        };
        open.Enqueue(start, 0);
        while (open.TryDequeue(out var p, out _))
        {
            if (p == goal)
            {
                var result = new List<Tile>();
                while (p != start)
                {
                    result.Add(p);
                    p = prev[p];
                }

                result.Reverse();
                return result;
            }

            foreach (var q in Neighbors(p))
            {
                if ((Blocked(q) && !GoalOverride(q)) || obstacles?.Contains(q) == true)
                    continue;
                int cost = costs[p] + 1;
                if (costs.TryGetValue(q, out var old) && old <= cost)
                    continue;
                costs[q] = cost;
                prev[q] = p;
                open.Enqueue(q, cost + Math.Abs(goal.X - q.X) + Math.Abs(goal.Y - q.Y));
            }
        }

        throw new InvalidOperationException("No walkable route to destination.");
    }

    public static IEnumerable<Tile> Neighbors(Tile p)
    {
        yield return new(p.X, p.Y - 1);
        yield return new(p.X + 1, p.Y);
        yield return new(p.X, p.Y + 1);
        yield return new(p.X - 1, p.Y);
    }

    internal int[] Distances(Tile start, IEnumerable<Tile> portals, IEnumerable<Tile> transfers, ISet<Tile>? obstacles, CancellationToken ct)
    {
        bool InBounds(Tile p) => p.X >= 0 && p.Y >= 0 && p.X < width && p.Y < height;
        if (!InBounds(start))
            throw new InvalidOperationException("Arrival tile is outside the recorded map.");
        int size = checked(width * height);
        var distances = new int[size];
        Array.Fill(distances, -1);
        var endpoints = new bool[size];
        var portalOverrides = new bool[size];
        var occupied = new bool[size];
        foreach (var p in portals.Where(InBounds))
        {
            endpoints[p.Y * width + p.X] = true;
            portalOverrides[p.Y * width + p.X] = true;
        }

        foreach (var p in transfers.Where(InBounds))
            endpoints[p.Y * width + p.X] = true;
        if (obstacles is not null)
            foreach (var p in obstacles.Where(InBounds))
                occupied[p.Y * width + p.X] = true;
        var queue = new int[size];
        int head = 0, tail = 0;
        int origin = start.Y * width + start.X;
        queue[tail++] = origin;
        distances[origin] = 0;
        while (head < tail)
        {
            if ((head & 1023) == 0)
                ct.ThrowIfCancellationRequested();
            int index = queue[head++], x = index % width, y = index / width;
            void Visit(int nx, int ny)
            {
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    return;
                int next = ny * width + nx;
                if (distances[next] >= 0 || occupied[next] || blocked(nx, ny) && !portalOverrides[next])
                    return;
                distances[next] = distances[index] + 1;
                if (!endpoints[next])
                    queue[tail++] = next; // Walking onto a trigger must not continue through it.
            }

            Visit(x, y - 1);
            Visit(x + 1, y);
            Visit(x, y + 1);
            Visit(x - 1, y);
        }

        return distances;
    }
}
