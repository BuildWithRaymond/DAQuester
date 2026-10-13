using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    public event Action<QuestCompletion>? QuestCompleted;
    readonly HashSet<(string Character, string Quest, DateTimeOffset At)> celebrated = [];

    void PublishCompletion(QuestCompletion result)
    {
        if (celebrated.Add((result.Character.ToUpperInvariant(), result.Quest.ToUpperInvariant(), result.At)))
            QuestCompleted?.Invoke(result);
    }

    internal void ObserveRunner(string character, QuestRunner runner)
    {
        var completed = new QuestCompletionTracker(runner.Job);
        runner.Changed += job => Ui(() =>
        {
            Notify();
            if (completed.Observe(job) is { } outcome)
                PublishCompletion(outcome);
            Log(character + ": " + QuestName(job.Quest) + " · " + job.Step + " · " + job.Status + " " + job.Reason);
        });
    }
}
