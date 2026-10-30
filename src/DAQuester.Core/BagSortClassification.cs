// Adapted from DAOrganizer's ItemCategories.
// MIT License
// Copyright (c) 2026 BuildWithRaymond and DAOrganizer contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Text.RegularExpressions;

namespace DAQuester.Core;

internal static class BagSortClassification
{
    private static readonly string[] Names = ["Weapons", "Armor", "Accessories", "Trinkets", "Consumables", "Materials", "Books & scrolls", "Tools", "Cosmetics", "Pets", "Other"];

    // DAOrganizer's local name/family rules, not a complete game item database.
    // Trinket names originate from vorlof.com/trinkets.html; no artwork or descriptions.
    private static readonly (string Category, string Family, string Names)[] NamedGroups =
    [
        ("Trinkets", "Trinkets", "Rest Cloak|Map of Ant Tunnels|Map of Ant Guardian Tunnels|Map of Royal Ant Guardian Tunnels|Mantid Scent|Potent Mantid Scent|Insect Cloak|Monster Cloak|Fungus Beetle Extract|Beetle Aid|Sleigh Mount|Headless Horseman Mount|Santa Costume|Tank Mount|Deerstalker Mount|Elemus Mount|Grime Scent|Assassin's Scroll|Blackstar Resting Cloak|Monster Call|Muscle Stimulant|Dark Ring Spell|Dragon's Fire|Dragon's Scale|Cursed Notes|Wake Scroll|Bard's Notes|Three Move Combo Scroll|Two Move Combo Scroll|Three Move Combo|Two Move Combo|Damage Scroll|Sprint Potion|Vanishing Elixir|Glowing Stone|Nerve Stimulant"),
        ("Consumables", "Potions", "Komadium|Red Potion|Exkuranum|Dibenomum|Hemloch|Satchel of Hemloch|Kurum|Sposen|Carvien|fior srad|fior athar|fior sal|fior creag"),
        ("Consumables", "Food & drink", "Papaya|Apricot|Pafrica|Rambutan|Banas|Tangerines|Tentacle|Starfish Arm|Green Tentacle|Red Tentacle|Cheese|Spices|Liver|Purple Whopper|Bee's Honey|Pure Honey|Royal Honey|Raw Honey|Mold|Small Mold|Herring|Sardine|Anchovy|Belladonna|Baguette|Mouldy Baguette|Marinade|Frosting|Broth|Flour|Salt"),
        ("Consumables", "Mass items", "Anarchist's Tome|Ceannlaidir's Valor|Embrace of Glioca|Deoch's Flame|Gramail's Spirit|Fiosachd's Gift|Luathas' Spectacles|Footstep's of Cail"),
        ("Weapons", "Swords", "Eppe|Scimitar|Cutlass"),
        ("Weapons", "Daggers", "Dirk"),
        ("Weapons", "Staves", "Magus Ares|Magus Zeus|Magus Gaea|Magus Diana|Magus Kronos|Magus Apollo|Assassin's Cross|Dark Star"),
        ("Weapons", "Secrets & claws", "Snow Secret|Center Secret|Blossom Secret|Moon Secret|Light Secret|Sun Secret|Lotus Secret"),
        ("Armor", "Headwear", "Mushroom Cap|Death Knell|Nagetier Jaw"),
        ("Armor", "Body armor", "Nagetier Cloak|Aosdic Pattern Walker"),
        ("Armor", "Gauntlets & bracers", "Lockpicks"),
        ("Armor", "Belts", "Ancusa ceir"),
        ("Cosmetics", "Effects", "Heart Effect|Broken Heart Display|Fairy Dust|Legendary Shine")
    ];

    private static readonly Dictionary<string, (string Category, string Family)> Known = NamedGroups
        .SelectMany(group => group.Names.Split('|').Select(name => (Name: name, group.Category, group.Family)))
        .ToDictionary(entry => entry.Name, entry => (entry.Category, entry.Family), StringComparer.OrdinalIgnoreCase);

    // Preserve source precedence: specific wearables/tools before generic materials/food.
    private static readonly (string Category, string Family, string Pattern)[] Patterns =
    [
        ("Tools", "Tickets & passes", @"\b(tickets?|passes?)\b"),
        ("Tools", "Keys", @"\bkeys?\b"),
        ("Tools", "Maps", @"\bmaps?\b"),
        ("Tools", "Gathering tools", @"\b(pickaxe|shovel|fishing rod)\b"),
        ("Pets", "Pets", @"\b(pet|ducklings|chicks|puppy|buddy)\b"),
        ("Cosmetics", "Effects", @"\b(effect|sparkles|display)\b"),
        ("Cosmetics", "Costumes", @"\b(costume|spectacles|face|beard|wings|cloak)\b"),
        ("Armor", "Headwear", @"\b(helm|helmet|hat|cap|toque|wimple|dugon)\b"),
        ("Armor", "Body armor", @"\b(armor|armour|coat|overcoat|robe|mantle|uniform|bliaut|cuirass|cotehardie|hauberk|talma|aiquil|skirt|benusta|stoller|yainar|dobok|quilas|kano|cotte|brigandine|corsette|jekin|kanon)\b"),
        ("Armor", "Shields", @"\bshield\b"),
        ("Armor", "Gauntlets & bracers", @"\b(gauntlets?|bracers?)\b"),
        ("Armor", "Greaves", @"\b(greaves|shinguards|buskins)\b"),
        ("Armor", "Boots", @"\b(boots?|shoes)\b"),
        ("Armor", "Belts", @"\b(belt|cincture|sash)\b"),
        ("Accessories", "Necklaces", @"\b(necklace|pendant|torc)\b"),
        ("Accessories", "Earrings", @"\bearrings?\b"),
        ("Accessories", "Rings", @"\b(ring|band)\b"),
        ("Weapons", "Swords", @"\b(sword|blade|saber|claidhmore|kindjal|escalon)\b"),
        ("Weapons", "Staves", @"\b(staff|staves|wand|stick)\b"),
        ("Weapons", "Spears", @"\b(spear|harpoon)\b"),
        ("Weapons", "Daggers", @"\bdagger\b"),
        ("Weapons", "Axes", @"\baxe\b"),
        ("Weapons", "Bows", @"\bbow\b"),
        ("Weapons", "Arrows", @"\barrows?\b"),
        ("Weapons", "Secrets & claws", @"\bclaw\b"),
        ("Weapons", "Maces", @"\bmace\b"),
        ("Weapons", "Whips", @"\bwhip\b"),
        ("Consumables", "Potions", @"\b(potion|tonic|hitonic|extonic|deum|lotion)\b"),
        ("Consumables", "Teleport songs", @"\b(song|recall scroll)\b"),
        ("Consumables", "Bonuses & runes", @"\b(experience|ability|exp|bonus|rune|exp-ap|expap)\b"),
        ("Consumables", "Chests & gifts", @"\b(chest|treasure bag|gift|satchel of goods)\b"),
        ("Consumables", "Food & drink", @"\b(wine|tea|apple|mushroom|food|bread|grapes|fruit|lobster|sandwich|steak|beef|chicken|turkey|cake|pie|soup|tomato|strawberry|bananas|candy|egg|trout|bass|fish|meat|honey|chocolate|lollipops|rum|brandy|cherry|vegetable|beer|ale)\b"),
        ("Books & scrolls", "Books", @"\b(book|tome)\b"),
        ("Books & scrolls", "Scrolls", @"\bscroll\b"),
        ("Books & scrolls", "Recipes & notes", @"\b(plan|recipe|note)\b"),
        ("Materials", "Gems: ruby", @"\bruby\b"),
        ("Materials", "Gems: emerald", @"\bemerald\b"),
        ("Materials", "Gems: beryl", @"\bberyl\b"),
        ("Materials", "Gems", @"\b(gem|gemstone|sapphire|amethyst|diamond|coral|pearl|jade|spinel|lapis)\b"),
        ("Materials", "Ores & stones", @"\b(ore|rock|stone)\b"),
        ("Materials", "Hides & cloth", @"\b(pelt|fur|silk|leather|hide|cloth)\b"),
        ("Materials", "Monster parts", @"\b(feather|tentacle|skull|hair|bone|scale|venom|sac|ear|spine|eye)\b"),
        ("Materials", "Crafting supplies", @"\bwax\b")
    ];

    private static readonly (string Category, string Family, Regex Pattern)[] Rules = Patterns
        .Select(entry => (entry.Category, entry.Family, new Regex(entry.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled))).ToArray();

    internal static string Infer(string name) => Known.TryGetValue(name.Trim(), out var known) ? known.Category :
        Rules.FirstOrDefault(rule => rule.Pattern.IsMatch(name)).Category ?? "Other";

    internal static string Family(string name, string category)
    {
        if (Known.TryGetValue(name.Trim(), out var known) && known.Category == category)
            return known.Family;
        return Rules.FirstOrDefault(rule => rule.Category == category && rule.Pattern.IsMatch(name)).Family ?? "General";
    }

    internal static int Order(string category)
    {
        var index = Array.IndexOf(Names, category);
        return index < 0 ? Names.Length - 1 : index == Names.Length - 1 ? Names.Length : index;
    }
}
