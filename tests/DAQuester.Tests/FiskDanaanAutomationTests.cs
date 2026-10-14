using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class FiskDanaanAutomationTests
{
    static string Temporary() => Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString());
    internal static QuestDefinition Recipe(string id) => new(id, 1, id, "https://vorlof.com", [],
        [new("preflight", id == "fisk-secret" ? "fisk-preflight" : "danaan-preflight", "run"),
         new("run", id == "fisk-secret" ? "fisk-run" : "danaan-run", "complete")], 0)
    {
        Repeatable = false
    };

    [Theory]
    [InlineData("fisk-secret", "fisk-run")]
    [InlineData("cap-band-of-danaan", "danaan-run")]
    public void Both_quests_have_registered_automatic_actions(string id, string action)
    {
        var runner = new QuestRunner(new SimulatedSession(), new FileQuestJournal(Temporary()), Recipe(id), new());
        Assert.Contains(action, runner.Actions.Keys);
    }

    [Theory]
    [InlineData("fisk-secret", "Kept Fisk's Secret - Deoch 223, Summer")]
    [InlineData("cap-band-of-danaan", "Worthy of the Cap of Danaan, Deoch 223, Summer")]
    [InlineData("cap-band-of-danaan", "Worthy of the Band of Danaan, Deoch 223, Summer")]
    public void Fresh_own_legend_prevents_another_claim_for_any_character(string id, string mark)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            LegendsKnown = true,
            LegendsRevision = session.Snapshot.Revision + 1,
            Legends = ImmutableArray.Create(mark)
        });
        var memory = new CharacterMemory { Character = session.Snapshot.Name! };
        Assert.Equal(mark, memory.CompletedLegend(id, session.Snapshot));
    }

    [Theory]
    [InlineData("fisk-secret", "Kept Fisk's Secret - Deoch 223, Summer")]
    [InlineData("cap-band-of-danaan", "Worthy of the Cap of Danaan, Deoch 223, Summer")]
    public async Task Existing_completion_sends_no_navigation_purchase_or_claim(string id, string mark)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            LegendsKnown = true,
            LegendsRevision = session.Snapshot.Revision + 1,
            Legends = ImmutableArray.Create(mark)
        });
        var runner = new QuestRunner(session, new FileQuestJournal(Temporary()), Recipe(id), new());
        await runner.RunAsync(true);
        Assert.Equal("Completed", runner.Job.Status);
        Assert.NotNull(runner.Job.CompletedAt);
        Assert.False(runner.Job.Repeat);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public void Deferred_paid_wait_survives_queue_serialization()
    {
        var state = JsonSerializer.Deserialize<QuestQueueState>("""
            {"Character":"Nuclear","Quests":["fisk-secret"],"Deferred":[{"Quest":"fisk-secret","Due":"2026-10-13T18:00:00Z"}]}
            """);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(state));
        Assert.True(document.RootElement.TryGetProperty("Deferred", out var saved));
        Assert.Equal("fisk-secret", saved[0].GetProperty("Quest").GetString());
    }

    [Fact]
    public async Task Paid_wait_yields_to_ready_queued_work_without_a_completed_outcome()
    {
        var session = new SimulatedSession();
        var calls = new List<string>();
        using var stop = new CancellationTokenSource();
        var queue = new QuestQueueCoordinator(new FileQuestQueueStore(Temporary()),
            new()
            {
                Character = session.Snapshot.Name!,
                Quests = ["fisk-secret", "another-ready-quest"]
            },
            () => session.Snapshot, (id, _, _) =>
            {
                calls.Add(id);
                if (id == "fisk-secret")
                {
                    var at = DateTimeOffset.UtcNow;
                    var reply = new Dialog(1, 10, 22, 1, "Viveka", FiskEvidence.BouquetAccepted, true, 0, 1, 0, []) { ObservedAt = at };
                    return Task.FromResult(new QuestJob { Character = session.Snapshot.Name!, Quest = id, Step = "wait-bouquet", Status = "Deferred", NextRun = at.AddHours(1), Accepted = true, FiskWait = new(at, reply, 50000, 30000, true) });
                }
                stop.Cancel();
                return Task.FromResult(new QuestJob { Character = session.Snapshot.Name!, Quest = id, Step = "complete", Status = "Completed", CompletedAt = DateTimeOffset.UtcNow });
            });
        await queue.RunAsync(stop.Token);
        Assert.Equal(new[] { "fisk-secret", "another-ready-quest" }, calls);
        Assert.Contains("fisk-secret", queue.State.Quests);
        Assert.DoesNotContain(queue.State.History, h => h.Quest == "fisk-secret");
    }

    [Fact]
    public async Task Unverified_timer_cannot_release_a_queue_checkpoint()
    {
        var session = new SimulatedSession();
        var calls = new List<string>();
        var queue = new QuestQueueCoordinator(new FileQuestQueueStore(Temporary()),
            new()
            {
                Character = session.Snapshot.Name!,
                Quests = ["fisk-secret", "other"]
            },
            () => session.Snapshot, (id, _, _) =>
            {
                calls.Add(id);
                return Task.FromResult(new QuestJob { Character = session.Snapshot.Name!, Quest = id, Status = "Deferred", NextRun = DateTimeOffset.UtcNow.AddHours(1) });
            });
        await queue.RunAsync();
        Assert.Equal(new[] { "fisk-secret" }, calls);
        Assert.Equal("fisk-secret", queue.State.ActiveQuest);
        Assert.Equal("Paused", queue.State.State);
        Assert.Empty(queue.State.History);
    }
}
