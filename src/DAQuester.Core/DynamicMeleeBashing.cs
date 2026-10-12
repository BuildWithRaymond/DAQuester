namespace DAQuester.Core;

// Ported from Talos Redux: a class-agnostic melee rotation built from the skillbook by SkillClassifier.
// Crashers and sleep combos are left out of this port.
public sealed class DynamicMeleeBashing(IGameSession session, ICombatMap map, BashOptions options) : BashingBase(session, map, options)
{
    ClassifiedSkills? classified;
    string? lastFiredSkill;

    // Surrounding skills with a cooldown of AoeDensityTickThreshold seconds or more wait for this many adjacent monsters.
    internal int AoeDensityGate { get; set; } = 3;
    internal int AoeDensityTickThreshold { get; set; } = 15;

    internal ClassifiedSkills EnsureClassified(bool force = false)
    {
        if (classified is null || force)
            classified = SkillClassifier.Classify(S);
        return classified;
    }

    protected override async Task UseSkills(Entity target, CancellationToken ct)
    {
        EnsureClassified();
        var now = DateTimeOffset.UtcNow;
        bool canUseSkill = now - LastUsedSkill > TimeSpan.FromMilliseconds(SkillIntervalMs);
        bool canAssail = now - LastAssailed > TimeSpan.FromMilliseconds(1000.0);
        int distanceToTarget = Distance(Here, target.Position);
        if (distanceToTarget > MaxSkillRange())
            return;

        // 1. Gap closer instead of walking when at range.
        if (canUseSkill && distanceToTarget > 1 && await TryGapCloser(target, distanceToTarget, ct))
        {
            LastUsedSkill = now;
            Options.Note?.Invoke("Gap closer " + lastFiredSkill);
            return;
        }

        // 2. An asgalled target is only hit while we are dioned, or with asgall bashing on.
        if (IsAsgalled(target) && !SelfDioned && !BashAsgall)
            return;

        // 3. Assails, with assail buffs first.
        if (canAssail && distanceToTarget == 1 && (BashAsgall || !IsAsgalled(target)))
        {
            await TryActivateBuffs(ct);
            await UseAssails(ct);
            LastAssailed = now;
        }

        if (!canUseSkill)
            return;

        // 4. Skill rotation.
        lastFiredSkill = null;
        if (await TrySkillRotation(target, distanceToTarget, ct))
        {
            LastUsedSkill = now;
            Options.Note?.Invoke("Used " + lastFiredSkill + " at " + distanceToTarget + " tiles, target at " + target.HealthPercent + "%");
        }
    }

    // A monster tagged by another player ("You can't attack.") pays no experience; Rescue claims it back.
    async Task<bool> TryRescueTaggedTarget(Entity target, CancellationToken ct)
    {
        if (!IsTaggedByOther(target))
            return false;
        if (!CanUseSkill("Rescue") || !await UseSkill("Rescue", ct))
            return false;
        ClearTagged(target);
        lastFiredSkill = "Rescue";
        return true;
    }

    // Priority: rescue, teleport-behind, surrounding (density-gated), frontal, melee, frontal at melee, any surrounding.
    internal async Task<bool> TrySkillRotation(Entity target, int distance, CancellationToken ct)
    {
        EnsureClassified();
        if (!MeetsKillCriteria(target) || !ShouldUseSkillsOnTarget(target))
            return false;
        byte hp = target.HealthPercent;
        if (distance == 1 && await TryRescueTaggedTarget(target, ct))
            return true;
        if (hp >= 70 && distance >= 2 && await TryTeleportBehind(target, distance, ct))
            return true;
        if (distance <= 1 && await TrySurroundingSkills(hp, gated: true, ct))
            return true;
        if (distance >= 2 && await TryFrontalSkills(target, hp, ct))
            return true;
        if (distance == 1 && await TryMeleeSkills(hp, ct))
            return true;
        if (distance == 1 && await TryFrontalSkillsAtMelee(hp, ct))
            return true;
        if (distance <= 1 && await TrySurroundingSkills(hp, gated: false, ct))
            return true;
        return false;
    }

    // Gated: long-cooldown skills wait for a clump of AoeDensityGate monsters. None is spent on a nearly dead target.
    async Task<bool> TrySurroundingSkills(byte targetHp, bool gated, CancellationToken ct)
    {
        int nearbyCount = -1;
        foreach (var skill in classified!.Surrounding)
        {
            if (!CanFireHpCostSkill(skill.Meta))
                continue;
            double ticks = skill.Ticks(S);
            if (targetHp <= 15 && ticks >= 20)
                continue;
            if (gated && ticks >= AoeDensityTickThreshold)
            {
                if (nearbyCount < 0)
                    nearbyCount = GetSurroundingCreatures().Count();
                if (nearbyCount < AoeDensityGate)
                    continue;
            }

            if (await UseSkill(skill.Name, ct))
            {
                lastFiredSkill = skill.Name;
                await FireFollowUp(ct);
                return true;
            }
        }

        return false;
    }

    // At range 2+ a frontal skill needs the target on our row or column and us facing it.
    async Task<bool> TryFrontalSkills(Entity target, byte targetHp, CancellationToken ct)
    {
        if (!IsAxisAlignedAndFacing(target))
            return false;
        int distance = Distance(Here, target.Position);
        foreach (var skill in classified!.Frontal)
        {
            if (!CanFireHpCostSkill(skill.Meta) || skill.Meta.Range < distance || targetHp <= 15 && skill.Ticks(S) >= 20)
                continue;
            if (await UseSkill(skill.Name, ct))
            {
                lastFiredSkill = skill.Name;
                return true;
            }
        }

        return false;
    }

    async Task<bool> TryFrontalSkillsAtMelee(byte targetHp, CancellationToken ct)
    {
        foreach (var skill in classified!.Frontal)
        {
            if (!CanFireHpCostSkill(skill.Meta) || skill.Meta.Range < 1 || targetHp <= 15 && skill.Ticks(S) >= 20)
                continue;
            if (await UseSkill(skill.Name, ct))
            {
                lastFiredSkill = skill.Name;
                return true;
            }
        }

        return false;
    }

    async Task<bool> TryMeleeSkills(byte targetHp, CancellationToken ct)
    {
        foreach (var skill in classified!.Melee)
        {
            if (!CanFireHpCostSkill(skill.Meta) || targetHp <= 15 && skill.Ticks(S) >= 20)
                continue;
            if (await UseSkill(skill.Name, ct))
            {
                lastFiredSkill = skill.Name;
                return true;
            }
        }

        return false;
    }

    // Charge, Pounce, Sneak Flight: damage and closing the distance in one; Charge needs facing.
    async Task<bool> TryGapCloser(Entity target, int distance, CancellationToken ct)
    {
        foreach (var gc in classified!.GapClosers)
        {
            if (gc.Meta.Range < distance)
                continue;
            if (gc.Meta.RequiresAxisAlign && !IsAxisAlignedAndFacing(target))
                continue;
            if (await UseSkill(gc.Name, ct))
            {
                lastFiredSkill = gc.Name;
                return true;
            }
        }

        return false;
    }

    // Ambush, Shadow Figure, Sneak Attack as an opener; never onto a wall, warp or taken tile.
    async Task<bool> TryTeleportBehind(Entity target, int distance, CancellationToken ct)
    {
        if (classified!.TeleportBehinds.Count == 0)
            return false;
        if (!IsTeleportLandingSafe(Map, TeleportLanding(Here, target.Position)))
            return false;
        foreach (var tp in classified.TeleportBehinds)
        {
            int skillRange = tp.Meta.Range > 0 ? tp.Meta.Range : 1;
            if (skillRange < distance)
                continue;
            if (await UseSkill(tp.Name, ct))
            {
                lastFiredSkill = tp.Name;
                return true;
            }
        }

        return false;
    }

    // Cyclone Blade and the like: free extra damage after any surrounding skill.
    async Task FireFollowUp(CancellationToken ct)
    {
        foreach (var followUp in classified!.FollowUps)
            await UseSkill(followUp.Name, ct);
    }

    // Only buffs that improve assails (Claw Fist); the rest are left to the player.
    async Task TryActivateBuffs(CancellationToken ct)
    {
        foreach (var buff in classified!.Buffs)
            if (buff.Meta.Notes?.Contains("assail", StringComparison.OrdinalIgnoreCase) == true)
                await UseSkill(buff.Name, ct);
    }

    bool IsAxisAlignedAndFacing(Entity target)
    {
        bool axisAligned = target.Position.X == Here.X || target.Position.Y == Here.Y;
        bool facing = DirectionOf(target.Position, Here) == ServerDirection;
        return axisAligned && facing;
    }

    int MaxSkillRange()
    {
        if (classified is null)
            return 3;
        int max = 1;
        foreach (var s in classified.Frontal.Concat(classified.GapClosers))
            if (s.Meta.Range > max)
                max = s.Meta.Range;
        return Math.Min(max, 5);
    }
}
