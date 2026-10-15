using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DreamerReturnTests
{
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    static QuestDefinition Recipe() => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root, "content/recipes/cap-band-of-danaan.v1.json")))!;
    static DanaanVarunaTests.Proof Proof() => JsonSerializer.Deserialize<DanaanVarunaTests.Proof>(File.ReadAllText(Path.Combine(Root,
        "content/evidence/datester-7.41/danaan-dreamer-threshold-return.json")))!;

    [Theory]
    [InlineData(3079)]
    [InlineData(777)]
    public async Task Final_Dreamer_Next_uses_actual_map_change_without_an_inn_whitelist(int destination)
    {
        var proof = Proof();
        var job = proof.Checkpoint;
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = job.Character, Generation = job.Pending!.Generation, Map = destination,
            Position = new(3, 1), Gold = job.Pending.Gold, Experience = job.Pending.Experience, Level = 99, LegendsKnown = true });
        while (session.Snapshot.Revision < proof.Snapshots[^1].Revision) session.Set(session.Snapshot);
        using var cancellation = new CancellationTokenSource();
        session.OnCommand = c => { if (c.Kind == "navigate") cancellation.Cancel(); };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(job);
        var runner = new QuestRunner(session, journal, Recipe() with { Capabilities = [] }, new());
        await runner.RunAsync(cancellation: cancellation.Token);
        Assert.Null(runner.Job.Pending);
        Assert.Equal(job.Route!.Task + 1, runner.Job.Route?.Task);
        Assert.DoesNotContain(session.Commands, c => c.Kind is "next" or "choose" or "step");
        Assert.Contains(session.Commands, c => c.Kind == "navigate");
    }

    [Theory]
    [InlineData("complete", true)]
    [InlineData("late-action-header", true)]
    [InlineData("missing-next", false)]
    [InlineData("same-map", false)]
    [InlineData("wrong-actor", false)]
    [InlineData("changed-gold", false)]
    [InlineData("later-handoff", false)]
    public async Task Reconnect_requires_original_final_Next_and_actual_return_receipt(string gap, bool expected)
    {
        var proof = Proof();
        var actions = proof.Actions;
        var states = proof.Snapshots;
        if (gap == "late-action-header")
        {
            var source = states.Last(s => s.Map == 720);
            long revision = proof.Checkpoint.Route!.SubmittedPage!.Revision + 1;
            actions = actions.Select(a => a.Kind == "next" ? a with { Revision = revision } : a).ToArray();
            states = states.Select(s => s.Revision == revision ? source with { Revision = revision } : s).ToArray();
        }
        if (gap == "missing-next") actions = [];
        if (gap == "same-map") states = states.Select(s => s with { Map = 720 }).ToArray();
        if (gap == "wrong-actor") states = states.Select(s => s with { Name = "OtherSynthetic" }).ToArray();
        if (gap == "changed-gold") states = states.Select(s => s with { Gold = s.Gold + 1 }).ToArray();
        if (gap == "later-handoff") actions = [..actions, actions[^1] with { Revision = states[^1].Revision + 1,
            Dialog = actions[^1].Dialog! with { Name = "Donnan" } }];
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = proof.Checkpoint.Character, Generation = 3 });
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(proof.Checkpoint);
        var runner = new QuestRunner(session, journal, Recipe(), new());
        Assert.Equal(expected, await runner.ReconcileDanaanCourtAsync(states, actions));
        Assert.Equal(!expected, runner.Job.Pending is not null);
        Assert.Equal(proof.Checkpoint.Route!.Task + (expected ? 1 : 0), runner.Job.Route?.Task);
        Assert.Empty(session.Commands);
        Assert.Null(runner.Job.CompletedAt);
    }
}
