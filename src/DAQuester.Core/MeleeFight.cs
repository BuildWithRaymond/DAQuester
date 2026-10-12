namespace DAQuester.Core;

public enum FightEnd
{
    GoalReached,
    NoTargets
}

// Runs the melee basher until the goal holds or nothing has been in reach for the idle limit.
// With roaming on, ticks without a target walk random waypoints to find monsters, as Talos does.
public static class MeleeFight
{
    // Talos's bash loop pauses 10 ms between ticks.
    public static TimeSpan Tick { get; set; } = TimeSpan.FromMilliseconds(10);

    public static async Task<FightEnd> RunAsync(IGameSession session, ICombatMap map, BashOptions options, Func<CharacterSnapshot, bool>? goal, TimeSpan idleLimit, CancellationToken ct, bool roam = false)
    {
        var basher = new DynamicMeleeBashing(session, map, options);
        var wander = roam ? new RandomWaypoints(session, map, options.Note) : null;
        // Weapon choice needs the base class, which comes from the profile.
        if (session.Snapshot.Connected && session.Snapshot.Class is null)
            await session.ExecuteAsync(new("inspect-self"), ct);
        DateTimeOffset? idleSince = null;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var s = session.Snapshot;
            if (!s.Connected)
                throw new QuestBlockedException("Disconnected during the fight.");
            if (goal?.Invoke(s) == true)
                return FightEnd.GoalReached;
            // No fighting while a map change is still loading.
            if (s.Map is null || s.Position is null)
            {
                await Task.Delay(Tick, ct);
                continue;
            }

            if (await basher.DoBashingAsync(ct))
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
}

// Ported from Talos Redux BotEngine.HandleRandomWaypoints: wander the current map between random reachable tiles.
public sealed class RandomWaypoints(IGameSession session, ICombatMap map, Action<string>? note = null)
{
    // A map with few open tiles stops looking for more instead of looping forever.
    const int MaxDraws = 500;
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
        for (int i = 0; i < count; i++)
        {
            for (int draw = 0; draw < MaxDraws; draw++)
            {
                var tile = new Tile(Random.Shared.Next(1, size.Width - 1), Random.Shared.Next(1, size.Height - 1));
                // A warp tile would walk her off the map, so it is never a waypoint.
                if (map.IsWall(tile) || map.IsWarp(tile) || map.PathLength(here, tile) == 0)
                    continue;
                if (!waypoints.Contains(tile))
                    waypoints.Add(tile);
                break;
            }
        }

        note?.Invoke("Roaming " + waypoints.Count + " random waypoints");
    }
}
