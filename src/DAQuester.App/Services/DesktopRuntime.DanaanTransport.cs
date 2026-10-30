using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    readonly HashSet<string> resumingDanaan = new(StringComparer.OrdinalIgnoreCase);

    public async Task TickDanaanContinuationsAsync()
    {
        // Auto-resume only the saved server handoff, never a player's ordinary
        // pause. Existing queue, scheduler and control ownership gates still apply.
        if (Preview || closing || shuttingDown || Updates.InstallRequested || !Ready || !recipes.TryGetValue(DanaanEvidence.Id, out var recipe))
            return;
        foreach (string name in sessions.Keys.ToArray())
        {
            if (closing || shuttingDown || Updates.InstallRequested) return;
            var queue = Queue(name);
            if (queue.State != "Paused" || queue.ActiveQuest != DanaanEvidence.Id ||
                queue.Reason is not (DanaanTransport.RedirectReason or "Character connection changed. Resume after fresh state.") ||
                QueueRunning(name) || QueueBusy(name) || SchedulerReserves(name) || RunnerBusy(name) ||
                controlActions.ContainsKey(name) || calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted)
                continue;
            var job = journal.Load(name, DanaanEvidence.Id);
            var snapshot = Snapshot(name);
            if (!DanaanTransport.CanResume(recipe, job, snapshot) || DanaanEvidence.Eligibility(snapshot, job).Kind != EligibilityKind.Eligible ||
                job.DanaanOptions is not { Avatar: "cail", SuppliedMoldRiteAccepted: true } || !resumingDanaan.Add(name))
                continue;
            try
            {
                await StartQueueAsync(name);
            }
            catch (Exception error) { Log(name + ": Danaan continuation preserved: " + error.Message); }
            finally { resumingDanaan.Remove(name); }
        }
    }
}
