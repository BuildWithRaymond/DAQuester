namespace DAQuester.Core;

static partial class ObservedJourney
{
    static async Task GatherAsync(QuestContext c, ObservedQuestTask task, CancellationToken ct)
    {
        // A ground sprite is only a candidate. The named inventory delta after
        // pickup proves the token; an unexpected item stays in the player's bag.
        var session = c.Session;
        string item = task.Item ?? throw new QuestBlockedException("Quest token name is missing.");
        if (c.Job.Pending is not null)
        {
            await ReconcilePickupAsync(c, task, ct);
            return;
        }
        if (session.Snapshot.Count(item) >= task.Quantity)
            return;
        if (!session.Snapshot.InventoryKnown || session.Snapshot.Inventory.Count >= 59 || session.Snapshot.Weight + task.WeightReserve > session.Snapshot.MaxWeight)
            throw new QuestBlockedException("Reserve bag space for " + item + "; no items are discarded.");
        await PrepareTravelAsync(c, task, ct);
        var ground = session.Snapshot.GroundItems.Values.Where(g => g.Sprite == task.Sprite).OrderBy(g => Distance(session.Snapshot.Position!, g.Position)).FirstOrDefault();
        if (ground is null)
        {
            if (session is not ICombatMap map || session.Snapshot.MaxHp is not > 0)
                throw new QuestBlockedException("Actual combat map and health are needed to gather " + item + ".");
            var seen = session.Snapshot.GroundItems.Keys.ToHashSet();
            var result = await MeleeFight.RunAsync(session, map, new BashOptions
            {
                PriorityOnly = true,
                PrioritySprites = [task.Monster],
                IsFriend = c.Hunting?.IsFriend,
                Note = line => c.Hunting?.Log?.Invoke(c.Job.Character, line),
                Activity = a => c.Hunting?.Activity?.Invoke(c.Job.Character, a)
            }, s => s.Count(item) >= task.Quantity || s.Map == task.Map && s.GroundItems.Values.Any(g => g.Sprite == task.Sprite && !seen.Contains(g.Id)),
                TimeSpan.FromSeconds(30), ct, roam: true, retreat: new(500, new(20, 20), "Mileth"));
            if (session.Snapshot.Count(item) >= task.Quantity)
                return;
            ground = session.Snapshot.GroundItems.Values.Where(g => g.Sprite == task.Sprite && !seen.Contains(g.Id)).OrderBy(g => Distance(session.Snapshot.Position!, g.Position)).FirstOrDefault();
            if (result != FightEnd.GoalReached || ground is null)
                throw new ResourceUnavailableException("No verified " + item + " drop acquired. Combat stopped safely; inspect supplies before resuming.");
        }
        var position = session.Snapshot.Position!;
        if (Distance(position, ground.Position) > 1)
        {
            if (session is not ICombatMap map)
                throw new QuestBlockedException("A reachable approach to the actual drop is required.");
            var sides = new[] { new Tile(ground.Position.X + 1, ground.Position.Y), new(ground.Position.X - 1, ground.Position.Y), new(ground.Position.X, ground.Position.Y + 1), new(ground.Position.X, ground.Position.Y - 1) };
            var approach = sides.Where(t => map.IsWalkable(t) && !map.IsWarp(t)).Select(t => (Tile: t, Steps: map.PathLength(position, t))).Where(x => x.Steps > 0).OrderBy(x => x.Steps).FirstOrDefault();
            if (approach.Tile is null)
                throw new QuestBlockedException("Quest drop has no safe reachable side.");
            await MoveAsync(c, task.Map, approach.Tile, ct);
        }
        if (!session.Snapshot.GroundItems.TryGetValue(ground.Id, out var fresh) || fresh != ground || session.Snapshot.Map != task.Map || Distance(session.Snapshot.Position!, fresh.Position) > 1)
            throw new QuestBlockedException("Ground identity or position changed; no guessed pickup sent.");
        await c.SaveAsync(c.Job with
        {
            Route = c.Job.Route! with
            {
                Resource = item,
                Baseline = Baseline(session.Snapshot)
            }
        });
        await c.BeginAsync("observed-pickup:" + task.Id);
        await session.ExecuteAsync(new("pickup", task.Map, fresh.Position, ExpectedGeneration: session.Snapshot.Generation), ct);
        await ReconcilePickupAsync(c, task, ct);
    }
    static int Distance(Tile a, Tile b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    static async Task ReconcilePickupAsync(QuestContext c, ObservedQuestTask task, CancellationToken ct)
    {
        var p = c.Job.Pending!;
        string wanted = task.Item!;
        if (p.Kind != "observed-pickup:" + task.Id || p.Generation != c.Session.Snapshot.Generation || !p.InventoryBaselineKnown)
            throw new QuestBlockedException("Pickup is unresolved in its original connection; no second pickup sent.");
        bool Receipt(CharacterSnapshot s) => s.Generation == p.Generation && s.InventoryKnown && s.Inventory.Values.Any(i => s.Count(i.Name) > p.InventoryCounts.GetValueOrDefault(i.Name));
        if (!Receipt(c.Session.Snapshot))
            await c.Session.WaitAsync(Receipt, p.Revision, ProofTimeout, ct);
        var s = c.Session.Snapshot;
        var received = s.Inventory.Values.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Where(n => s.Count(n) > p.InventoryCounts.GetValueOrDefault(n)).ToArray();
        await c.SaveAsync(c.Job with
        {
            Pending = null
        });
        if (s.Count(wanted) <= p.InventoryCounts.GetValueOrDefault(wanted))
            throw new ResourceUnavailableException("Pickup produced " + string.Join(", ", received) + ", not " + wanted + ". It is retained; shared sprite alone is not token proof.");
    }
}
