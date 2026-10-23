using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed record SchedulerCalloughGoal(DateTimeOffset At, string Description);

public sealed partial class DesktopRuntime
{
    public SchedulerCalloughGoal? SchedulerCalloughMark(QuestSchedule entry)
    {
        if (entry.Quest != "spare-a-stick") return null;
        var job = journal.Load(entry.Character, entry.Quest);
        if (job.Pending is not null || StickRewardCleanup.Unfinished(job) || job.PermanentCompletion is not { } outcome || entry.CalloughRetryAfter == outcome.ObservedAt) return null;
        var snapshot = Snapshot(entry.Character);
        var memory = Memory(entry.Character);
        bool fresh = snapshot.Connected && snapshot.Id is not null && string.Equals(snapshot.Name, entry.Character, StringComparison.OrdinalIgnoreCase)
            && snapshot.LegendsKnown && snapshot.LegendsRevision > 0 && snapshot.LegendsRevision <= snapshot.Revision;
        var legends = fresh ? snapshot.Legends.AsEnumerable() : memory.LegendsObservedAt is not null ? memory.Legends : [];
        var mark = CalloughMarkProgress.Legend(legends);
        return new(outcome.ObservedAt, mark is null ? "Callough reward obtained; legend count not yet observed"
            : "Legend mark obtained: " + mark + (fresh ? "" : " · last observed"));
    }

    async Task ReconcileCalloughSchedulesAsync()
    {
        foreach (var entry in SchedulerState.Entries)
            if (SchedulerCalloughMark(entry) is { } goal)
                await scheduler.CompleteGoalAsync(entry.Id, goal.Description);
    }

    public async Task ResumeForMoreStickMarksAsync(Guid id, DateTimeOffset obtainedAt, bool confirmed)
    {
        RequireOpen();
        if (!confirmed) throw new QuestBlockedException("Confirm that you want to resume this schedule for more marks.");
        var entry = SchedulerState.Entries.Single(e => e.Id == id);
        RequireNoScheduler(entry.Character);
        if (QueueReserves(entry.Character) || IsRunning(entry.Character) || RunnerBusy(entry.Character) || controlActions.ContainsKey(entry.Character) || LaunchPending(entry.Character))
            throw new QuestBlockedException("Wait for this character's current operation before trying for more marks.");
        var retry = CalloughMarkProgress.Resume(journal.Load(entry.Character, entry.Quest), obtainedAt);
        ValidateSchedule(entry.Character, entry.Quest, true, obtainedAt);
        if (entry.Profile != Settings.ServerProfile || entry.Connection != SchedulerConnection || entry.RecipeVersion != recipes[entry.Quest].Version)
            throw new QuestBlockedException("Profile or recipe changed. Review and edit this schedule first.");
        if (Memory(entry.Character).SelectionRestriction(entry.Quest, Snapshot(entry.Character)) is { } restriction)
            throw new QuestBlockedException(restriction.Reason);
        var due = retry.NextRun!.Value;
        if (due < DateTimeOffset.UtcNow) due = DateTimeOffset.UtcNow;
        // One durable scheduler write saves consent. The journal is untouched
        // until a due run owns the character and is ready to prepare its runner.
        await scheduler.ApproveCalloughRetryAsync(id, obtainedAt, due, confirmed);
    }

    async Task PrepareCalloughMarkRetryAsync(QuestSchedule entry)
    {
        if (entry.Quest != "spare-a-stick") return;
        var job = journal.Load(entry.Character, entry.Quest);
        if (StickRewardCleanup.Unfinished(job)) return;
        if (job.PermanentCompletion is not { } outcome) return;
        if (entry.CalloughRetryAfter != outcome.ObservedAt)
            throw new QuestBlockedException("A new Callough outcome needs your choice before trying for more marks.");
        if (PermanentQuestCompletion.Matches(Snapshot(entry.Character)))
            throw new QuestBlockedException("Close Callough's previous reply before resuming for more marks.");
        await journal.SaveAsync(CalloughMarkProgress.Resume(job, outcome.ObservedAt));
        runners.Remove(entry.Character);
    }

    internal async Task<QuestJob> ScheduledQuestOutcomeAsync(string name, QuestJob running)
    {
        if (running.Quest != "spare-a-stick") return running;
        // The passive observer first releases the runner, then saves its final
        // receipt. The paused in-memory job is not the final scheduled result.
        if (permanentTasks.TryGetValue(name, out var observed)) await observed;
        var saved = journal.Load(name, running.Quest);
        return saved.PermanentCompletion is not null ? saved : running;
    }
}
