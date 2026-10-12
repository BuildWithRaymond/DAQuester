namespace DAQuester.Core;

public static class StickCompletionEvidence
{
    public static bool Reply(CharacterSnapshot snapshot, Transaction transaction, Calibration calibration) => snapshot.Generation == transaction.Generation && snapshot.Dialog is { } dialog && dialog.Generation == transaction.Generation && dialog.Revision > transaction.Revision && dialog.Revision <= snapshot.Revision && dialog.Name.Equals("Callough", StringComparison.OrdinalIgnoreCase) && dialog.IsPursuit && dialog.EntityType == 1 && dialog.Type == 0 && (transaction.EntityId is null || dialog.EntityId == transaction.EntityId) && calibration.Matches(dialog.Text, calibration.Completed);
    public static bool Outcome(CharacterSnapshot snapshot, Transaction transaction, long terminalRevision, Calibration calibration) => snapshot.Generation == transaction.Generation && snapshot.Connected && snapshot.InventoryKnown && snapshot.Revision >= terminalRevision && transaction.Branches >= 6 && snapshot.Count(calibration.BranchName) <= transaction.Branches - 6 && snapshot.Experience > transaction.Experience;
    public static QuestJob Complete(QuestJob job, CharacterSnapshot outcome)
    {
        var transaction = job.Pending ?? throw new InvalidOperationException("Recorded turn-in required.");
        var completed = job.CompletionDialog is { } reply && reply.Generation == transaction.Generation && reply.Revision == transaction.TerminalRevision ? reply.ObservedAt ?? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow;
        return job with
        {
            Step = "complete",
            Status = "Completed",
            Reason = "Ordinary Callough reward verified",
            Pending = null,
            CompletedAt = completed,
            LastAttemptAt = completed,
            NextRun = completed.AddHours(8),
            Accepted = false,
            AcceptedGeneration = null,
            CompletionText = null,
            Rewards = RewardEvidence.Record(transaction, outcome)
        };
    }

    public static QuestJob Observe(QuestJob job, Calibration calibration, EvidenceHistory history)
    {
        if (!calibration.Ready || job.Quest != "spare-a-stick" || job.PermanentCompletion is not null || job.Pending is not { Kind: "turn-in" } transaction)
            return job;
        if (!transaction.TerminalSeen || transaction.TerminalRevision is null)
        {
            var reply = history.Find(transaction.Generation, transaction.Revision, s => s.Name?.Equals(job.Character, StringComparison.OrdinalIgnoreCase) == true && Reply(s, transaction, calibration));
            if (reply is null)
                return job;
            transaction = transaction with
            {
                TerminalSeen = true,
                TerminalRevision = reply.Dialog!.Revision
            };
            job = job with
            {
                Pending = transaction,
                CompletionDialog = reply.Dialog
            };
        }

        var outcome = history.Find(transaction.Generation, Math.Max(transaction.Revision, transaction.TerminalRevision.Value - 1), s => s.Name?.Equals(job.Character, StringComparison.OrdinalIgnoreCase) == true && Outcome(s, transaction, transaction.TerminalRevision.Value, calibration));
        return outcome is null ? job : Complete(job, outcome);
    }
}
