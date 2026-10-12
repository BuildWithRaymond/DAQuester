namespace DAQuester.Core;

public static class QuestRunPolicy
{
    public static bool AttemptCompleted(QuestJob job) => job.Pending is null && job.Step == "complete" && job.CompletedAt is not null;
    public static bool Finished(QuestDefinition recipe, QuestJob job) => job.PermanentCompletion is not null || (!recipe.Repeatable && recipe.Id != ShinewoodFaerieEvidence.Id && job.CompletedAt is not null) || (recipe.Id == "perfect-hairstyle" && job.LegendReward == true);
    public static DateTimeOffset? NextAttempt(QuestDefinition recipe, QuestJob job)
    {
        // The flower and altar runs wait between picks and offerings themselves; that wait is never a cooldown.
        if (Finished(recipe, job) || SickChildEvidence.For(recipe.Id) is not null || recipe.Id == AltarWorshipEvidence.Id)
            return null;
        var next = job.NextRun;
        if (recipe.Repeatable && job.CompletedAt is { } completed)
        {
            var cooldown = completed.AddHours(recipe.CooldownHours);
            if (next is null || cooldown > next)
                next = cooldown;
        }

        return next;
    }
}
