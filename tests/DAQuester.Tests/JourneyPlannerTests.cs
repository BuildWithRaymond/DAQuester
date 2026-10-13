using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class JourneyPlannerTests
{
    static CharacterSnapshot Observed() => new()
    {
        Name = "Synthetic", Id = 1, Generation = 1, Revision = 5, Connected = true,
        Level = 1, Hp = 100, Gold = 50_000, InventoryKnown = true, LegendsKnown = true,
        LegendsRevision = 5, Weight = 0, MaxWeight = 100, Experience = 0
    };
    static QuestQueueState Queue(params string[] ids) => new() { Character = "Synthetic", Quests = ids };
    static JourneyPlanningCheck Check(string id, CharacterSnapshot s) => new(id switch
    {
        NewCharacterPath.FirstStepsId => NewCharacterPath.FirstStepsEligibility(s),
        NewCharacterPath.ClimbId => NewCharacterPath.ClimbEligibility(s),
        "emerald-dawn" => EmeraldDawnEvidence.Eligibility(s),
        SickChildEvidence.Id => SickChildEvidence.Eligibility(s, new()),
        "perfect-hairstyle" => PerfectHairstyleEvidence.Eligibility(s),
        "spare-a-stick" => StickEligibility.Check(s, null, DateTimeOffset.MaxValue),
        _ => new(EligibilityKind.Unknown, "No supported rule")
    });
    static JourneyPlan Plan(CharacterSnapshot s, params string[] ids) => JourneyPlanner.Plan(Queue(ids), s, Check);

    [Fact]
    public void Starter_goals_explain_the_chain_without_mutating_observations()
    {
        var s = Observed();
        var plan = Plan(s, NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId, "emerald-dawn");
        Assert.Equal([JourneyPlanningState.ReadyNow, JourneyPlanningState.Conditional, JourneyPlanningState.Conditional], plan.Entries.Select(e => e.State));
        Assert.Equal(NewCharacterPath.FirstStepsId, Assert.Single(plan.Entries[1].Conditions).EarlierQuest);
        Assert.Contains("Loures Saber", plan.Entries[1].Reason);
        Assert.Equal(NewCharacterPath.ClimbId, Assert.Single(plan.Entries[2].Conditions).EarlierQuest);
        Assert.Equal(11, plan.Projection.MinimumLevel);
        Assert.Equal(1, s.Level);
        Assert.Empty(s.Equipment);
        Assert.Equal(50_000u, s.Gold);
        Assert.Equal(EligibilityKind.Blocked, Check("emerald-dawn", s).Eligibility.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_missing_or_later_prerequisite_cannot_unlock_climb(bool later)
    {
        var plan = Plan(Observed(), later ? [NewCharacterPath.ClimbId, NewCharacterPath.FirstStepsId, "emerald-dawn"] : [NewCharacterPath.ClimbId, "emerald-dawn"]);
        Assert.Equal(JourneyPlanningState.Blocked, plan.Entries[0].State);
        Assert.Empty(plan.Entries[0].Conditions);
        Assert.Equal(JourneyPlanningState.Blocked, plan.Entries[^1].State);
    }

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(true, 0, true)]
    [InlineData(true, 1, false)]
    public void Missing_identity_or_inventory_cannot_supply_projected_goals(bool connected, uint id, bool inventory)
    {
        var plan = Plan(Observed() with { Connected = connected, Id = id == 0 ? null : id, InventoryKnown = inventory }, NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId);
        Assert.Equal(JourneyPlanningState.Unknown, plan.Entries[0].State);
        Assert.Null(plan.Projection.GearQuest);
        Assert.Empty(plan.Entries[1].Conditions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Stale_legend_revisions_remain_unknown(long revision)
    {
        var plan = Plan(Observed() with { Level = 11, LegendsRevision = revision }, "emerald-dawn");
        Assert.Equal(JourneyPlanningState.Unknown, plan.Entries[0].State);
    }

    [Fact]
    public void Known_resources_and_choice_changes_are_not_hidden_by_dependencies()
    {
        var poor = Plan(Observed() with { Gold = 0 }, NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId, "emerald-dawn");
        Assert.All(poor.Entries, e => Assert.Equal(JourneyPlanningState.Blocked, e.State));
        // Repair costs are unknown; First Steps cannot promise gold for five later handoffs.
        var child = Plan(Observed(), NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId, SickChildEvidence.Id);
        Assert.Equal(JourneyPlanningState.Unknown, child.Entries[^1].State);
        var queue = Queue("emerald-dawn");
        var s = Observed() with { Level = 11 };
        var unchosen = JourneyPlanner.Plan(queue, s, (id, facts) => Check(id, facts) with { ChoicesRequired = true });
        Assert.Equal(JourneyPlanningState.Unknown, unchosen.Entries[0].State);
        Assert.True(unchosen.Entries[0].ChoicesRequired);
        Assert.Equal(JourneyPlanningState.ReadyNow, JourneyPlanner.Plan(queue, s, Check).Entries[0].State);
    }

    [Fact]
    public void Pending_receipts_and_random_rewards_never_supply_dependencies()
    {
        var pending = JourneyPlanner.Plan(Queue(NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId), Observed(), (id, s) => Check(id, s) with { ReconciliationRequired = id == NewCharacterPath.FirstStepsId });
        Assert.Equal(JourneyPlanningState.Unknown, pending.Entries[0].State);
        Assert.Equal(JourneyPlanningState.Blocked, pending.Entries[1].State);
        var random = Plan(Observed(), "perfect-hairstyle", "emerald-dawn");
        Assert.Equal(JourneyPlanningState.Blocked, random.Entries[1].State);
        Assert.Null(random.Projection.LevelQuest);
    }

    [Fact]
    public void A_leveling_goal_cannot_prove_a_later_upper_level_limit()
    {
        var s = Observed() with { Gold = 0 };
        foreach (var gear in NewCharacterPath.ClimbGear)
            s = s with { Equipment = s.Equipment.SetItem(gear.Slot, gear.Name) };
        var plan = Plan(s, NewCharacterPath.ClimbId, "spare-a-stick");
        Assert.Equal(JourneyPlanningState.Unknown, plan.Entries[1].State);
        Assert.Contains("level limit", plan.Entries[1].Reason);
    }

    [Theory]
    [InlineData(25_000, JourneyPlanningState.Unknown)]
    [InlineData(0, JourneyPlanningState.Blocked)]
    public void Hunting_cannot_promise_leftover_gold_or_hide_a_known_shortfall(uint gold, JourneyPlanningState expected)
    {
        var s = Observed() with { Gold = gold };
        foreach (var gear in NewCharacterPath.ClimbGear)
            s = s with { Equipment = s.Equipment.SetItem(gear.Slot, gear.Name) };
        var plan = Plan(s, NewCharacterPath.ClimbId, SickChildEvidence.Id);
        Assert.Equal(expected, plan.Entries[1].State);
        if (expected == JourneyPlanningState.Unknown)
            Assert.Equal(NewCharacterPath.ClimbId, Assert.Single(plan.Entries[1].Conditions).EarlierQuest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_or_missing_observed_goal_stops_the_queue_before_the_dependent_dispatch(bool receipt)
    {
        var s = Observed();
        var store = new FileQuestQueueStore(Path.Combine(Path.GetTempPath(), "DAQuester-queue-" + Guid.NewGuid()));
        var calls = new List<string>();
        var queue = new QuestQueueCoordinator(store, Queue(NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId), () => s, (id, _, _) =>
        {
            if (id == NewCharacterPath.ClimbId && NewCharacterPath.ClimbEligibility(s) is { Kind: not EligibilityKind.Eligible } unmet)
                throw new QuestBlockedException(unmet.Reason);
            calls.Add(id);
            return Task.FromResult(new QuestJob
            {
                Character = s.Name!, Quest = id, Step = receipt ? "complete" : "run", Status = receipt ? "Completed" : "Paused",
                CompletedAt = receipt ? DateTimeOffset.UtcNow : null, Reason = receipt ? "" : "Hunting failed before the goal"
            });
        });
        await queue.RunAsync();
        Assert.Equal([NewCharacterPath.FirstStepsId], calls);
        Assert.Equal("Paused", queue.State.State);
        Assert.Contains(NewCharacterPath.ClimbId, queue.State.Quests);
        Assert.Contains(receipt ? "Missing" : "Hunting failed", queue.State.Reason);
    }
}
