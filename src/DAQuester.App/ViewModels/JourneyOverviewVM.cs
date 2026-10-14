using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

// Presentation only. Neither a projection nor a reservation is an execution receipt.
public sealed class JourneyOverviewVM
{
    public JourneyPlan Plan
    {
        get;
    }
    public JourneyForecast Forecast
    {
        get;
    }
    public IReadOnlyList<JourneyPlanRowVM> Rows
    {
        get;
    }
    public bool HasQueue => Rows.Count > 0;
    public string Consumption
    {
        get;
    }
    public string StartingBalance
    {
        get;
    }
    public string NextActionBalance
    {
        get;
    }
    public string VerifiedGold
    {
        get;
    }
    public string Shortfall
    {
        get;
    }
    public string Wait
    {
        get;
    }
    public string CompactSummary => HasQueue ? Consumption + " · " + Shortfall : "No journey planned";
    public string ReadinessSummary => !HasQueue ? "No queued quests" : string.Join(" · ", Rows.GroupBy(r => r.State).Select(g => $"{g.Count()} {JourneyPlanRowVM.Label(g.Key).ToLowerInvariant()}"));
    public string[] Uncertainties
    {
        get;
    }
    public string SupplySummary => string.Join(" · ", Forecast.Entries.SelectMany(e => e.Supplies)
        .GroupBy(s => (s.Character, s.Item)).Select(g => $"{g.Key.Character}: {g.Sum(s => s.Quantity)} {g.Key.Item} reserved"));
    public bool HasSupplies => SupplySummary.Length > 0;

    public JourneyOverviewVM(DesktopRuntime runtime, string character, DateTimeOffset now)
    {
        Plan = runtime.PlanQueue(character);
        Forecast = runtime.ForecastQueue(character, () => now);
        Rows = Plan.Entries.Select((entry, index) => new JourneyPlanRowVM(index + 1, runtime.QuestName(entry.Quest), entry,
            Forecast.Entries.FirstOrDefault(f => f.Quest == entry.Quest), runtime.QuestName)).ToArray();
        bool covered = Forecast.Entries.Any(e => e.Covered);
        // A usable automatic recipe may have no resource model yet. Keep those
        // costs explicitly unavailable; absence of an estimate must not look free.
        bool upper = Forecast.Entries.Any(e => e.Covered && e.Gold.Budget > 0
            && (e.Quest == AltarWorshipEvidence.Id || e.Gold.Basis.Contains("Upper bound", StringComparison.OrdinalIgnoreCase)));
        Consumption = !covered ? "Consumption unavailable" : (upper ? "Up to " : "") + Gold(Forecast.KnownGoldBudget)
            + (Forecast.Entries.Any(e => !e.Covered) ? " + unavailable costs" : " consumed");
        StartingBalance = !HasQueue ? "No queued actions" : !covered && Forecast.MinimumStartingBalanceLowerBound == 0
            ? "Starting-balance estimate unavailable" : "At least " + Gold(Forecast.MinimumStartingBalanceLowerBound) + " · lower bound";
        var first = Forecast.Entries.FirstOrDefault();
        NextActionBalance = first is null ? "No queued actions" : first.Covered || first.Gold.MinimumStartingBalance > 0
            ? Gold(first.Gold.MinimumStartingBalance) + " for next action" : "Next-action balance unavailable";
        VerifiedGold = Forecast.UsableOwnedGold is { } owned ? Gold(owned) + " allocated once" : "Gold allocation unavailable";
        Shortfall = Forecast.KnownGoldShortfall is { } shortage ? shortage > 0 ? Gold(shortage) + " short" : "No modeled gold shortfall" : "Gold shortfall unknown";
        Wait = Forecast.Entries.Any(e => e.Timing.MinimumWait is not null)
            ? "At least " + Duration(Forecast.KnownWaitLowerBound) + " known wait" + (Forecast.TimingComplete ? " · lower bound" : " · other waits unavailable")
            : "Wait estimate unavailable";
        Uncertainties = Forecast.Entries.SelectMany(e => e.Unknowns.Select(note => runtime.QuestName(e.Quest) + ": " + note))
            .Concat(Forecast.Unknowns).Distinct().ToArray();
    }

    internal static string Gold(long amount) => amount.ToString("N0") + " gold";
    internal static string Duration(TimeSpan duration)
    {
        double minutes = Math.Max(0, Math.Ceiling(duration.TotalMinutes));
        return minutes >= 60 ? $"{(int)(minutes / 60)}h" + (minutes % 60 > 0 ? $" {(int)(minutes % 60)}m" : "") : $"{(int)minutes}m";
    }
}

public sealed class JourneyPlanRowVM
{
    public string Id
    {
        get;
    }
    public int Position
    {
        get;
    }
    public string Title
    {
        get;
    }
    public JourneyPlanningState State
    {
        get;
    }
    public bool IsReady => State == JourneyPlanningState.ReadyNow;
    public bool IsConditional => State == JourneyPlanningState.Conditional;
    public bool IsBlocked => State == JourneyPlanningState.Blocked;
    public string Summary
    {
        get;
    }
    public string Explanation
    {
        get;
    }
    public string Items
    {
        get;
    }
    public string Gold
    {
        get;
    }
    public string NextBalance
    {
        get;
    }
    public string Wait
    {
        get;
    }
    public string Progress
    {
        get;
    }
    public string Supplies
    {
        get;
    }
    public bool HasSupplies => Supplies.Length > 0;
    public string TimingDetails
    {
        get;
    }
    public string Uncertainties
    {
        get;
    }

    internal JourneyPlanRowVM(int position, string title, JourneyPlanEntry entry, QuestResourceForecast? forecast, Func<string, string> name)
    {
        Id = entry.Quest;
        Position = position;
        Title = title;
        State = entry.State;
        Summary = Label(State) + (entry.ChoicesRequired ? " · choices required" : "")
            + (entry.Conditions.Length > 0 ? " · after " + string.Join(", ", entry.Conditions.Select(c => name(c.EarlierQuest)).Distinct()) : "");
        Explanation = MainWindowViewModel.PlayerQuestReason(entry.Reason) + " "
            + string.Join(" ", entry.Conditions.Select(c => c.Explanation).Where(c => !entry.Reason.Contains(c)));
        bool covered = forecast?.Covered == true;
        Items = !covered ? "Item estimate unavailable" : string.Join(" · ", forecast!.Items.Select(item =>
            $"{item.RemainingRequired} {item.Item} needed · " + (item.UsableOwned is { } owned ? $"{owned} verified/reserved" : "owned unknown")
            + " · " + (item.AcquisitionShortfall is { } missing ? $"{missing} to obtain" : "shortfall unknown")));
        Gold = !covered ? "Consumption unavailable" : (entry.Quest == AltarWorshipEvidence.Id || forecast!.Gold.Basis.Contains("Upper bound") ? "Up to " : "")
            + JourneyOverviewVM.Gold(forecast!.Gold.Budget) + " consumed · "
            + (forecast.Gold.AllocatedOwned is { } allocated ? JourneyOverviewVM.Gold(allocated) + " reserved" : "allocation unknown") + " · "
            + (forecast.Gold.Shortfall is { } shortage ? JourneyOverviewVM.Gold(shortage) + " short" : "shortfall unknown");
        NextBalance = forecast is null || !covered && forecast.Gold.MinimumStartingBalance == 0 ? "Next-action balance unavailable"
            : JourneyOverviewVM.Gold(forecast.Gold.MinimumStartingBalance) + " for next action";
        Wait = forecast?.Timing.MinimumWait is { } wait ? "At least " + JourneyOverviewVM.Duration(wait) + " configured wait · lower bound" : "Wait estimate unavailable";
        Progress = forecast?.RecordedProgress is { } recorded ? $"{recorded} recorded receipts · {forecast.DeclaredProgress ?? 0} player-declared prior actions" : "Progress estimate unavailable";
        Supplies = forecast is null ? "" : string.Join(" · ", forecast.Supplies.Select(s => $"{s.Character}: {s.Quantity} {s.Item} reserved"));
        static string Date(DateTimeOffset? value) => value is { } at ? at.ToLocalTime().ToString("MMM d, h:mm tt") : "unavailable";
        TimingDetails = forecast is null ? "Timing unavailable" : "Eligibility estimate: " + Date(forecast.Timing.EarliestEligibility)
            + " · first action no earlier than " + Date(forecast.Timing.FirstActionNotBefore)
            + " · last action no earlier than " + Date(forecast.Timing.LastActionNotBefore);
        Uncertainties = forecast is null ? "Forecast unavailable" : forecast.Gold.Basis + ". " + forecast.Timing.Basis + ". " + string.Join(" ", forecast.Unknowns);
    }

    public static string Label(JourneyPlanningState state) => state switch
    {
        JourneyPlanningState.ReadyNow => "Ready now",
        JourneyPlanningState.Conditional => "Conditional",
        JourneyPlanningState.Blocked => "Blocked",
        _ => "Unknown"
    };
}
