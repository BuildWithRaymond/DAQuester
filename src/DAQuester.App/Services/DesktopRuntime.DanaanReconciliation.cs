using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    async Task RestoreDanaanReplayAsync(QuestRunner runner, CancellationToken cancellation)
    {
        if (runner.Job.Quest != DanaanEvidence.Id)
            return;
        if (DanaanRiteEvidence.IsCheckpoint(recipes[DanaanEvidence.Id], runner.Job))
        {
            await RestoreDanaanRiteAsync(runner, cancellation);
            return;
        }
        if (runner.Job.Pending is not { } pending) return;
        if (pending.Kind == "observed-page:donnan-next-task" && runner.Job.Route is { Page: 0, LastReply.Text: DanaanDonnanRecovery.NotFinished })
        {
            await RestoreDonnanAssignmentAsync(runner, cancellation);
            return;
        }
        bool court = runner.Job.JourneyVariant == "loures-orders" && pending.Kind == "observed-page:ase-history" &&
            runner.Job.Route is { Task: 4, Page: 0 };
        bool varuna = pending.Kind == "observed-page:varuna-dreamer" && runner.Job.Route is { Page: 9, Baseline: { } };
        bool blanchefleur = runner.Job.JourneyVariant is null && pending.Kind == "observed-page:blanchefleur-origin" &&
            runner.Job.Route is { Task: 4, Page: 2 };
        bool dreamer = DanaanDreamerReturn.IsSubmitted(recipes[DanaanEvidence.Id], runner.Job);
        if (!court && !varuna && !blanchefleur && !dreamer)
            return;
        // Varuna recovery needs the original pre-handoff inventory, not only the
        // later pending informational Next. The verifier binds the whole stage.
        long after = varuna ? runner.Job.Route!.Baseline!.Revision : pending.Revision;
        string character = runner.Job.Character;
        var memory = recorder.JourneyReceipts(character, pending.Generation, after);
        var newerStates = memory.Snapshots.ToList();
        var newerActions = memory.Actions.ToList();
        if ((blanchefleur || dreamer) && Snapshot(character).Generation is var currentGeneration && currentGeneration > pending.Generation)
        {
            var current = recorder.JourneyReceipts(character, currentGeneration, 0);
            newerStates.AddRange(current.Snapshots);
            newerActions.AddRange(current.Actions);
        }
        bool recovered = await runner.ReconcileDanaanCourtAsync(newerStates, newerActions, cancellation);
        string directory = Path.Combine(AppSettings.DataPath, "replays");
        if (!recovered && Directory.Exists(directory))
            foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                cancellation.ThrowIfCancellationRequested();
                (CharacterSnapshot[] Snapshots, RecordedJourneyAction[] Actions) receipts;
                try
                {
                    receipts = blanchefleur || dreamer
                        ? await DanaanCourtReplay.ReadReferralsAsync(file, character, pending.Generation, after, Settings.ServerProfile, cancellation)
                        : await DanaanCourtReplay.ReadAsync(file, character, pending.Generation, after, Settings.ServerProfile, cancellation);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
                {
                    Log("Saved Danaan replay could not be read: " + Path.GetFileName(file) + " (" + e.Message + ")");
                    continue;
                }
                // Durable writes and cancellation remain outside the read-failure
                // handler. Partial evidence must never clear a pending handoff.
                if (varuna)
                {
                    var preview = DanaanVarunaRecovery.Reconcile(runner.Job, recipes[DanaanEvidence.Id], receipts.Snapshots, receipts.Actions);
                    if (preview.VarunaRecovery is { } proof &&
                        DanaanVarunaRecovery.ConflictsWithLaterProgress(runner.Job, proof, newerStates, newerActions))
                    {
                        Log(character + ": newer replay progress conflicts with Varuna recovery; saved submission preserved.");
                        return;
                    }
                }
                if (blanchefleur)
                {
                    var preview = DanaanBlanchefleurRecovery.Reconcile(runner.Job, recipes[DanaanEvidence.Id], receipts.Snapshots, receipts.Actions);
                    if (preview.BlanchefleurRecovery is { } proof &&
                        DanaanBlanchefleurRecovery.ConflictsWithLaterProgress(runner.Job, proof, newerStates, newerActions))
                    {
                        Log(character + ": newer replay progress conflicts with Blanchefleur recovery; saved submission preserved.");
                        return;
                    }
                }
                if (dreamer)
                {
                    var preview = DanaanDreamerReturn.Reconcile(runner.Job, recipes[DanaanEvidence.Id], receipts.Snapshots, receipts.Actions);
                    if (preview.DreamerReturnRecovery is { } proof &&
                        DanaanDreamerReturn.ConflictsWithLaterProgress(runner.Job, proof, newerStates, newerActions))
                    {
                        Log(character + ": newer replay progress conflicts with Dreamer return recovery; saved submission preserved.");
                        return;
                    }
                }
                recovered = await runner.ReconcileDanaanCourtAsync(receipts.Snapshots, receipts.Actions, cancellation);
                if (recovered)
                    break;
                newerStates.AddRange(receipts.Snapshots);
                newerActions.AddRange(receipts.Actions);
            }
        if (recovered)
            Log(character + ": " + runner.Job.Reason + "; no submission resent.");
    }

    async Task RestoreDanaanRiteAsync(QuestRunner runner, CancellationToken cancellation)
    {
        var baseline = runner.Job.Route!.Baseline!;
        string character = runner.Job.Character;
        var memory = recorder.JourneyReceipts(character, baseline.Generation, baseline.Revision);
        var newerStates = memory.Snapshots.ToList();
        var newerActions = memory.Actions.ToList();
        if (Snapshot(character).Generation is var current && current > baseline.Generation)
        {
            var fresh = recorder.JourneyReceipts(character, current, 0);
            newerStates.AddRange(fresh.Snapshots);
            newerActions.AddRange(fresh.Actions);
        }
        if (await runner.ReconcileDanaanCourtAsync(newerStates, newerActions, cancellation)) return;
        string directory = Path.Combine(AppSettings.DataPath, "replays");
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            cancellation.ThrowIfCancellationRequested();
            (CharacterSnapshot[] Snapshots, RecordedJourneyAction[] Actions) receipts;
            try
            {
                receipts = await DanaanCourtReplay.ReadAsync(file, character, baseline.Generation,
                    baseline.Revision, Settings.ServerProfile, cancellation);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Log("Saved Greim replay could not be read: " + Path.GetFileName(file) + " (" + e.Message + ")");
                continue;
            }
            var preview = DanaanRiteEvidence.Reconcile(runner.Job, recipes[DanaanEvidence.Id], receipts.Snapshots, receipts.Actions);
            if (preview.RiteRecovery is { } proof && DanaanRiteEvidence.ConflictsWithLaterProgress(runner.Job, proof, newerStates, newerActions))
            {
                Log(character + ": newer replay progress conflicts with Greim recovery; saved stage preserved.");
                return;
            }
            if (await runner.ReconcileDanaanCourtAsync(receipts.Snapshots, receipts.Actions, cancellation))
            {
                Log(character + ": " + runner.Job.Reason + "; no submission resent.");
                return;
            }
            newerStates.AddRange(receipts.Snapshots);
            newerActions.AddRange(receipts.Actions);
        }
    }

    async Task RestoreDonnanAssignmentAsync(QuestRunner runner, CancellationToken cancellation)
    {
        var pending = runner.Job.Pending!;
        string character = runner.Job.Character;
        var memory = recorder.JourneyReceipts(character, pending.Generation, 0);
        var states = memory.Snapshots.ToList();
        var actions = memory.Actions.ToList();
        string directory = Path.Combine(AppSettings.DataPath, "replays");
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            cancellation.ThrowIfCancellationRequested();
            (CharacterSnapshot[] Snapshots, RecordedJourneyAction[] Actions) receipts;
            try
            {
                receipts = await DanaanCourtReplay.ReadDonnanAsync(file, character, Settings.ServerProfile, cancellation);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Log("Saved Donnan replay could not be read: " + Path.GetFileName(file) + " (" + e.Message + ")");
                continue;
            }
            states.AddRange(receipts.Snapshots);
            actions.AddRange(receipts.Actions);
            // Newer captures remain in the verifier so an older assignment
            // cannot erase a later Kyros handoff or Erin reward submission.
            if (await runner.ReconcileDanaanCourtAsync(states, actions, cancellation))
            {
                Log(character + ": " + runner.Job.Reason + "; no submission resent.");
                return;
            }
        }
    }
}
