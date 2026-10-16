namespace DAQuester.Core;

public enum WeaponType
{
    Melee,
    Staff,
    Bow,
    Arrow
}

// Class is the Temuair base class as the profile names it ("Peasant" = any class).
public sealed record WeaponData(
    string Name,
    WeaponType Type,
    string Class,
    string? Medenia = null,
    int Ability = 0,
    int Insight = 0,
    bool Master = false)
{
    public bool IsMelee => Type == WeaponType.Melee;
    public bool IsBow => Type == WeaponType.Bow;
}

// Ported from Talos Redux WeaponRegistry: which item is a weapon, for whom, and what it requires.
public static class WeaponRegistry
{
    public static readonly IReadOnlyDictionary<string, WeaponData> All;

    static WeaponRegistry()
    {
        var w = new Dictionary<string, WeaponData>();
        void M(string n, string c, int ins = 0, int ab = 0, bool master = false, string? mc = null) => w[n] = new(n, WeaponType.Melee, c, mc, ab, ins, master);
        void S(string n, string c, int ins = 0, int ab = 0, bool master = false, string? mc = null) => w[n] = new(n, WeaponType.Staff, c, mc, ab, ins, master);
        void B(string n, int ab) => w[n] = new(n, WeaponType.Bow, "Rogue", "Archer", ab);
        void A(string n) => w[n] = new(n, WeaponType.Arrow, "Rogue", "Archer", 1);

        // Peasant: any class.
        M("Stick", "Peasant", 1);
        M("Oak Stick", "Peasant", 1);
        M("Eppe", "Peasant", 2);
        M("Dirk", "Peasant", 2);
        M("Loures Saber", "Peasant", 7);
        M("Harpoon", "Peasant", 11);
        M("Hatchet", "Peasant", 13);
        M("Claidheamh", "Peasant", 14);
        M("Broad Sword", "Peasant", 17);
        M("Dragon Scale Sword", "Peasant", 20);
        M("Battle Sword", "Peasant", 26);
        M("Templar", "Peasant", 29);
        M("Masquerade", "Peasant", 31);
        M("Bramble", "Peasant", 41);
        M("Black Death", "Peasant", 45);
        M("Primitive Spear", "Peasant", 49);
        M("Wooden Club", "Peasant", 50);
        M("Long Sword", "Peasant", 56);
        M("Stilla", "Peasant", 61);
        M("Spiked Club", "Peasant", 66);
        M("Giant Club", "Peasant", 75);
        M("Fine Claidhmore", "Peasant", 75);
        M("Talgonite Axe", "Peasant", 81);
        M("Great Emerald Sword", "Peasant", 85);
        M("Scythe", "Peasant", 94);
        M("Stone Axe", "Peasant", 95);
        M("Phoenix", "Peasant", 99);
        M("Dragon Slayer", "Peasant", 99);
        M("Good Dragon Slayer", "Peasant", 99);
        M("Great Dragon Slayer", "Peasant", 99);
        M("Rudolph", "Peasant", 1);
        M("Shovel", "Peasant", 41);
        M("Mileth Dragon Slayer", "Peasant", 41);
        M("Insect Net", "Peasant", 1);
        M("Fishing Rod", "Peasant", 1);
        M("Magical Net", "Peasant", 1);
        M("Lethal Shoe", "Peasant", 1);
        M("Torbjorn's Axe", "Peasant", 1);
        M("Lumberjack's Axe", "Peasant", 1);
        M("Island Fishing Rod", "Peasant", 1);
        M("Dendron Bouquet", "Peasant", ab: 40);
        M("Yowien Fishing Pole", "Peasant", ab: 40);
        S("Staff of Ages", "Peasant", 99, master: true);
        S("Staff of Brilliance", "Peasant", 99, master: true);
        S("Staff of Deliverance", "Peasant", 99, 25, true);
        S("Staff of Clarity", "Peasant", 99, 50, true);
        S("Staff of Eternity", "Peasant", 99, 75, true);

        // Warrior.
        M("Scimitar", "Warrior", 36);
        M("Cutlass", "Warrior", 86);
        M("Hand Axe", "Warrior", 90);
        M("Chain Mace", "Warrior", 95);
        M("Two-Handed Claidhmore", "Warrior", 71);
        M("Two-Handed Emerald Sword", "Warrior", 77);
        M("Two-Handed Gladius", "Warrior", 86);
        M("Two-Handed Kindjal", "Warrior", 90);
        M("Giant Stone Axe", "Warrior", 93);
        M("Giant Stone Club", "Warrior", 95);
        M("Giant Stone Hammer", "Warrior", 97);
        M("Two-Handed Dragon Slayer", "Warrior", 97);
        M("Hy-brasyl Battle Axe", "Warrior", 99);
        foreach (string n in new[] { "Eclipse", "Dane Axe", "Astion Axe", "Ruby Saber", "Amber Saber", "Master Saber", "Emerald Saber", "Diamond Saber", "Master Falcata", "Sapphire Saber", "Gold Kindjal", "Master Battle Axe", "Masters Battle Axe", "Hy-brasyl Escalon", "Empowered Escalon", "Enchanted Escalon", "Ancient Hy-brasyl Escalon Replica" })
            M(n, "Warrior", 99, master: true);
        M("Crystal Saber", "Warrior", 99, 65, true, "Gladiator");
        M("Crystal Saber1", "Warrior", 99, 65, true, "Gladiator");
        M("Yowien Hatchet", "Warrior", 99, 80, true, "Gladiator");
        M("Yowien Hatchet1", "Warrior", 99, 80, true, "Gladiator");
        M("Defiled Ruby Saber", "Warrior", 99, 95, true, "Gladiator");
        M("Dane Blade", "Warrior", 99, 1, true, "Gladiator");
        M("Astion Blade", "Warrior", 99, 8, true, "Gladiator");
        M("Brune Blade", "Warrior", 99, 15, true, "Gladiator");
        M("Veltain Sword", "Warrior", 99, 15, true, "Gladiator");
        M("Tempered Veltain Sword", "Warrior", 99, 15, true, "Gladiator");
        M("Tuned Veltain Sword", "Warrior", 99, 15, true, "Gladiator");
        M("Blazed Veltain Sword", "Warrior", 99, 15, true, "Gladiator");
        M("Grum Blade", "Warrior", 99, 22, true, "Gladiator");
        M("Andor Saber", "Warrior", 99, 30, true, "Gladiator");
        M("Hwarone Guandao", "Warrior", 99, 45, true, "Gladiator");
        M("Empowered Hwarone Guandao", "Warrior", 99, 55, true, "Gladiator");
        M("Hellreavers Blade", "Warrior", 99, 90, true, "Gladiator");
        M("Ancient Hy-brasyl Axe", "Warrior", 99, 99, true, "Gladiator");

        // Wizard staves, spheres and wands.
        foreach (string n in new[] { "Magus Diana", "Magus Zeus", "Magus Ares", "Magus Gaea", "Magus Kronos", "Magus Apollo", "Oak Staff", "Magus Cail", "Magus Deoch", "Magus Glioca", "Magus Sgrios", "Magus Gramail", "Magus Luathas", "Magus Fiosachd", "Magus Ceannlaidir" })
            S(n, "Wizard", 19);
        S("Staff of Protection", "Wizard", 50);
        foreach (string n in new[] { "Magus Orb", "Empowered Magus Orb", "Enchanted Magus Orb", "Ancient Hy-brasyl Orb Replica" })
            S(n, "Wizard", 99, master: true);
        foreach (var (n, ab) in new[] { ("Sphere", 1), ("Shaine Sphere", 8), ("Maron Sphere", 15), ("Chernol Sphere", 22), ("Serpant Sphere", 45), ("Empowered Serpant Sphere", 55), ("Glimmering Wand", 65), ("Glimmering Wand1", 65), ("Yowien Tree Staff", 80), ("Yowien Tree Staff1", 80), ("Dragon Infused Staff", 90), ("Ancient Hy-brasyl Orb", 99), ("Veltain Staff", 15), ("Andor Staff", 30), ("Skylight Staff", 75), ("Star Crafted Staff", 95) })
            S(n, "Wizard", 99, ab, true, "Summoner");

        // Priest staves and Bard harps.
        foreach (string n in new[] { "Holy Diana", "Holy Zeus", "Holy Gaea", "Holy Kronos", "Holy Apollo", "Holy Hermes", "Holy Cail", "Holy Deoch", "Holy Glioca", "Holy Sgrios", "Holy Gramail", "Holy Luathas", "Holy Fiosachd", "Holy Ceannlaidir" })
            S(n, "Priest", 19);
        S("Holy Restoration", "Priest", 50);
        S("Stone Cross", "Priest", 98);
        foreach (string n in new[] { "Master Divine Staff", "Holy Hy-brasyl Gnarl", "Empowered Holy Gnarl", "Enchanted Holy Gnarl", "Master Celestial Staff", "Ancient Hy-brasyl Gnarl Replica" })
            S(n, "Priest", 99, master: true);
        foreach (var (n, ab) in new[] { ("Wooden Harp", 1), ("Goldberry Harp", 8), ("Rosewood Harp", 15), ("Ironwood Harp", 22), ("Hwarone Lute", 45), ("Empowered Hwarone Lute", 55), ("Holy Hy-brasyl Baton", 65), ("Holy Hy-brasyl Baton1", 65), ("Brute's Quill", 80), ("Brute's Quill1", 80), ("Dragon Emberwood Staff", 90), ("Ancient Hy-brasyl Gnarl", 99), ("Assassin's Cross", 27), ("Divinities Staff", 75), ("Dark Star", 95) })
            S(n, "Priest", 99, ab, true, "Bard");

        // Monk claws, master claws and Druid claws.
        M("Wolf Claw", "Monk", 50);
        M("Wolf Claws", "Monk", 50);
        M("Useless Obsidian", "Monk", 50);
        foreach (string n in new[] { "Kalkuri", "Empowered Kalkuri", "Enchanted Kalkuri", "Obsidian", "Nunchaku", "Stone Fist", "Stone Fists", "Tilian Claw", "Phoenix Claw", "Phoenix Claws", "Ancient Hy-brasyl Tonfa Replica" })
            M(n, "Monk", 99, master: true);
        M("Yowien's Claw", "Monk", 99, 65, true, "Druid");
        M("Yowien's Claw1", "Monk", 99, 65, true, "Druid");
        M("Yowien's Fist", "Monk", 99, 80, true, "Druid");
        M("Yowien's Fist1", "Monk", 99, 80, true, "Druid");
        M("Eagles Grasp", "Monk", 99, 90, true, "Druid");
        M("Blackstar Night Claw", "Monk", 99, 95, true, "Druid");
        M("Ancient Hy-brasyl Tonfa", "Monk", 99, 99, true, "Druid");

        // Rogue daggers, secrets and whips; Archer weapons, bows and arrows.
        foreach (var (n, ins) in new[] { ("Snow Dagger", 1), ("Center Dagger", 4), ("Blossom Dagger", 14), ("Moon Dagger", 30), ("Curved Dagger", 30), ("Light Dagger", 40), ("Sun Dagger", 60), ("Lotus Dagger", 75), ("Nagetier Dagger", 79), ("Blood Dagger", 89), ("Throw Axe", 90), ("Snow Secret", 1), ("Center Secret", 7), ("Blossom Secret", 21), ("Moon Secret", 39), ("Light Secret", 52), ("Sun Secret", 68), ("Lotus Secret", 81), ("Whip", 26), ("Mythril Whip", 49), ("Talgonite Whip", 69), ("Chain", 80) })
            M(n, "Rogue", ins);
        foreach (string n in new[] { "Hy-brasyl Azoth", "Empowered Azoth", "Enchanted Azoth", "Desert Skewer", "Scurvy Dagger", "Master Skean", "Master Kris", "Ancient Hy-brasyl Azoth Replica", "Ruby Whip" })
            M(n, "Rogue", 99, master: true);
        M("Inferno Blade", "Rogue", 99, 90, true, "Archer");
        M("Ancient Hy-brasyl Azoth", "Rogue", 99, 99, true, "Archer");
        M("Andor Whip", "Rogue", 99, 30, true, "Archer");
        M("Blackstar Whip", "Rogue", 99, 95, true, "Archer");
        foreach (var (n, ab) in new[] { ("Wooden Bow", 1), ("Royal Bow", 8), ("Jenwir Bow", 15), ("Sen Bow", 22), ("Andor Bow", 30), ("Yumi Bow", 45), ("Empowered Yumi Bow", 55), ("Thunderfury", 90) })
            B(n, ab);
        foreach (string n in new[] { "Wooden Arrow", "Fire Arrow", "Spear Arrow", "Crystal Arrow1", "Dual Wooden Arrows", "Dual Fire Arrows", "Dual Crystal Arrows1", "Triple Wooden Arrows", "Triple Fire Arrows", "Ruby Tipped Arrow" })
            A(n);

        All = w;
    }

    // Talos EquipWeaponForClass: the best melee weapon in the bag this character may wield, most demanding first.
    public static Item? BestMelee(CharacterSnapshot s) => s.Inventory.Values
        .Select(item => (item, wd: All.GetValueOrDefault(item.Name)))
        .Where(x => x.wd is { IsMelee: true } wd && (wd.Class == "Peasant" || wd.Class.Equals(s.Class, StringComparison.OrdinalIgnoreCase)) && wd.Ability <= (s.AbilityLevel ?? 0) && wd.Insight <= (s.Level ?? 0) && (!wd.Master || s.ToNextLevel == 0))
        .OrderByDescending(x => x.wd!.Ability)
        .ThenByDescending(x => x.wd!.Insight)
        .Select(x => x.item)
        .FirstOrDefault();

    public static bool HoldsWeapon(CharacterSnapshot s) => s.Equipment.TryGetValue(1, out var name) && All.GetValueOrDefault(name) is { } wd && (wd.IsMelee || wd.IsBow);
}
