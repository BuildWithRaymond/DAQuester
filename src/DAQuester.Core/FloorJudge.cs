namespace DAQuester.Core;

public sealed record KillSample(uint MonsterId, ushort Sprite, double CostPercent, double Seconds, bool OneOnOne);

public enum FloorVerdict
{
    Undecided,
    TooHard,
    Fine,
    Easy
}

public sealed record LevelingThresholds
{
    public int Samples { get; init; } = 3;
    public double TooHardPercent { get; init; } = 25;
    public double TooSlowSeconds { get; init; } = 20;
    public double EasyPercent { get; init; } = 10;
    public double FastSeconds { get; init; } = 10;
    public int SurroundedCount { get; init; } = 3;
    public TimeSpan SurroundedFor { get; init; } = TimeSpan.FromSeconds(3);
    // A floor is only marked safe after this long on it at one level without a walk-out.
    public TimeSpan SafeAfter { get; init; } = TimeSpan.FromMinutes(5);
    // A monster type that walks up to her this often counts as aggressive.
    public int AggressiveApproaches { get; init; } = 2;
    // Health lost with nothing beside her is charged to a monster this close (spells such as beag cradh).
    public int RangedReach { get; init; } = 8;
    // A walk-out blames the monster type that took the most health in this window before the last hit.
    public TimeSpan CulpritWindow { get; init; } = TimeSpan.FromSeconds(60);
}

// Judges a floor from what the server reports: health lost while monsters stand next to the character,
// or to the nearest monster in reach when none is beside her. A kill counts as 1v1 when no hit taken during
// it came with two or more monsters adjacent. Each monster type is judged on its own; a type too hard is avoided.
// With a list, creatures off it (players' pets) are ignored entirely.
public sealed class FloorJudge(LevelingThresholds thresholds, Func<DateTimeOffset>? clock = null, IReadOnlyCollection<ushort>? listed = null)
{
    sealed class Engagement(ushort sprite, DateTimeOffset start)
    {
        public ushort Sprite { get; } = sprite;
        public DateTimeOffset Start { get; } = start;
        public double Damage
        {
            get; set;
        }
        public bool Crowded
        {
            get; set;
        }
    }

    // Experience and the monster's removal arrive within this window of each other.
    static readonly TimeSpan pairing = TimeSpan.FromSeconds(1.5);
    readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    readonly object gate = new();
    readonly Dictionary<uint, Engagement> engaged = [];
    readonly List<(uint Id, Engagement E, DateTimeOffset At)> removed = [];
    readonly List<KillSample> kills = [];
    // Times a monster of each type walked up to her while she stood still.
    readonly Dictionary<ushort, int> approaches = [];
    // Types known too hard before this judge started (saved from earlier runs).
    readonly HashSet<ushort> avoided = [];
    readonly List<(DateTimeOffset At, ushort Sprite, double Lost)> hits = [];
    CharacterSnapshot? previous;
    DateTimeOffset? expAt, surroundedSince, lastHit;

    public IReadOnlyList<KillSample> Kills
    {
        get
        {
            lock (gate)
                return kills.ToArray();
        }
    }

    public event Action<KillSample>? Killed;

    // Saved aggressive types count from the start, so passive monsters can't vouch for them.
    public void Seed(IEnumerable<ushort> aggressive, IEnumerable<ushort> tooHard)
    {
        lock (gate)
        {
            foreach (var a in aggressive)
                approaches[a] = Math.Max(approaches.GetValueOrDefault(a), thresholds.AggressiveApproaches);
            avoided.UnionWith(tooHard);
        }
    }

    public void Add(KillSample sample)
    {
        lock (gate)
            kills.Add(sample);
    }

    bool Monster(Entity e) => !e.IsPlayer && e.Kind == 0 && (listed is null || listed.Contains(e.Sprite));
    static int Distance(Tile a, Tile b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    static bool Adjacent(Tile a, Tile b) => Distance(a, b) == 1;

    public void Observe(CharacterSnapshot s)
    {
        KillSample[] done;
        lock (gate)
            done = ObserveLocked(s);
        foreach (var k in done)
            Killed?.Invoke(k);
    }

    KillSample[] ObserveLocked(CharacterSnapshot s)
    {
        var t = now();
        var before = previous;
        previous = s;
        if (before is null || s.Position is not { } here || s.Map != before.Map)
        {
            engaged.Clear();
            removed.Clear();
            surroundedSince = null;
            return [];
        }

        var adjacent = s.Entities.Values.Where(e => Monster(e) && Adjacent(e.Position, here)).ToList();
        if (before.Position == here)
            foreach (var e in adjacent)
                if (before.Entities.TryGetValue(e.Id, out var earlier) && !Adjacent(earlier.Position, here) && earlier.Position != e.Position)
                    approaches[e.Sprite] = approaches.GetValueOrDefault(e.Sprite) + 1;
        foreach (var e in adjacent)
            if (!engaged.ContainsKey(e.Id))
                engaged[e.Id] = new(e.Sprite, t);
        if (s.Hp is { } hp && before.Hp is { } was && hp < was && s.MaxHp is { } max && max > 0)
        {
            lastHit = t;
            double lost = (was - hp) * 100.0 / max;
            foreach (var e in adjacent)
            {
                var g = engaged[e.Id];
                g.Damage += lost;
                hits.Add((t, e.Sprite, lost));
                if (adjacent.Count > 1)
                    g.Crowded = true;
            }

            if (adjacent.Count == 0)
            {
                var live = engaged.Where(g => s.Entities.ContainsKey(g.Key)).Select(g => g.Value).ToList();
                foreach (var g in live)
                    g.Damage += lost;
                var caster = s.Entities.Values.Where(e => Monster(e) && Distance(e.Position, here) <= thresholds.RangedReach).OrderBy(e => Distance(e.Position, here)).FirstOrDefault();
                if (caster is not null)
                    hits.Add((t, caster.Sprite, lost));
                else
                    foreach (var g in live)
                        hits.Add((t, g.Sprite, lost));
            }

            hits.RemoveAll(h => t - h.At > thresholds.CulpritWindow);
        }

        if (adjacent.Count >= thresholds.SurroundedCount)
            surroundedSince ??= t;
        else
            surroundedSince = null;
        foreach (var id in engaged.Keys.Where(id => !s.Entities.ContainsKey(id)).ToList())
        {
            removed.Add((id, engaged[id], t));
            engaged.Remove(id);
        }

        if (s.Experience is { } xp && before.Experience is { } xpBefore && xp > xpBefore)
            expAt = t;
        var finished = new List<KillSample>();
        if (expAt is { } gained)
            foreach (var r in removed.Where(r => (r.At - gained).Duration() <= pairing).ToList())
            {
                var sample = new KillSample(r.Id, r.E.Sprite, r.E.Damage, (r.At - r.E.Start).TotalSeconds, !r.E.Crowded);
                kills.Add(sample);
                finished.Add(sample);
                removed.Remove(r);
                expAt = null;
            }

        // A monster that left without experience walked away; it is not a kill.
        removed.RemoveAll(r => t - r.At > pairing);
        return [.. finished];
    }

    // Three or more monsters adjacent for a few seconds while health is still dropping.
    public bool Surrounded
    {
        get
        {
            lock (gate)
            {
                var t = now();
                return surroundedSince is { } since && t - since >= thresholds.SurroundedFor && lastHit is { } hit && t - hit <= thresholds.SurroundedFor;
            }
        }
    }

    public IReadOnlyCollection<ushort> Aggressive
    {
        get
        {
            lock (gate)
                return approaches.Where(a => a.Value >= thresholds.AggressiveApproaches).Select(a => a.Key).ToArray();
        }
    }

    // The type that took the most health in the window before the last hit; survives the walk out.
    public ushort? Culprit
    {
        get
        {
            lock (gate)
            {
                if (lastHit is not { } last)
                    return null;
                var worst = hits.Where(h => last - h.At <= thresholds.CulpritWindow).GroupBy(h => h.Sprite).OrderByDescending(g => g.Sum(h => h.Lost)).FirstOrDefault();
                return worst?.Key;
            }
        }
    }

    // Types to leave alone: judged too hard on their own 1v1 kills, or saved as too hard.
    public IReadOnlyCollection<ushort> Avoided
    {
        get
        {
            lock (gate)
                return AvoidedLocked(Judged()).ToArray();
        }
    }

    HashSet<ushort> AvoidedLocked(Dictionary<ushort, List<KillSample>> judged) => [.. avoided, .. judged.Where(j => VerdictOf(j.Value) == FloorVerdict.TooHard).Select(j => j.Key)];

    FloorVerdict VerdictOf(List<KillSample> recent)
    {
        double cost = recent.Average(k => k.CostPercent), seconds = recent.Average(k => k.Seconds);
        if (cost > thresholds.TooHardPercent || seconds > thresholds.TooSlowSeconds)
            return FloorVerdict.TooHard;
        return cost < thresholds.EasyPercent && seconds < thresholds.FastSeconds ? FloorVerdict.Easy : FloorVerdict.Fine;
    }

    Dictionary<ushort, List<KillSample>> Judged() => kills.Where(k => k.OneOnOne).GroupBy(k => k.Sprite).Select(g => (g.Key, Recent: g.TakeLast(thresholds.Samples).ToList())).Where(g => g.Recent.Count >= thresholds.Samples).ToDictionary(g => g.Key, g => g.Recent);

    // The floor is judged on the types still fought: too hard only when every judged type is too hard.
    // Passive monsters never vouch for aggressive ones, and a floor with an avoided type is never easy.
    public FloorVerdict Verdict
    {
        get
        {
            lock (gate)
            {
                var judged = Judged();
                var skip = AvoidedLocked(judged);
                var verdicts = judged.Where(j => !skip.Contains(j.Key)).Select(j => VerdictOf(j.Value)).ToList();
                if (verdicts.Count == 0)
                    return judged.Count > 0 ? FloorVerdict.TooHard : FloorVerdict.Undecided;
                if (approaches.Any(a => a.Value >= thresholds.AggressiveApproaches && !judged.ContainsKey(a.Key) && !skip.Contains(a.Key)))
                    return FloorVerdict.Undecided;
                return skip.Count > 0 || verdicts.Contains(FloorVerdict.Fine) ? FloorVerdict.Fine : FloorVerdict.Easy;
            }
        }
    }

    public string Describe()
    {
        lock (gate)
        {
            var judged = Judged();
            var skip = AvoidedLocked(judged);
            var unjudged = approaches.Where(a => a.Value >= thresholds.AggressiveApproaches && !judged.ContainsKey(a.Key) && !skip.Contains(a.Key)).Select(a => a.Key).ToList();
            if (judged.Count == 0 && unjudged.Count == 0 && skip.Count == 0)
                return "no monster type has " + thresholds.Samples + " 1v1 kills yet";
            var parts = judged.Select(j => "sprite " + j.Key + " 1v1 cost " + j.Value.Average(k => k.CostPercent).ToString("0") + "% and " + j.Value.Average(k => k.Seconds).ToString("0.0") + " s").ToList();
            parts.AddRange(unjudged.Select(u => "aggressive sprite " + u + " not judged yet"));
            parts.AddRange(skip.Order().Select(a => "sprite " + a + " avoided"));
            return string.Join("; ", parts);
        }
    }
}
