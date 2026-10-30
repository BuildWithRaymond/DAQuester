using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    bool updateExitClaimed;
    Task? updateRequest;
    readonly HashSet<Task> passiveCheckpointTasks = [];
    readonly Dictionary<QuestRunner, OrdinarySaveFailure> failedOrdinarySaves = [];
    Exception? schedulerReconciliationFailure;
    sealed record OrdinarySaveFailure(string Character, QuestRunner Runner, CharacterSnapshot Snapshot, Exception Error);
    public event Action? UpdateExitRequested;

    bool UpdateWorkPending => QuestActionsRunning || passiveCheckpointTasks.Any(task => !task.IsCompleted)
        || supplyPlanMutations.Count > 0 || supplyActionOwners.Count > 0 || supplyReturnDispatches.Count > 0
        || resumingDanaan.Count > 0;

    Task TrackUpdateCheckpoint(Task work)
    {
        passiveCheckpointTasks.Add(work);
        _ = ForgetUpdateCheckpointAsync(work);
        return work;
    }

    async Task ForgetUpdateCheckpointAsync(Task work)
    {
        try { await work; }
        catch { /* The owning operation reports its failure. */ }
        finally { passiveCheckpointTasks.Remove(work); Notify(); }
    }

    public Task UpdateAndRestartAsync() => updateRequest is { IsCompleted: false }
        ? updateRequest : updateRequest = UpdateAndRestartCoreAsync();

    async Task UpdateAndRestartCoreAsync()
    {
        RequireOpen();
        if (Preview)
            throw new InvalidOperationException("Updates are disabled in previews.");
        await Updates.UpdateAsync();
        if (closing || shuttingDown)
        {
            if (!updateExitClaimed) Updates.CancelInstallRequest();
            return;
        }
        TryBeginUpdateExit();
    }

    // The resident timer calls this before admitting scheduled work, including
    // while the window is hidden. A waiting update never cancels an owner.
    public bool TryBeginUpdateExit()
    {
        if (updateExitClaimed) return true;
        var requestExit = UpdateExitRequested;
        if (requestExit is null || !TryClaimUpdateExit()) return false;
        try
        {
            requestExit();
            return true;
        }
        catch
        {
            RestoreAfterUpdateFailure();
            throw;
        }
    }

    bool TryClaimUpdateExit()
    {
        if (Preview || closing || shuttingDown ||
            !Updates.CanPrepareExit(UpdateWorkPending, ConnectedGame || host.HasConnections, LaunchedClientRunning) ||
            !host.TryPauseForUpdate())
            return false;
        updateExitClaimed = true;
        shuttingDown = true;
        return true;
    }

    void RestoreAfterUpdateFailure()
    {
        // Revoke consent before publishing another status change, so the next
        // resident tick cannot turn an error into an automatic retry loop.
        try { Updates.CancelInstallRequest(); }
        finally
        {
            if (updateExitClaimed) host.ResumeAfterUpdateFailure();
            updateExitClaimed = false;
            shuttingDown = false;
            Notify();
        }
    }

    async Task SaveBeforeUpdateExitAsync()
    {
        // A transport can finish just after posting its final receipt to the UI.
        // Flush those already queued callbacks before capturing pending writes.
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatch(() => observed.TrySetResult());
        await observed.Task;
        if (presentationRefresh is { } displayRead) await displayRead;
        AutoExport("updating");
        await Task.WhenAll(characterMemoryWrites.Concat(passiveCheckpointTasks).Concat(permanentTasks.Values).ToArray());
        await FlushCharacterMemorySavesAsync();
        foreach (var failed in failedOrdinarySaves.Values.ToArray())
        {
            if (runners.GetValueOrDefault(failed.Character) != failed.Runner)
            {
                if (QueueAttemptOutcome.Evaluate(journal.Load(failed.Character, "spare-a-stick")) is not null)
                {
                    failedOrdinarySaves.Remove(failed.Runner);
                    continue;
                }
                throw new IOException("Reward confirmation still needs reconciliation before updating.", failed.Error);
            }
            // This reconciles the original recorded receipt and writes its
            // checkpoint only; it never repeats a game transaction.
            await failed.Runner.ReconcileAsync(failed.Snapshot);
            if (QueueAttemptOutcome.Evaluate(failed.Runner.Job) is null)
                throw new IOException("Reward confirmation still needs reconciliation before updating.", failed.Error);
            failedOrdinarySaves.Remove(failed.Runner);
        }
        if (schedulerReconciliationFailure is not null)
        {
            await ReconcileCalloughSchedulesAsync();
            schedulerReconciliationFailure = null;
        }
    }

    internal static async Task AwaitUpdateCleanupAsync(Task cleanup, TimeSpan timeout, Action<Exception> report)
    {
        try { await cleanup.WaitAsync(timeout); }
        catch (Exception error)
        {
            // The helper is already armed and durable saves are complete. Exit
            // even if nonessential handle cleanup or its diagnostic fails.
            try { report(error); }
            catch { }
            _ = cleanup.ContinueWith(completed => { _ = completed.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
