namespace DAQuester.Core;

// Documented practical peasant equipment, not a name-based guess about arbitrary items.
// Sources: Vorlof armors.php, daitems/boots.htm and daitems/rings.htm.
internal static class OwnedStarterEquipment
{
    sealed record Gear(string Name, int Level, byte Slot, int Armor = 0, int Health = 0,
        int Damage = 0, int Hit = 0, int Con = 0, int Wis = 0, int Str = 0, int Dex = 0,
        int Int = 0, int Mana = 0, int Regen = 0, int Resist = 0, char? Gender = null)
    {
        public int[] Benefits => [Armor, Health, Damage, Hit, Con, Wis, Str, Dex, Int, Mana, Regen, Resist];
        public bool Improves(Gear old) => Benefits.Zip(old.Benefits).All(v => v.First >= v.Second) && Benefits.Zip(old.Benefits).Any(v => v.First > v.Second);
    }
    static readonly Gear[] gear =
    [
        new("Ragged Shirt", 1, 2, Armor: 1, Gender: 'm'), new("Shirt", 1, 2, Armor: 4, Gender: 'm'),
        new("Ragged Blouse", 1, 2, Armor: 1, Gender: 'w'), new("Blouse", 1, 2, Armor: 4, Gender: 'w'),
        new("Boots", 1, 13), new("Might Boots", 1, 13, Health: 100), new("Magic Boots", 1, 13, Mana: 50),
        new("Abundance Boots", 1, 13, Damage: 1), new("Blessed Boots", 1, 13, Hit: 5),
        new("Ceannlaidir Boots", 1, 13, Str: 1), new("Luathas Boots", 1, 13, Int: 1),
        new("Glioca Boots", 1, 13, Wis: 1), new("Cail Boots", 1, 13, Con: 1), new(" Boots", 1, 13, Con: 1),
        new("Fiosachd Boots", 1, 13, Dex: 1), new("Deoch Boots", 1, 13, Regen: 5), new("Gramail Boots", 1, 13, Resist: 10), new("Sgrios Boots", 1, 13),
        new("Small Emerald Ring", 1, 7, Health: 300, Damage: 2), new("Small Spinel Ring", 1, 7, Health: 300, Hit: 2),
        new("Talos Ring", 4, 7, Health: 100), new("Spinel Ring", 11, 7, Health: 500, Hit: 2),
        new("Emerald Ring", 12, 7, Health: 500, Damage: 2)
    ];

    public static IEnumerable<StarterGear> ToEquip(CharacterSnapshot s)
    {
        if (!s.InventoryKnown || s.Level is null || s.Class is null)
            yield break;
        char? gender = s.Equipment.GetValueOrDefault((byte)2) switch
        {
            "Shirt" or "Ragged Shirt" => 'm', "Blouse" or "Ragged Blouse" => 'w',
            _ => s.Appearance is { } look ? look.Body is 0x20 or 0x40 or 0x60 or 0x90 ? 'w' : 'm' : null
        };
        var available = s.Inventory.Values.Where(i => i.Quantity > 0).ToList();
        foreach (byte slot in new byte[] { 2, 7, 8, 13 })
        {
            string? worn = s.Equipment.GetValueOrDefault(slot);
            var old = gear.FirstOrDefault(g => g.Name.Equals(worn, StringComparison.OrdinalIgnoreCase));
            if (worn is not null && old is null)
                continue; // Unknown equipment is never replaced by an invented ranking.
            var candidates = available.Select(i => (Item: i, Gear: gear.FirstOrDefault(g => g.Name.Equals(i.Name, StringComparison.OrdinalIgnoreCase))))
                .Where(p => p.Gear is { } g && (slot == 8 ? g.Slot == 7 : g.Slot == slot)
                    && s.Level >= g.Level && (g.Gender is null || g.Gender == gender) && (old is null || g.Improves(old)))
                .OrderByDescending(p => p.Gear!.Armor).ThenByDescending(p => p.Gear!.Health)
                .ThenByDescending(p => p.Gear!.Damage).ThenByDescending(p => p.Gear!.Hit)
                .ThenByDescending(p => p.Gear!.Con).ThenByDescending(p => p.Gear!.Wis)
                .ThenBy(p => p.Item.Slot).ToArray();
            var best = candidates.FirstOrDefault();
            if (best.Gear is not { } chosen)
                continue;
            available.Remove(best.Item); // A single bag ring cannot fill both hands.
            yield return new(chosen.Name, chosen.Level, slot, "", 0, "", 0);
        }
    }
}
