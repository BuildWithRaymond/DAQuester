using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class RepeatStickCompletionTests
{
    sealed record Fixture(ReplayEvent[] Events, RecordedAction[] Actions);
    static string Content(string path) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content", path));

    [Fact]
    public void Captured_repeat_reply_and_rewards_reconcile_the_bound_submission()
    {
        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Content("evidence/datester-7.41/stick-repeat-completion.json")))!;
        var calibration = JsonSerializer.Deserialize<Calibration>(File.ReadAllText(Content("calibration/datester-7.41.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var action = Assert.Single(fixture.Actions);
        var baseline = fixture.Events.First(e => e.Revision == action.Revision).Snapshot;
        var terminal = fixture.Events.Last().Snapshot;
        var tracker = new StateTracker(1);
        tracker.Identity("ReplayCharacter", 42);
        foreach (var replayEvent in fixture.Events)
            tracker.Observe(replayEvent.Opcode, Convert.FromBase64String(replayEvent.Payload));
        Assert.Equal("Ah thank ya, you've done well.", tracker.Snapshot.Dialog!.Text);
        Assert.Equal(73, tracker.Snapshot.Dialog.Step);
        Assert.Equal(terminal.Experience, tracker.Snapshot.Experience);

        var submission = new Transaction("turn-in", 1, action.Revision, baseline.Gold!.Value, baseline.Count("Branch"), baseline.Experience!.Value, action.Time)
        {
            EntityId = action.Dialog!.EntityId,
            InventoryBaselineKnown = true,
            InventoryCounts = baseline.Inventory.Values.GroupBy(i => i.Name).ToDictionary(g => g.Key, g => (int)g.Sum(i => (long)i.Quantity))
        };
        var history = new EvidenceHistory();
        foreach (var replayEvent in fixture.Events)
            history.Record(replayEvent.Snapshot);
        var job = new QuestJob { Character = "ReplayCharacter", Step = "confirm", Pending = submission };

        var completed = StickCompletionEvidence.Observe(job, calibration, history);

        Assert.Null(completed.Pending);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(terminal.Dialog!.ObservedAt, completed.CompletedAt);
        Assert.Equal(3500, completed.Rewards["experience"]);
        Assert.Equal(6, completed.Rewards["branchesConsumed"]);
        Assert.Equal(1, completed.Rewards["item:Stick"]);
        Assert.Equal(1, completed.Rewards["item:Wooden Shield"]);
        Assert.False(completed.Rewards.ContainsKey("gold"));
        Assert.Null(StickCompletionEvidence.Observe(job with { Pending = submission with { Generation = 2 } }, calibration, history).CompletedAt);
        Assert.Null(StickCompletionEvidence.Observe(job with { Pending = submission with { EntityId = 123 } }, calibration, history).CompletedAt);
        Assert.Null(StickCompletionEvidence.Observe(job with { Character = "SomeoneElse" }, calibration, history).CompletedAt);
    }
}
