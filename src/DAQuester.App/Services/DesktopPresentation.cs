using DAQuester.Core;

namespace DAQuester.App.Services;

// A read-only plan/cache owned by one view-model refresh. Its reader uses display
// receipts; commands, reconciliation and dispatch still read the real journal.
// Explicit refreshes and the background scan pick up checkpoints written elsewhere.
internal sealed class DesktopPresentation(DesktopRuntime runtime, Func<string, string, QuestJob> readSaved)
{
    readonly Dictionary<(string Character, string Quest), QuestJob> jobs = [];
    readonly Dictionary<(string Character, QuestQueueState Queue), JourneyPlan> plans = [];
    readonly Dictionary<(string Character, string Quest), QuestAvailability> availability = [];

    QuestJob SavedJob(string character, string quest)
    {
        var key = (character.ToUpperInvariant(), quest);
        if (!jobs.TryGetValue(key, out var job))
            jobs[key] = job = readSaved(character, quest);
        return job;
    }

    internal QuestJob Job(string character, string quest) => runtime.Job(character, quest, SavedJob);
    internal JourneyPlan PlanQueue(string character) => PlanQueue(character, runtime.Queue(character));

    JourneyPlan PlanQueue(string character, QuestQueueState queue)
    {
        // Several library rows inspect the same queued dependencies. Share that
        // plan, but keep hypothetical appended queues and characters separate.
        var key = (character.ToUpperInvariant(), queue);
        if (!plans.TryGetValue(key, out var plan))
            plans[key] = plan = runtime.PlanQueue(character, queue, SavedJob);
        return plan;
    }

    internal QuestAvailability PlanningAvailability(string character, string quest)
    {
        var key = (character.ToUpperInvariant(), quest);
        if (!availability.TryGetValue(key, out var value))
            availability[key] = value = runtime.PlanningAvailability(character, quest, SavedJob, queue => PlanQueue(character, queue));
        return value;
    }
    internal JourneyForecast ForecastQueue(string character, DateTimeOffset now) => runtime.ForecastQueue(character, () => now, SavedJob);
}
