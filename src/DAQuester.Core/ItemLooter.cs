namespace DAQuester.Core;

// Sprite is the item's Vorlof image number, assumed to be its ground sprite until verified live.
public sealed record LootTarget(string Item, ushort Sprite, int Count);

// Picks up gold in reach and the target item, walking to the target. Items that arrive alongside the
// target (a pile is taken from the top) are dropped again; nothing that was already carried is touched.
// Loot work done between fights: one action per Tick, and whatever must happen before the fight ends in Finish.
public interface ILooter
{
    Task<bool> Tick(CancellationToken ct);
    Task Finish(CancellationToken ct);
}

public sealed class ItemLooter(IGameSession session, LootTarget target, Action<string>? note = null) : ILooter
{
    public const ushort GoldSprite = 140;
    // The pickup packet names a tile; one this close is picked up without walking.
    const int Reach = 2, UsableSlots = 59;
    static readonly TimeSpan settle = TimeSpan.FromMilliseconds(1500), dropEvery = TimeSpan.FromSeconds(5), retryEmptyTile = TimeSpan.FromSeconds(10);
    readonly long bagFullSeen = session.Snapshot.BagFullRevision;
    readonly List<(string Name, uint Quantity)> extras = [];
    readonly Dictionary<Tile, DateTimeOffset> emptyTiles = [];
    DateTimeOffset lastDrop = DateTimeOffset.MinValue;

    public LootTarget Target => target;
    public IReadOnlyList<(string Name, uint Quantity)> Extras => extras;
    public bool Done(CharacterSnapshot s) => s.Count(target.Item) >= target.Count;

    // Checked before setting out: a free slot (unless the target already stacks in the bag) and room under max weight.
    public static string? NotReady(CharacterSnapshot s, LootTarget t)
    {
        if (!s.InventoryKnown || s.Weight is null || s.MaxWeight is null)
            return "Waiting for the bag and weight.";
        bool stacks = s.Inventory.Values.Any(i => i.Stackable && i.Name.Equals(t.Item, StringComparison.OrdinalIgnoreCase));
        if (!stacks && s.Inventory.Count >= UsableSlots)
            return "Free a bag slot for the " + t.Item + ".";
        if (s.Weight >= s.MaxWeight)
            return "The bag is too heavy to carry the " + t.Item + ". Lighten it first.";
        return null;
    }

    static bool InReach(Tile a, Tile b) => Math.Abs(a.X - b.X) <= Reach && Math.Abs(a.Y - b.Y) <= Reach;

    // One loot action per call; true when something was sent.
    public async Task<bool> Tick(CancellationToken ct)
    {
        var s = session.Snapshot;
        if (s.BagFullRevision > bagFullSeen)
            throw new QuestBlockedException("The bag is full, so the " + target.Item + " can't be picked up.");
        if (s.Position is not { } here)
            return false;
        var now = DateTimeOffset.UtcNow;
        var targets = Done(s) ? [] : s.GroundItems.Values.Where(g => g.Sprite == target.Sprite && !(emptyTiles.TryGetValue(g.Position, out var until) && until > now)).OrderBy(g => BashingBase.Distance(here, g.Position)).ToList();
        if (targets.FirstOrDefault() is { } item)
        {
            if (InReach(here, item.Position))
                return await PickUp(item.Position, ct);
            try
            {
                await session.ExecuteAsync(new("pathfind", Target: item.Position), ct);
            }
            catch (Exception e) when (e is QuestBlockedException or TimeoutException)
            {
                emptyTiles[item.Position] = now + retryEmptyTile;
            }

            return true;
        }

        // Extras go down only once no target lies in reach, so they never land back on its pile.
        if (extras.Count > 0 && now - lastDrop >= dropEvery)
            return await DropExtras(here, ct);
        if (s.GoldCapAt is null && s.GroundItems.Values.FirstOrDefault(g => g.Sprite == GoldSprite && InReach(here, g.Position) && !(emptyTiles.TryGetValue(g.Position, out var until) && until > now)) is { } gold)
            return await PickUp(gold.Position, ct);
        return false;
    }

    async Task<bool> PickUp(Tile tile, CancellationToken ct)
    {
        var before = session.Snapshot;
        string sprites = string.Join(",", before.GroundItems.Values.Where(g => g.Position == tile).Select(g => g.Sprite));
        await session.ExecuteAsync(new("loot", Target: tile), ct);
        CharacterSnapshot after;
        try
        {
            after = await session.WaitAsync(s => !SameBag(s, before), before.Revision, settle, ct);
        }
        catch (TimeoutException)
        {
            emptyTiles[tile] = DateTimeOffset.UtcNow + retryEmptyTile;
            note?.Invoke("Pickup at (" + tile.X + "," + tile.Y + ") brought nothing; ground sprites " + sprites);
            return true;
        }

        if (after.Gold > before.Gold)
            note?.Invoke("Picked up " + (after.Gold - before.Gold) + " gold at (" + tile.X + "," + tile.Y + ")");
        foreach (var (name, gained) in Gained(before, after))
        {
            ushort sprite = after.Inventory.Values.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Sprite ?? 0;
            note?.Invoke("Picked up " + name + " x" + gained + " (sprite " + sprite + ") at (" + tile.X + "," + tile.Y + "); ground sprites " + sprites);
            // A lookalike (same sprite, another name) is kept: dropped, it would be picked straight back up.
            if (!name.Equals(target.Item, StringComparison.OrdinalIgnoreCase) && sprite != target.Sprite)
                extras.Add((name, gained));
        }

        return true;
    }

    static bool SameBag(CharacterSnapshot a, CharacterSnapshot b) => a.Gold == b.Gold && a.Inventory.Count == b.Inventory.Count && a.Inventory.All(p => b.Inventory.TryGetValue(p.Key, out var i) && i == p.Value);

    internal static IEnumerable<(string Name, uint Gained)> Gained(CharacterSnapshot before, CharacterSnapshot after)
    {
        var was = before.Inventory.Values.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity), StringComparer.OrdinalIgnoreCase);
        foreach (var now in after.Inventory.Values.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
        {
            long gained = now.Sum(i => (long)i.Quantity) - was.GetValueOrDefault(now.Key);
            if (gained > 0)
                yield return (now.Key, (uint)gained);
        }
    }

    // Drops go at the character's feet: only what this quest picked up, never more than it gained.
    async Task<bool> DropExtras(Tile here, CancellationToken ct)
    {
        lastDrop = DateTimeOffset.UtcNow;
        bool sent = false;
        foreach (var (name, quantity) in extras.ToArray())
        {
            var held = session.Snapshot.Inventory.Values.Where(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Slot).FirstOrDefault();
            extras.Remove((name, quantity));
            if (held is null)
                continue;
            uint amount = Math.Min(quantity, held.Quantity);
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

    // Before the fight ends: anything still waiting to be dropped goes down now.
    public async Task Finish(CancellationToken ct)
    {
        if (extras.Count > 0 && session.Snapshot.Position is { } here)
            await DropExtras(here, ct);
    }
}
