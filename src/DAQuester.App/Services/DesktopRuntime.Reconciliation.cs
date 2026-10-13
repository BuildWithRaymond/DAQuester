using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    async Task RestoreStickCompletionAsync(QuestRunner runner, CancellationToken cancellation)
    {
        if (runner.Job.Quest != "spare-a-stick" || runner.Job.Pending is not { Kind: "turn-in" } pending || !Calibration.Ready)
            return;
        string character = runner.Job.Character;

        async Task ObserveAsync(IEnumerable<CharacterSnapshot> snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                cancellation.ThrowIfCancellationRequested();
                await runner.ReconcileAsync(snapshot);
                if (runner.Job.Pending is null)
                    break;
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
                    snapshots = await ReadStickReplayAsync(file, character, pending, cancellation);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
                {
                    Log("Saved reward replay could not be read: " + Path.GetFileName(file) + " (" + e.Message + ")");
                    continue;
                }

                // Reconciliation uses the runner's checkpoint gate. Read failures must never
                // conceal a failed durable write or race an active quest writer.
                await ObserveAsync(snapshots);
                if (runner.Job.Pending is null)
                    break;
            }

        if (runner.Job.Pending is null)
            Log(character + ": saved Callough turn-in reconciled from matching replay evidence; no submission resent.");
    }

    async Task<CharacterSnapshot[]> ReadStickReplayAsync(string path, string character, Transaction pending, CancellationToken cancellation)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellation);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("SchemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version != 2
            || !root.TryGetProperty("Sanitized", out var sanitized) || sanitized.ValueKind != JsonValueKind.True
            || !root.TryGetProperty("ServerProfile", out var profile) || profile.ValueKind != JsonValueKind.String || profile.GetString() != Settings.ServerProfile
            || !root.TryGetProperty("Events", out var events) || events.ValueKind != JsonValueKind.Array)
            return [];
        var snapshots = new List<CharacterSnapshot>();
        foreach (var element in events.EnumerateArray())
        {
            cancellation.ThrowIfCancellationRequested();
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("Character", out var name) || name.ValueKind != JsonValueKind.String || !character.Equals(name.GetString(), StringComparison.OrdinalIgnoreCase)
                || !element.TryGetProperty("Generation", out var generation) || generation.ValueKind != JsonValueKind.Number || !generation.TryGetInt64(out long observedGeneration) || observedGeneration != pending.Generation
                || !element.TryGetProperty("Revision", out var revision) || revision.ValueKind != JsonValueKind.Number || !revision.TryGetInt64(out long observedRevision) || observedRevision <= pending.Revision
                || !element.TryGetProperty("Snapshot", out var state) || state.ValueKind != JsonValueKind.Object)
                continue;
            var snapshot = state.Deserialize<CharacterSnapshot>();
            if (snapshot is not null && snapshot.Generation == observedGeneration && snapshot.Revision == observedRevision && character.Equals(snapshot.Name, StringComparison.OrdinalIgnoreCase))
                snapshots.Add(snapshot);
        }

        return snapshots.OrderBy(s => s.Revision).ToArray();
    }
}
