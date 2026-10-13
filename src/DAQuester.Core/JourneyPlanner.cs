namespace DAQuester.Core;

public enum JourneyPlanningState
{
    ReadyNow,
    Conditional,
    Blocked,
    Unknown
}

public sealed record JourneyCondition(string EarlierQuest, string Explanation);
public sealed record JourneyPlanningCheck(Eligibility Eligibility, bool ChoicesRequired = false, bool ReconciliationRequired = false);
public sealed record JourneyPlanEntry(string Quest, JourneyPlanningState State, string Reason, JourneyCondition[] Conditions)
{
    public bool ChoicesRequired { get; init; }
}

// These facts describe successful future goals. They are never character observations or receipts.
public sealed record JourneyProjection(int? MinimumLevel, string? LevelQuest, StarterGear[] Gear, string? GearQuest);
public sealed record JourneyPlan(JourneyPlanEntry[] Entries, JourneyProjection Projection);

public static class JourneyPlanner
{
    // The caller supplies its existing availability rules, including memory, cooldowns and calibration.
    // Copies used to evaluate a projection stay inside this method and must never enter persistence.
    public static JourneyPlan Plan(QuestQueueState queue, CharacterSnapshot observed, Func<string, CharacterSnapshot, JourneyPlanningCheck> check)
    {
        var entries = new List<JourneyPlanEntry>();
        var projected = observed;
        string? levelQuest = null, gearQuest = null;
        StarterGear[] gear = [];
        bool fresh = observed.Connected && observed.Id is not null && observed.Generation > 0 && observed.Revision > 0
            && string.Equals(observed.Name, queue.Character, StringComparison.OrdinalIgnoreCase);
        if (!fresh)
            projected = observed with { Connected = false };
        if (!observed.LegendsKnown || observed.LegendsRevision <= 0 || observed.LegendsRevision > observed.Revision)
            projected = projected with { LegendsKnown = false, Legends = [] };

        foreach (string id in queue.Quests)
        {
            var future = check(id, projected);
            var capital = check(id, projected with { Gold = observed.Gold });
            // Spending cannot fix a shortage that is already known, and future loot is not promised.
            if (projected.Gold is null && future.Eligibility.Kind == EligibilityKind.Unknown && capital.Eligibility.Kind == EligibilityKind.Blocked)
                future = future with { Eligibility = capital.Eligibility };
            var conditions = new List<JourneyCondition>();
            if (levelQuest is not null && projected.Level != observed.Level
                && check(id, projected with { Level = observed.Level, Gold = observed.Gold }).Eligibility.Kind == EligibilityKind.Blocked
                && capital.Eligibility.Kind != EligibilityKind.Blocked)
                conditions.Add(new(levelQuest, "Successful completion of " + LevelName(levelQuest) + " reaches at least level " + projected.Level + ". Fresh level must confirm this before dispatch."));
            if (gearQuest is not null && id == NewCharacterPath.ClimbId && NewCharacterPath.MissingForClimb(observed).Count > 0)
                conditions.Add(new(gearQuest, "Successful completion of " + NewCharacterPath.FirstStepsName + " provides "
                    + string.Join(", ", NewCharacterPath.MissingForClimb(observed).Select(g => g.Name)) + ". Fresh ownership must confirm each item before dispatch."));

            var state = future.Eligibility.Kind switch
            {
                EligibilityKind.Blocked => JourneyPlanningState.Blocked,
                EligibilityKind.Unknown => JourneyPlanningState.Unknown,
                _ => conditions.Count > 0 ? JourneyPlanningState.Conditional : JourneyPlanningState.ReadyNow
            };
            string reason = future.Eligibility.Reason;
            if (!fresh && state != JourneyPlanningState.Blocked)
            {
                state = JourneyPlanningState.Unknown;
                reason = "Waiting for fresh connected character identity and state.";
            }
            if (levelQuest is not null && id == "spare-a-stick" && state != JourneyPlanningState.Blocked)
            {
                state = JourneyPlanningState.Unknown;
                reason = "The earlier leveling goal supplies a minimum level, not proof that the character will remain within Spare a Stick's level limit.";
            }
            if (future.ReconciliationRequired)
            {
                state = JourneyPlanningState.Unknown;
                reason = "Saved submission must reconcile before its result can satisfy a later quest.";
            }
            // A producer's explicit goals do not depend on a random reward or an outcome choice.
            bool suppliesGoal = fresh && !future.ReconciliationRequired && future.Eligibility.Kind == EligibilityKind.Eligible;
            if (future.ChoicesRequired && state is JourneyPlanningState.ReadyNow or JourneyPlanningState.Conditional)
            {
                state = JourneyPlanningState.Unknown;
                reason = "Required choices have not been saved. " + reason;
            }
            if (state == JourneyPlanningState.Conditional)
                reason = string.Join(" ", conditions.Select(c => c.Explanation)) + " " + reason;
            entries.Add(new(id, state, reason, conditions.ToArray()) { ChoicesRequired = future.ChoicesRequired });

            if (!suppliesGoal)
                continue;
            if (id == NewCharacterPath.FirstStepsId)
            {
                gear = NewCharacterPath.ClimbGear;
                gearQuest = id;
                var equipment = projected.Equipment;
                foreach (var item in gear)
                    equipment = equipment.SetItem(item.Slot, item.Name);
                projected = projected with { Equipment = equipment };
                if (projected.Level is { } level && level < NewCharacterPath.FirstStepsLevel)
                {
                    projected = projected with { Level = NewCharacterPath.FirstStepsLevel };
                    levelQuest = id;
                }
                // Purchases and repairs have variable live costs. Do not promise leftover gold.
                projected = projected with { Gold = null };
            }
            if (id == NewCharacterPath.ClimbId && projected.Level is { } start && start < NewCharacterPath.ClimbLevel)
            {
                projected = projected with { Level = NewCharacterPath.ClimbLevel, Gold = null };
                levelQuest = id;
            }
        }
        return new(entries.ToArray(), new(projected.Level, levelQuest, gear.ToArray(), gearQuest));
    }

    static string LevelName(string quest) => quest == NewCharacterPath.ClimbId ? NewCharacterPath.ClimbName : NewCharacterPath.FirstStepsName;
}
