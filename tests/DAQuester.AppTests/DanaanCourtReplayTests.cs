using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class DanaanCourtReplayTests
{
    [Fact]
    public async Task Greim_projection_preserves_decay_debit_trance_and_actual_landing()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence"))) root = root.Parent;
        Assert.NotNull(root);
        using var capture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName,
            "content/evidence/datester-7.41/danaan-greim-rotten-apple.json")));
        var job = capture.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()!;
        var states = capture.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!;
        var actions = capture.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!;
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-AppTests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = "datester-7.41", Sanitized = true,
            Events = states.Select(s => new { Character = s.Name, s.Generation, s.Revision, Direction = "S", Snapshot = s }), Actions = actions
        }));
        var receipts = await DanaanCourtReplay.ReadAsync(path, job.Character, job.Route!.Baseline!.Generation,
            job.Route.Baseline.Revision, "datester-7.41", CancellationToken.None);
        var landing = receipts.Snapshots.Single(s => s.Revision == 14430);
        Assert.Equal(new Tile(14, 27), landing.Position);
        Assert.Equal("Your mind swirls and you enter a trance", landing.ActiveStatus?.Text);
        Assert.Equal(1, landing.Count("Rotten Apple"));
        Assert.Equal(1, landing.Count("Mold"));
    }

    [Fact]
    public async Task Dreamer_return_projection_keeps_actual_map_and_landing_without_loading_bag_payloads()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence"))) root = root.Parent;
        Assert.NotNull(root);
        using var capture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName,
            "content/evidence/datester-7.41/danaan-dreamer-threshold-return.json")));
        var job = capture.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()!;
        var states = capture.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!;
        var actions = capture.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!;
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-AppTests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = "datester-7.41", Sanitized = true,
            Events = states.Select(s => new { Character = s.Name, s.Generation, s.Revision, Direction = "S", Snapshot = s }), Actions = actions
        }));
        var receipts = await DanaanCourtReplay.ReadReferralsAsync(path, job.Character, job.Pending!.Generation, job.Pending.Revision, "datester-7.41", CancellationToken.None);
        var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root.FullName, "content/recipes/cap-band-of-danaan.v1.json")))!;
        var result = DanaanDreamerReturn.Reconcile(job, recipe, receipts.Snapshots, receipts.Actions);
        Assert.Null(result.Pending);
        Assert.Equal(3079, result.DreamerReturnRecovery?.Map);
        Assert.Equal(job.Route!.Task + 1, result.Route?.Task);
    }

    [Fact]
    public async Task Blanchefleur_projection_recovers_owned_prefix_without_loading_bag_payloads()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence"))) root = root.Parent;
        Assert.NotNull(root);
        using var capture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName,
            "content/evidence/datester-7.41/danaan-blanchefleur-recovery.json")));
        var job = capture.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()!;
        var states = capture.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!;
        var actions = capture.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!;
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-AppTests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = "datester-7.41", Sanitized = true,
            Events = states.Select(s => new { Character = s.Name, s.Generation, s.Revision, Direction = "S", Snapshot = s }), Actions = actions
        }));
        var receipts = await DanaanCourtReplay.ReadReferralsAsync(path, job.Character, job.Pending!.Generation, job.Pending.Revision, "datester-7.41", CancellationToken.None);
        var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root.FullName, "content/recipes/cap-band-of-danaan.v1.json")))!;
        var result = DanaanBlanchefleurRecovery.Reconcile(job, recipe, receipts.Snapshots, receipts.Actions);
        Assert.Equal("blanchefleur-fiosachd", result.JourneyVariant);
        Assert.Null(result.Pending);
    }

    [Theory]
    [InlineData("datester-7.41", true, "S", true, true)]
    [InlineData("other-profile", true, "S", true, false)]
    [InlineData("datester-7.41", false, "S", true, false)]
    [InlineData("datester-7.41", true, "C", true, false)]
    [InlineData("datester-7.41", true, "S", false, false)]
    public async Task Donnan_assignment_projection_restores_only_owned_server_receipts(
        string profile, bool sanitized, string direction, bool sameHeader, bool expected)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence"))) root = root.Parent;
        Assert.NotNull(root);
        using var capture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName,
            "content/evidence/datester-7.41/danaan-donnan-assignment-recovery.json")));
        var job = capture.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()!;
        var states = capture.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!;
        var actions = capture.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!;
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-AppTests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = profile, Sanitized = sanitized,
            Events = states.Select(s => new { Character = sameHeader ? s.Name : "OtherSynthetic", s.Generation, s.Revision,
                Direction = direction, Snapshot = s, Payload = new string('x', 65536) }), Actions = actions
        }));
        var receipts = await DanaanCourtReplay.ReadDonnanAsync(path, job.Character, "datester-7.41", CancellationToken.None);
        var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root.FullName, "content/recipes/cap-band-of-danaan.v1.json")))!;
        var result = DanaanDonnanRecovery.Reconcile(job, recipe, receipts.Snapshots, receipts.Actions);
        Assert.Equal(expected ? 27 : 26, result.Route?.Task);
        Assert.Equal(!expected, result.Pending is not null);
        Assert.Null(result.CompletedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Varuna_recovery_projection_requires_original_stage_baseline(bool includeBaseline)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence")))
            root = root.Parent;
        Assert.NotNull(root);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName,
            "content/evidence/datester-7.41/danaan-varuna-gatekeeper.json")));
        var actions = document.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!;
        var snapshots = document.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!;
        var job = document.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()!;
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-AppTests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = "datester-7.41", Sanitized = true,
            Events = snapshots.Select(s => new { Character = s.Name, s.Generation, s.Revision, Direction = "S", Snapshot = s }),
            Actions = actions
        }));
        long after = includeBaseline ? job.Route!.Baseline!.Revision : job.Pending!.Revision;
        var receipts = await DanaanCourtReplay.ReadAsync(path, job.Character, job.Pending!.Generation, after, "datester-7.41", CancellationToken.None);
        var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root.FullName, "content/recipes/cap-band-of-danaan.v1.json")))!;
        var recovered = DanaanVarunaRecovery.Reconcile(job, recipe, receipts.Snapshots, receipts.Actions);
        Assert.Equal(includeBaseline ? 13 : 12, recovered.Route?.Task);
        Assert.Equal(!includeBaseline, recovered.Pending is not null);
        Assert.Null(recovered.CompletedAt);
    }

    [Fact]
    public async Task Captured_prefix_survives_replay_projection_and_recovers_saved_checkpoint()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence")))
            root = root.Parent;
        Assert.NotNull(root);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName,
            "content/evidence/datester-7.41/danaan-court-recovery.json")));
        var actions = document.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!;
        var snapshots = document.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!;
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-AppTests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = "datester-7.41", Sanitized = true,
            Events = snapshots.Select(s => new { Character = s.Name, s.Generation, s.Revision, Direction = "S", Snapshot = s }),
            Actions = actions
        }));
        var opening = actions[0].Dialog!;
        var baseline = new Transaction("observed-stage", opening.Generation, opening.Revision, 967001, 0, 193264281, DateTimeOffset.UtcNow)
        { InventoryBaselineKnown = true };
        var job = new QuestJob
        {
            Character = "ResearchPixel", Quest = DanaanEvidence.Id, JourneyVariant = "loures-orders", Step = "run",
            DanaanOptions = new("cail", true),
            Pending = baseline with { Kind = "observed-page:ase-history", Revision = opening.Revision - 1, EntityId = opening.EntityId, EntityType = 1 },
            Route = new() { Task = 4, Page = 0, Baseline = baseline, SubmittedPage = opening, BoundGeneration = opening.Generation, BoundEntity = opening.EntityId }
        };
        var receipts = await DanaanCourtReplay.ReadAsync(path, job.Character, opening.Generation, opening.Revision - 1, "datester-7.41", CancellationToken.None);
        var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root.FullName, "content/recipes/cap-band-of-danaan.v1.json")))!;
        var recovered = DanaanCourtRecovery.Reconcile(job, recipe, receipts.Snapshots, receipts.Actions);
        Assert.Equal("loures-court", recovered.JourneyVariant);
        Assert.Equal(9, recovered.Route?.Task);
        Assert.Null(recovered.Pending);
    }

    [Theory]
    [InlineData(3, "datester-7.41", "S", true, true)]
    [InlineData(2, "datester-7.41", "S", true, false)]
    [InlineData(3, "other-profile", "S", true, false)]
    [InlineData(3, "datester-7.41", "C", true, false)]
    [InlineData(3, "datester-7.41", "S", false, false)]
    public async Task Replay_requires_matching_server_profile_schema_and_event_identity(
        int schema, string profile, string direction, bool matchingHeader, bool accepted)
    {
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-AppTests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var d = new Dialog(7, 10, 55, 1, "Ase", "Synthetic page", true, 1100, 1, 0, [], HasNext: true);
        var s = new CharacterSnapshot { Name = "ResearchActor", Connected = true, Generation = 7, Revision = 10, Map = 195, Dialog = d };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = schema, ServerProfile = profile, Sanitized = true,
            Events = new[] { new { Character = matchingHeader ? "ResearchActor" : "OtherActor", Generation = 7, Revision = 10, Direction = direction, Snapshot = s, Payload = "ignored" } },
            Actions = new[] { new RecordedJourneyAction("ResearchActor", 7, 10, "next", null, d) }
        }));
        var result = await DanaanCourtReplay.ReadAsync(path, "researchactor", 7, 9, "datester-7.41", CancellationToken.None);
        Assert.Equal(accepted ? 1 : 0, result.Snapshots.Length);
        if (schema != 3 || profile != "datester-7.41")
            Assert.Empty(result.Actions);
    }

    [Fact]
    public async Task Cancelled_read_does_not_return_partial_receipts()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DanaanCourtReplay.ReadAsync("unused", "ResearchActor", 1, 1, "datester-7.41", cancellation.Token));
    }
}
