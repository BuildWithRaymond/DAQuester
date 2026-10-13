namespace DAQuester.Core;

// A floor split into four quarters, tracking time spent and aggressive monsters seen in each, so escapes and
// roaming head for the part of the floor least covered and quietest.
public sealed class FloorQuarters
{
    // A quarter counts as tried after this long in it.
    public static readonly TimeSpan Tried = TimeSpan.FromSeconds(20);
    readonly double[] seconds = new double[4];
    readonly HashSet<uint>[] aggressive = [[], [], [], []];
    DateTimeOffset? lastAt;

    public static int Of(Tile t, (int Width, int Height) size) => (t.X < size.Width / 2 ? 0 : 1) + (t.Y < size.Height / 2 ? 0 : 2);

    public static (int X0, int Y0, int X1, int Y1) Bounds(int quarter, (int Width, int Height) size)
    {
        int hx = size.Width / 2, hy = size.Height / 2;
        return quarter switch
        {
            0 => (1, 1, hx - 1, hy - 1),
            1 => (hx, 1, size.Width - 2, hy - 1),
            2 => (1, hy, hx - 1, size.Height - 2),
            _ => (hx, hy, size.Width - 2, size.Height - 2)
        };
    }

    public double Seconds(int quarter) => seconds[quarter];
    public int AggressiveSeen(int quarter) => aggressive[quarter].Count;
    public bool AllTried => seconds.All(s => s >= Tried.TotalSeconds);

    public void Observe(CharacterSnapshot s, (int Width, int Height) size, IReadOnlyCollection<ushort> aggressiveSprites, DateTimeOffset now)
    {
        if (s.Position is not { } here)
            return;
        int q = Of(here, size);
        if (lastAt is { } last)
            seconds[q] += Math.Min((now - last).TotalSeconds, 5);
        lastAt = now;
        foreach (var e in s.Entities.Values)
            if (!e.IsPlayer && e.Kind == 0 && aggressiveSprites.Contains(e.Sprite))
                aggressive[Of(e.Position, size)].Add(e.Id);
    }

    public void Pause() => lastAt = null;

    // Quarters ordered quietest and least covered first.
    public IEnumerable<int> Preferred() => Enumerable.Range(0, 4).OrderBy(q => aggressive[q].Count).ThenBy(q => seconds[q]);
}
