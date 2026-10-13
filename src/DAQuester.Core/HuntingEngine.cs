namespace DAQuester.Core;

// The hunting engine: hunts a ground on server signals alone. Fights, judges each floor by its 1v1 kills,
// moves up when a floor is too hard or she is surrounded, tries the stairs down after an easy level-up,
// and rests to full health in town after a walk-out.
public sealed class HuntingEngine(IGameSession session, ICombatMap map, HuntingGround ground, BashOptions options, HuntingProgress progress, Action<HuntingProgress> saved, Retreat retreat, LevelingThresholds thresholds, int? targetLevel = null, DropSurvey? survey = null, StatSpender? stats = null, IReadOnlyCollection<int>? untilSafe = null)
{
    public Func<Func<CharacterSnapshot, bool>, CancellationToken, Task<FightEnd>>? Fight
    {
        get; init;
    }
    public Func<LevelingThresholds, FloorJudge> NewJudge { get; init; } = t => new FloorJudge(t, listed: ground.Monsters);
    public TimeSpan IdleLimit { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan RestCheck { get; init; } = TimeSpan.FromSeconds(30);
    // Index into the ground's floors of the deepest floor this run may use.
    public int DeepestFloor { get; init; } = ground.Floors.Count - 1;
    // Map -> monster types never fought there, whatever the judge says.
    public IReadOnlyDictionary<int, ushort[]> AlwaysAvoid { get; init; } = new Dictionary<int, ushort[]>();
    FloorJudge? judge;
    int? judgedMap;
    // Kills are recorded from the network thread while the loop records gates and verdicts.
    readonly object progressGate = new();
    // Health lost and time spent per "map@level" since the last save.
    readonly Dictionary<(int Map, int Level), (double Lost, double Seconds)> drain = [];
    CharacterSnapshot? lastSeen;
    DateTimeOffset lastSeenAt;
    int? floorMap;
    // Two walk-outs from one floor at one level mark it too hard there.
    const int WalkOutsTooHard = 2;
    static readonly TimeSpan longestGap = TimeSpan.FromSeconds(5);

    void Update(Action<HuntingProgress> change)
    {
        lock (progressGate)
        {
            foreach (var ((map, level), (lost, seconds)) in drain)
            {
                var f = progress.At(map, level);
                progress.Set(map, level, f with
                {
                    HealthLost = f.HealthLost + lost,
                    FloorSeconds = f.FloorSeconds + seconds
                });
            }

            drain.Clear();
            change(progress);
            saved(progress);
        }
    }

    void Drain(CharacterSnapshot s)
    {
        var now = DateTimeOffset.UtcNow;
        bool left;
        lock (progressGate)
        {
            var before = lastSeen;
            var since = lastSeenAt;
            lastSeen = s;
            lastSeenAt = now;
            left = before is not null && before.Map != s.Map && drain.Count > 0;
            if (before is not null && before.Map == s.Map && ground.FloorOf(s.Map) >= 0)
            {
                var key = (s.Map!.Value, s.Level ?? 0);
                var d = drain.GetValueOrDefault(key);
                double lost = s.Hp < before.Hp && s.MaxHp > 0 ? (before.Hp!.Value - s.Hp!.Value) * 100.0 / s.MaxHp!.Value : 0;
                drain[key] = (d.Lost + lost, d.Seconds + Math.Min((now - since).TotalSeconds, longestGap.TotalSeconds));
            }
        }

        if (left)
            Update(_ => { });
    }

    // Gear is repaired at this durability; below 10% items are destroyed rather than broken.
    public const int RepairBelowPercent = 25;

    void Note(string line) => options.Note?.Invoke(line);
    static bool NeedsRepair(CharacterSnapshot s) => s.WorstDurability <= RepairBelowPercent;
    static bool Touching(CharacterSnapshot s) => s.Position is { } p && s.Entities.Values.Any(e => !e.IsPlayer && e.Kind == 0 && Math.Max(Math.Abs(e.Position.X - p.X), Math.Abs(e.Position.Y - p.Y)) <= 1);

    // Repair at the NPC by the safe spot (Skan in the Crypt Vestibule). Unable to pay stops leveling there.
    async Task RepairAsync(CancellationToken ct)
    {
        var s = session.Snapshot;
        Note("Gear at " + s.WorstDurability + "%; repairing at " + retreat.Place);
        if (s.Map != retreat.Map)
            await MeleeFight.WalkToRetreat(session, retreat, ct);
        try
        {
            await session.ExecuteAsync(new("repair"), ct);
        }
        catch (ResourceUnavailableException e)
        {
            throw new QuestBlockedException(e.Message + "; leveling stopped in " + retreat.Place + ".");
        }

        Note("Gear repaired");
    }
    string Who => session.Snapshot.Name ?? "The character";
    static string Where(CharacterSnapshot s) => s.MapName is { Length: > 0 } name ? name : "map " + s.Map;

    public async Task RunAsync(CancellationToken ct)
    {
        void Feed(CharacterSnapshot s)
        {
            WarnUnlisted(s);
            judge?.Observe(s);
            survey?.Observe(s);
            Drain(s);
        }

        lock (progressGate)
        {
            lastSeen = session.Snapshot;
            lastSeenAt = DateTimeOffset.UtcNow;
        }

        session.Changed += Feed;
        try
        {
            await StartClearAsync(ct);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await StepAsync(ct))
                        return;
                }
                catch (RetreatedException e)
                {
                    Note(e.Message);
                    var culprit = judge?.Culprit;
                    judge = null;
                    WalkedOut(culprit);
                }
                catch (EntranceRefusedException e)
                {
                    await Refused(e, ct);
                }
            }
        }
        finally
        {
            session.Changed -= Feed;
            Update(_ => { });
        }
    }

    // The monster type that took the most health is avoided from then on; walk-outs that keep coming mark the floor.
    readonly HashSet<(int Map, ushort Sprite)> unlistedSeen = [];

    void WarnUnlisted(CharacterSnapshot s)
    {
        if (s.Map is not { } map || ground.FloorOf(map) < 0)
            return;
        foreach (var e in s.Entities.Values)
            if (!e.IsPlayer && e.Kind == 0 && !ground.Monsters.Contains(e.Sprite) && unlistedSeen.Add((map, e.Sprite)))
                Note("Unlisted sprite " + e.Sprite + " on map " + map + ", not attacked; add it to the " + ground.Name + " monster list if it is a real monster");
    }

    void WalkedOut(ushort? culprit)
    {
        if (floorMap is not { } map)
            return;
        int level = session.Snapshot.Level ?? 0;
        int count = 0;
        Update(p =>
        {
            var f = p.At(map, level);
            count = f.WalkOuts + 1;
            p.Set(map, level, f with
            {
                WalkOuts = count
            });
            if (culprit is { } c)
                p.MarkMonsterTooHard(map, level, c);
            if (count >= WalkOutsTooHard)
                p.MarkTooHard(map, level);
        });
        Note(count >= WalkOutsTooHard ? "Walk-out " + count + " from map " + map + " at level " + level + "; judged too hard there." : "Walk-out " + count + " of " + WalkOutsTooHard + " from map " + map + " at level " + level);
        if (culprit is { } sprite)
            Note("Sprite " + sprite + " took the most health before the walk-out; avoiding it on map " + map + " up to level " + level);
    }

    async Task<bool> StepAsync(CancellationToken ct)
    {
        var s = session.Snapshot;
        if (!s.Connected)
            throw new QuestBlockedException("Disconnected while leveling.");
        int level = s.Level ?? 0;
        if (targetLevel is { } goal && level >= goal)
        {
            // A running fight is finished before anything else happens.
            if (ground.FloorOf(s.Map) >= 0 && Touching(s))
                await FightAsync(x => !Touching(x), ct);
            Note(Who + " reached level " + level + "; leveling stopped.");
            return true;
        }

        bool allSafe;
        lock (progressGate)
            allSafe = untilSafe is { Count: > 0 } && untilSafe.All(progress.SafeAt.ContainsKey);
        if (allSafe)
        {
            Note("Every listed floor has been judged safe; leveling stopped.");
            return true;
        }

        if (stats is not null)
            await stats.SpendAsync(ct);

        int floor = ground.FloorOf(s.Map);
        floorMap = floor >= 0 ? s.Map : null;
        if (floor < 0)
        {
            if (NeedsRepair(s))
                await RepairAsync(ct);
            await RestAsync(ct);
            if (progress.TooHard(ground.Floors[0].Map, level))
                throw new QuestBlockedException(ground.FirstFloorName + " was too hard at level " + level + ", so leveling stopped.");
            var entry = ground.Floors[Math.Min(ground.Entry(progress, level), DeepestFloor)];
            Note("Heading down to map " + entry.Map + " at level " + level);
            await WalkAsync(entry.Map, entry.DownArrival, ct);
            return false;
        }

        if (floor > DeepestFloor)
        {
            var allowed = ground.Floors[DeepestFloor];
            Note("Map " + s.Map + " is deeper than this run goes; walking up to map " + allowed.Map);
            await WalkAsync(allowed.Map, allowed.UpArrival!, ct);
            return false;
        }

        if (judge is null || judgedMap != s.Map)
        {
            judge = NewJudge(thresholds);
            judgedMap = s.Map;
            var judged = judge;
            int map = s.Map!.Value;
            lock (progressGate)
                judge.Seed(progress.Aggressive.GetValueOrDefault(map, []), [.. progress.AvoidAt(map, level), .. AlwaysAvoid.GetValueOrDefault(map, [])]);
            judge.Killed += k =>
            {
                Note((k.OneOnOne ? "1v1" : "Crowded") + " kill of sprite " + k.Sprite + ": " + k.CostPercent.ToString("0") + "% health in " + k.Seconds.ToString("0.0") + " s");
                if (!k.OneOnOne)
                    return;
                int at = session.Snapshot.Level ?? 0;
                bool newlySafe = false;
                var avoid = judged.Avoided;
                List<ushort> newlyAvoided = [];
                Update(p =>
                {
                    p.Record(map, at, k);
                    p.Aggressive[map] = p.Aggressive.GetValueOrDefault(map, []).Union(judged.Aggressive).ToList();
                    newlyAvoided = avoid.Except(p.AvoidAt(map, at)).ToList();
                    foreach (var sprite in newlyAvoided)
                        p.MarkMonsterTooHard(map, at, sprite);
                    var here = p.At(map, at);
                    // Safe needs time on the floor without a walk-out or an avoided monster type, not just cheap kills.
                    bool settled = here.FloorSeconds >= thresholds.SafeAfter.TotalSeconds && here.WalkOuts == 0 && !p.TooHard(map, at) && avoid.Count == 0 && p.AvoidAt(map, at).Count == 0;
                    if (settled && judged.Verdict is FloorVerdict.Fine or FloorVerdict.Easy && (!p.SafeAt.TryGetValue(map, out int safe) || at < safe))
                    {
                        p.SafeAt[map] = at;
                        newlySafe = true;
                    }
                });
                foreach (var sprite in newlyAvoided)
                    Note("Sprite " + sprite + " is too hard on map " + map + " at level " + at + "; avoiding it (" + judged.Describe() + ")");
                if (newlySafe)
                    Note("Map " + map + " judged safe at level " + at + " (" + judged.Describe() + ")");
            };
        }

        var j = judge;
        // Packs are walked away from inside the fight; being surrounded only sends her up once health is under half.
        var end = await FightAsync(x => (x.Level ?? 0) > level || j.Verdict == FloorVerdict.TooHard || j.Surrounded && Hurt(x) || NeedsRepair(x) && !Touching(x), ct);
        var after = session.Snapshot;
        int now = after.Level ?? level;
        // Worn gear: the monsters on her are finished, then she walks out to repair.
        if (NeedsRepair(after) && !Touching(after))
        {
            judge = null;
            await WalkAsync(retreat.Map, retreat.Tile, ct);
            await RepairAsync(ct);
            return false;
        }

        if (end == FightEnd.Overrun || j.Surrounded && Hurt(after))
        {
            Note((end == FightEnd.Overrun ? "Overrun by a pack on " : "Surrounded on ") + Where(after) + "; going up a floor.");
            judge = null;
            await GoUpAsync(floor, now, ct);
            return false;
        }

        if (j.Verdict == FloorVerdict.TooHard)
        {
            int map = ground.Floors[floor].Map;
            Update(p => p.MarkTooHard(map, now));
            Note(Where(after) + " is too hard at level " + now + " (" + j.Describe() + "); going up.");
            judge = null;
            await GoUpAsync(floor, now, ct);
            return false;
        }

        if (now > level)
        {
            var verdict = j.Verdict;
            Note(Who + " reached level " + now + " on " + Where(after) + " (" + j.Describe() + ")");
            // Costs change with level, so the next level is judged afresh.
            judge = null;
            if (targetLevel is { } stop && now >= stop)
                return false;
            if (floor < DeepestFloor && ground.TryDown(progress, floor, now, verdict))
            {
                var next = ground.Floors[floor + 1];
                Note("Trying the stairs down to map " + next.Map + " at level " + now);
                await WalkAsync(next.Map, next.DownArrival, ct);
                Update(p => p.AllowedAt[next.Map] = Math.Min(p.AllowedAt.GetValueOrDefault(next.Map, int.MaxValue), now));
                Note("Let through to " + Where(session.Snapshot) + " at level " + now);
            }
        }
        else if (end == FightEnd.NoTargets)
            Note("No monster found on " + Where(after) + " for " + IdleLimit.TotalSeconds + " s; still looking.");

        return false;
    }

    static bool Hurt(CharacterSnapshot s) => s.Hp is { } hp && s.MaxHp is { } max && max > 0 && hp * 100L < max * (long)MeleeFight.OverrunBelowPercent;

    // Aggressive types known for this floor from earlier runs, plus any this run has seen walk up to her.
    IReadOnlyCollection<ushort> AggressiveHere()
    {
        var known = new HashSet<ushort>(judge?.Aggressive ?? []);
        lock (progressGate)
            if (judgedMap is { } map && progress.Aggressive.TryGetValue(map, out var stored))
                known.UnionWith(stored);
        known.IntersectWith(ground.Monsters);
        return known;
    }

    // Types too hard here at this level: saved from earlier runs plus any this run has judged too hard.
    IReadOnlyCollection<ushort> AvoidHere()
    {
        var avoid = new HashSet<ushort>(judge?.Avoided ?? []);
        var s = session.Snapshot;
        lock (progressGate)
            if (s.Map is { } map)
                avoid.UnionWith([.. progress.AvoidAt(map, s.Level ?? 0), .. AlwaysAvoid.GetValueOrDefault(map, [])]);
        return avoid;
    }

    // Floor coverage per floor and level, kept across fights so escapes and roaming reach every quarter.
    readonly Dictionary<(int Map, int Level), FloorQuarters> coverage = [];

    Task<FightEnd> FightAsync(Func<CharacterSnapshot, bool> goal, CancellationToken ct)
    {
        if (Fight is { } fight)
            return fight(goal, ct);
        var s = session.Snapshot;
        var key = (s.Map ?? 0, s.Level ?? 0);
        if (!coverage.TryGetValue(key, out var quarters))
            coverage[key] = quarters = new();
        return MeleeFight.RunAsync(session, map, options with
        {
            AggressiveSprites = AggressiveHere,
            AvoidSprites = AvoidHere,
            PrioritySprites = ground.Monsters,
            PriorityOnly = true
        }, goal, IdleLimit, ct, roam: true, loot: survey, retreat: retreat, quarters: quarters);
    }

    // She logs back in where she dropped: possibly beside stairs, possibly hurt. Clear the warp, then rest to full.
    async Task StartClearAsync(CancellationToken ct)
    {
        var s = session.Snapshot;
        if (s.Position is { } here && BashingBase.BesideWarp(map, here) && BashingBase.SafeTileNear(map, here) is { } safe)
        {
            Note("Starting beside a warp at (" + here.X + "," + here.Y + "); moving clear first");
            await session.ExecuteAsync(new("navigate", s.Map, safe), ct);
        }

        s = session.Snapshot;
        if (ground.FloorOf(s.Map) >= 0 && s.Hp < s.MaxHp)
        {
            Note("Starting at " + s.Hp + "/" + s.MaxHp + " health; walking out to rest first");
            await MeleeFight.WalkToRetreat(session, retreat, ct);
        }
    }

    async Task GoUpAsync(int floor, int level, CancellationToken ct)
    {
        if (ground.Up(progress, floor, level) is { } up)
        {
            await WalkAsync(ground.Floors[up].Map, ground.Floors[up].UpArrival!, ct);
            return;
        }

        var s = session.Snapshot;
        await MeleeFight.WalkToRetreat(session, retreat, ct);
        if (progress.TooHard(ground.Floors[0].Map, level))
            throw new QuestBlockedException(ground.FirstFloorName + " was too hard at level " + level + ", so " + Who + " walked out to " + retreat.Place + " and stopped.");
        Note("No floor above " + Where(s) + "; walked out to " + retreat.Place);
    }

    async Task Refused(EntranceRefusedException e, CancellationToken ct)
    {
        var s = session.Snapshot;
        int floor = ground.FloorOf(s.Map);
        int level = s.Level ?? 0;
        if (floor >= 0 && floor + 1 < ground.Floors.Count)
        {
            int next = ground.Floors[floor + 1].Map;
            Update(p => p.RefusedAt[next] = Math.Max(p.RefusedAt.GetValueOrDefault(next), level));
        }

        Note("Stairs at (" + e.Stairs.X + "," + e.Stairs.Y + ") refused " + Who + " at level " + level + ": \"" + e.Message + "\"; moving away from the stairs and staying on " + Where(s));
        // Lingering beside the stairs would get her pulled in and kicked.
        if (s.Position is { } here && BashingBase.SafeTileNear(map, here) is { } safe)
            await session.ExecuteAsync(new("navigate", s.Map, safe), ct);
    }

    // Walking has the same 30% rule as fighting: below it, the walk is abandoned for the walk-out.
    async Task WalkAsync(int destination, Tile tile, CancellationToken ct)
    {
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bool low = false;
        void Check(CharacterSnapshot x)
        {
            if (x.Hp is { } hp && x.MaxHp is { } max && max > 0 && hp * 100L < max * (long)MeleeFight.RetreatBelowPercent)
            {
                low = true;
                guard.Cancel();
            }
        }

        session.Changed += Check;
        try
        {
            Check(session.Snapshot);
            guard.Token.ThrowIfCancellationRequested();
            await session.ExecuteAsync(new("navigate", destination, tile), guard.Token);
        }
        catch (OperationCanceledException) when (low && !ct.IsCancellationRequested)
        {
            var s = session.Snapshot;
            await MeleeFight.BreakOff(session, s, s.Hp!.Value * 100 / s.MaxHp!.Value, retreat, options.Note, ct);
        }
        finally
        {
            session.Changed -= Check;
        }
    }

    async Task RestAsync(CancellationToken ct)
    {
        var s = session.Snapshot;
        if (s.Hp is null || s.MaxHp is null || s.Hp >= s.MaxHp)
            return;
        if (s.Map != retreat.Map)
            await MeleeFight.WalkToRetreat(session, retreat, ct);
        // The walk-out lands beside the crypt door; resting there gets her pulled back in.
        if (session.Snapshot.Position is { } here && BashingBase.BesideWarp(map, here) && BashingBase.SafeTileNear(map, here) is { } clear)
            await session.ExecuteAsync(new("navigate", retreat.Map, clear), ct);
        Note("Resting in " + retreat.Place + " until health is full (" + session.Snapshot.Hp + "/" + session.Snapshot.MaxHp + ")");
        while (true)
        {
            var r = session.Snapshot;
            if (!r.Connected)
                throw new QuestBlockedException("Disconnected while resting.");
            if (r.Hp >= r.MaxHp)
                return;
            try
            {
                await session.WaitAsync(x => !x.Connected || x.Hp >= x.MaxHp, r.Revision, RestCheck, ct);
            }
            catch (TimeoutException)
            {
            } // Checked again after the next interval.
        }
    }
}
