using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed record QuestCompletion(string Character, string Quest, string Status, DateTimeOffset At);

// Presentation follows the existing receipt contract; it cannot establish completion.
internal sealed class QuestCompletionTracker
{
    readonly HashSet<(string Character, string Quest, DateTimeOffset At)> seen = [];

    public QuestCompletionTracker(QuestJob initial)
    {
        // Load normalizes saved jobs to Paused. Seed verified old receipts without
        // changing the saved job or emitting an event when that job is resumed.
        Observe(initial with { Status = "Completed" });
    }

    public QuestCompletion? Observe(QuestJob job)
    {
        var outcome = QueueAttemptOutcome.Evaluate(job);
        // Individual weekly runs go directly from a saved reward to Scheduled.
        // Evaluate the same receipt as an attempt; this never changes queue state.
        if (outcome is null && job.Quest == "perfect-hairstyle" && job.Status is "Running" or "Scheduled")
            outcome = QueueAttemptOutcome.Evaluate(job with { Status = "AttemptCompleted" });
        if (string.IsNullOrWhiteSpace(job.Character) || outcome is null
            || !seen.Add((job.Character.ToUpperInvariant(), job.Quest.ToUpperInvariant(), outcome.At)))
            return null;
        return new(job.Character, job.Quest, outcome.Outcome, outcome.At);
    }
}
