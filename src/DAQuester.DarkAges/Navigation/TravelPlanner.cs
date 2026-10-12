using DAQuester.Core;
using Arbiter.Net.Server.Types;

namespace DAQuester.DarkAges.Navigation;

public record TravelStep(int SourceMap, Tile From, int Map, Tile To, bool WorldMap = false);
public record TravelPlan(IReadOnlyList<TravelStep> Steps, int Cost);
public partial class MapRepository
{
    readonly Dictionary<(int Map, Tile Origin), int[]> distanceFields = [];
    readonly Queue<(int Map, Tile Origin)> fieldOrder = [];
    int cachedCells;
    const int MaxCachedCells = 4 * 1024 * 1024; // At most 16 MiB of distance arrays per character.
    readonly Dictionary<int, HashSet<int>> destinationReachability = [];
    readonly record struct RouteNode(int Map, Tile Position, bool Goal = false);
    HashSet<int> MapsReaching(int destination)
    {
        if (destinationReachability.TryGetValue(destination, out var saved))
            return saved;
        var reverse = records.Values.SelectMany(r => r.Portals.Select(p => (To: p.Map, From: r.Id))).ToLookup(p => p.To, p => p.From);
        var result = new HashSet<int>
        {
            destination
        };
        var pending = new Queue<int>();
        pending.Enqueue(destination);
        while (pending.TryDequeue(out int map))
            foreach (int parent in reverse[map])
                if (result.Add(parent))
                    pending.Enqueue(parent);
        if (destinationReachability.Count >= 16)
            destinationReachability.Clear();
        destinationReachability[destination] = result;
        return result;
    }

    int[] WalkingDistances(int map, Tile origin, ISet<Tile>? obstacles, CancellationToken ct)
    {
        bool reusable = obstacles is null || obstacles.Count == 0;
        var key = (map, origin);
        if (reusable && distanceFields.TryGetValue(key, out var saved))
            return saved;
        var r = Record(map);
        var field = Load(map).Distances(origin, r.ExitTiles, r.InternalTransfers.Select(t => t.From), obstacles, ct);
        if (reusable)
        {
            while (distanceFields.Count > 0 && (distanceFields.Count >= 2048 || cachedCells + field.Length > MaxCachedCells))
            {
                var expired = fieldOrder.Dequeue();
                cachedCells -= distanceFields[expired].Length;
                distanceFields.Remove(expired);
            }

            if (field.Length <= MaxCachedCells)
            {
                distanceFields[key] = field;
                fieldOrder.Enqueue(key);
                cachedCells += field.Length;
            }
        }

        return field;
    }

    static int Distance(MapRecord r, int[] field, Tile target) => target.X < 0 || target.Y < 0 || target.X >= r.Width || target.Y >= r.Height ? -1 : field[target.Y * r.Width + target.X];
    int TriggerDistance(MapRecord r, int[] field, Tile origin, Tile portal, ISet<Tile>? obstacles)
    {
        int distance = Distance(r, field, portal);
        if (distance != 0)
            return distance;
        // Standing on a doorway does not re-enter it. Leave and step back onto it.
        var triggers = r.ExitTiles.Concat(r.InternalTransfers.Select(t => t.From)).ToHashSet();
        return Grid.Neighbors(origin).Any(p => !Load(r.Id).Blocked(p) && !triggers.Contains(p) && obstacles?.Contains(p) != true) ? 2 : -1;
    }

    public TravelPlan Plan(int from, Tile origin, int destination, Tile? target = null, ISet<Tile>? obstacles = null, CancellationToken ct = default)
    {
        var reachable = MapsReaching(destination);
        var start = new RouteNode(from, origin);
        var costs = new Dictionary<RouteNode, (int Actions, int Changes)>
        {
            {
                start,
                (0, 0)
            }
        };
        var parents = new Dictionary<RouteNode, (RouteNode Before, TravelStep? Via)>();
        var open = new PriorityQueue<RouteNode, (int Actions, int Changes)>();
        open.Enqueue(start, (0, 0));
        HashSet<int> unavailableCollision = [];
        void Offer(RouteNode node, RouteNode before, TravelStep? via, (int Actions, int Changes) cost)
        {
            if (costs.TryGetValue(node, out var old) && Comparer<(int, int)>.Default.Compare(cost, old) >= 0)
                return;
            costs[node] = cost;
            parents[node] = (before, via);
            open.Enqueue(node, cost);
        }

        while (open.TryDequeue(out var node, out var cost))
        {
            ct.ThrowIfCancellationRequested();
            if (costs[node] != cost)
                continue;
            if (node.Goal || node.Map == destination && target is null)
            {
                var steps = new List<TravelStep>();
                var n = node;
                while (n != start)
                {
                    var parent = parents[n];
                    if (parent.Via is { } step)
                        steps.Add(step);
                    n = parent.Before;
                }

                steps.Reverse();
                return new(steps, cost.Actions);
            }

            if (!reachable.Contains(node.Map) || !records.TryGetValue(node.Map, out var r))
                continue;
            var blocks = node.Map == from ? obstacles : null;
            int[] field;
            try
            {
                field = WalkingDistances(node.Map, node.Position, blocks, ct);
            }
            catch (Exception e) when (node != start && e is FileNotFoundException or InvalidDataException or InvalidOperationException)
            {
                unavailableCollision.Add(node.Map);
                continue;
            }

            if (node.Map == destination && target is not null && Distance(r, field, target) is >= 0 and var tail)
                Offer(new(destination, target, true), node, null, (cost.Actions + tail, cost.Changes));
            foreach (var p in r.Portals.Where(p => reachable.Contains(p.Map) && (node.Map != destination || p.Map == destination)))
            {
                int walk = TriggerDistance(r, field, node.Position, p.From, blocks);
                if (walk < 0)
                    continue;
                Offer(new(p.Map, p.To), node, new(node.Map, p.From, p.Map, p.To, p.WorldMap), (cost.Actions + walk + (p.WorldMap ? 1 : 0), cost.Changes + 1));
            }

            foreach (var t in r.InternalTransfers)
            {
                int walk = TriggerDistance(r, field, node.Position, t.From, blocks);
                if (walk < 0)
                    continue;
                Offer(new(node.Map, t.To), node, new(node.Map, t.From, node.Map, t.To), (cost.Actions + walk, cost.Changes + 1));
            }
        }

        string unavailable = unavailableCollision.Count == 0 ? "" : " Collision data could not be loaded for: " + string.Join(", ", unavailableCollision.Order().Select(MapLabel)) + ".";
        throw new InvalidOperationException("No walkable recorded route from " + from + " to " + destination + ". Check map data or blocked exits." + unavailable);
    }

    public ServerWorldMapNode WorldDestination(int destination, IEnumerable<ServerWorldMapNode> available, int sourceMap, Tile entrance, Tile? target = null, CancellationToken ct = default)
    {
        var nodes = available.ToArray();
        var exits = Record(sourceMap).Portals.Where(p => p.WorldMap && p.From == entrance).ToArray();
        if (exits.Length == 0)
            exits = Record(sourceMap).Portals.Where(p => p.WorldMap && Math.Abs(p.From.X - entrance.X) + Math.Abs(p.From.Y - entrance.Y) <= 1).ToArray();
        var candidates = new List<(ServerWorldMapNode Node, int Cost, int Changes)>();
        foreach (var node in nodes)
            foreach (var exit in exits.Where(p => p.Map == node.MapId))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var plan = Plan(exit.Map, exit.To, destination, target, ct: ct);
                    candidates.Add((node, plan.Cost, plan.Steps.Count));
                }
                catch (Exception e) when (e is FileNotFoundException or InvalidDataException or InvalidOperationException)
                {
                }
            }

        var best = candidates.OrderBy(c => c.Cost).ThenBy(c => c.Changes).ThenBy(c => c.Node.MapId).Select(c => c.Node).FirstOrDefault();
        if (best is not null)
            return best;
        if (exits.Any(e => nodes.Any(n => n.MapId == e.Map)))
            throw new QuestBlockedException("World-map destination has no walkable recorded route. Check map data or blocked exits.");
        return WorldDestination(destination, nodes); // Already-open field without a recorded entrance/landing.
    }
}
