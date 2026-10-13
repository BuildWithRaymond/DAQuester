namespace DAQuester.Core;

// What the hunting engine needs from the app: progress per ground and character, own-character checks and a log.
public sealed record HuntingServices(Func<HuntingGround, string, HuntingProgress> Load, Action<HuntingGround, string, HuntingProgress> Save, Func<string, bool>? IsFriend = null, Action<string, string>? Log = null);

// One starter purchase: the level it needs, the equipment slot it's worn in, and who sells it.
public sealed record StarterGear(string Name, int Level, byte Slot, string Town, int Map, string Npc, uint Price);

// Peasant's First Steps (starter gear, levels to 7, ends with a Loures Saber) and Crypt Climb (crypt to 11).
public static class NewCharacterPath
{
    public const string FirstStepsId = "peasants-first-steps", ClimbId = "crypt-climb";
    public const string FirstStepsName = "Peasant's First Steps", ClimbName = "Crypt Climb";
    public const int FirstStepsLevel = 7, ClimbLevel = 11, TerrorLastLevel = 9, FreeSlotsNeeded = 3;
    public const uint FirstStepsGold = 50_000;
    // Crypt 2-1 (Mileth Crypt floor index 1): neither quest goes deeper.
    public const int DeepestFloor = 1;
    public const ushort Centipede = 52;
    public static readonly Retreat Vestibule = new(425, new(5, 8), "the Crypt Vestibule");

    // Slots from live 0x37 packets (weapon 1, shield 3, necklace 6, waist 11); vendors from vorlof.com.
    public static readonly StarterGear Dirk = new("Dirk", 2, 1, "Mileth", 132, "Callough", 500);
    public static readonly StarterGear WoodenShield = new("Wooden Shield", 3, 3, "Mileth", 133, "Torrance", 100);
    public static readonly StarterGear WindBelt = new("Wind Belt", 4, 11, "Rucesion", 421, "Carlos", 10_000);
    public static readonly StarterGear SeaNecklace = new("Sea Necklace", 6, 6, "Rucesion", 421, "Carlos", 10_000);
    public static readonly StarterGear LouresSaber = new("Loures Saber", 7, 1, "Piet", 165, "Gunnar", 1_500);
    public static readonly StarterGear[] Gear = [Dirk, WoodenShield, WindBelt, SeaNecklace, LouresSaber];
    // What Crypt Climb needs worn; the Dirk is replaced by the saber.
    public static readonly StarterGear[] ClimbGear = [WoodenShield, WindBelt, SeaNecklace, LouresSaber];

    // First Steps leaves centipedes alone on Crypt 2-1: they beat a starter weapon (live 2026-10-12).
    public static readonly IReadOnlyDictionary<int, ushort[]> FirstStepsAvoid = new Dictionary<int, ushort[]> { [2] = [Centipede] };

    public static bool Worn(CharacterSnapshot s, StarterGear g) => s.Equipment.TryGetValue(g.Slot, out var name) && name.Equals(g.Name, StringComparison.OrdinalIgnoreCase);
    public static bool Owned(CharacterSnapshot s, StarterGear g) => Worn(s, g) || s.Count(g.Name) > 0;

    // Gear to buy now. The Dirk only while no saber is owned; the belt waits for the necklace so Rucesion is one trip.
    public static IReadOnlyList<StarterGear> Due(CharacterSnapshot s)
    {
        int level = s.Level ?? 0;
        var due = new List<StarterGear>();
        foreach (var g in Gear)
        {
            if (Owned(s, g))
                continue;
            if (g == Dirk && Owned(s, LouresSaber))
                continue;
            int at = g == WindBelt ? SeaNecklace.Level : g.Level;
            if (level >= at)
                due.Add(g);
        }

        return due;
    }

    // Owned gear the character can wear but doesn't, best weapon only.
    public static IReadOnlyList<StarterGear> ToEquip(CharacterSnapshot s) =>
        Gear.Where(g => s.Count(g.Name) > 0 && !Worn(s, g) && (s.Level ?? 0) >= g.Level && !(g == Dirk && (Worn(s, LouresSaber) || s.Count(LouresSaber.Name) > 0 && (s.Level ?? 0) >= LouresSaber.Level))).ToArray();

    // The next level where First Steps buys something, else its end level.
    public static int NextStop(int level) => new[] { Dirk.Level, WoodenShield.Level, SeaNecklace.Level, LouresSaber.Level, FirstStepsLevel }.Where(l => l > level).DefaultIfEmpty(FirstStepsLevel).Min();

    public static IReadOnlyList<StarterGear> MissingForClimb(CharacterSnapshot s) => ClimbGear.Where(g => !Owned(s, g)).ToArray();

    public static bool HasTerrorMark(IEnumerable<string> legends) => legends.Any(l => l.Contains("Terror of the", StringComparison.OrdinalIgnoreCase));

    // Crypt Climb ends at 11: shown when the character could still do Terror of the Alley / Garden.
    public static string? TerrorWarning(string character, CharacterSnapshot s) =>
        s.Level is { } level && level <= TerrorLastLevel && s.LegendsKnown && !HasTerrorMark(s.Legends)
            ? character + " will reach level 11 and can never do Terror of the Alley / Garden. It can only be done up to level 9."
            : null;

    public static Eligibility FirstStepsEligibility(CharacterSnapshot s)
    {
        if (!s.Connected || s.Name is null || s.Level is null || s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for connected identity, level and health.");
        if (s.Hp == 0)
            return new(EligibilityKind.Blocked, "Peasant's First Steps requires a living character.");
        if (!s.InventoryKnown || s.Gold is null)
            return new(EligibilityKind.Unknown, "Waiting for inventory and gold.");
        if (s.Level >= FirstStepsLevel && Gear.All(g => g == Dirk || Worn(s, g)))
            return new(EligibilityKind.Blocked, s.Name + " is already level " + s.Level + " with all the starter gear.");
        if (s.Gold < FirstStepsGold)
            return new(EligibilityKind.Blocked, "Peasant's First Steps needs " + FirstStepsGold.ToString("N0") + " gold; " + s.Name + " has " + s.Gold.Value.ToString("N0") + ". Send gold from another character, about 500,000 is recommended.");
        if (s.Inventory.Count > 59 - FreeSlotsNeeded)
            return new(EligibilityKind.Blocked, "Free " + FreeSlotsNeeded + " inventory slots for the starter gear.");
        return new(EligibilityKind.Eligible, "Starter gear, levels to 7, ends with a Loures Saber");
    }

    public static Eligibility ClimbEligibility(CharacterSnapshot s)
    {
        if (!s.Connected || s.Name is null || s.Level is null || s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for connected identity, level and health.");
        if (s.Hp == 0)
            return new(EligibilityKind.Blocked, "Crypt Climb requires a living character.");
        if (s.Level >= ClimbLevel)
            return new(EligibilityKind.Blocked, s.Name + " is already level " + s.Level + ".");
        if (!s.InventoryKnown)
            return new(EligibilityKind.Unknown, "Waiting for inventory.");
        if (MissingForClimb(s) is { Count: > 0 } missing)
            return new(EligibilityKind.Blocked, "Missing " + string.Join(", ", missing.Select(g => g.Name)) + ". Run Peasant's First Steps first.");
        return new(EligibilityKind.Eligible, "Fights in the Mileth crypt until level 11");
    }
}
