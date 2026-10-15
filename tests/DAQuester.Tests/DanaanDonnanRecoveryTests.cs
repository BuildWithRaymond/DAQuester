using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanDonnanRecoveryTests
{
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    public sealed record Evidence(QuestJob Checkpoint, CharacterSnapshot[] Snapshots, RecordedJourneyAction[] Actions);
    public static Evidence Capture() => JsonSerializer.Deserialize<Evidence>(File.ReadAllText(Path.Combine(Root,
        "content/evidence/datester-7.41/danaan-donnan-assignment-recovery.json")))!;
    public static QuestDefinition Recipe() => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root,
        "content/recipes/cap-band-of-danaan.v1.json")))!;

    [Theory]
    [InlineData(null)]
    [InlineData("missing-next")]
    [InlineData("missing-root")]
    [InlineData("missing-referral")]
    [InlineData("wrong-character")]
    [InlineData("wrong-generation")]
    [InlineData("wrong-actor")]
    [InlineData("stale-referral")]
    [InlineData("wrong-page")]
    [InlineData("wrong-checkpoint")]
    [InlineData("no-consent")]
    [InlineData("later-handoff")]
    [InlineData("later-connection")]
    [InlineData("later-reply")]
    public async Task Actual_requery_failure_recovers_only_from_complete_owned_assignment_proof(string? gap)
    {
        var proof = Capture();
        var job = proof.Checkpoint;
        var states = proof.Snapshots;
        var actions = proof.Actions;
        if (gap == "missing-next") actions = actions[..1];
        if (gap == "missing-root") actions = actions[1..];
        if (gap == "missing-referral") states = states[..2];
        if (gap == "wrong-character") states = states.Select(s => s with { Name = "OtherSynthetic" }).ToArray();
        if (gap == "wrong-generation") states = states.Select(s => s with { Generation = 9 }).ToArray();
        if (gap == "wrong-actor") states = states.Select((s, i) => i == 2 ? s with { Dialog = s.Dialog! with { EntityId = 99 } } : s).ToArray();
        if (gap == "stale-referral") states = states.Select((s, i) => i == 2 ? s with { Dialog = s.Dialog! with { Revision = states[1].Revision } } : s).ToArray();
        if (gap == "wrong-page") states = states.Select((s, i) => i == 2 ? s with { Dialog = s.Dialog! with { Text = "Synthetic unknown assignment" } } : s).ToArray();
        if (gap == "wrong-checkpoint") job = job with { Pending = job.Pending! with { Kind = "observed-page:meaveen-fruit" } };
        if (gap == "no-consent") job = job with { DanaanOptions = null };
        if (gap is "later-handoff" or "later-connection") actions = actions.Append(new(job.Character,
            gap == "later-connection" ? 8 : 1, 2600, "choose", "I have some for you.",
            states[2].Dialog! with { Name = "Kyros", Revision = 2600 })).ToArray();
        if (gap == "later-reply") states = states.Append(states[2] with { Revision = 2600,
            Dialog = states[2].Dialog! with { Name = "Kyros", Revision = 2600 } }).ToArray();
        var s = new SimulatedSession();
        s.Set(s.Snapshot with { Name = job.Character, Generation = 3, Map = 348, Position = new(8, 5) });
        var j = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await j.SaveAsync(job);
        var runner = new QuestRunner(s, j, Recipe(), new());
        // Reconciliation reads the saved job without executing or querying an NPC.
        await runner.ReconcileAsync(s.Snapshot);
        bool changed = await runner.ReconcileDanaanCourtAsync(states, actions);
        Assert.Equal(gap is null, changed);
        Assert.Equal(gap is null ? 27 : 26, runner.Job.Route?.Task);
        Assert.Equal(gap is not null, runner.Job.Pending is not null);
        Assert.Empty(s.Commands);
        Assert.Null(runner.Job.CompletedAt);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("open")]
    [InlineData("unconfirmed")]
    [InlineData("foreign")]
    public async Task Restored_assignment_closes_only_its_exact_report_and_never_repeats_uncertain_close(string presentation)
    {
        var proof = Capture();
        var recipe = Recipe();
        var job = DanaanDonnanRecovery.Reconcile(proof.Checkpoint, recipe, proof.Snapshots, proof.Actions);
        var report = job.DonnanAssignment!.Report!;
        var s = new SimulatedSession();
        ((HashSet<string>)s.Capabilities).UnionWith(recipe.Capabilities);
        s.Set(s.Snapshot with { Name = job.Character, Generation = report.Generation, Map = 348, Position = new(8, 5),
            Level = 99, LegendsKnown = true, Experience = proof.Checkpoint.Pending!.Experience, Gold = 44800235, MaxWeight = 287,
            Inventory = s.Snapshot.Inventory.SetItem(38, new(38, "Leather Greaves", 1)),
            Dialog = presentation == "closed" ? null : presentation == "foreign" ? report with { Text = "Synthetic unrelated popup" } : report });
        while (s.Snapshot.Revision < report.Revision) s.Set(s.Snapshot);
        Assert.Equal(EligibilityKind.Eligible, DanaanEvidence.Eligibility(s.Snapshot, job).Kind);
        s.OnCommand = c =>
        {
            if (c.Kind != "close") throw new QuestBlockedException("Synthetic stop after report cleanup, before travel.");
            Assert.Equal(report.Generation, c.ExpectedGeneration);
            Assert.Equal(report.Revision, c.ExpectedDialogRevision);
            Assert.Equal(report.Text, c.ExpectedPageText);
            if (presentation != "unconfirmed") s.Set(s.Snapshot with { Dialog = null });
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(job);
        var runner = new QuestRunner(s, journal, recipe, new());
        await runner.RunAsync();
        if (presentation == "unconfirmed") await runner.RunAsync();
        int expectedCloses = presentation is "open" or "unconfirmed" ? 1 : 0;
        Assert.True(expectedCloses == s.Commands.Count(c => c.Kind == "close"), runner.Job.Reason);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "choose");
        Assert.Equal(presentation is "foreign" or "unconfirmed", runner.Job.DonnanAssignment?.Report is not null);
        Assert.Equal(presentation == "unconfirmed", runner.Job.Pending is not null);
        Assert.Equal(27, runner.Job.Route?.Task);
        Assert.Null(runner.Job.CompletedAt);
    }
}
