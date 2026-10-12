using System.Text.RegularExpressions;

namespace DAQuester.Core;

public record PermanentCompletionEvidence(DateTimeOffset ObservedAt, Dialog Dialog);
public sealed class QuestPermanentlyCompletedException(Dialog dialog) : Exception("Callough confirmed permanent completion: " + dialog.Text)
{
    public Dialog Dialog { get; } = dialog;
}

public static class PermanentQuestCompletion
{
    public static async Task<QuestJob?> ObserveAsync(IQuestJournal journal, CharacterSnapshot snapshot, Func<Task> stopOwners)
    {
        if (snapshot.Name is null || !snapshot.Connected || !Matches(snapshot))
            return null;
        await stopOwners(); // Read the durable baseline after every active writer has released ownership.
        var job = journal.Load(snapshot.Name, "spare-a-stick");
        if (job.PermanentCompletion is not null)
            return job;
        var recorded = Record(job, snapshot.Dialog!, snapshot);
        await journal.SaveAsync(recorded);
        return recorded;
    }

    // Player-specified server refusal rule. Keep the full observed reply for review.
    static readonly Regex childReminder = new(@"\bremind(?:s|ed|ing)?\s+(?:me|him|us)\s+of\b.{0,100}\bchild\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
    public static bool Matches(CharacterSnapshot snapshot) => snapshot.Dialog is { } d && d.Generation == snapshot.Generation && d.Revision <= snapshot.Revision && d.IsPursuit && d.Name.Equals("Callough", StringComparison.OrdinalIgnoreCase) && childReminder.IsMatch(d.Text);
    public static void Check(CharacterSnapshot snapshot)
    {
        if (Matches(snapshot))
            throw new QuestPermanentlyCompletedException(snapshot.Dialog!);
    }

    public static QuestJob Record(QuestJob job, Dialog dialog, CharacterSnapshot? snapshot = null)
    {
        bool proven = job.Pending is { Kind: "turn-in" } t && snapshot is not null && snapshot.Generation == t.Generation && snapshot.Revision >= dialog.Revision && snapshot.InventoryKnown && snapshot.Count("Branch") <= t.Branches - 6 && snapshot.Experience > t.Experience;
        return job with
        {
            Step = "complete",
            Status = "PermanentlyCompleted",
            Reason = "Callough child-reminder reply: quest finished permanently",
            Repeat = false,
            NextRun = null,
            Accepted = false,
            AcceptedGeneration = null,
            PermanentCompletion = new(DateTimeOffset.UtcNow, dialog),
            CompletedAt = job.CompletedAt ?? DateTimeOffset.UtcNow,
            Rewards = proven ? RewardEvidence.Record(job.Pending!, snapshot!) : job.Rewards,
            Pending = proven ? null : job.Pending
        };
    }
}
