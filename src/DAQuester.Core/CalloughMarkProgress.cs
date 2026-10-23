using System.Text.RegularExpressions;

namespace DAQuester.Core;

public static class CalloughMarkProgress
{
    static readonly Regex mark = new(@"^Loved by Mileth Mundanes(?: \([0-9]+\))?(?: - .*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string? Legend(IEnumerable<string> legends) => legends.FirstOrDefault(l => mark.IsMatch(l.TrimStart('-', ' ')));

    public static bool Recorded(QuestJob job, Dialog dialog) => job.CalloughOutcomes.Any(o =>
        o.Dialog.Generation == dialog.Generation && o.Dialog.Revision == dialog.Revision &&
        o.Dialog.EntityId == dialog.EntityId && o.Dialog.ObservedAt == dialog.ObservedAt && o.Dialog.Text == dialog.Text);

    // Only the explicitly approved scheduled path calls this. Preserve the old
    // outcome for audit; its timestamp still owns the next eligible attempt.
    public static QuestJob Resume(QuestJob job, DateTimeOffset obtainedAt)
    {
        if (job.Quest != "spare-a-stick" || job.PermanentCompletion is not { } outcome || outcome.ObservedAt != obtainedAt)
            throw new QuestBlockedException("Callough's saved outcome changed. Review the current mark before trying for more.");
        if (job.Pending is not null || StickRewardCleanup.Unfinished(job))
            throw new QuestBlockedException("Reconcile the saved Callough submission before trying for more marks.");
        var completed = job.CompletedAt is { } previous && previous > obtainedAt ? previous : obtainedAt;
        var due = completed.AddHours(8);
        if (job.NextRun is { } saved && saved > due) due = saved;
        return job with
        {
            CalloughOutcomes = Recorded(job, outcome.Dialog) ? job.CalloughOutcomes : [.. job.CalloughOutcomes, outcome],
            PermanentCompletion = null, Step = "complete", Status = "Completed", Repeat = true,
            CompletedAt = completed, LastAttemptAt = completed, NextRun = due,
            Accepted = false, AcceptedGeneration = null, CompletionDialog = null,
            Reason = "Approved to try for more Callough marks; previous outcome retained"
        };
    }
}
