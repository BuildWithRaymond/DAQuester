using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    async Task RestoreResearchCompletionAsync(QuestRunner runner, CancellationToken cancellation)
    {
        if (runner.Job.Quest != WizardyResearchEvidence.Id || runner.Job.Pending is not { Kind: "research-submit" } pending)
            return;
        string character = runner.Job.Character;
        async Task ObserveAsync(IEnumerable<CharacterSnapshot> snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                cancellation.ThrowIfCancellationRequested();
                await runner.ReconcileAsync(snapshot);
                if (runner.Job.Pending is null) break;
            }
        }
        await ObserveAsync(recorder.Snapshots(character, pending.Generation, pending.Revision));
        string directory = Path.Combine(AppSettings.DataPath, "replays");
        if (runner.Job.Pending is not null && Directory.Exists(directory))
            foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                cancellation.ThrowIfCancellationRequested();
                CharacterSnapshot[] snapshots;
                try
                {
                    snapshots = await ResearchReplay.ReadAsync(file, character, pending.Generation,
                        pending.Revision, Settings.ServerProfile, cancellation);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
                {
                    Log("Saved research evidence could not be read: " + Path.GetFileName(file) + " (" + e.Message + ")");
                    continue;
                }
                await ObserveAsync(snapshots);
                if (runner.Job.Pending is null) break;
            }
        if (runner.Job.Pending is null)
            Log(character + ": saved research result reconciled from matching original status and item receipts; no research submission resent.");
    }
}
