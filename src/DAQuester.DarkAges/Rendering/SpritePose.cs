using DAQuester.Core;

namespace DAQuester.DarkAges.Rendering;
public sealed record SpritePose(string Suffix, int Frame, bool Flip, bool Front, int EmoteFrame = -1)
{
    // Original client EPFs store up/right poses; down/left mirror those poses.
    public static SpritePose From(CharacterMotion? motion, DateTimeOffset now)
    {
        bool front = motion?.Direction is 1 or 2;
        bool flip = motion?.Direction is 2 or 3;
        double elapsed = Math.Max(0, (now - (motion?.StartedAt ?? now)).TotalMilliseconds);
        int idle = front ? 5 : 0;
        if (motion?.Walking == true && elapsed < motion.DurationMilliseconds)
            return new("01", idle + 1 + Math.Min(3, (int)(elapsed / Math.Max(1, motion.DurationMilliseconds / 4d))), flip, front);
        var(suffix, count, up, right) = Animation(motion?.Animation ?? 0);
        double interval = motion?.Animation switch
        {
            21 => 500,
            22 => 343.75,
            _ => Math.Max(150, motion?.DurationMilliseconds ?? 0)};
        int repeats = motion?.Animation == 22 ? 2 : 1;
        int emote = Emote(motion?.Animation ?? 0, elapsed);
        if (count > 0 && elapsed < count * interval * repeats)
            return new(suffix, (front ? right : up) + (int)(elapsed / interval) % count, flip, front, emote);
        return new("04", (int)(now.ToUnixTimeMilliseconds() / 300 % 10000), flip, front, emote);
    }

    static (string Suffix, int Count, int Up, int Right) Animation(byte animation) => animation switch
    {
        1 => ("02", 2, 0, 2),
        6 or 138 => ("03", 1, 0, 1),
        21 => ("03", 2, 2, 4),
        22 => ("03", 2, 6, 8),
        128 => ("b", 3, 0, 3),
        137 => ("b", 3, 6, 9),
        129 => ("c", 4, 0, 4),
        141 => ("c", 3, 8, 11),
        139 => ("c", 2, 14, 16),
        140 => ("c", 3, 18, 21),
        130 => ("c", 3, 24, 27),
        131 => ("d", 3, 0, 3),
        132 => ("d", 2, 6, 8),
        133 => ("d", 4, 10, 14),
        134 => ("e", 2, 0, 2),
        135 => ("e", 2, 4, 6),
        142 => ("e", 4, 8, 12),
        143 => ("e", 6, 16, 22),
        144 => ("e", 4, 28, 32),
        136 => ("f", 2, 0, 2),
        145 => ("f", 4, 4, 8),
        _ => ("01", 0, 0, 0)};
    static int Emote(byte action, double elapsed)
    {
        if (action is 9 or 10 or 11 or 12 or 13 or 14 or 15)
            return elapsed < 1500 ? action - 9 : -1;
        if (action is 16 or 17)
            return elapsed < 2000 ? (action == 16 ? 7 : 9) + (int)(elapsed / 1000) : -1;
        if (action is >= 23 and <= 44)
        {
            int start = action switch
            {
                23 => 11,
                24 => 12,
                25 => 13,
                26 => 14,
                27 => 15,
                >= 28 and <= 41 => action - 10,
                42 => 32,
                43 => 35,
                _ => 38
            };
            int count = action is 27 or 42 or 43 ? 3 : action == 44 ? 4 : 1;
            int duration = action == 44 ? 2000 : 1500;
            return elapsed < duration ? start + Math.Min(count - 1, (int)(elapsed * count / duration)) : -1;
        }

        return -1;
    }
}
