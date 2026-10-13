using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    public JourneyForecast ForecastQueue(string name, Func<DateTimeOffset>? clock = null)
    {
        var queue = Queue(name);
        var helpers = queue.Quests.SelectMany(id => Job(name, id).SickChildOptions?.Helpers ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(helper =>
            {
                var state = Snapshot(helper);
                var availability = !Settings.Characters.Contains(helper, StringComparer.OrdinalIgnoreCase)
                    ? new Eligibility(EligibilityKind.Blocked, "Not a saved helper character")
                    : HelperBusy(helper) is { } busy
                        ? new(EligibilityKind.Blocked, busy)
                        : new(state.Connected ? EligibilityKind.Eligible : EligibilityKind.Unknown, state.Connected ? "Connected helper" : "Fresh helper state unavailable");
                return new ForecastHelper(helper, state, availability);
            }).ToArray();
        return new JourneyResourceForecast(clock ?? (() => DateTimeOffset.UtcNow))
            .Calculate(queue, Snapshot(name), id => Job(name, id), helpers, FlowerPicks());
    }
}
