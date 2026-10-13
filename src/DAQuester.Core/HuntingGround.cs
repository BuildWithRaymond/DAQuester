namespace DAQuester.Core;

// One floor of a hunting ground: where the stairs down into it land, and where the stairs up from the next floor land.
public sealed record HuntingFloor(int Map, Tile DownArrival, Tile? UpArrival);

// What the character has learned about one hunting ground, kept between sessions.
public sealed class HuntingProgress
{
    // Map -> highest level the stairs into it refused.
    public Dictionary<int, int> RefusedAt { get; init; } = [];
    // Map -> lowest level the stairs into it let through.
    public Dictionary<int, int> AllowedAt { get; init; } = [];
    // Map -> highest level its 1v1 fights were judged too hard.
    public Dictionary<int, int> TooHardAt { get; init; } = [];

    // Map -> lowest level its 1v1 fights were judged fine or easy.
    public Dictionary<int, int> SafeAt { get; init; } = [];
    // "map@level" -> the 1v1 kills behind each verdict.
    public Dictionary<string, FloorLevelStats> Fights { get; init; } = [];

    public void Record(int map, int level, KillSample kill)
    {
        string key = map + "@" + level;
        var f = Fights.GetValueOrDefault(key) ?? new();
        Fights[key] = f with
        {
            Kills = f.Kills + 1,
            CostPercent = f.CostPercent + kill.CostPercent,
            Seconds = f.Seconds + kill.Seconds
        };
        string type = key + "#" + kill.Sprite;
        var m = Monsters.GetValueOrDefault(type) ?? new();
        Monsters[type] = m with
        {
            Kills = m.Kills + 1,
            CostPercent = m.CostPercent + kill.CostPercent,
            Seconds = m.Seconds + kill.Seconds
        };
    }

    // "map@level#sprite" -> 1v1 kills per monster type.
    public Dictionary<string, FloorLevelStats> Monsters { get; init; } = [];
    // Map -> monster types seen walking up to her there.
    public Dictionary<int, List<ushort>> Aggressive { get; init; } = [];
    // "map#sprite" -> highest level that monster type was too hard there; she leaves it alone at or below it.
    public Dictionary<string, int> TooHardMonsters { get; init; } = [];

    public void MarkMonsterTooHard(int map, int level, ushort sprite)
    {
        string key = map + "#" + sprite;
        TooHardMonsters[key] = Math.Max(TooHardMonsters.GetValueOrDefault(key), level);
        if (SafeAt.TryGetValue(map, out int safe) && safe <= level)
            SafeAt.Remove(map);
    }

    public IReadOnlyCollection<ushort> AvoidAt(int map, int level) =>
        TooHardMonsters.Where(t => t.Value >= level && t.Key.StartsWith(map + "#", StringComparison.Ordinal)).Select(t => ushort.Parse(t.Key[(t.Key.IndexOf('#') + 1)..])).ToArray();

    // A too-hard verdict at a level overrules any safe mark at or below it.
    public void MarkTooHard(int map, int level)
    {
        TooHardAt[map] = Math.Max(TooHardAt.GetValueOrDefault(map), level);
        if (SafeAt.TryGetValue(map, out int safe) && safe <= level)
            SafeAt.Remove(map);
    }

    public FloorLevelStats At(int map, int level) => Fights.GetValueOrDefault(map + "@" + level) ?? new();
    public void Set(int map, int level, FloorLevelStats stats) => Fights[map + "@" + level] = stats;

    public bool Allowed(int map, int level) => AllowedAt.TryGetValue(map, out int at) && at <= level;
    public bool Refused(int map, int level) => !Allowed(map, level) && RefusedAt.TryGetValue(map, out int at) && at >= level;
    public bool TooHard(int map, int level) => TooHardAt.TryGetValue(map, out int at) && at >= level;
}

// 1v1 kills, plus all health lost on the floor (kills or not), time spent there and walk-outs.
public sealed record FloorLevelStats(int Kills = 0, double CostPercent = 0, double Seconds = 0, double HealthLost = 0, double FloorSeconds = 0, int WalkOuts = 0)
{
    public double AverageCost => Kills == 0 ? 0 : CostPercent / Kills;
    public double AverageSeconds => Kills == 0 ? 0 : Seconds / Kills;
    public double LostPerMinute => FloorSeconds < 1 ? 0 : HealthLost / (FloorSeconds / 60);
}

// A chain of floors joined by stairs, and the only monster sprites the engine may attack there.
public sealed class HuntingGround(string id, string name, string firstFloorName, IReadOnlyList<HuntingFloor> floors, IReadOnlyCollection<ushort> monsters)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string FirstFloorName { get; } = firstFloorName;
    public IReadOnlyList<HuntingFloor> Floors { get; } = floors;
    // Anything else, such as a player's pet, is never touched.
    public IReadOnlyCollection<ushort> Monsters { get; } = monsters;

    public int FloorOf(int? map)
    {
        for (int i = 0; i < Floors.Count; i++)
            if (Floors[i].Map == map)
                return i;
        return -1;
    }

    // Coming back from town: the deepest floor already let through at this level and not judged too hard.
    public int Entry(HuntingProgress p, int level)
    {
        int floor = 0;
        while (floor + 1 < Floors.Count && p.Allowed(Floors[floor + 1].Map, level) && !p.TooHard(Floors[floor + 1].Map, level))
            floor++;
        return floor;
    }

    // The nearest floor above that isn't also judged too hard at this level; null when none is left.
    public int? Up(HuntingProgress p, int floor, int level)
    {
        for (int f = floor - 1; f >= 0; f--)
            if (!p.TooHard(Floors[f].Map, level))
                return f;
        return null;
    }

    // One row per floor: gate, too-hard and safe levels, and the 1v1 numbers per level.
    public string Report(string character, HuntingProgress p)
    {
        static string At(Dictionary<int, int> d, int map) => d.TryGetValue(map, out int l) ? l.ToString() : "-";
        var lines = new List<string>
        {
            "# " + Name + ": " + character,
            "",
            "Safe = 1v1 kills judged fine or easy. 1v1 = no hit taken with two or more monsters beside her.",
            "",
            "| Map | Gate refused up to | Gate let through at | Too hard up to | Safe from level |",
            "| --- | --- | --- | --- | --- |"
        };
        foreach (var f in Floors)
            lines.Add("| " + f.Map + " | " + At(p.RefusedAt, f.Map) + " | " + At(p.AllowedAt, f.Map) + " | " + At(p.TooHardAt, f.Map) + " | " + At(p.SafeAt, f.Map) + " |");
        lines.AddRange(["", "| Map | Level | 1v1 kills | Avg health cost | Avg kill time | Time on floor | Health lost per minute | Walk-outs |", "| --- | --- | --- | --- | --- | --- | --- | --- |"]);
        foreach (var (key, s) in p.Fights.OrderBy(k => int.Parse(k.Key.Split('@')[0])).ThenBy(k => int.Parse(k.Key.Split('@')[1])))
        {
            var parts = key.Split('@');
            lines.Add("| " + parts[0] + " | " + parts[1] + " | " + s.Kills + " | " + s.AverageCost.ToString("0.0") + "% | " + s.AverageSeconds.ToString("0.0") + " s | " + (s.FloorSeconds / 60).ToString("0.0") + " min | " + s.LostPerMinute.ToString("0.0") + "% | " + s.WalkOuts + " |");
        }

        lines.AddRange(["", "| Map | Level | Monster sprite | Aggressive | Avoided | 1v1 kills | Avg health cost | Avg kill time |", "| --- | --- | --- | --- | --- | --- | --- | --- |"]);
        foreach (var (key, s) in p.Monsters.OrderBy(k => k.Key))
        {
            var parts = key.Split('@', '#');
            int m = int.Parse(parts[0]), l = int.Parse(parts[1]);
            var sprite = ushort.Parse(parts[2]);
            bool aggressive = p.Aggressive.TryGetValue(m, out var list) && list.Contains(sprite);
            bool avoided = p.AvoidAt(m, l).Contains(sprite);
            lines.Add("| " + parts[0] + " | " + parts[1] + " | " + parts[2] + " | " + (aggressive ? "yes" : "no") + " | " + (avoided ? "yes" : "no") + " | " + s.Kills + " | " + s.AverageCost.ToString("0.0") + "% | " + s.AverageSeconds.ToString("0.0") + " s |");
        }

        if (p.TooHardMonsters.Count > 0)
        {
            lines.AddRange(["", "| Map | Monster sprite | Too hard up to level |", "| --- | --- | --- |"]);
            foreach (var (key, level) in p.TooHardMonsters.OrderBy(k => k.Key))
                lines.Add("| " + key.Replace("#", " | ") + " | " + level + " |");
        }

        return string.Join("\n", lines) + "\n";
    }

    // Deeper only after a level-up, only when this floor's 1v1 fights are easy, and never into a known refusal.
    public bool TryDown(HuntingProgress p, int floor, int level, FloorVerdict verdict) =>
        verdict == FloorVerdict.Easy && floor + 1 < Floors.Count && !p.Refused(Floors[floor + 1].Map, level) && !p.TooHard(Floors[floor + 1].Map, level);
}


public static class HuntingGrounds
{
    // vorlof.com Mileth Crypt monsters: Mouse, Spider, Centipede, Bat, Scorpion, Great Bat, Chest, White Bat, Kardi,
    // Marauder, Succubus, Wraith. Floors 1-1, 2-1, 3-1, 4-1, 5-1, 6-1, 7-1.
    public static readonly HuntingGround MilethCrypt = new("mileth-crypt", "Mileth Crypt", "Crypt 1-1",
    [
        new(1, new(13, 48), new(2, 6)),
        new(2, new(44, 23), new(12, 21)),
        new(5, new(1, 31), new(30, 43)),
        new(8, new(6, 1), new(5, 41)),
        new(11, new(13, 1), new(5, 28)),
        new(13, new(43, 10), new(21, 17)),
        new(16, new(13, 1), null)
    ], [7, 53, 52, 47, 55, 46, 90, 48, 5, 6, 13, 68]);
}
