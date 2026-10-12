using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed record QuestProgress(int Total, int Completed, int Current, string Label)
{
    public double Percent => Total == 0 ? 0 : Completed * 100d / Total;

    public static QuestProgress From(QuestDefinition? quest, QuestJob job)
    {
        if (quest?.Id == AltarWorshipEvidence.Id)
        {
            int made = Math.Min(AltarWorshipEvidence.Made(job), AltarWorshipEvidence.Goal);
            return new(AltarWorshipEvidence.Goal, made, made < AltarWorshipEvidence.Goal ? made + 1 : 0, made + " of " + AltarWorshipEvidence.Goal + " offerings");
        }

        int total = quest?.Steps.Length ?? 0;
        if (total == 0)
            return new(0, 0, 0, "Select an automatic quest to see its steps");
        if (job.Step == "complete" && job.Pending is null)
            return new(total, total, 0, $"{total} of {total} steps complete");
        int index = Array.FindIndex(quest!.Steps, step => step.Id == job.Step);
        if (index < 0)
            return new(total, 0, 0, $"{total} steps · checkpoint unavailable");
        return new(total, index, index + 1, $"Step {index + 1} of {total}");
    }
}
