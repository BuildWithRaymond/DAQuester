using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class JourneyResourceForecastTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 13, 15, 0, 0, TimeSpan.Zero);
    static CharacterSnapshot Observed(string name = "Synthetic", params string[] items) => new()
    {
        Name = name, Id = 1, Connected = true, Generation = 1, Revision = 10,
        Level = 11, Hp = 100, Gold = 25_000, InventoryKnown = true, Class = "Priest",
        Inventory = items.Select((item, index) => new Item((byte)(index + 1), item, 1)).ToImmutableDictionary(i => i.Slot),
        LegendsKnown = true, LegendsRevision = 10
    };
    static QuestJob Job(string id) => new()
    {
        Character = "Synthetic", Quest = id,
        SickChildOptions = id == SickChildEvidence.Id ? new(SickChildOptions.Daily, [], 0) : null
    };
    static JourneyForecast Forecast(CharacterSnapshot s, QuestJob[] jobs, ForecastHelper[]? helpers = null, SickChildPick[]? picks = null) =>
        new JourneyResourceForecast(() => Now).Calculate(new() { Character = "Synthetic", Quests = jobs.Select(j => j.Quest).ToArray() }, s,
            id => jobs.First(j => j.Quest == id), helpers, picks);
    static ForecastHelper Helper(string name, EligibilityKind kind = EligibilityKind.Eligible, params string[] items) =>
        new(name, Observed(name, items), new(kind, kind == EligibilityKind.Blocked ? "Busy with another action" : "Synthetic helper"));

    [Fact]
    public void Five_child_handoffs_budget_twenty_five_thousand_not_just_the_next_five_thousand()
    {
        var row = Assert.Single(Forecast(Observed(), [Job(SickChildEvidence.Id)]).Entries);
        Assert.Equal(25_000, row.Gold.Budget);
        Assert.Equal(5_000, row.Gold.MinimumStartingBalance);
        Assert.Equal(5, Assert.Single(row.Items).RemainingRequired);
        Assert.Equal(5, row.Items[0].AcquisitionShortfall);
        Assert.Equal(TimeSpan.FromHours(96), row.Timing.MinimumWait);
        Assert.Equal(Now.AddHours(96), row.Timing.LastActionNotBefore);
    }

    [Fact]
    public void Ten_altar_offerings_use_configured_prices_and_twenty_seven_hours_of_spacing()
    {
        var result = Forecast(Observed(), [Job(AltarWorshipEvidence.Id)]);
        var row = Assert.Single(result.Entries);
        Assert.Equal(7_500, row.Gold.Budget);
        Assert.Equal(750, row.Gold.MinimumStartingBalance);
        Assert.Equal(10, row.Items[0].RemainingRequired);
        Assert.Equal(TimeSpan.FromHours(27), row.Timing.MinimumWait);
        Assert.Equal(TimeSpan.FromHours(27), result.KnownWaitLowerBound);
        Assert.Contains("not proof", row.Timing.Basis);
        Assert.Contains("live shop price", row.Gold.Basis);
    }

    [Fact]
    public void Existing_shields_and_recorded_offerings_reduce_the_purchase_budget()
    {
        var job = Job(AltarWorshipEvidence.Id) with
        {
            AltarOfferings = [new(Now.AddHours(-4), 1, 750, "", 0), new(Now.AddHours(-1), 1, 750, "", 0)]
        };
        var row = Assert.Single(Forecast(Observed("Synthetic", AltarWorshipEvidence.Shield, AltarWorshipEvidence.Shield, AltarWorshipEvidence.Shield), [job]).Entries);
        Assert.Equal(2, row.RecordedProgress);
        Assert.Equal(8, row.Items[0].RemainingRequired);
        Assert.Equal(3, row.Items[0].UsableOwned);
        Assert.Equal(5, row.Items[0].AcquisitionShortfall);
        Assert.Equal(3_750, row.Gold.Budget);
        Assert.Equal(0, row.Gold.MinimumStartingBalance);
        Assert.Equal(Now.AddHours(2), row.Timing.FirstActionNotBefore);
        Assert.Equal(TimeSpan.FromHours(23), row.Timing.MinimumWait);
    }

    [Fact]
    public void Child_progress_distinguishes_recorded_receipts_and_declared_turn_ins()
    {
        var job = Job(SickChildEvidence.Id) with
        {
            SickChildOptions = new(SickChildOptions.Daily, [], 1),
            SickChildTurnIns = [new(Now.AddDays(-2), 1, 25_000, 20_000, "", null), new(Now.AddDays(-1), 1, 20_000, 15_000, "", null)]
        };
        var row = Assert.Single(Forecast(Observed("Synthetic", SickChildEvidence.Blossom), [job], picks: [new("Synthetic", Now.AddHours(-1), false)]).Entries);
        Assert.Equal(2, row.RecordedProgress);
        Assert.Equal(1, row.DeclaredProgress);
        Assert.Equal(2, row.Items[0].RemainingRequired);
        Assert.Equal(1, row.Items[0].UsableOwned);
        Assert.Equal(10_000, row.Gold.Budget);
        Assert.Equal(TimeSpan.FromHours(23), row.Timing.MinimumWait);
        Assert.Contains(row.Unknowns, n => n.Contains("declared by the player"));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void Flower_cooldown_boundary_uses_shared_records_and_the_supplied_clock(int secondsUntil, int secondsWait)
    {
        var job = Job(SickChildEvidence.Id) with { SickChildOptions = new(SickChildOptions.Daily, [], 4) };
        var pick = new SickChildPick("Synthetic", Now - SickChildEvidence.PickInterval + TimeSpan.FromSeconds(secondsUntil), true);
        var row = Assert.Single(Forecast(Observed(), [job], picks: [pick]).Entries);
        Assert.Equal(TimeSpan.FromSeconds(secondsWait), row.Timing.MinimumWait);
        Assert.Equal(Now.AddSeconds(secondsWait), row.Timing.FirstActionNotBefore);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void Altar_cooldown_boundary_does_not_invent_an_extra_interval(int secondsUntil, int secondsWait)
    {
        var job = Job(AltarWorshipEvidence.Id) with
        {
            AltarOfferings = Enumerable.Range(0, 9).Select(_ => new AltarOffering(Now - AltarWorshipEvidence.Interval + TimeSpan.FromSeconds(secondsUntil), 1, 750, "", 0)).ToArray()
        };
        var row = Assert.Single(Forecast(Observed(), [job]).Entries);
        Assert.Equal(TimeSpan.FromSeconds(secondsWait), row.Timing.MinimumWait);
    }

    [Fact]
    public void Verified_flowers_bypass_pick_cooldowns_but_saved_picks_do_not_prove_ownership()
    {
        var s = Observed("Synthetic", Enumerable.Repeat(SickChildEvidence.Blossom, 5).ToArray());
        var job = Job(SickChildEvidence.Id) with { SickChildPicks = [new("Synthetic", Now, false)] };
        var row = Assert.Single(Forecast(s, [job]).Entries);
        Assert.Equal(5, row.Items[0].UsableOwned);
        Assert.Equal(0, row.Items[0].AcquisitionShortfall);
        Assert.Equal(TimeSpan.Zero, row.Timing.MinimumWait);
        var empty = Assert.Single(Forecast(Observed(), [job]).Entries);
        Assert.Equal(0, empty.Items[0].UsableOwned);
        Assert.Equal(TimeSpan.FromHours(120), empty.Timing.MinimumWait);
    }

    [Fact]
    public void Helpers_share_pick_cooldowns_and_only_fresh_eligible_supplies_are_credited()
    {
        var job = Job(SickChildEvidence.Id) with { SickChildOptions = new(SickChildOptions.WithHelpers, ["Wren", "Lark", "Robin"], 0) };
        var row = Assert.Single(Forecast(Observed(), [job], [Helper("Wren"), Helper("Lark"), Helper("Robin")]).Entries);
        Assert.Equal(TimeSpan.FromHours(24), row.Timing.MinimumWait);
        var one = job with { SickChildOptions = new(SickChildOptions.WithHelpers, ["Wren"], 4) };
        var supplies = Assert.Single(Forecast(Observed(), [one], [Helper("Wren", items: [SickChildEvidence.Blossom])], [new("Wren", Now, false), new("Synthetic", Now, true)]).Entries);
        Assert.Equal("Wren", Assert.Single(supplies.Supplies).Character);
        Assert.Equal(1, supplies.Items[0].UsableOwned);
        Assert.Equal(TimeSpan.Zero, supplies.Timing.MinimumWait);
        var savedOnly = one with { SickChildPicks = [new("Wren", Now, false)] };
        var noSupply = Assert.Single(Forecast(Observed(), [savedOnly], [Helper("Wren")], [new("Synthetic", Now, false)]).Entries);
        Assert.Empty(noSupply.Supplies);
        Assert.Equal(TimeSpan.FromHours(24), noSupply.Timing.MinimumWait);
    }

    [Fact]
    public void Blocked_helpers_are_excluded_and_unknown_helpers_make_the_wait_unavailable()
    {
        var job = Job(SickChildEvidence.Id) with { SickChildOptions = new(SickChildOptions.WithHelpers, ["Wren"], 0) };
        var blocked = Assert.Single(Forecast(Observed(), [job], [Helper("Wren", EligibilityKind.Blocked, SickChildEvidence.Blossom)]).Entries);
        Assert.Equal(0, blocked.Items[0].UsableOwned);
        Assert.Equal(TimeSpan.FromHours(96), blocked.Timing.MinimumWait);
        Assert.Contains(blocked.Unknowns, n => n.Contains("excluded"));
        var unknown = Forecast(Observed(), [job], [Helper("Wren", EligibilityKind.Unknown, SickChildEvidence.Blossom)]);
        Assert.Null(unknown.Entries[0].Timing.MinimumWait);
        Assert.Equal(0, unknown.Entries[0].Items[0].UsableOwned);
        Assert.False(unknown.TimingComplete);
    }

    [Fact]
    public void Gold_is_reserved_once_across_the_journey_and_no_future_rewards_are_credited()
    {
        var s = Observed("Synthetic", Enumerable.Repeat(SickChildEvidence.Blossom, 5).ToArray());
        var result = Forecast(s, [Job(SickChildEvidence.Id), Job(AltarWorshipEvidence.Id)]);
        Assert.Equal(32_500, result.KnownGoldBudget);
        Assert.Equal(32_500, result.MinimumStartingBalanceLowerBound);
        Assert.Equal(25_000, result.UsableOwnedGold);
        Assert.Equal(7_500, result.KnownGoldShortfall);
        Assert.Equal(25_000, result.Entries[0].Gold.AllocatedOwned);
        Assert.Equal(0, result.Entries[1].Gold.AllocatedOwned);
        Assert.Null(result.Entries[1].Timing.EarliestEligibility);
        Assert.Equal(25_000u, s.Gold);
        Assert.Equal(5, s.Count(SickChildEvidence.Blossom));
    }

    [Fact]
    public void Cooldowns_overlap_other_quest_waits_instead_of_being_summed()
    {
        var result = Forecast(Observed() with { Gold = 100_000 }, [Job(AltarWorshipEvidence.Id), Job(SickChildEvidence.Id)],
            picks: [new("Synthetic", Now.AddHours(-1), false)]);
        Assert.Equal(TimeSpan.FromHours(27), result.Entries[0].Timing.MinimumWait);
        Assert.Equal(Now.AddHours(27), result.Entries[1].Timing.FirstActionNotBefore);
        Assert.Equal(TimeSpan.FromHours(96), result.Entries[1].Timing.MinimumWait);
        Assert.Equal(TimeSpan.FromHours(123), result.KnownWaitLowerBound);
    }

    [Fact]
    public void Full_bags_and_helper_transfers_report_capacity_without_requiring_all_materials_at_once()
    {
        var full = Observed("Synthetic", Enumerable.Repeat("Synthetic item", CharacterSnapshot.InventoryCapacity).ToArray());
        var row = Assert.Single(Forecast(full, [Job(SickChildEvidence.Id)]).Entries);
        Assert.Equal(1, row.Capacity!.FreeSlotsRequiredForAcquisition);
        Assert.Equal(1, row.Capacity.AdditionalFreeSlotsNeededNow);
        Assert.Equal(EligibilityKind.Blocked, row.Eligibility.Kind);
        var job = Job(SickChildEvidence.Id) with { SickChildOptions = new(SickChildOptions.WithHelpers, ["Wren"], 4) };
        var trade = Assert.Single(Forecast(full, [job], [Helper("Wren", items: [SickChildEvidence.Blossom])]).Entries);
        Assert.Equal(1, trade.Capacity!.AdditionalFreeSlotsNeededNow);
        Assert.Equal(0, trade.Items[0].AcquisitionShortfall);
    }

    [Fact]
    public void Unknown_or_uncovered_state_never_reuses_supplies_or_authorizes_spending()
    {
        var s = Observed("Synthetic", SickChildEvidence.Blossom) with { Connected = false };
        var unknown = Forecast(s, [Job(SickChildEvidence.Id)]);
        Assert.Null(unknown.Entries[0].Items[0].UsableOwned);
        Assert.Null(unknown.Entries[0].Items[0].AcquisitionShortfall);
        Assert.Null(unknown.UsableOwnedGold);
        Assert.Null(unknown.Entries[0].Timing.EarliestEligibility);
        var uncovered = Forecast(Observed(), [Job(NewCharacterPath.FirstStepsId), Job(SickChildEvidence.Id)]);
        Assert.Equal(50_000, uncovered.MinimumStartingBalanceLowerBound);
        Assert.False(uncovered.ResourcesComplete);
        Assert.Null(uncovered.Entries[1].Gold.AllocatedOwned);
        Assert.Null(uncovered.Entries[1].Items[0].UsableOwned);
        Assert.Contains(uncovered.Unknowns, n => n.Contains("do not authorize"));
    }

    [Fact]
    public void Unresolved_submission_and_unverified_final_reward_do_not_become_progress_evidence()
    {
        var pending = Job(SickChildEvidence.Id) with { Pending = new("sick-turnin", 1, 10, 1, 0, 0, Now) };
        var row = Assert.Single(Forecast(Observed(), [pending]).Entries);
        Assert.Equal(5, row.Items[0].RemainingRequired);
        Assert.Equal(0, row.RecordedProgress);
        Assert.Null(row.Timing.MinimumWait);
        Assert.NotNull(pending.Pending);
        var offerings = Job(AltarWorshipEvidence.Id) with { AltarOfferings = Enumerable.Range(0, 10).Select(_ => new AltarOffering(Now, 1, 750, "", 0)).ToArray() };
        var final = Assert.Single(Forecast(Observed(), [offerings]).Entries);
        Assert.Equal(0, final.Items[0].RemainingRequired);
        Assert.Equal(0, final.Gold.Budget);
        Assert.Contains(final.Unknowns, n => n.Contains("final reward still needs verification"));
        Assert.Null(offerings.CompletedAt);
    }

    [Fact]
    public void Clock_is_read_once_and_duplicate_saved_entries_do_not_double_reserve_resources()
    {
        int calls = 0;
        var job = Job(SickChildEvidence.Id);
        var result = new JourneyResourceForecast(() => { calls++; return Now; }).Calculate(
            new() { Character = "Synthetic", Quests = [job.Quest, job.Quest] }, Observed(), _ => job);
        Assert.Equal(1, calls);
        Assert.Single(result.Entries);
        Assert.Equal(25_000, result.KnownGoldBudget);
        Assert.Equal(Now, result.CalculatedAt);
    }

    [Fact]
    public void Invalid_choices_and_stale_legends_leave_uncertainty_explicit()
    {
        var job = Job(SickChildEvidence.Id) with { SickChildOptions = new(SickChildOptions.Daily, [], 5) };
        var invalid = Forecast(Observed(), [job]);
        Assert.Equal(5, invalid.Entries[0].Items[0].RemainingRequired);
        Assert.Null(invalid.Entries[0].Timing.MinimumWait);
        Assert.False(invalid.ResourcesComplete);
        var stale = Assert.Single(Forecast(Observed() with { LegendsRevision = 11, Legends = [SickChildEvidence.Legend] }, [Job(SickChildEvidence.Id)]).Entries);
        Assert.Equal(5, stale.Items[0].RemainingRequired);
        Assert.Null(stale.Timing.EarliestEligibility);
    }

    [Fact]
    public void Unknown_shield_inventory_can_show_an_upper_budget_but_no_known_gold_shortfall()
    {
        var forecast = Forecast(Observed() with { InventoryKnown = false, Gold = 1_000 }, [Job(AltarWorshipEvidence.Id)]);
        var row = Assert.Single(forecast.Entries);
        Assert.Equal(7_500, row.Gold.Budget);
        Assert.Null(row.Gold.Shortfall);
        Assert.Null(forecast.KnownGoldShortfall);
        Assert.Equal(TimeSpan.FromHours(27), row.Timing.MinimumWait);
    }

    [Fact]
    public void A_saved_verified_completion_needs_no_materials_even_without_a_live_legend()
    {
        var complete = Job(SickChildEvidence.Id) with { Step = "complete", Status = "Completed", CompletedAt = Now };
        var row = Assert.Single(Forecast(Observed() with { LegendsKnown = false }, [complete]).Entries);
        Assert.Equal(0, row.Items[0].RemainingRequired);
        Assert.Equal(0, row.Gold.Budget);
        Assert.Equal(TimeSpan.Zero, row.Timing.MinimumWait);
        Assert.Equal(Now, complete.CompletedAt);
    }

    [Fact]
    public void All_required_flowers_already_owned_need_no_pick_wait_even_if_choices_are_missing()
    {
        var s = Observed("Synthetic", Enumerable.Repeat(SickChildEvidence.Blossom, 5).ToArray());
        var job = Job(SickChildEvidence.Id) with { SickChildOptions = null };
        var row = Assert.Single(Forecast(s, [job]).Entries);
        Assert.Equal(TimeSpan.Zero, row.Timing.MinimumWait);
        Assert.Equal(0, row.Items[0].AcquisitionShortfall);
        Assert.Null(row.Timing.EarliestEligibility);
    }
}
