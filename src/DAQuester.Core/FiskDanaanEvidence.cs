namespace DAQuester.Core;

public static class FiskEvidence
{
    public const string Id = "fisk-secret", CatalogId = "fisksecret", Legend = "Kept Fisk's Secret";
    public const string BouquetAccepted = "Will do. Return to me in one hour and I shall have it ready for you.";
    public const string BouquetNotReady = "I can't rush perfection. Come back in a bit.";
    public const string Bouquet = "Special Bouquet";
    public static bool IsNotice(ServerNotice n, int? map) => map == 415 && n.Type == 3 &&
        n.Text is "750000 experience!" or "You gain 750,000 experience!";
    public static string? Mark(CharacterSnapshot s) => s.Legends.FirstOrDefault(l =>
        l == Legend || l.StartsWith(Legend + " -", StringComparison.OrdinalIgnoreCase));
    public static bool CanDefer(QuestJob j) => j.Quest == Id && j.Status == "Deferred" &&
        j.Pending is null && j.FiskWait is { Verified: true } wait && j.NextRun >= wait.Due;
    public static Eligibility Eligibility(CharacterSnapshot s, QuestJob j) =>
        ObservedQuestEvidence.Eligibility(s, 21, "Fisk's Secret", j);
}

public static class DanaanEvidence
{
    public const string Id = "cap-band-of-danaan";
    public static bool IsNotice(ServerNotice n, int? map) => map == 345 && n.Type == 3 &&
        n.Text is "1000000 experience!" or "You gain 1,000,000 experience!";
    public static string? Mark(CharacterSnapshot s) => s.Legends.FirstOrDefault(l =>
        l == "Worthy of the Cap of Danaan" || l == "Worthy of the Band of Danaan" ||
        l.StartsWith("Worthy of the Cap of Danaan,", StringComparison.OrdinalIgnoreCase) ||
        l.StartsWith("Worthy of the Band of Danaan,", StringComparison.OrdinalIgnoreCase));
    public static bool ChoicesLocked(QuestJob j) => j.Pending is not null || j.Route?.Task > 0 || j.CompletedAt is not null;
    public static Eligibility Eligibility(CharacterSnapshot s, QuestJob j) =>
        ObservedQuestEvidence.Eligibility(s, 50, "Cap / Band of Danaan", j);
}

static class ObservedQuestEvidence
{
    internal static Eligibility Eligibility(CharacterSnapshot s, int guideMinimum, string name, QuestJob j)
    {
        if (!s.Connected || s.Id is null || s.Name is null || s.Generation < 1)
            return new(EligibilityKind.Unknown, "Waiting for connected character identity.");
        if (s.Hp is null || s.Level is null || s.Experience is null || !s.InventoryKnown || !s.LegendsKnown || s.Gold is null || s.Weight is null || s.MaxWeight is null)
            return new(EligibilityKind.Unknown, "Waiting for own level, health, inventory, gold and legend.");
        if (s.Hp == 0)
            return new(EligibilityKind.Blocked, "Requires a living character.");
        // These level limits come from guides. Preserve that uncertainty rather
        // than treating a synthetic eligible snapshot as lower-level live evidence.
        if (s.Level < guideMinimum)
            return new(EligibilityKind.Blocked, name + " guide lists level " + guideMinimum + "+; lower-level eligibility is not verified.");
        if (s.Inventory.Count >= 59 || s.Weight >= s.MaxWeight)
            return new(EligibilityKind.Blocked, "Free carrying capacity required; no items are discarded automatically.");
        return new(EligibilityKind.Eligible, j.FiskWait is { Verified: true } wait ? "Bouquet accepted; check after " + wait.Due.ToLocalTime().ToString("g") : "Observed route ready; unexpected server variants pause for review.");
    }
}
