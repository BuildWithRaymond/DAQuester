namespace DAQuester.Core;

public enum Stat : byte
{
    Str = 1,
    Dex = 2,
    Int = 4,
    Wis = 8,
    Con = 16
}

// Undecided-class stat paths from docs/research/stat-plan-open-path.md; each step raises a stat to a target.
public static class StatPaths
{
    // Best path: keeps every class plan open as long as possible; points after it are saved for the class choice.
    public static readonly IReadOnlyList<(Stat Stat, byte Target)> Best = [(Stat.Con, 8), (Stat.Wis, 8), (Stat.Con, 22), (Stat.Wis, 40)];
    // Strict path: only what every stat plan spends anyway (CON 13, WIS 13 by level 11); later points are saved.
    public static readonly IReadOnlyList<(Stat Stat, byte Target)> Strict = [(Stat.Con, 8), (Stat.Wis, 8), (Stat.Con, 13), (Stat.Wis, 13)];

    public static byte Value(CharacterStats s, Stat stat) => stat switch
    {
        Stat.Str => s.Str,
        Stat.Dex => s.Dex,
        Stat.Int => s.Int,
        Stat.Wis => s.Wis,
        _ => s.Con
    };

    // The first step whose target isn't reached yet; null once the path is done.
    public static Stat? Next(CharacterStats s, IReadOnlyList<(Stat Stat, byte Target)> path)
    {
        foreach (var (stat, target) in path)
            if (Value(s, stat) < target)
                return stat;
        return null;
    }
}

// Spends unspent points along a path, one at a time, each confirmed by the server's stat block.
// A raise the server answers with any other change stops spending for good: a wrong point can't be undone.
public sealed class StatSpender(IGameSession session, IReadOnlyList<(Stat Stat, byte Target)> path, Action<string>? note = null)
{
    static readonly TimeSpan confirm = TimeSpan.FromSeconds(3);
    bool stopped, doneNoted;

    public bool Stopped => stopped;

    public async Task SpendAsync(CancellationToken ct)
    {
        while (!stopped)
        {
            var s = session.Snapshot;
            if (s.Stats is not { Unspent: > 0 } stats)
                return;
            if (StatPaths.Next(stats, path) is not { } stat)
            {
                if (!doneNoted)
                    note?.Invoke("Stat path done; " + stats.Unspent + " points saved for the class choice.");
                doneNoted = true;
                return;
            }

            var expected = Raised(stats, stat);
            await session.ExecuteAsync(new("raise-stat", Amount: (uint)stat), ct);
            CharacterStats? after;
            try
            {
                after = (await session.WaitAsync(x => x.Stats is { } n && n != stats, s.Revision, confirm, ct)).Stats;
            }
            catch (TimeoutException)
            {
                after = null;
            }

            if (after != expected)
            {
                stopped = true;
                note?.Invoke("Stat spending stopped: raising " + stat + " from " + Describe(stats) + " gave " + (after is null ? "no answer" : Describe(after)) + ".");
                return;
            }

            note?.Invoke("Raised " + stat + " to " + StatPaths.Value(after, stat) + " (" + after.Unspent + " unspent)");
        }
    }

    static CharacterStats Raised(CharacterStats s, Stat stat) => stat switch
    {
        Stat.Str => s with { Str = (byte)(s.Str + 1), Unspent = (byte)(s.Unspent - 1) },
        Stat.Dex => s with { Dex = (byte)(s.Dex + 1), Unspent = (byte)(s.Unspent - 1) },
        Stat.Int => s with { Int = (byte)(s.Int + 1), Unspent = (byte)(s.Unspent - 1) },
        Stat.Wis => s with { Wis = (byte)(s.Wis + 1), Unspent = (byte)(s.Unspent - 1) },
        _ => s with { Con = (byte)(s.Con + 1), Unspent = (byte)(s.Unspent - 1) }
    };

    static string Describe(CharacterStats s) => "STR " + s.Str + " INT " + s.Int + " WIS " + s.Wis + " CON " + s.Con + " DEX " + s.Dex + ", " + s.Unspent + " unspent";
}
