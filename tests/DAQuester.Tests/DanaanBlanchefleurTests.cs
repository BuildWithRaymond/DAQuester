using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanBlanchefleurTests
{
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    static QuestDefinition Recipe() => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root, "content/recipes/cap-band-of-danaan.v1.json")))!;
    static (QuestJob Job, CharacterSnapshot[] States, RecordedJourneyAction[] Actions) Capture()
    {
        using var d = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-blanchefleur-recovery.json")));
        return (d.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()!,
            d.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!,
            d.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!);
    }

    [Theory]
    [InlineData("complete", true)]
    [InlineData("missing-choice", false)]
    [InlineData("missing-close", false)]
    [InlineData("wrong-actor", false)]
    [InlineData("changed-balance", false)]
    [InlineData("cousin-paid", false)]
    [InlineData("later-generation-payment", false)]
    [InlineData("later-progress", false)]
    [InlineData("wrong-source-revision", false)]
    public async Task Saved_branch_recovers_only_complete_original_receipts(string scenario, bool expected)
    {
        var (job, states, actions) = Capture();
        if (scenario == "missing-choice") actions = actions.Where(a => a.Selection != "Something involving Fiosachd?").ToArray();
        if (scenario == "missing-close") states = states.Where(s => s.Map != 398 || s.Dialog is not null).ToArray();
        if (scenario == "wrong-actor") actions = actions.Select(a => a with { Character = "OtherSynthetic" }).ToArray();
        if (scenario == "changed-balance") states = states.Select(s => s with { Gold = s.Gold - 1 }).ToArray();
        if (scenario == "cousin-paid") actions = [..actions, actions[^1] with { Kind = "choose", Selection = "How about 1,000 gold coins?" }];
        if (scenario == "later-generation-payment") actions = [..actions, actions[^1] with
            { Generation = 2, Revision = 5, Kind = "choose", Selection = "How about 1,000 gold coins?", Dialog = actions[^1].Dialog! with { Generation = 2, Revision = 5 } }];
        if (scenario == "later-progress") actions = [..actions, actions[^1] with { Kind = "next", Dialog = actions[^1].Dialog! with { Name = "Varuna" } }];
        if (scenario == "wrong-source-revision") job = job with { Pending = job.Pending! with { Revision = job.Pending.Revision - 1 } };
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = job.Character, Connected = true, Generation = 2 });
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(job);
        var runner = new QuestRunner(session, journal, Recipe(), new());
        Assert.Equal(expected, await runner.ReconcileDanaanCourtAsync(states, actions));
        Assert.Equal(expected ? "blanchefleur-fiosachd" : null, runner.Job.JourneyVariant);
        Assert.Equal(expected ? 7 : 4, runner.Job.Route?.Task);
        Assert.Equal(!expected, runner.Job.Pending is not null);
        Assert.Null(runner.Job.CompletedAt);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task Fresh_sailor_reply_runs_captured_loop_once_and_continues_to_Keefe_and_Logan()
    {
        var (job, states, recorded) = Capture();
        var actions = recorded.Where(a => a.Dialog?.Name is "Blanchefleur" or "Keefe" or "Logan").Skip(1).ToArray();
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = job.Character, Generation = 1, Revision = 3743, Map = 117, Position = new(7, 5),
            Level = 99, LegendsKnown = true, Gold = job.Route!.Baseline!.Gold, Experience = job.Route.Baseline.Experience,
            Dialog = job.Route.LastReply, MaxWeight = 500, Weight = 1,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Viper's Gland", 1)).Add(2, new(2, "Mold", 1))
                .Add(3, new(3, "Cherry", 2)).Add(4, new(4, "Grapes", 1)).Add(5, new(5, "Leather Greaves", 1)) });
        while (session.Snapshot.Revision < 3743) session.Set(session.Snapshot);
        int index = 0;
        session.OnCommand = command =>
        {
            if (command.Kind == "navigate") session.Set(session.Snapshot with { Map = command.Map, Position = command.Target });
            if (command.Kind == "interact" && index < actions.Length)
                session.Set(session.Snapshot with { Map = command.Map, Position = command.Target,
                    Dialog = actions[index].Dialog! with { Generation = 1, Revision = session.Snapshot.Revision + 1 } });
            if (command.Kind is not ("choose" or "next" or "close")) return;
            Assert.True(index < actions.Length, "Unexpected extra dialogue submission");
            var action = actions[index++];
            Assert.Equal(action.Kind, command.Kind);
            Assert.Equal(action.Selection, command.Text);
            Assert.Equal(action.Dialog!.Text, command.ExpectedPageText);
            var next = index < actions.Length && actions[index].Dialog!.Name == action.Dialog.Name ? actions[index].Dialog : null;
            session.Set(session.Snapshot with { Dialog = next is null ? null : next with { Generation = 1, Revision = session.Snapshot.Revision + 1 } });
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(job);
        var recipe = Recipe() with { Capabilities = [] };
        var branch = recipe.JourneyVariants["blanchefleur-fiosachd"];
        // This fixture replays dialogue only. Native entry/access fixtures cover
        // movement and Aoife's separate guarded reception transport.
        recipe.JourneyVariants["blanchefleur-fiosachd"] = branch with
            { Journey = branch.Journey.Select(t => t with { ApproachStrategy = null }).ToArray() };
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync();
        Assert.True(actions.Length == index, runner.Job.Reason + "; actions=" + index);
        Assert.Equal("blanchefleur-fiosachd", runner.Job.JourneyVariant);
        Assert.Equal(7, runner.Job.Route?.Task);
        Assert.Null(runner.Job.Pending);
        Assert.Single(session.Commands, c => c.Kind == "choose" && c.Text == "Something involving Fiosachd?");
        Assert.DoesNotContain(session.Commands, c => c.Kind == "choose" && c.Text == "Where did you get it?");
        Assert.Null(runner.Job.CompletedAt);
    }

    [Fact]
    public void Sailor_branch_keeps_captured_Varuna_recovery()
    {
        using var capture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-varuna-gatekeeper.json")));
        var job = capture.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()!;
        var recipe = Recipe();
        job = job with { JourneyVariant = "blanchefleur-fiosachd", Route = job.Route! with
            { Task = Array.FindIndex(recipe.JourneyVariants["blanchefleur-fiosachd"].Journey, t => t.Id == "varuna-dreamer") } };
        var result = DanaanVarunaRecovery.Reconcile(job, recipe,
            capture.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!,
            capture.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!);
        Assert.Null(result.Pending);
        Assert.Equal("mold", recipe.JourneyVariants["blanchefleur-fiosachd"].Journey[result.Route!.Task].Id);
    }

    [Fact]
    public void Captured_sailor_branch_preserves_submitted_prefix_and_joins_Keefe_then_Logan()
    {
        var recipe = Recipe();
        var (job, states, _) = Capture();
        Assert.True(recipe.JourneyVariants.TryGetValue("blanchefleur-fiosachd", out var branch));
        Assert.Null(branch.ParentVariant);
        Assert.Equal("blanchefleur-origin", branch.TaskId);
        Assert.Equal(3, branch.Page);
        Assert.Equal(job.Route!.LastReply!.Text, branch.Text);
        Assert.Empty(branch.Choices);
        for (int page = 0; page < 3; page++)
            Assert.Equal(JsonSerializer.Serialize(recipe.Journey[4].Pages[page]), JsonSerializer.Serialize(branch.Journey[4].Pages[page]));
        Assert.Equal(new[] { "keefe-answers", "logan-cap", "cousin" }, branch.Journey.Skip(5).Take(3).Select(t => t.Id));
        Assert.Equal(8, branch.Journey[4].Pages.Length);
        Assert.Equal("close", branch.Journey[4].Pages[^1].Kind);
        Assert.Equal("I paid little attention to him.", branch.Journey[4].Pages[^1].Text);
        Assert.Contains(states, s => s.Map == 3071 && s.Dialog is null && s.Gold == job.Route.Baseline!.Gold);
    }
}
