using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanCourtRecoveryTests
{
    public sealed record Proof(RecordedJourneyAction[] Actions, CharacterSnapshot[] Snapshots);
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    static QuestDefinition Recipe() => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root, "content/recipes/cap-band-of-danaan.v1.json")))!;
    static Proof Evidence() => JsonSerializer.Deserialize<Proof>(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-court-recovery.json")))!;
    internal static QuestJob Pending(Proof proof)
    {
        var opening = proof.Actions[0].Dialog!;
        var baseline = new Transaction("observed-stage", opening.Generation, opening.Revision, 967001, 0, 193264281, DateTimeOffset.UtcNow)
        {
            InventoryBaselineKnown = true
        };
        return new()
        {
            Character = "ResearchPixel", Quest = DanaanEvidence.Id, JourneyVariant = "loures-orders", Step = "run",
            DanaanOptions = new("cail", true),
            Pending = baseline with { Kind = "observed-page:ase-history", Revision = opening.Revision - 1, EntityId = opening.EntityId, EntityType = 1 },
            Route = new() { Task = 4, Page = 0, Baseline = baseline, BoundGeneration = opening.Generation, BoundEntity = opening.EntityId, SubmittedPage = opening }
        };
    }

    [Fact]
    public void Actual_recorded_prefix_recovers_to_cousin_without_claiming_completion()
    {
        var proof = Evidence();
        var job = Pending(proof);
        var recovered = DanaanCourtRecovery.Reconcile(job, Recipe(), proof.Snapshots, proof.Actions);
        Assert.Equal("loures-court", recovered.JourneyVariant);
        Assert.Equal(9, recovered.Route?.Task);
        Assert.Null(recovered.Pending);
        Assert.Null(recovered.CompletedAt);
        Assert.Equal(job.DanaanOptions, recovered.DanaanOptions);
        Assert.NotNull(recovered.CourtRecovery);
        Assert.Same(recovered, DanaanCourtRecovery.Reconcile(recovered, Recipe(), proof.Snapshots, proof.Actions));
    }

    [Theory]
    [InlineData("missing-action")]
    [InlineData("missing-final-next")]
    [InlineData("missing-close-receipt")]
    [InlineData("wrong-quote")]
    [InlineData("wrong-gold")]
    [InlineData("missing-item-gain")]
    [InlineData("missing-item-consumption")]
    [InlineData("wrong-character")]
    [InlineData("wrong-generation")]
    [InlineData("other-pending")]
    [InlineData("no-consent")]
    [InlineData("incomplete-logan")]
    [InlineData("unoffered-alias")]
    [InlineData("stale-reply")]
    public void Missing_or_inconsistent_proof_preserves_original_pending_submission(string gap)
    {
        var proof = Evidence();
        var job = Pending(proof);
        var snapshots = proof.Snapshots;
        var actions = proof.Actions;
        switch (gap)
        {
            case "missing-action": actions = actions.Where(a => a.Selection != "Here, have a drink.").ToArray(); break;
            case "missing-final-next": actions = actions[..^1]; break;
            case "missing-close-receipt": snapshots = snapshots.Where(s => s.Revision <= 1611).ToArray(); break;
            case "wrong-quote": actions = actions.Select(a => a.Selection == "Oren Brandy - 75 gold" ? a with { Selection = "Oren Brandy - 50 gold" } : a).ToArray(); break;
            case "wrong-gold": snapshots = snapshots.Select(s => s.Revision >= 895 ? s with { Gold = 967000 } : s).ToArray(); break;
            case "missing-item-gain": snapshots = snapshots.Select(s => s with { Inventory = [] }).ToArray(); break;
            case "missing-item-consumption": snapshots = snapshots.Select(s => s.Revision >= 1197 ? s with { Inventory = snapshots.Single(t => t.Revision == 1196).Inventory } : s).ToArray(); break;
            case "wrong-character": actions = actions.Select(a => a with { Character = "OtherResearchActor" }).ToArray(); break;
            case "wrong-generation": snapshots = snapshots.Select(s => s with { Generation = s.Generation + 1 }).ToArray(); break;
            case "other-pending": job = job with { Pending = job.Pending! with { Kind = "observed-buy:oren-brandy" } }; break;
            case "no-consent": job = job with { DanaanOptions = new("cail", false) }; break;
            case "incomplete-logan": actions = actions.Where(a => a.Dialog?.Name != "Logan").ToArray(); break;
            case "unoffered-alias": actions = actions.Select((a, i) => i == 0 ? a with { Selection = "Have you seen anything unusual lately?" } : a).ToArray(); break;
            case "stale-reply": actions = actions.Select((a, i) => i == 1 ? a with { Dialog = a.Dialog! with { Revision = actions[0].Revision } } : a).ToArray(); break;
        }
        Assert.Same(job, DanaanCourtRecovery.Reconcile(job, Recipe(), snapshots, actions));
    }

    [Fact]
    public async Task Reconnected_runner_saves_recovery_without_sending_live_commands()
    {
        var proof = Evidence();
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = "researchpixel", Generation = proof.Actions[0].Generation + 1 });
        await journal.SaveAsync(Pending(proof));
        var runner = new QuestRunner(session, journal, Recipe(), new());
        Assert.True(await runner.ReconcileDanaanCourtAsync(proof.Snapshots, proof.Actions));
        Assert.Equal(9, journal.Load("ResearchPixel", DanaanEvidence.Id).Route?.Task);
        Assert.Empty(session.Commands);
        Assert.False(await runner.ReconcileDanaanCourtAsync(proof.Snapshots, proof.Actions));
    }
}
