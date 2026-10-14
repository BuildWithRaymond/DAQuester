namespace DAQuester.Core;

// One app instance shares this allocator; server occupancy remains authoritative.
public sealed class FiskWaitingArea
{
    readonly object gate = new();
    readonly Dictionary<IGameSession, Tile> reservations = [];
    readonly HashSet<IGameSession> arriving = [];
    public const int Map = 413;
    // Arbiter WorldDirection.Down; this is a normal typed turn, not key injection.
    public const uint Down = 2;

    public async Task<Tile?> ParkAsync(IGameSession session, CancellationToken ct)
    {
        if (session is not ICombatMap map || session.Snapshot.Map != Map || session.Snapshot.Position is null)
            throw new QuestBlockedException("Return to Suomi Tavern before choosing a bouquet waiting spot.");
        long generation = session.Snapshot.Generation;
        Tile? tile;
        lock (gate)
        {
            foreach (var stale in reservations.Keys.Where(s => !s.Snapshot.Connected || s.Snapshot.Map != Map || !arriving.Contains(s) && s.Snapshot.Position != reservations[s]).ToArray())
                reservations.Remove(stale);
            tile = Enumerable.Range(10, 5).Select(x => new Tile(x, 1)).FirstOrDefault(t =>
                map.IsWalkable(t) && !map.IsWarp(t) &&
                !session.Snapshot.Entities.Values.Any(e => e.Id != session.Snapshot.Id && e.Blocks && e.Position == t) &&
                !reservations.Any(r => r.Key != session && r.Value == t) &&
                (session.Snapshot.Position == t || map.PathLength(session.Snapshot.Position!, t) > 0));
            if (tile is not null)
            {
                reservations[session] = tile;
                arriving.Add(session);
            }
        }
        if (tile is null)
            return null; // Keep the paid wait; nobody is pushed out of an occupied spot.
        try
        {
            if (session.Snapshot.Position != tile)
                await session.ExecuteAsync(new("navigate", Map, tile, ExpectedGeneration: generation), ct);
            var s = session.Snapshot;
            if (!s.Connected || s.Generation != generation || s.Map != Map || s.Position != tile)
                throw new QuestBlockedException("Waiting spot arrival is uncertain; inspect the actual position before another move.");
            if (s.Motion?.Direction != Down)
            {
                await session.ExecuteAsync(new("turn", Amount: Down, ExpectedGeneration: generation), ct);
                if (session.Snapshot.Motion?.Direction != Down)
                    await session.WaitAsync(x => x.Generation == generation && x.Map == Map && x.Position == tile && x.Motion?.Direction == Down,
                        s.Revision, TimeSpan.FromSeconds(5), ct);
            }
            lock (gate)
                arriving.Remove(session);
            return tile;
        }
        catch
        {
            lock (gate)
            {
                reservations.Remove(session);
                arriving.Remove(session);
            }
            throw;
        }
    }
    public void Release(IGameSession session)
    {
        lock (gate)
        {
            reservations.Remove(session);
            arriving.Remove(session);
        }
    }
}
