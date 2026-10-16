using Arbiter.Net.Client.Messages;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    async Task Visit(int map, string name, CancellationToken ct, Tile? anchor = null)
    {
        bool faerie = map == ShinewoodFaerieEvidence.Map && name.Equals("Faerie", StringComparison.OrdinalIgnoreCase)
            && anchor == ShinewoodFaerieEvidence.Anchor;
        await Navigate(map, faerie ? ShinewoodFaerieEvidence.Approach : anchor, ct,
            popupGuard: faerie ? TravelPopupVisible : null, visibleNpc: faerie ? null : name);
        if (!faerie && VisibleNpc(map, name))
            return;
        var before = Snapshot;
        bool Matches(Entity e) => faerie
            ? ShinewoodFaerieEvidence.FaerieEntity(e)
            : !e.IsPlayer && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && (anchor is null || Math.Abs(e.Position.X - anchor.X) + Math.Abs(e.Position.Y - anchor.Y) <= 4);
        // The live Faerie is a map reactor, without a drawn creature. Her documented
        // greeting tile is sufficient for speech; the reply supplies its real ID.
        if (faerie && !before.Entities.Values.Any(Matches))
        {
            PreserveTravelPopup();
            if (before.Map != map || before.Position != ShinewoodFaerieEvidence.Approach)
                throw new QuestBlockedException("Verified Faerie approach tile required before greeting.");
            return;
        }

        if (!before.Entities.Values.Any(Matches))
            await WaitAsync(s => s.Map == map && (s.Entities.Values.Any(Matches) || faerie && TravelPopupVisible()),
                before.Revision, TimeSpan.FromSeconds(5), ct);
        if (faerie)
            PreserveTravelPopup();
        before = Snapshot;
        if (before.Map != map || before.Position is null)
            throw new QuestBlockedException("Map changed before NPC approach; inspect doorway pushback.");
        var npc = before.Entities.Values.Single(Matches);
        var blocked = before.Entities.Values.Where(e => e.Blocks).Select(e => e.Position).ToHashSet();
        blocked.UnionWith(maps.Record(map).ExitTiles);
        var goal = NpcApproach.Find(maps.Load(map), before.Position, npc.Position, blocked);
        await WalkTo(goal, ct, map, replanWhen: () => faerie ? TravelPopupVisible() : VisibleNpc(map, name));
        if (faerie)
            PreserveTravelPopup();
        if (Snapshot.Map != map)
            throw new QuestBlockedException("Server moved character away during NPC approach; inspect doorway pushback.");
        if (!faerie && !VisibleNpc(map, name))
            throw new QuestBlockedException("NPC not visible after approach: " + name);
    }

    bool GoalReached(InventoryGoal? goal) => goal is not null && Snapshot.InventoryKnown
        && Snapshot.Count(goal.Item) >= goal.Count;

    // Positive tolerance replaces a blocked or occupied target with the nearest
    // free reachable tile within that distance.
    async Task Navigate(int destination, Tile? target, CancellationToken ct, InventoryGoal? stopWhen = null,
        bool preservePopup = false, Func<bool>? popupGuard = null, int tolerance = 0, string? visibleNpc = null)
    {
        bool NpcReached() => visibleNpc is not null && VisibleNpc(destination, visibleNpc);
        bool PopupVisible() => preservePopup && CollectionPopupVisible() || popupGuard?.Invoke() == true || NpcReached();
        void PreservePopup()
        {
            if (preservePopup)
                PreserveCollectionPopup();
            if (popupGuard?.Invoke() == true)
                PreserveTravelPopup();
        }

        ReconcileNpcTravel();
        bool skipKeane = ReconcileKeaneTravel();
        if (GoalReached(stopWhen))
            return;
        await DismissTravelNotice(ct);
        PreservePopup();
        await CloseDialog(Snapshot.Dialog, ct);
        if (Snapshot.Map is null || Snapshot.Position is null)
            throw new QuestBlockedException("Waiting for server map and position.");
        Tile? worldEntrance = null;
        bool findingWorldRoute = false;
        var visitedWorldHubs = new HashSet<int>();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (GoalReached(stopWhen))
                return;
            await DismissTravelNotice(ct);
            PreservePopup();
            if ((visibleNpc is not null || target is null) && Snapshot.Map == destination && !NpcPositionStable(Snapshot))
                await DepartNpcLanding(ct);
            if (NpcReached())
                return;
            var world = connection.Tracker.WorldMap;
            if (world is not null)
            {
                var observed = Snapshot;
                Arbiter.Net.Server.Types.ServerWorldMapNode node;
                try
                {
                    node = maps.WorldDestination(destination,
                        world.Locations.Where(n => !findingWorldRoute || !visitedWorldHubs.Contains(n.MapId)),
                        observed.Map!.Value, worldEntrance ?? observed.Position!, target, ct);
                }
                // With a tolerance the exact tile may be a prop; reach the map and substitute on arrival.
                catch (QuestBlockedException) when (tolerance > 0 && target is not null)
                {
                    node = maps.WorldDestination(destination,
                        world.Locations.Where(n => !findingWorldRoute || !visitedWorldHubs.Contains(n.MapId)),
                        observed.Map!.Value, worldEntrance ?? observed.Position!, null, ct);
                }

                var old = Snapshot;
                await Send(new ClientFieldMapMessage { MapId = node.MapId, X = node.MapX, Y = node.MapY, Checksum = node.Checksum }, ct);
                await WaitAsync(x => x.Map == node.MapId && x.Position is not null && connection.Tracker.WorldMap is null,
                    old.Revision, TimeSpan.FromSeconds(5), ct);
                visitedWorldHubs.Add(node.MapId);
                worldEntrance = null;
                continue;
            }

            var entry = Snapshot;
            if (entry.Map is null || entry.Position is null)
                entry = await WaitAsync(s => s.Map is not null && s.Position is not null, entry.Revision - 1, TimeSpan.FromSeconds(2), ct);
            if (entry.Map is null || entry.Position is null)
                throw new QuestBlockedException("Waiting for server map and position.");
            int from = entry.Map.Value;
            var obstacles = entry.Entities.Values.Where(e => e.Blocks && e.Id != entry.Id).Select(e => e.Position).ToHashSet();
            var goal = target;
            if (tolerance > 0 && target is not null && from == destination)
            {
                var here = maps.Record(from);
                var standing = new HashSet<Tile>(obstacles);
                standing.UnionWith(here.ExitTiles);
                standing.UnionWith(here.InternalTransfers.Select(t => t.From));
                goal = NpcApproach.Resolve(maps.Load(from), entry.Position, target, standing, tolerance);
            }

            if (entry.Map == destination && (goal is null || entry.Position == goal))
                return;
            TravelPlan? plan = null;
            try
            {
                try
                {
                    plan = maps.Plan(from, entry.Position, destination, goal, obstacles, ct);
                }
                catch (InvalidOperationException) when (obstacles.Count > 0 || tolerance > 0 && from != destination)
                {
                    if (obstacles.Count > 0)
                    {
                        try
                        {
                            plan = maps.Plan(from, entry.Position, destination, goal, ct: ct);
                        }
                        catch (InvalidOperationException) when (tolerance > 0 && from != destination)
                        {
                        }
                    }
                    // Reach the map first; the substitute tile is chosen once there.
                    plan ??= maps.Plan(from, entry.Position, destination, null, ct: ct);
                }
            }
            catch (InvalidOperationException) when (from != destination)
            {
                plan = maps.PlanToWorldMap(from, entry.Position, obstacles, ct);
                findingWorldRoute = true;
                connection.Report((entry.Name ?? "Walker") + ": taking a recorded route to a world-map hub to resolve the onward path.");
            }

            if (Snapshot.Map != from || Snapshot.Position != entry.Position || !ReferenceEquals(Snapshot.Entities, entry.Entities))
                continue;
            if (!skipKeane && from == 503 && plan.Steps.Any(s => s.Map is 502 or 3014))
            {
                if (await TryKeaneTravel(plan, ct))
                    continue;
                skipKeane = true;
                continue;
            }

            var step = plan.Steps.FirstOrDefault();
            if (step?.NpcTransfer is not null)
            {
                await ExecuteNpcTravel(step, ct);
                continue;
            }
            if (step is null)
            {
                await WalkTo(goal!, ct, destination, stopWhen,
                    replanWhen: preservePopup || popupGuard is not null || visibleNpc is not null ? PopupVisible : null);
                PreservePopup();
                return;
            }

            // The walker checks current occupancy before every step. Off-path monster
            // updates must not drain fast movement and restart the entire town route.
            // Reconsider the route when its selected doorway becomes occupied.
            bool BlocksDoor(Entity e) => e.Blocks && e.Id != entry.Id && e.Position == step.From;
            bool Replan() => PopupVisible() || !entry.Entities.Values.Any(BlocksDoor) && Snapshot.Entities.Values.Any(BlocksDoor);
            if (entry.Position == step.From)
            {
                var record = maps.Record(from);
                var triggers = record.ExitTiles.Concat(record.InternalTransfers.Select(t => t.From)).ToHashSet();
                var off = Grid.Neighbors(step.From).FirstOrDefault(p => !maps.Load(from).Blocked(p)
                    && !triggers.Contains(p) && !obstacles.Contains(p))
                    ?? throw new QuestBlockedException("Doorway cannot be re-entered while neighboring tiles are blocked.");
                await WalkTo(off, ct, from, stopWhen, replanWhen: Replan);
                continue;
            }

            if (step.Map == from)
            {
                await WalkTo(step.From, ct, from, stopWhen, step.To, Replan);
                PreservePopup();
                if (GoalReached(stopWhen))
                    return;
                if (Snapshot.Map == from && Snapshot.Position != step.From && Snapshot.Position != step.To)
                    continue;
                await WaitAsync(s => s.Map == from && s.Position == step.To, entry.Revision, TimeSpan.FromSeconds(5), ct);
                continue;
            }

            if (step.WorldMap)
                worldEntrance = step.From;
            await WalkTo(step.From, ct, from, stopWhen, replanWhen: Replan);
            PreservePopup();
            if (GoalReached(stopWhen))
                return;
            if (Snapshot.Map == from && connection.Tracker.WorldMap is null && Snapshot.Position != step.From)
                continue;
            await WaitAsync(x => connection.Tracker.WorldMap is not null || x.Map != from && x.Position is not null,
                Snapshot.Revision - 1, TimeSpan.FromSeconds(5), ct);
        }
    }
}
