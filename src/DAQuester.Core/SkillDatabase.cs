using System.Text.RegularExpressions;
namespace DAQuester.Core;

// Combat role of a skill; the melee rotation is built from these, never from the class.
public enum SkillCategory
{
    Surrounding,
    Frontal,
    Melee,
    SleepSetup,
    FollowUp,
    HpBasedAttack,
    HpSetup,
    Buff,
    GapCloser,
    TeleportBehind,
    CrowdControl,
    Assail,
    RangedAssail,
    RangedSkill,
    Utility
}

public enum DamageTier
{
    None,
    Minor,
    Moderate,
    Heavy,
    Massive,
    HpScaled
}

public sealed record SkillMeta(
    string Name,
    SkillCategory Category,
    DamageTier Damage,
    int Range = 1,
    bool RequiresAxisAlign = false,
    bool RequiresLowHp = false,
    bool CostsHp = false,
    bool IsNumbered = false,
    bool UsesArrowItem = false,
    string? Notes = null,
    bool NotWithBow = false);

// Ported from Talos Redux; data from vorlof.com skill tables and in-game observation.
public static class SkillDatabase
{
    static readonly Dictionary<string, SkillMeta> skills = new(StringComparer.OrdinalIgnoreCase);

    static SkillDatabase()
    {
        // Warrior
        Add("Assail", SkillCategory.Assail, DamageTier.Minor);
        Add("Assault", SkillCategory.Assail, DamageTier.Minor);
        Add("Clobber", SkillCategory.Assail, DamageTier.Minor);
        Add("Wallop", SkillCategory.Assail, DamageTier.Minor);
        Add("Long Strike", SkillCategory.Assail, DamageTier.Minor);
        Add("Thrash", SkillCategory.Assail, DamageTier.Minor);
        Add("Wind Blade", SkillCategory.Frontal, DamageTier.Minor, range: 3, requiresAxisAlign: true);
        Add("Cyclone Blade", SkillCategory.FollowUp, DamageTier.Minor, notes: "Fire after any skill for free extra damage");
        Add("Charge", SkillCategory.GapCloser, DamageTier.Moderate, range: 4, requiresAxisAlign: true, notes: "4 tiles forward, hits first enemy");
        Add("Crasher", SkillCategory.HpBasedAttack, DamageTier.HpScaled, requiresLowHp: true, notes: "150% max HP damage");
        Add("Rescue", SkillCategory.Utility, DamageTier.None, notes: "Forces creature to target you");
        Add("beag suain", SkillCategory.CrowdControl, DamageTier.None, notes: "Stuns enemy infront");
        // Warrior Pure Master
        Add("Mad Soul", SkillCategory.HpBasedAttack, DamageTier.HpScaled, costsHp: true, notes: "HP-based damage, reduces own HP");
        Add("Sacrifice", SkillCategory.HpBasedAttack, DamageTier.HpScaled, costsHp: true, notes: "HP-based damage, drastically reduces own HP");
        Add("Sever", SkillCategory.Frontal, DamageTier.Heavy, range: 3, requiresAxisAlign: true);
        Add("Sneak Attack", SkillCategory.TeleportBehind, DamageTier.Minor, notes: "Teleports to other side of enemy");
        Add("Paralyze Force", SkillCategory.CrowdControl, DamageTier.None, notes: "Stuns everything in sight 25s");
        Add("Perfect Defence", SkillCategory.Buff, DamageTier.None, notes: "Spell immunity 14s");

        // Gladiator
        Add("Lullaby Punch", SkillCategory.SleepSetup, DamageTier.None, notes: "Sleep 8s, break deals double damage");
        Add("Cyclone Kick", SkillCategory.Surrounding, DamageTier.Massive);
        Add("Dark's Mega Blade", SkillCategory.Surrounding, DamageTier.Massive, notes: "130% of Cyclone Kick damage; daily quest cosmetic");
        Add("Execute", SkillCategory.HpBasedAttack, DamageTier.HpScaled, requiresLowHp: true, notes: "150% max HP damage");
        Add("Auto Hemloch", SkillCategory.HpSetup, DamageTier.None, notes: "Sets HP to 1 for Crasher/Execute");
        Add("Ground Stomp", SkillCategory.CrowdControl, DamageTier.None, notes: "Stuns everything in sight 28s");
        Add("Frost Strike", SkillCategory.CrowdControl, DamageTier.Minor, notes: "Slows target attack/move/regen");
        Add("Intimidate", SkillCategory.Utility, DamageTier.None, notes: "Forces surrounding enemies to face away; Pure Gladiator");
        Add("Spinning Kelberoth Strike", SkillCategory.HpBasedAttack, DamageTier.HpScaled, costsHp: true, notes: "1/3 HP damage, pierces dion, costs HP");
        Add("Strikedown", SkillCategory.Frontal, DamageTier.Moderate, range: 3, requiresAxisAlign: true, isNumbered: true);
        Add("Dune Swipe", SkillCategory.Surrounding, DamageTier.Moderate, isNumbered: true);
        Add("Wheel Kick", SkillCategory.Surrounding, DamageTier.Heavy, isNumbered: true);

        // Monk
        Add("Double Punch", SkillCategory.Assail, DamageTier.Minor);
        Add("Triple Kick", SkillCategory.Assail, DamageTier.Minor);
        Add("Kick", SkillCategory.Frontal, DamageTier.Minor, range: 1);
        Add("High Kick", SkillCategory.Frontal, DamageTier.Minor, range: 1);
        Add("Poison Punch", SkillCategory.Frontal, DamageTier.Minor, range: 1, notes: "Poisons target");
        Add("Mantis Kick", SkillCategory.Frontal, DamageTier.Moderate, range: 1);
        Add("Eagle Strike", SkillCategory.Frontal, DamageTier.Minor, range: 3, requiresAxisAlign: true);
        Add("Claw Fist", SkillCategory.Buff, DamageTier.None, notes: "Doubles unarmed assail damage");
        Add("Wolf Fang Fist", SkillCategory.SleepSetup, DamageTier.None, notes: "Sleep 8s, break deals double damage");
        Add("Ambush", SkillCategory.TeleportBehind, DamageTier.None, range: 3, notes: "Leap up to 3 tiles behind target");
        Add("Throw", SkillCategory.Utility, DamageTier.None, notes: "Moves player infront 1 tile");
        Add("Whirlwind Attack", SkillCategory.Surrounding, DamageTier.HpScaled, costsHp: true, notes: "HP-based surrounding damage");
        Add("Draco Tail Kick", SkillCategory.Surrounding, DamageTier.Moderate, notes: "Draco form");
        Add("Kelberoth Strike", SkillCategory.HpBasedAttack, DamageTier.HpScaled, costsHp: true, notes: "1/3 HP damage, pierces dion, costs 2/3 HP; Kelberoth form");
        Add("Dark Spear", SkillCategory.Frontal, DamageTier.Minor, range: 1, notes: "Blinds target; White Bat form");
        Add("Sting", SkillCategory.Frontal, DamageTier.Minor, range: 1, notes: "Poisons target; Scorpion form");

        // Rogue
        Add("Midnight Slash", SkillCategory.Assail, DamageTier.Minor);
        Add("Assassin Strike", SkillCategory.Melee, DamageTier.Massive, notes: "First-hit bonus; no cooldown");
        Add("Shadow Figure", SkillCategory.TeleportBehind, DamageTier.Minor, notes: "Teleports behind target; kept for melee combos");
        Add("Stab and Twist", SkillCategory.Melee, DamageTier.Minor, notes: "Requires dagger or monk weapon");
        Add("Stab Twice", SkillCategory.Melee, DamageTier.Moderate, notes: "Requires rogue weapon or secret");
        Add("Throw Surigum", SkillCategory.RangedAssail, DamageTier.Minor, range: 3, requiresAxisAlign: true, notWithBow: true, notes: "Not with a bow equipped");
        Add("Amnesia", SkillCategory.Utility, DamageTier.None, range: 3, requiresAxisAlign: true, notes: "Confuses enemy, resets hit counter");
        Add("Throw Smoke bomb", SkillCategory.CrowdControl, DamageTier.None, notes: "Blinds all enemies in sight 7s");

        // Archer
        Add("Shadow Strike", SkillCategory.Melee, DamageTier.Heavy);
        Add("Kidney Shot", SkillCategory.Melee, DamageTier.Moderate);
        Add("Frozen Strike", SkillCategory.SleepSetup, DamageTier.None, notes: "Sleep, break deals double damage");
        Add("Special Arrow Attack", SkillCategory.RangedSkill, DamageTier.Heavy, range: 11, requiresAxisAlign: true, usesArrowItem: true, notes: "Uses equipped arrow item from slot 1");
        Add("Precision Shot", SkillCategory.RangedSkill, DamageTier.Heavy, range: 11, requiresAxisAlign: true, costsHp: true, notes: "Reduces own HP on use, like Kelberoth Strike");
        Add("Incapacitate", SkillCategory.CrowdControl, DamageTier.None, notes: "CC at range");
        Add("Arrow Shot", SkillCategory.RangedAssail, DamageTier.Minor, range: 11, requiresAxisAlign: true, isNumbered: true);
        Add("Rear Strike", SkillCategory.RangedSkill, DamageTier.Moderate, range: 8, requiresAxisAlign: true, isNumbered: true);

        // Druid
        Add("Raging Attack", SkillCategory.Surrounding, DamageTier.Massive);
        Add("Furious Bash", SkillCategory.Surrounding, DamageTier.Heavy);
        Add("Animal Feast", SkillCategory.HpBasedAttack, DamageTier.HpScaled, requiresLowHp: true, notes: "Druid equivalent of Crasher/Execute");
        Add("Animal Roar", SkillCategory.Buff, DamageTier.None, notes: "Buff/taunt");
        Add("Twist Kick", SkillCategory.Melee, DamageTier.Moderate);
        Add("Puncture", SkillCategory.Melee, DamageTier.Heavy, notes: "Pure Druid only");
        Add("Mass Strike", SkillCategory.Surrounding, DamageTier.Moderate, isNumbered: true);
        Add("Double Rake", SkillCategory.Melee, DamageTier.Moderate, isNumbered: true);
        Add("Pounce", SkillCategory.GapCloser, DamageTier.Moderate, range: 5, requiresAxisAlign: true, isNumbered: true, notes: "Leap to target");
        Add("Beak Pierce", SkillCategory.Melee, DamageTier.Moderate, isNumbered: true);
        Add("Sneak Flight", SkillCategory.GapCloser, DamageTier.Moderate, range: 3, isNumbered: true, notes: "Gap closer / frontal attack");
        Add("Talon Kick", SkillCategory.Frontal, DamageTier.Moderate, range: 3, requiresAxisAlign: true, isNumbered: true);
        // Knocks the monster away, so the druid walks after it; never bashed with.
        Add("Tail Slam", SkillCategory.Utility, DamageTier.Heavy, notes: "Knocks the target away; not bashed with");
        Add("Tail Sweep", SkillCategory.Surrounding, DamageTier.Moderate, isNumbered: true);
        Add("Claw Slash", SkillCategory.Melee, DamageTier.Moderate, isNumbered: true);
        Add("Venom Attack", SkillCategory.Melee, DamageTier.Moderate, isNumbered: true, notes: "Poisons target");

        // Bard
        Add("Instrumental Attack", SkillCategory.Surrounding, DamageTier.Moderate, isNumbered: true);
    }

    static void Add(string name, SkillCategory category, DamageTier damage, int range = 1, bool requiresAxisAlign = false, bool requiresLowHp = false, bool costsHp = false, bool isNumbered = false, bool usesArrowItem = false, string? notes = null, bool notWithBow = false) =>
        skills[name] = new(name, category, damage, range, requiresAxisAlign, requiresLowHp, costsHp, isNumbered, usesArrowItem, notes, notWithBow);

    // "Strikedown 5" falls back to "Strikedown" when that skill is numbered.
    public static SkillMeta? Lookup(string? skillName)
    {
        if (string.IsNullOrEmpty(skillName))
            return null;
        if (skills.TryGetValue(skillName, out var meta))
            return meta;
        int space = skillName.LastIndexOf(' ');
        if (space > 0 && space < skillName.Length - 1 && int.TryParse(skillName[(space + 1)..], out _) && skills.TryGetValue(skillName[..space], out meta) && meta.IsNumbered)
            return meta;
        return null;
    }

    public static bool IsKnown(string skillName) => Lookup(skillName) is not null;

    // The server names skills "Mass Strike 11 (Lev:100/100)"; Talos strips the level the same way.
    static readonly Regex levelled = new(@"^(.*?) \([a-zA-Z]+:(\d+)/(\d+)\)$", RegexOptions.CultureInvariant);
    public static string BaseName(string raw) => levelled.Match(raw) is { Success: true } m ? m.Groups[1].Value : raw;
}
