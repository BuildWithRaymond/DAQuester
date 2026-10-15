namespace DAQuester.Core;

public static class NpcVisibility
{
    public const int ScreenRange = 10;

    public static Entity? Find(CharacterSnapshot snapshot, int map, string name)
    {
        if (!snapshot.Connected || snapshot.Map != map || snapshot.Position is not { } position)
            return null;
        // The entity cache can retain mundanes beyond the drawn screen. A named
        // cached actor alone does not establish a usable interaction position.
        var matches = snapshot.Entities.Values.Where(e => !e.IsPlayer &&
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length == 1 && Math.Abs(matches[0].Position.X - position.X) +
            Math.Abs(matches[0].Position.Y - position.Y) <= ScreenRange ? matches[0] : null;
    }
}
