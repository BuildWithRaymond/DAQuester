using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    async Task RestoreResearchCompletionAsync(QuestRunner runner, CancellationToken cancellation)
    {
        if (runner.Job.Quest != WizardyResearchEvidence.Id) return;
        bool hadSubmission = runner.Job.Pending is { Kind: "research-submit" };
        bool hadHealing = runner.Job.Research?.Healing is not null;
        var checkpoints = new List<(long Generation, long Revision)>();
        if (runner.Job.Pending is { Kind: "research-submit" } pending) checkpoints.Add((pending.Generation, pending.Revision));
        if (runner.Job.Research?.Healing is { } dose) checkpoints.Add((dose.Generation, dose.Revision));
        if (checkpoints.Count == 0) return;
        string character = runner.Job.Character;
        bool NeedsEvidence() => runner.Job.Pending is { Kind: "research-submit" } || runner.Job.Research?.Healing is not null;
        async Task ObserveAsync(IEnumerable<CharacterSnapshot> snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                cancellation.ThrowIfCancellationRequested();
                await runner.ReconcileAsync(snapshot);
                if (!NeedsEvidence()) break;
            }
        }
        string directory = Path.Combine(AppSettings.DataPath, "replays");
        foreach (var window in checkpoints.GroupBy(p => p.Generation).Select(g => (Generation: g.Key, Revision: g.Min(p => p.Revision))))
        {
            if (!NeedsEvidence()) break;
            await ObserveAsync(recorder.Snapshots(character, window.Generation, window.Revision));
            if (NeedsEvidence() && Directory.Exists(directory))
                foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    cancellation.ThrowIfCancellationRequested();
                    CharacterSnapshot[] snapshots;
                    try
                    {
                        snapshots = await ResearchReplay.ReadAsync(file, character, window.Generation,
                            window.Revision, Settings.ServerProfile, cancellation);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
                    {
                        Log("Saved research evidence could not be read: " + Path.GetFileName(file) + " (" + e.Message + ")");
                        continue;
                    }
                    await ObserveAsync(snapshots);
                    if (!NeedsEvidence()) break;
                }
        }
        if (hadSubmission && runner.Job.Pending is null)
            Log(character + ": saved research result reconciled from matching original status and item receipts; no research submission resent.");
        if (hadHealing && runner.Job.Research?.Healing is null)
            Log(character + ": saved Exkuranum dose reconciled from matching original item and full-health receipts; current health still requires checking.");
    }
}
