using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanVarunaTests
{
    [Theory]
    [InlineData("blanchefleur-fiosachd", false)]
    [InlineData("loures-orders", false)]
    [InlineData("loures-court", false)]
    [InlineData("blanchefleur-fiosachd", true)]
    public async Task Fresh_spirit_reply_selects_captured_local_continuation_independently_of_opening(string variant, bool unknown)
    {
        var recipe = Recipe();
        var original = recipe.Journey.Single(t => t.Id == "varuna-dreamer");
        var session = new SimulatedSession();
        var submitted = new Dialog(1, 2, 55, 1, "Varuna", original.Pages[9].Text, true, 1106, 27, 0, [], HasNext: true);
        var reply = submitted with { Revision = 3, Step = 36, Text = unknown ? "Unobserved spirit bargain" : original.Pages[10].Text };
        session.Set(session.Snapshot with { Map = 3075, Position = new(29, 12), Level = 99, Experience = 1000,
            Gold = 20000, LegendsKnown = true, Dialog = reply });
        while (session.Snapshot.Revision < 3) session.Set(session.Snapshot);
        session.OnCommand = c => { if (c.Kind == "next") session.Set(session.Snapshot with { Dialog = null }); };
        int index = Array.FindIndex(recipe.JourneyVariants[variant].Journey, t => t.Id == "varuna-dreamer");
        var baseline = new Transaction("observed-stage", 1, 1, 20000, 0, 1000, DateTimeOffset.UtcNow)
            { InventoryBaselineKnown = true, InventoryCounts = new() { ["Viper's Gland"] = 1 } };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new() { Character = session.Snapshot.Name!, Quest = DanaanEvidence.Id, Step = "run", JourneyVariant = variant,
            DanaanOptions = new("cail", true), Pending = baseline with { Kind = "observed-page:varuna-dreamer", Revision = 1, EntityId = 55, EntityType = 1 },
            Route = new() { Task = index, Page = 9, Baseline = baseline, SubmittedPage = submitted, LastReply = reply, BoundGeneration = 1, BoundEntity = 55 } });
        var runner = new QuestRunner(session, journal, recipe with { Capabilities = [] }, new());
        // Stop at the next NPC approach; this fixture verifies the page receipt
        // and chosen continuation, rather than simulating another conversation.
        using var cancellation = new CancellationTokenSource();
        session.OnCommand = c =>
        {
            if (c.Kind == "next") session.Set(session.Snapshot with { Dialog = null });
            if (c.Kind == "navigate") cancellation.Cancel();
        };
        await runner.RunAsync(cancellation: cancellation.Token);
        Assert.Equal(unknown, runner.Job.Pending is not null);
        Assert.Equal(unknown ? 0 : 1, session.Commands.Count(c => c.Kind == "next"));
        if (!unknown)
            Assert.Equal("greim-warning", DanaanTransport.Journey(recipe, runner.Job)[runner.Job.Route!.Task].Id);
        Assert.DoesNotContain(session.Commands, c => c.Kind == "choose");
        Assert.Null(runner.Job.CompletedAt);
    }

    [Fact]
    public async Task Actual_Blanchfleur_spirit_ending_recovers_to_warning_without_another_Gland()
    {
        var proof = JsonSerializer.Deserialize<Proof>(File.ReadAllText(Path.Combine(Root,
            "content/evidence/datester-7.41/danaan-blanchefleur-varuna-spirits.json")))!;
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = proof.Checkpoint.Character, Generation = 2 });
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(proof.Checkpoint);
        var runner = new QuestRunner(session, journal, Recipe(), new());
        Assert.True(await runner.ReconcileDanaanCourtAsync(proof.Snapshots, proof.Actions));
        Assert.Equal("greim-warning", DanaanTransport.Journey(Recipe(), runner.Job)[runner.Job.Route!.Task].Id);
        Assert.Null(runner.Job.Pending);
        Assert.Equal("blanchefleur-fiosachd", runner.Job.JourneyVariant);
        Assert.Empty(session.Commands);
    }

    public sealed record Proof(QuestJob Checkpoint, RecordedJourneyAction[] Actions, CharacterSnapshot[] Snapshots);
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    static QuestDefinition Recipe() => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root, "content/recipes/cap-band-of-danaan.v1.json")))!;
    static Proof Evidence() => JsonSerializer.Deserialize<Proof>(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-varuna-gatekeeper.json")))!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Older_complete_replay_cannot_hide_newer_original_generation_progress(bool replacementGeneration)
    {
        var proof = Evidence();
        var receipt = new DanaanVarunaReceipt(proof.Checkpoint.Pending!.Generation, proof.Checkpoint.Route!.Baseline!.Revision, 1692);
        var newer = proof.Snapshots[^1] with { Revision = 1694, Gold = 965925, Generation = receipt.Generation + (replacementGeneration ? 1 : 0) };
        Assert.Equal(!replacementGeneration, DanaanVarunaRecovery.ConflictsWithLaterProgress(proof.Checkpoint, receipt, [newer], []));
    }

    [Theory]
    [InlineData("loures-orders", false)]
    [InlineData("loures-court", false)]
    [InlineData("loures-orders", true)]
    [InlineData("loures-court", true)]
    [InlineData("blanchefleur-fiosachd", false)]
    [InlineData("blanchefleur-fiosachd", true)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public async Task Captured_gatekeeper_successor_resumes_pending_next_without_another_gland_handoff(string? variant, bool unknown)
    {
        var full = Recipe();
        var task = (variant is null ? full.Journey : full.JourneyVariants[variant].Journey).Single(t => t.Id == "varuna-dreamer");
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [task], JourneyVariants = variant is null ? [] : new() { [variant] = full.JourneyVariants[variant] with { Journey = [task] } }
        };
        var source = Evidence();
        var session = new SimulatedSession();
        var last = source.Actions[^1].Dialog! with { Generation = 1, Revision = 2, Text = unknown ? "Unobserved gatekeeper terms" : source.Actions[^1].Dialog!.Text };
        session.Set(session.Snapshot with { Name = "ResearchPixel", Generation = 1, Map = 3075, Position = new(30,21), Level = 98, Gold = 965926, Experience = 193264283, LegendsKnown = true, LegendsRevision = 1, Inventory = [], Dialog = last });
        session.Set(session.Snapshot);
        session.OnCommand = command =>
        {
            if (command.Kind == "next")
                session.Set(session.Snapshot with { Dialog = null });
        };
        var job = source.Checkpoint with
        {
            JourneyVariant = variant,
            Pending = source.Checkpoint.Pending! with { Generation = 1, Revision = 0 },
            Route = source.Checkpoint.Route! with
            {
                Task = 0, BoundGeneration = 1, Baseline = source.Checkpoint.Route!.Baseline! with { Generation = 1, Revision = 0 },
                SubmittedPage = source.Checkpoint.Route!.SubmittedPage! with { Generation = 1, Revision = 1 }
            }
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(job);
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync();
        Assert.Equal(unknown ? 0 : 1, runner.Job.Route?.Task);
        Assert.Equal(unknown, runner.Job.Pending is not null);
        Assert.Equal(unknown ? 0 : 1, session.Commands.Count(c => c.Kind == "next"));
        Assert.DoesNotContain(session.Commands, c => c.Kind is "choose" or "say" or "interact");
        Assert.Equal(0, session.Snapshot.Count("Viper's Gland"));
        Assert.Null(runner.Job.CompletedAt);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("missing-handoff")]
    [InlineData("missing-close")]
    [InlineData("unknown-ending")]
    public async Task Original_opening_recovers_only_the_complete_captured_stage_and_keeps_the_Greim_warning(string gap)
    {
        var proof = JsonSerializer.Deserialize<Proof>(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-agent-gatekeeper.json")))!;
        var actions = gap == "missing-handoff" ? proof.Actions.Where(a => a.Selection != "I have what you need.").ToArray() : proof.Actions;
        if (gap == "unknown-ending")
            actions = [..actions[..^1], actions[^1] with { Dialog = actions[^1].Dialog! with { Text = "Unobserved terms" } }];
        var states = gap == "missing-close" ? proof.Snapshots.Where(s => s.Revision < 3590).ToArray() : proof.Snapshots;
        var result = DanaanVarunaRecovery.Reconcile(proof.Checkpoint, Recipe(), states, actions);
        bool complete = gap == "complete";
        Assert.Equal(complete ? 16 : 15, result.Route?.Task);
        Assert.Equal(!complete, result.Pending is not null);
        Assert.Null(result.JourneyVariant);
        Assert.Null(result.CompletedAt);
        Assert.Equal("greim-warning", Recipe().Journey[16].Id);
        if (complete)
            Assert.Equal(3590, result.VarunaRecovery?.ClosedRevision);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("missing-handoff")]
    [InlineData("missing-final-next")]
    [InlineData("missing-close")]
    [InlineData("missing-gland")]
    [InlineData("wrong-gold")]
    [InlineData("wrong-xp")]
    [InlineData("wrong-generation")]
    [InlineData("wrong-character")]
    [InlineData("wrong-submitted-page")]
    [InlineData("extra-submission")]
    [InlineData("later-spending")]
    [InlineData("later-dialog")]
    public async Task Reconnect_recovery_requires_the_complete_own_recorded_stage(string gap)
    {
        var proof = Evidence();
        var job = proof.Checkpoint;
        var actions = proof.Actions;
        var states = proof.Snapshots;
        switch (gap)
        {
            case "missing-handoff": actions = actions.Where(a => a.Selection != "I have what you need.").ToArray(); break;
            case "missing-final-next": actions = actions[..^1]; break;
            case "missing-close": states = states.Where(s => s.Revision < 1692).ToArray(); break;
            case "missing-gland": states = states.Select(s => s with { Inventory = [] }).ToArray(); break;
            case "wrong-gold": states = states.Select(s => s with { Gold = 965925 }).ToArray(); break;
            case "wrong-xp": states = states.Select(s => s with { Experience = 193264284 }).ToArray(); break;
            case "wrong-generation": states = states.Select(s => s with { Generation = s.Generation + 1 }).ToArray(); break;
            case "wrong-character": actions = actions.Select(a => a with { Character = "OtherResearchActor" }).ToArray(); break;
            case "wrong-submitted-page": job = job with { Route = job.Route! with { SubmittedPage = job.Route!.SubmittedPage! with { Text = "Unrecorded page" } } }; break;
            case "extra-submission": actions = [..actions, actions[^1] with { Revision = 1693 }]; break;
            case "later-spending": states = [..states, states[^1] with { Revision = 1694, Gold = 965925 }]; break;
            case "later-dialog": actions = [..actions, actions[^1] with { Revision = 1694, Dialog = actions[^1].Dialog! with { Name = "Greim" } }]; break;
        }
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = job.Character.ToLowerInvariant(), Generation = job.Pending!.Generation + 1 });
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(job);
        var runner = new QuestRunner(session, journal, Recipe(), new());
        bool complete = gap == "complete";
        Assert.Equal(complete, await runner.ReconcileDanaanCourtAsync(states, actions));
        Assert.Equal(complete ? 13 : 12, runner.Job.Route?.Task);
        Assert.Equal(!complete, runner.Job.Pending is not null);
        Assert.Null(runner.Job.CompletedAt);
        Assert.Equal(job.DanaanOptions, runner.Job.DanaanOptions);
        Assert.Equal(job.CourtRecovery, runner.Job.CourtRecovery);
        Assert.Empty(session.Commands);
        Assert.Equal(complete ? 13 : 12, journal.Load(job.Character, job.Quest).Route?.Task);
        Assert.False(await runner.ReconcileDanaanCourtAsync(states, actions));
    }
}
