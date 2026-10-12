using System.Globalization;
namespace DAQuester.Core;

public record AltarOffering(DateTimeOffset At, long Generation, uint Paid, string Experience, int RingsBefore);

public static class AltarWorshipEvidence
{
    public const string Id = "altar-worship", CatalogId = "altarworship", QuestName = "Altar Worship";
    public const string Legend = "Mileth Altar Worshipper", Torrance = "Torrance", Buy = "Buy", Shield = "Leather Shield";
    // Every offering is answered with this exact line (live 2026-10-12, Ferda).
    public const string Touch = "You feel a touch of light around you";
    public const int Goal = 10, ShopMap = 133, Map = 500, Reach = 3;
    public const uint ShieldPrice = 750;
    public static readonly Tile Altar = new(31, 52), Stand = new(33, 53);
    // The server takes offerings back to back; Vorlof's tested spacing is 3 hours.
    public static readonly TimeSpan Interval = TimeSpan.FromHours(3);
    public static readonly string[] Rings = ["Red Jade Ring", "Jade Ring", "Amethyst Ring"];

    public static bool IsPriest(string? characterClass) => string.Equals(characterClass, "Priest", StringComparison.OrdinalIgnoreCase);
    public static int Made(QuestJob job) => job.AltarOfferings.Length;
    public static int RingCount(CharacterSnapshot s) => Rings.Sum(s.Count);
    public static DateTimeOffset? NextOffering(QuestJob job) => Made(job) is > 0 and < Goal ? job.AltarOfferings[^1].At + Interval : null;
    public static string? Mark(IEnumerable<string> legends) => legends.FirstOrDefault(l => l.Contains(Legend, StringComparison.OrdinalIgnoreCase));
    public static bool IsNotice(ServerNotice notice, int? map) => notice.Type == 3 && map == Map && (notice.Text == Touch || SickChildEvidence.Experience(notice.Text) is not null);
    public static bool InReach(Tile? position) => position is not null && Math.Abs(position.X - Altar.X) + Math.Abs(position.Y - Altar.Y) <= Reach;
    public static string? NoMark(string character, string? characterClass) => characterClass is null || IsPriest(characterClass) ? null : character + " is a " + characterClass + " and won't get the legend mark. Only Priests earn \"" + Legend + "\".";
    public static string Rewards(string? characterClass) => IsPriest(characterClass) ? "\"" + Legend + "\", a Jade or Amethyst Ring and 100,000 experience" : "a Red Jade Ring and 50,000 experience";

    public static string Progress(QuestJob job)
    {
        int made = Made(job);
        if (made == 0)
            return "10 offerings, 3 hours apart · " + ShieldPrice.ToString("N0", CultureInfo.InvariantCulture) + " gold each";
        if (made >= Goal)
            return "10 of 10 offerings made";
        return "Offering " + made + " of 10 · next offering " + NextOffering(job)!.Value.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.CurrentCulture);
    }

    public static Eligibility Eligibility(CharacterSnapshot s, QuestJob job)
    {
        if (!s.Connected || s.Name is null || s.Id is null || s.Level is null || s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for connected identity, level and health.");
        if (s.Hp == 0)
            return new(EligibilityKind.Blocked, "Altar Worship requires a living character.");
        if (Made(job) >= Goal)
            return new(EligibilityKind.Eligible, "10 of 10 offerings made; checking the reward");
        if (!s.InventoryKnown || s.Gold is null)
            return new(EligibilityKind.Unknown, "Waiting for inventory and gold.");
        if (s.Count(Shield) == 0)
        {
            if (s.Gold < ShieldPrice)
                return new(EligibilityKind.Blocked, "Carry at least 750 gold for a Leather Shield from Torrance.");
            if (s.Inventory.Count >= 59)
                return new(EligibilityKind.Blocked, "Free an inventory slot for a Leather Shield.");
        }
        return new(EligibilityKind.Eligible, Progress(job));
    }
}
