namespace DAQuester.Core;

public enum FightEnd
{
    GoalReached,
    NoTargets,
    // A pack closed in and walking away wasn't possible (no clear tile, or health already under half).
    Overrun
}

// Where a fight walks to when health runs low, such as out of the crypt to Mileth.
public sealed record Retreat(int Map, Tile Tile, string Place);

// Runs the melee basher until the goal holds or nothing has been in reach for the idle limit.
// With roaming on, ticks without a target walk random waypoints to find monsters, as Talos does.
public static class MeleeFight
{
    // Talos's bash loop pauses 10 ms between ticks.
    public static TimeSpan Tick { get; set; } = TimeSpan.FromMilliseconds(10);
    // Health is read from the server every tick; nothing predicts whether a map is too strong.
    public const int RetreatBelowPercent = 30;
    // Swarmed = this many aggressive monsters touching her; she walks to a tile at least EscapeDistance from
    // every aggressive monster within PackTrigger tiles.
    public const int PackTrigger = 4, EscapeDistance = 12, OverrunBelowPercent = 50, EscapeCandidates = 20;
    // A monster type too hard here this many steps away is stepped away from, to a tile AvoidClearance clear of it.
    public const int AvoidRadius = 2, AvoidClearance = 5, AvoidSearch = 8;
    static readonly TimeSpan avoidRetry = TimeSpan.FromSeconds(5), shunChasedTarget = TimeSpan.FromSeconds(30);

    // With a looter, ticks without a fight action pick up gold and the target item; the goal then waits for extras to be dropped.
    public static async Task<FightEnd> RunAsync(IGameSession session, ICombatMap map, BashOptions options, Func<CharacterSnapshot, bool>? goal, TimeSpan idleLimit, CancellationToken ct, bool roam = false, ILooter? loot = null, Retreat? retreat = null, FloorQuarters? quarters = null)
    {
        var basher = new DynamicMeleeBashing(session, map, options);
        quarters ??= new();
        quarters.Pause();
        var wander = roam ? new RandomWaypoints(session, map, options.Note, options.AggressiveSprites, quarters, options.AvoidSprites) : null;
        // Weapon choice needs the base class, which comes from the profile.
        if (session.Snapshot.Connected && session.Snapshot.Class is null)
            await session.ExecuteAsync(new("inspect-self"), ct);
        DateTimeOffset? idleSince = null;
        var avoidBlockedUntil = DateTimeOffset.MinValue;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var s = session.Snapshot;
            if (!s.Connected)
                throw new QuestBlockedException("Disconnected during the fight.");
            if (s.Hp == 0)
                throw new QuestBlockedException((s.Name ?? "The character") + " died in " + (s.MapName ?? "the fight") + ", so the fight stopped.");
            if (s.Hp is { } hp && s.MaxHp is { } max && max > 0 && hp * 100L < max * (long)RetreatBelowPercent)
                await BreakOff(session, s, hp * 100 / max, retreat, options.Note, ct);
            if (goal?.Invoke(s) == true)
            {
                if (loot is not null)
                    await loot.Finish(ct);
                return FightEnd.GoalReached;
            }
            // No fighting while a map change is still loading.
            if (s.Map is null || s.Position is null)
            {
                await Task.Delay(Tick, ct);
                continue;
            }

            var known = options.AggressiveSprites?.Invoke() ?? [];
            if (map.Size is { } floor)
                quarters.Observe(s, floor, known, DateTimeOffset.UtcNow);
            if (known.Count > 0 && s.Position is { } here)
            {
                // One or two touching her are fought; three or more is a swarm to walk away from.
                var touching = BashingBase.AggressiveNear(s.Entities.Values, known, here, 1);
                if (touching.Count >= BashingBase.PackSize)
                {
                    bool hurt = s.Hp is { } now && s.MaxHp is { } top && top > 0 && now * 100L < top * (long)OverrunBelowPercent;
                    // Up a floor only once hurt and every part of this floor has been tried.
                    if (hurt && quarters.AllTried)
                        return FightEnd.Overrun;
                    if (Escape(map, here, BashingBase.AggressiveNear(s.Entities.Values, known, here, PackTrigger), quarters) is not { } away)
                        return FightEnd.Overrun;
                    options.Note?.Invoke("Swarmed by " + touching.Count + " aggressive monsters; walking to (" + away.X + "," + away.Y + ")");
                    try
                    {
                        await session.ExecuteAsync(new("navigate", s.Map, away), ct);
                    }
                    catch (Exception e) when (e is QuestBlockedException or TimeoutException)
                    {
                        return FightEnd.Overrun;
                    }

                    idleSince = null;
                    continue;
                }
            }

            var avoid = options.AvoidSprites?.Invoke() ?? [];
            if (avoid.Count > 0 && s.Position is { } at && DateTimeOffset.UtcNow >= avoidBlockedUntil)
            {
                var close = s.Entities.Values.Where(e => IsMonster(e) && avoid.Contains(e.Sprite) && BashingBase.Distance(e.Position, at) <= AvoidRadius).OrderBy(e => BashingBase.Distance(e.Position, at)).ToList();
                bool Fightable(Entity e) => IsMonster(e) && !avoid.Contains(e.Sprite) && (!options.PriorityOnly || options.PrioritySprites.Contains(e.Sprite));
                // A monster she can fight already beside her is finished first; it takes a few seconds.
                if (close.Count > 0 && !s.Entities.Values.Any(e => Fightable(e) && BashingBase.Distance(e.Position, at) <= 1))
                {
                    var prey = s.Entities.Values.Where(Fightable).OrderBy(e => BashingBase.Distance(e.Position, at)).FirstOrDefault();
                    var threats = s.Entities.Values.Where(e => IsMonster(e) && avoid.Contains(e.Sprite) && BashingBase.Distance(e.Position, at) <= AvoidSearch).ToList();
                    var away = StepAside(map, at, threats, prey?.Position) ?? Escape(map, at, threats, quarters);
                    if (away is null)
                        avoidBlockedUntil = DateTimeOffset.UtcNow + avoidRetry;
                    else
                    {
                        options.Note?.Invoke("Sprite " + close[0].Sprite + " is " + BashingBase.Distance(close[0].Position, at) + " steps away and too hard here; stepping to (" + away.X + "," + away.Y + ")");
                        // The monster she was chasing lies past it: leave that one alone for a while.
                        if (basher.Target is { } chased)
                            basher.Shun(chased.Id, shunChasedTarget);
                        try
                        {
                            await session.ExecuteAsync(new("navigate", s.Map, away), ct);
                            idleSince = null;
                            continue;
                        }
                        catch (Exception e) when (e is QuestBlockedException or TimeoutException)
                        {
                            // Cornered: fight what is beside her rather than stand still.
                            avoidBlockedUntil = DateTimeOffset.UtcNow + avoidRetry;
                        }
                    }
                }
            }

            if (await basher.DoBashingAsync(ct))
                idleSince = null;
            else if (loot is not null && await loot.Tick(ct))
                idleSince = null;
            else
            {
                if ((idleSince ??= DateTimeOffset.UtcNow) + idleLimit <= DateTimeOffset.UtcNow)
                    return FightEnd.NoTargets;
                if (wander is not null)
                    await wander.Step(ct);
            }

            await Task.Delay(Tick, ct);
        }
    }

    static bool IsMonster(Entity e) => !e.IsPlayer && e.Kind == 0;

    // The nearest reachable tile AvoidClearance clear of every threat, leaning toward a monster she can fight.
    public static Tile? StepAside(ICombatMap map, Tile here, IReadOnlyCollection<Entity> threats, Tile? toward)
    {
        var candidates = new List<Tile>();
        for (int x = here.X - AvoidSearch; x <= here.X + AvoidSearch; x++)
            for (int y = here.Y - AvoidSearch; y <= here.Y + AvoidSearch; y++)
            {
                var tile = new Tile(x, y);
                if (x < 0 || y < 0 || map.Size is { } size && (x >= size.Width || y >= size.Height))
                    continue;
                if (threats.All(m => Math.Max(Math.Abs(m.Position.X - x), Math.Abs(m.Position.Y - y)) >= AvoidClearance) && map.IsWalkable(tile) && !BashingBase.BesideWarp(map, tile))
                    candidates.Add(tile);
            }

        return candidates.OrderBy(t => BashingBase.Distance(here, t)).ThenBy(t => toward is { } p ? BashingBase.Distance(t, p) : 0).Take(EscapeCandidates).FirstOrDefault(t => map.PathLength(here, t) > 0);
    }

    // A reachable tile clear of warps, at least EscapeDistance from every pack member, in the quietest and
    // least-covered quarter other than her own; the nearest such tile there.
    public static Tile? Escape(ICombatMap map, Tile here, IReadOnlyCollection<Entity> pack, FloorQuarters? quarters = null)
    {
        if (map.Size is not { } size)
            return null;
        var candidates = new List<Tile>();
        for (int x = 1; x < size.Width - 1; x++)
            for (int y = 1; y < size.Height - 1; y++)
            {
                var tile = new Tile(x, y);
                if (pack.All(m => Math.Max(Math.Abs(m.Position.X - x), Math.Abs(m.Position.Y - y)) >= EscapeDistance) && map.IsWalkable(tile) && !BashingBase.BesideWarp(map, tile))
                    candidates.Add(tile);
            }

        Tile? Nearest(IEnumerable<Tile> tiles) => tiles.OrderBy(t => BashingBase.Distance(here, t)).Take(EscapeCandidates).FirstOrDefault(t => map.PathLength(here, t) > 0);
        if (quarters is not null)
        {
            int mine = FloorQuarters.Of(here, size);
            foreach (int q in quarters.Preferred().Where(q => q != mine).Append(mine))
                if (Nearest(candidates.Where(t => FloorQuarters.Of(t, size) == q)) is { } tile)
                    return tile;
        }

        return Nearest(candidates);
    }

    // The safe spot may be taken by another player: retry with more slack, and anywhere on the retreat map counts as out.
    public static async Task WalkToRetreat(IGameSession session, Retreat retreat, CancellationToken ct)
    {
        foreach (int tolerance in new[] { 1, 4 })
        {
            try
            {
                await session.ExecuteAsync(new("navigate", retreat.Map, retreat.Tile, Tolerance: tolerance), ct);
                return;
            }
            catch (Exception e) when (e is QuestBlockedException or TimeoutException)
            {
                if (session.Snapshot.Map == retreat.Map && tolerance > 1)
                    return;
                if (tolerance > 1)
                    throw;
            }
        }
    }

    public static async Task BreakOff(IGameSession session, CharacterSnapshot s, long percent, Retreat? retreat, Action<string>? note, CancellationToken ct)
    {
        string who = s.Name ?? "The character", where = s.MapName ?? "the fight";
        string low = who + "'s health fell to " + percent + "% in " + where;
        if (retreat is null)
            throw new QuestBlockedException(low + ", so the fight stopped.");
        note?.Invoke(low + "; walking out to " + retreat.Place);
        try
        {
            await WalkToRetreat(session, retreat, ct);
        }
        catch (Exception e) when (e is QuestBlockedException or TimeoutException)
        {
            throw new QuestBlockedException(low + " and the walk out to " + retreat.Place + " failed: " + e.Message);
        }

        throw new RetreatedException(low + ", so " + who + " walked out to " + retreat.Place + " and stopped.");
    }
}

// Ported from Talos Redux BotEngine.HandleRandomWaypoints: wander the current map between random reachable tiles.
public sealed class RandomWaypoints(IGameSession session, ICombatMap map, Action<string>? note = null, Func<IReadOnlyCollection<ushort>>? aggressive = null, FloorQuarters? quarters = null, Func<IReadOnlyCollection<ushort>>? avoid = null)
{
    // A map with few open tiles stops looking for more instead of looping forever.
    const int MaxDraws = 500;
    // Waypoints stay this far from any pack of aggressive monsters.
    const int PackClearance = 8;

    bool PackNear(Tile tile) =>
        aggressive?.Invoke() is { Count: > 0 } a && BashingBase.PackNear(session.Snapshot.Entities.Values, a, tile, PackClearance) ||
        avoid?.Invoke() is { Count: > 0 } v && BashingBase.AggressiveNear(session.Snapshot.Entities.Values, v, tile, PackClearance).Count > 0;
    readonly List<Tile> waypoints = [];
    int current;
    bool generated;
    int? generatedFor;

    public IReadOnlyList<Tile> Waypoints => waypoints;

    public async Task Step(CancellationToken ct)
    {
        if (session.Snapshot.Map != generatedFor)
            generated = false;
        if (!generated)
        {
            Generate();
            generated = true;
        }
        else if (!await NavigateToNext(ct))
            generated = false;
    }

    async Task<bool> NavigateToNext(CancellationToken ct)
    {
        if (waypoints.Count == 0)
            return false;
        // All waypoints visited: the caller generates a new set.
        if (current >= waypoints.Count)
        {
            current = 0;
            return false;
        }

        var next = waypoints[current];
        // A pack moved near the next waypoint: draw a fresh set away from it.
        if (PackNear(next))
            return false;
        if (session.Snapshot.Position == next)
        {
            current++;
            return true;
        }

        try
        {
            await session.ExecuteAsync(new("pathfind", Target: next), ct);
        }
        catch (Exception e) when (e is QuestBlockedException or TimeoutException)
        {
            return false; // A blocked route draws a fresh set rather than retrying the same tile.
        }

        return true;
    }

    void Generate()
    {
        current = 0;
        waypoints.Clear();
        var s = session.Snapshot;
        generatedFor = s.Map;
        if (s.Position is not { } here || map.Size is not { } size || size.Width < 3 || size.Height < 3)
            return;
        int count = Math.Clamp((size.Height + size.Width) / 8, 5, 12);
        // With quarters tracked, each set covers the quietest, least-visited quarter so the whole floor gets seen.
        var (x0, y0, x1, y1) = quarters?.Preferred().First() is { } q ? FloorQuarters.Bounds(q, size) : (1, 1, size.Width - 2, size.Height - 2);
        for (int i = 0; i < count; i++)
        {
            for (int draw = 0; draw < MaxDraws; draw++)
            {
                var tile = new Tile(Random.Shared.Next(x0, x1 + 1), Random.Shared.Next(y0, y1 + 1));
                // A warp tile would walk her off the map, so it is never a waypoint.
                if (map.IsWall(tile) || BashingBase.BesideWarp(map, tile) || PackNear(tile) || map.PathLength(here, tile) == 0)
                    continue;
                if (!waypoints.Contains(tile))
                    waypoints.Add(tile);
                break;
            }
        }

        note?.Invoke("Roaming " + waypoints.Count + " random waypoints");
    }
}
