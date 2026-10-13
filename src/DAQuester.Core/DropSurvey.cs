namespace DAQuester.Core;

// Picks up everything that drops where a monster died, between fights, so drop rates can be tracked by item
// name. Gold and named keepers stay in the bag; everything else picked up is dropped again at her feet.
// Items already on the floor (a pile left by another player) never count: an item counts only the first time it
// is seen, on a tile she could already see, and more than MaxDropsPerDeath at one death is a pile, not a drop.
public sealed class DropSurvey(IGameSession session, IReadOnlyCollection<string> keep, Action<string>? note = null) : ILooter
{
    const int Reach = 2, UsableSlots = 59, Sight = 8, MaxDropsPerDeath = 3;
    // A drop can arrive just before or just after the monster's removal.
    static readonly TimeSpan dropWindow = TimeSpan.FromSeconds(2.5), dropEarly = TimeSpan.FromSeconds(0.5), settle = TimeSpan.FromMilliseconds(1500), dropEvery = TimeSpan.FromSeconds(5), forget = TimeSpan.FromSeconds(60);
    readonly object gate = new();
    readonly Dictionary<uint, (Tile At, DateTimeOffset When)> deaths = [], fresh = [], pending = [];
    readonly List<(string Name, uint Quantity)> extras = [];
    readonly HashSet<uint> seen = [];
    readonly long bagFullSeen = session.Snapshot.BagFullRevision;
    CharacterSnapshot? previous;
    (Tile At, DateTimeOffset When)? ownDrop;
    DateTimeOffset lastDrop = DateTimeOffset.MinValue;
    bool fullNoted;

    public IReadOnlyCollection<Tile> Pending
    {
        get
        {
            lock (gate)
                return pending.Values.Select(p => p.At).ToArray();
        }
    }

    static bool Near(Tile a, Tile b) => Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;
    static bool InReach(Tile a, Tile b) => Math.Abs(a.X - b.X) <= Reach && Math.Abs(a.Y - b.Y) <= Reach;

    public void Observe(CharacterSnapshot s)
    {
        lock (gate)
        {
            var before = previous;
            previous = s;
            var now = DateTimeOffset.UtcNow;
            if (before is null || s.Map != before.Map)
            {
                deaths.Clear();
                fresh.Clear();
                pending.Clear();
                seen.Clear();
                seen.UnionWith(s.GroundItems.Keys);
                return;
            }

            foreach (var (id, e) in before.Entities)
                if (!e.IsPlayer && e.Kind == 0 && !s.Entities.ContainsKey(id))
                    deaths[id] = (e.Position, now);
            foreach (var (id, g) in s.GroundItems)
            {
                if (!seen.Add(id))
                    continue;
                bool inSight = before.Position is { } was && Math.Max(Math.Abs(was.X - g.Position.X), Math.Abs(was.Y - g.Position.Y)) <= Sight;
                if (inSight && !(ownDrop is { } mine && mine.At == g.Position && now - mine.When <= dropWindow))
                    fresh[id] = (g.Position, now);
            }

            foreach (var (deathId, d) in deaths)
            {
                var dropped = fresh.Where(f => Near(d.At, f.Value.At) && d.When - f.Value.When <= dropEarly && f.Value.When - d.When <= dropWindow).ToList();
                if (dropped.Count > MaxDropsPerDeath)
                {
                    note?.Invoke(dropped.Count + " items beside a death at (" + d.At.X + "," + d.At.Y + ") look like a pile on the floor, not a drop; left alone");
                    foreach (var (id, _) in dropped)
                        fresh.Remove(id);
                    continue;
                }

                foreach (var (id, item) in dropped)
                    pending.TryAdd(id, item);
            }
            foreach (var id in deaths.Where(d => now - d.Value.When > dropWindow).Select(d => d.Key).ToList())
                deaths.Remove(id);
            foreach (var id in fresh.Where(f => now - f.Value.When > dropWindow).Select(f => f.Key).ToList())
                fresh.Remove(id);
            foreach (var id in pending.Where(p => !s.GroundItems.ContainsKey(p.Key) || now - p.Value.When > forget).Select(p => p.Key).ToList())
                pending.Remove(id);
        }
    }

    public async Task<bool> Tick(CancellationToken ct)
    {
        var s = session.Snapshot;
        if (s.Position is not { } here)
            return false;
        bool full = s.BagFullRevision > bagFullSeen || s.Inventory.Count >= UsableSlots || s.Weight >= s.MaxWeight;
        if (full && !fullNoted)
            note?.Invoke("The bag is full or too heavy; drops are no longer picked up.");
        fullNoted = full;
        (uint Id, Tile At)? next = null;
        if (!full)
            lock (gate)
                next = pending.Where(p => s.GroundItems.ContainsKey(p.Key)).OrderBy(p => BashingBase.Distance(here, p.Value.At)).Select(p => ((uint, Tile)?)(p.Key, p.Value.At)).FirstOrDefault();
        if (next is { } n)
        {
            if (InReach(here, n.At))
                return await PickUp(n.At, ct);
            try
            {
                await session.ExecuteAsync(new("pathfind", Target: n.At), ct);
            }
            catch (Exception e) when (e is QuestBlockedException or TimeoutException)
            {
                Forget(n.At);
            }

            return true;
        }

        if (extras.Count > 0 && DateTimeOffset.UtcNow - lastDrop >= dropEvery)
            return await DropExtras(here, ct);
        return false;
    }

    void Forget(Tile tile)
    {
        lock (gate)
            foreach (var id in pending.Where(p => p.Value.At == tile).Select(p => p.Key).ToList())
                pending.Remove(id);
    }

    async Task<bool> PickUp(Tile tile, CancellationToken ct)
    {
        var before = session.Snapshot;
        string sprites = string.Join(",", before.GroundItems.Values.Where(g => g.Position == tile).Select(g => g.Sprite));
        await session.ExecuteAsync(new("loot", Target: tile), ct);
        CharacterSnapshot after;
        try
        {
            after = await session.WaitAsync(x => x.Gold != before.Gold || x.Inventory.Count != before.Inventory.Count || x.Inventory.Any(p => !before.Inventory.TryGetValue(p.Key, out var i) || i != p.Value), before.Revision, settle, ct);
        }
        catch (TimeoutException)
        {
            note?.Invoke("Pickup at (" + tile.X + "," + tile.Y + ") brought nothing; ground sprites " + sprites);
            Forget(tile);
            return true;
        }

        if (after.Gold > before.Gold)
            note?.Invoke("Picked up " + (after.Gold - before.Gold) + " gold at (" + tile.X + "," + tile.Y + "); ground sprites " + sprites);
        foreach (var (name, gained) in ItemLooter.Gained(before, after))
        {
            ushort sprite = after.Inventory.Values.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Sprite ?? 0;
            note?.Invoke("Picked up " + name + " x" + gained + " (sprite " + sprite + ") at (" + tile.X + "," + tile.Y + "); ground sprites " + sprites);
            if (!keep.Contains(name, StringComparer.OrdinalIgnoreCase))
                extras.Add((name, gained));
        }

        return true;
    }

    // Only what the survey picked up goes back down, never more than it gained.
    async Task<bool> DropExtras(Tile here, CancellationToken ct)
    {
        lastDrop = DateTimeOffset.UtcNow;
        bool sent = false;
        foreach (var (name, quantity) in extras.ToArray())
        {
            extras.Remove((name, quantity));
            var held = session.Snapshot.Inventory.Values.Where(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Slot).FirstOrDefault();
            if (held is null)
                continue;
            uint amount = Math.Min(quantity, held.Quantity);
            lock (gate)
                ownDrop = (here, DateTimeOffset.UtcNow);
            try
            {
                await session.ExecuteAsync(new("drop", Target: here, Slot: held.Slot, Amount: amount), ct);
                note?.Invoke("Dropped " + name + " x" + amount);
                sent = true;
            }
            catch (QuestBlockedException e)
            {
                note?.Invoke("Couldn't drop " + name + ": " + e.Message);
            }
        }

        return sent;
    }

    public async Task Finish(CancellationToken ct)
    {
        if (extras.Count > 0 && session.Snapshot.Position is { } here)
            await DropExtras(here, ct);
    }
}
