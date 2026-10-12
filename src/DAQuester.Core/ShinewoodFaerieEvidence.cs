namespace DAQuester.Core;

public record FaerieGoalReceipt(string Goal, long Generation, long Revision, DateTimeOffset VerifiedAt, Dialog? Reply);
public record FaerieReturnReceipt(string Method, long Generation, long Revision, int Map, Tile Position, DateTimeOffset VerifiedAt);
public record FaerieSpell(string Name, string Class, int Level = 1, bool Master = false, bool Pure = false);
public record FaerieEscape(string Kind, byte Slot, string Name);
public record FaerieExit(int SourceMap, Tile From, int Map, string Source);
public static class ShinewoodFaerieEvidence
{
    public const string Id = "shinewood-faerie", Pet = "Pet Faerie";
    public const int Map = 992, MinimumLevel = 11;
    public static readonly Tile Anchor = new(71, 37);
    public static readonly Tile Approach = new(73, 38); // Archived map 992 WalkSpot, read only.
    public static readonly FaerieSpell[] SpellRules = [new("deo lamh", "Priest", 11), new("creag neart", "Priest", 20), new("deo searg gar", "Priest", Master: true), new("deo searg", "Priest", Master: true, Pure: true), new("fas spiorad", "Wizard", Master: true), new("Wings of Protection", "Wizard", Master: true, Pure: true)];
    // Exact town items only. Recall/Teleport/dungeon songs require different targets.
    public static readonly string[] TownSongs = ["Mileth Song", "Abel Song", "Piet Song", "Rucesion Song", "Suomi Song", "Undine Song", "Loures Song", "Tagor Song", "Nation Song"];
    public static readonly int[] SafeMaps = [136, 150, 498, 500, 501, 502, 503, 504, 505, 950, 3008, 3010, 3012, 3014, 3020, 3082]; // Observed Rucesion Song landing.
    public static readonly FaerieExit[] ReturnExits = [new(992, new(38, 99), 547, "normalized WorldLogs"), new(547, new(29, 49), 546, "https://vorlof.com/map.php?map=547"), new(546, new(0, 9), 545, "https://vorlof.com/map.php?map=546"), new(545, new(0, 35), 544, "https://vorlof.com/map.php?map=545"), new(544, new(88, 99), 543, "normalized WorldLogs"), new(543, new(57, 99), 564, "normalized WorldLogs"), new(564, new(44, 99), 565, "normalized WorldLogs"), new(565, new(40, 99), 542, "normalized WorldLogs")];
    public static bool InForest(int? map) => map is >= 542 and <= 568 or >= 989 and <= 999;
    public static bool SafeArrival(CharacterSnapshot s) => s.Connected && s.Map is { } map && s.Position is not null && (SafeMaps.Contains(map) || s.MapName is { } name && new[]
    {
        "Mileth",
        "Abel",
        "Piet",
        "Rucesion",
        "Suomi",
        "Undine",
        "Loures",
        "Tagor"
    }.Any(t => name.Equals(t + " Inn", StringComparison.OrdinalIgnoreCase)));
    public static bool HasPet(CharacterSnapshot s) => s.InventoryKnown && s.Count(Pet) > 0 || s.Equipment.Values.Any(x => x.Equals(Pet, StringComparison.OrdinalIgnoreCase));
    public static bool SpellNameEquals(string observed, string name) => observed.Equals(name, StringComparison.OrdinalIgnoreCase) || observed.StartsWith(name, StringComparison.OrdinalIgnoreCase) && System.Text.RegularExpressions.Regex.IsMatch(observed[name.Length..], @"^ \(Lev:[0-9]+/[0-9]+\)\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    public static bool HasSpell(CharacterSnapshot s, string name) => s.SpellsKnown && s.Spells.Values.Any(x => SpellNameEquals(x, name));
    public static bool FaerieEntity(Entity e) => !e.IsPlayer && Math.Abs(e.Position.X - Anchor.X) + Math.Abs(e.Position.Y - Anchor.Y) <= 4 && (e.Name.Equals("Faerie", StringComparison.OrdinalIgnoreCase) || e.Name.Length == 0 && e.Position == Anchor);
    public static bool RecognizedFaeriePage(Dialog d) => d.Name.Equals("Faerie", StringComparison.OrdinalIgnoreCase) || d.Text.Contains("faerie", StringComparison.OrdinalIgnoreCase) || d.Choices.Any(x => PetChoice(x.Text) || SpellRules.Any(r => SpellChoice(x.Text, r.Name)));
    public static bool? Eligible(CharacterSnapshot s, FaerieSpell rule)
    {
        if (s.Class is not null && !s.Class.Equals(rule.Class, StringComparison.OrdinalIgnoreCase) || s.Level is { } level && level < rule.Level || rule.Master && s.IsMaster == false || rule.Pure && s.IsPure == false)
            return false;
        if (s.Class is null || s.Level is null || rule.Master && s.IsMaster is null || rule.Pure && s.IsPure is null)
            return null;
        return true;
    }

    public static string[] Goals(CharacterSnapshot s, QuestJob job) => (HasPet(s) || job.FaerieReceipts.Any(r => r.Goal == Pet) ? Array.Empty<string>() : new[]
    {
        Pet
    }

    ).Concat(SpellRules.Where(r => Eligible(s, r) != false && !HasSpell(s, r.Name)).Select(r => r.Name)).ToArray();
    public static FaerieEscape? Escape(CharacterSnapshot s)
    {
        if (!s.InventoryKnown || !s.SpellsKnown || s.Level is null)
            return null;
        foreach (var name in TownSongs)
        {
            var item = s.Inventory.Values.Where(i => i.Quantity > 0 && i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Slot).FirstOrDefault();
            if (item is not null)
                return new("use-item", item.Slot, item.Name);
        }

        var spell = s.Spells.OrderBy(p => p.Key).FirstOrDefault(p => SpellNameEquals(p.Value, "dachaidh"));
        return spell.Key != 0 && s.Mana >= 30 ? new("spell", spell.Key, "dachaidh") : null;
    }

    public static Eligibility Eligibility(CharacterSnapshot s, QuestJob job)
    {
        if (!s.Connected || s.Name is null || s.Id is null)
            return new(EligibilityKind.Unknown, "Waiting for connected character details.");
        if (s.Level is null || s.Hp is null || !s.InventoryKnown || !s.SpellsKnown)
            return new(EligibilityKind.Unknown, "Waiting for level, health, inventory and spellbook.");
        if (s.Level < MinimumLevel)
            return new(EligibilityKind.Blocked, "Requires level 11+ to pass Shinewood Forest 7 and reach the Faerie.");
        if (s.Hp == 0)
            return new(EligibilityKind.Blocked, "A living character is required.");
        if (!HasPet(s) && !job.FaerieReceipts.Any(r => r.Goal == Pet))
        {
            if (s.Weight is null || s.MaxWeight is null)
                return new(EligibilityKind.Unknown, "Waiting for inventory weight.");
            if (s.Inventory.Count >= 59 || s.MaxWeight - s.Weight < 5)
                return new(EligibilityKind.Blocked, "Free one inventory slot and five weight for your Pet Faerie.");
        }

        return new(EligibilityKind.Eligible, "Pet for every class; learn eligible missing spells. Bring town songs/scrolls or ready dachaidh for a quick escape.");
    }

    public static void RequireEscape(CharacterSnapshot s, QuestJob job)
    {
        if (Escape(s) is null && job.FaerieWalkAcknowledgedGeneration != s.Generation)
            throw new QuestBlockedException("No usable town escape: bring town songs/scrolls or ready dachaidh. Confirm the Shinewood danger warning before accepting a walking return.");
    }

    public static bool PetChoice(string text)
    {
        var v = text.Trim().TrimEnd('.', '!', '?');
        return (v.Contains("adopt", StringComparison.OrdinalIgnoreCase) || v.Contains("pet", StringComparison.OrdinalIgnoreCase)) && v.Contains("faerie", StringComparison.OrdinalIgnoreCase) && !v.Contains("release", StringComparison.OrdinalIgnoreCase) && !v.Contains("return", StringComparison.OrdinalIgnoreCase);
    }

    public static bool PetWaitPage(Dialog d) => d is { IsPursuit: true, EntityType: 4, HasNext: false, Prompt: null } && d.Choices.Length == 0 && d.Text.Trim().Equals("Sorry, no faeries are willing to bond with you. Come back in one deoch.", StringComparison.OrdinalIgnoreCase);
    public static bool NoncasterPage(Dialog d) => d is { IsPursuit: true, EntityType: 4, HasNext: false, Prompt: null } && d.Choices.Length == 0 && d.Text.Trim().Equals("I only know spells for the Priest or Wizard.", StringComparison.OrdinalIgnoreCase);
    public static bool SpellMenu(string text) => new[]
    {
        "Learn spells",
        "Learn a spell",
        "Learn some spells",
        "Teach me a spell",
        "Teach me spells",
        "Do you know any spells you can teach me"
    }.Any(x => text.Trim().TrimEnd('.', '!', '?').Equals(x, StringComparison.OrdinalIgnoreCase));
    public static bool SpellChoice(string text, string spell) => text.Trim().TrimEnd('.', '!', '?').Equals(spell, StringComparison.OrdinalIgnoreCase) || text.Trim().TrimEnd('.', '!', '?').Equals("Learn " + spell, StringComparison.OrdinalIgnoreCase);
}
