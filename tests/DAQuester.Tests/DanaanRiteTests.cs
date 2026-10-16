using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanRiteTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Observed_apple_debit_and_arrival_settle_without_repeating_the_rite(bool savedArrival, bool onLanding)
    {
        var proof = Evidence();
        var full = Recipe();
        var task = DanaanTransport.Journey(full, proof.Checkpoint)[proof.Checkpoint.Route!.Task];
        var recipe = full with { Capabilities = [], Journey = [task], JourneyVariants = [] };
        var current = onLanding ? proof.Snapshots.Single(x => x.Revision == 14430) :
            savedArrival ? proof.Snapshots[^1] : proof.Snapshots.Single(x => x.Revision == 14427);
        var s = Session(current);
        var job = proof.Checkpoint with { JourneyVariant = null, Route = proof.Checkpoint.Route with { Task = 0, Page = savedArrival ? 9 : 8 } };
        s.OnCommand = command =>
        {
            if (command.Kind == "next")
                Apply(s, proof.Snapshots.Single(x => x.Revision == 14430));
            if (command.Kind == "step")
                s.Set(s.Snapshot with { Position = command.Target });
        };
        var journal = Journal();
        await journal.SaveAsync(job);
        var runner = new QuestRunner(s, journal, recipe, new());
        await runner.RunAsync();
        Assert.True(runner.Job.Route?.Task == 1, runner.Job.Reason);
        Assert.Equal(savedArrival ? 0 : 1, s.Commands.Count(c => c.Kind == "next"));
        Assert.Equal(!savedArrival || onLanding ? 1 : 0, s.Commands.Count(c => c.Kind == "step"));
        Assert.DoesNotContain(s.Commands, c => c.Kind is "choose" or "interact" or "navigate");
        Assert.Null(runner.Job.Pending);
        Assert.Null(runner.Job.CompletedAt);
    }

    [Theory]
    [InlineData("no-debit")]
    [InlineData("both-debits")]
    [InlineData("other-decay")]
    [InlineData("wrong-map")]
    [InlineData("no-position")]
    [InlineData("generation")]
    [InlineData("unknown-bag")]
    [InlineData("gold")]
    [InlineData("xp")]
    [InlineData("missing-notice")]
    [InlineData("stale-notice")]
    [InlineData("notice-generation")]
    [InlineData("other-stage")]
    [InlineData("no-supplied-mold")]
    public async Task Incomplete_or_conflicting_apple_receipts_preserve_saved_stage(string gap)
    {
        var proof = Evidence();
        var task = DanaanTransport.Journey(Recipe(), proof.Checkpoint)[proof.Checkpoint.Route!.Task];
        var current = proof.Snapshots[^1];
        byte apple = current.Inventory.Values.Single(i => i.Name == "Rotten Apple").Slot;
        byte mold = current.Inventory.Values.Single(i => i.Name == "Mold").Slot;
        byte grapes = current.Inventory.Values.Single(i => i.Name == "Rotten Grapes").Slot;
        current = gap switch
        {
            "no-debit" => current with { Inventory = current.Inventory.SetItem(apple, new(apple, "Rotten Apple", 2)) },
            "both-debits" => current with { Inventory = current.Inventory.Remove(mold) },
            "other-decay" => current with { Inventory = current.Inventory.SetItem(apple, new(apple, "Rotten Apple", 2)).Remove(grapes) },
            "wrong-map" => current with { Map = 721 },
            "no-position" => current with { Position = null },
            "generation" => current with { Generation = 2 },
            "unknown-bag" => current with { InventoryKnown = false },
            "gold" => current with { Gold = current.Gold - 1 },
            "xp" => current with { Experience = current.Experience + 1 },
            "missing-notice" => current with { ActiveStatus = null },
            "stale-notice" => current with { ActiveStatus = current.ActiveStatus! with { Revision = 14417 } },
            "notice-generation" => current with { ActiveStatus = current.ActiveStatus! with { Generation = 2 } },
            _ => current
        };
        if (gap == "other-stage") task = task with { Id = "unobserved-rite" };
        var job = proof.Checkpoint with { JourneyVariant = null, Route = proof.Checkpoint.Route with { Task = 0 } };
        if (gap == "no-supplied-mold") job = job with { Route = job.Route! with
            { Baseline = job.Route.Baseline! with { InventoryCounts = new() { ["Rotten Apple"] = 2, ["Rotten Grapes"] = 1 } } } };
        var s = Session(current);
        var journal = Journal();
        await journal.SaveAsync(job);
        var runner = new QuestRunner(s, journal, Recipe() with { Capabilities = [], Journey = [task], JourneyVariants = [] }, new());
        await runner.RunAsync();
        Assert.Equal(0, runner.Job.Route?.Task);
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Empty(s.Commands);
        Assert.Null(runner.Job.CompletedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing-action")]
    [InlineData("duplicate-action")]
    [InlineData("wrong-choice")]
    [InlineData("missing-baseline")]
    [InlineData("no-landing")]
    [InlineData("no-notice")]
    [InlineData("later-dialogue")]
    [InlineData("later-resource-change")]
    [InlineData("other-character")]
    public async Task Restart_recovery_requires_the_original_complete_recorded_rite(string? gap)
    {
        var proof = Evidence();
        var snapshots = proof.Snapshots;
        var actions = proof.Actions;
        if (gap == "missing-action") actions = actions.Skip(1).ToArray();
        if (gap == "duplicate-action") actions = [.. actions, actions[^1]];
        if (gap == "wrong-choice") actions = actions.Select((a, i) => i == 7 ? a with { Selection = "Unobserved option" } : a).ToArray();
        if (gap == "missing-baseline") snapshots = snapshots.Skip(1).ToArray();
        if (gap == "no-landing") snapshots = snapshots.Select(s => s with { Position = null }).ToArray();
        if (gap == "no-notice") snapshots = snapshots.Select(s => s with { ActiveStatus = null }).ToArray();
        if (gap == "later-dialogue") actions = [.. actions, actions[^1] with { Revision = 14454, Dialog = actions[^1].Dialog! with { Name = "Dreamer", Revision = 14454 } }];
        if (gap == "later-resource-change") snapshots = [.. snapshots, snapshots[^1] with { Revision = 14454, Gold = 1 }];
        if (gap == "other-character") snapshots = snapshots.Select(s => s with { Name = "OtherResearchActor" }).ToArray();
        var s = Session(proof.Snapshots[^1] with { Generation = 2 });
        var journal = Journal();
        await journal.SaveAsync(proof.Checkpoint);
        var runner = new QuestRunner(s, journal, Recipe(), new());
        bool recovered = await runner.ReconcileDanaanCourtAsync(snapshots, actions);
        Assert.Equal(gap is null, recovered);
        Assert.Equal(gap is null ? 15 : 14, runner.Job.Route?.Task);
        Assert.Empty(s.Commands);
        Assert.Null(runner.Job.CompletedAt);
        Assert.Equal(runner.Job.Route?.Task, journal.Load(proof.Checkpoint.Character, DanaanEvidence.Id).Route?.Task);
    }

    static SimulatedSession Session(CharacterSnapshot current)
    {
        var s = new SimulatedSession();
        Apply(s, current);
        return s;
    }

    static void Apply(SimulatedSession s, CharacterSnapshot current)
    {
        long revision = current.Revision;
        s.Set(s.Snapshot with { Name = current.Name, Generation = current.Generation, Map = current.Map, Position = current.Position,
            Level = 99, Gold = current.Gold, Experience = current.Experience, InventoryKnown = current.InventoryKnown,
            Inventory = current.Inventory, Dialog = current.Dialog, ActiveStatus = current.ActiveStatus, LegendsKnown = true, LegendsRevision = 1 });
        while (s.Snapshot.Revision < revision) s.Set(s.Snapshot);
    }

    public sealed record Proof(QuestJob Checkpoint, RecordedJourneyAction[] Actions, CharacterSnapshot[] Snapshots);
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    static Proof Evidence() => JsonSerializer.Deserialize<Proof>(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-greim-rotten-apple.json")))!;
    static QuestDefinition Recipe() => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root, "content/recipes/cap-band-of-danaan.v1.json")))!;
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
}
