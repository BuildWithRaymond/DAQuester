using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed record QuestProgress(int Total, int Completed, int Current, string Label)
{
    public double Percent => Total == 0 ? 0 : Completed * 100d / Total;

    public static QuestProgress From(QuestDefinition? quest, QuestJob job)
    {
        if (quest?.Id == DaydreamerEvidence.Id)
        {
            bool goal = DaydreamerEvidence.GoalVerified(job);
            int made = goal ? DaydreamerEvidence.Goal : Math.Clamp(job.Daydreamer?.Streak ?? 0, 0, DaydreamerEvidence.Goal);
            string label = goal && job.Repeat
                ? "First 30 verified · server count " + (job.Daydreamer?.Streak?.ToString() ?? "unknown") + " · continuation on"
                : made + " of 30 check-ins";
            return new(DaydreamerEvidence.Goal, made, made < DaydreamerEvidence.Goal ? made + 1 : 0, label);
        }
        if (quest?.Journey.Length > 0)
        {
            int count = DanaanTransport.Journey(quest, job).Length;
            if (count == 0) return new(0,0,0,"Saved quest path unavailable");
            bool done = job.Step == "complete" && job.Pending is null && job.Route?.CleanupPending != true;
            int made = done ? count : Math.Clamp(job.Route?.Task ?? 0, 0, count - 1);
            return new(count, made, done ? 0 : made + 1, done ? $"{count} of {count} stages complete" : $"Stage {made + 1} of {count}");
        }
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
