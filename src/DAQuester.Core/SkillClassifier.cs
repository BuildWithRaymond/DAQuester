namespace DAQuester.Core;

// A known skill with its combat role. Cooldowns are read live from the snapshot by name.
public sealed record RankedSkill(byte Slot, string Name, SkillMeta Meta)
{
    public double Ticks(CharacterSnapshot s) => s.SkillCooldowns.TryGetValue(Name, out var c) ? c.Seconds : 0;
    public bool IsOffCooldown(CharacterSnapshot s, DateTimeOffset now) => !s.SkillCooldowns.TryGetValue(Name, out var c) || c.Over(now);
}

// A skillbook bucketed by combat role, each bucket strongest first (shortest cooldown breaks ties).
public sealed class ClassifiedSkills
{
    public IReadOnlyList<RankedSkill> Surrounding { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> Frontal { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> Melee { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> SleepSetups { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> FollowUps { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> Crashers { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> HpSetups { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> Buffs { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> GapClosers { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> TeleportBehinds { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> CrowdControl { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> Assails { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> RangedAssails { get; internal set; } = [];
    public IReadOnlyList<RankedSkill> RangedSkills { get; internal set; } = [];
    public int TotalSkills => Surrounding.Count + Frontal.Count + Melee.Count + SleepSetups.Count + FollowUps.Count + Crashers.Count + HpSetups.Count + Buffs.Count + GapClosers.Count + TeleportBehinds.Count + CrowdControl.Count + Assails.Count + RangedAssails.Count + RangedSkills.Count;
}

public static class SkillClassifier
{
    public static ClassifiedSkills Classify(CharacterSnapshot s)
    {
        var buckets = Enum.GetValues<SkillCategory>().ToDictionary(c => c, _ => new List<RankedSkill>());
        // Slot order stands in for Talos's skillbook order, which breaks equal-priority ties.
        foreach (var (slot, raw) in s.Skills.OrderBy(p => p.Key))
            if (SkillDatabase.BaseName(raw) is var name && SkillDatabase.Lookup(name) is { Category: not SkillCategory.Utility } meta)
                buckets[meta.Category].Add(new(slot, name, meta));
        IReadOnlyList<RankedSkill> Priority(SkillCategory c) => buckets[c].OrderByDescending(x => x.Meta.Damage).ThenBy(x => x.Ticks(s)).ToList();
        return new()
        {
            Surrounding = Priority(SkillCategory.Surrounding),
            Frontal = Priority(SkillCategory.Frontal),
            Melee = Priority(SkillCategory.Melee),
            SleepSetups = Priority(SkillCategory.SleepSetup),
            FollowUps = Priority(SkillCategory.FollowUp),
            Crashers = buckets[SkillCategory.HpBasedAttack].OrderByDescending(x => x.Meta.RequiresLowHp ? 1 : 0).ThenByDescending(x => x.Meta.Damage).ThenBy(x => x.Ticks(s)).ToList(),
            HpSetups = buckets[SkillCategory.HpSetup],
            Buffs = buckets[SkillCategory.Buff],
            GapClosers = Priority(SkillCategory.GapCloser),
            TeleportBehinds = buckets[SkillCategory.TeleportBehind],
            CrowdControl = buckets[SkillCategory.CrowdControl],
            // The game fires assails in skillbook slot order.
            Assails = buckets[SkillCategory.Assail].OrderBy(x => x.Slot).ToList(),
            RangedAssails = Priority(SkillCategory.RangedAssail),
            RangedSkills = Priority(SkillCategory.RangedSkill)
        };
    }
}
