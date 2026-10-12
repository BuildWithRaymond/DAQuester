namespace DAQuester.Core;

public static class QueueAttemptOutcome
{
    public static bool HasTerminalReceipt(QuestJob job)
    {
        if (job.Pending is not null)
            return false;
        if (job.Quest == "mundane-birth")
            return job.Step == "confirm" && job.BirthCompletion is { } birth && MundaneBirthEvidence.Matches(birth) && job.CompletedAt == birth.ObservedAt;
        if (job.Quest == "entered-muisir-village")
            return job.Step == "confirm" && job.CompletedAt is not null && job.CompletionLegend is { } legend && (legend.Equals(MuisirEvidence.Legend, StringComparison.OrdinalIgnoreCase) || legend.StartsWith(MuisirEvidence.Legend + " -", StringComparison.OrdinalIgnoreCase)) && job.Rewards.GetValueOrDefault("mushroomsConsumed") == 5 && job.Rewards.GetValueOrDefault("item:" + MuisirEvidence.Potion) == 4;
        if (job.Quest == ShinewoodFaerieEvidence.Id)
            return job.Step == "return" && job.CompletedAt is { } completed && job.FaerieReturn is { } returned && returned.VerifiedAt >= completed;
        if (job.Quest != "perfect-hairstyle" || job.Step != "confirm" || job.LastAttemptAt is not { } at || job.HairstyleCompletion is not { } reply || !reply.Name.Equals("Eeva", StringComparison.OrdinalIgnoreCase) || !reply.IsPursuit || reply.EntityType != 1 || reply.Type != 0 || reply.Choices.Length != 0 || reply.Text is not (PerfectHairstyleEvidence.Completion or PerfectHairstyleEvidence.TwoRingCompletion))
            return false;
        return job.HairstyleAttempts.Any(a => a.CompletedAt == at && a.Completion.Generation == reply.Generation && a.Completion.Revision == reply.Revision && a.Completion.EntityId == reply.EntityId && a.Completion.Text == reply.Text && a.Completion.ObservedAt == reply.ObservedAt);
    }

    public static QueueOutcome? Evaluate(QuestJob job)
    {
        if (job.Pending is not null || job.Status is not ("Completed" or "AttemptCompleted" or "PermanentlyCompleted"))
            return null;
        if (job.PermanentCompletion is { } permanent)
            return new(job.Quest, "PermanentlyCompleted", permanent.ObservedAt, job.CompletionText ?? job.Reason);
        if (job.Quest == "perfect-hairstyle" && job.LegendReward != true)
        {
            if (job.LastAttemptAt is not { } at || !job.HairstyleAttempts.Any(a => a.CompletedAt == at) || job.Step is not ("complete" or "preflight") && !HasTerminalReceipt(job))
                return null;
            return new(job.Quest, "AttemptCompleted", at, "Weekly attempt verified; Suomi legend still needed" + (job.NextRun is { } next ? ". Next attempt " + next.ToLocalTime().ToString("g") : ""));
        }

        if (job.Step != "complete" && !HasTerminalReceipt(job) || job.CompletedAt is not { } completed)
            return null;
        if (job.Quest == ShinewoodFaerieEvidence.Id)
        {
            if (job.FaerieReturn is null)
                return null;
            return new(job.Quest, job.FaeriePetWait is null ? "Completed" : "AttemptCompleted", completed, job.CompletionText ?? "Faerie visit and return verified");
        }

        return new(job.Quest, "Completed", completed, job.CompletionText ?? "Completion verified");
    }

    public static string BlockReason(QuestJob job) => job.Pending is not null ? "Saved submission requires reconciliation before the queue can continue." : !string.IsNullOrWhiteSpace(job.Reason) ? job.Reason : "Quest attempt has no verified outcome. Resume to reconcile its checkpoint.";
}
