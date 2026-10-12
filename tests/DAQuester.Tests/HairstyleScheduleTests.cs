using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class HairstyleScheduleTests
{
    internal static async Task RunAttempt(QuestRunner runner)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        void Scheduled(QuestJob j)
        {
            if (j.Status == "Scheduled")
                stop.Cancel();
        }

        runner.Changed += Scheduled;
        try
        {
            await runner.RunAsync(cancellation: stop.Token);
        }
        finally
        {
            runner.Changed -= Scheduled;
        }
    }

    static QuestJob Pending()
    {
        var baseline = PerfectHairstyleLiveTests.Load().Events.Single(e => e.Revision == 845).Snapshot;
        return new()
        {
            Character = "Nuclear",
            Quest = "perfect-hairstyle",
            Step = "confirm",
            Pending = new("hairstyle-turn-in", baseline.Generation, baseline.Revision, 0, 0, baseline.Experience!.Value, DateTimeOffset.UtcNow)
            {
                EntityId = baseline.Dialog!.EntityId,
                InventoryBaselineKnown = true,
                InventoryCounts = [],
                Legends = baseline.Legends.ToArray()
            }
        };
    }

    [Fact]
    public async Task Ring_reward_records_attempt_and_seven_day_deadline_without_completing_legend_goal()
    {
        var s = new PerfectHairstyleLiveTests.ReplaySession(true);
        var c = new QuestContext(s, new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-weekly-" + Guid.NewGuid())), new(), Pending());
        await new PerfectHairstyleActions().ExecuteAsync(c, new("confirm", "hair-confirm", "complete"), default);
        Assert.Null(c.Job.CompletedAt);
        Assert.True(c.Job.Repeat);
        Assert.Equal(PerfectHairstyleLiveTests.Load().Events.Single(e => e.Revision == 855).Time.AddDays(7), c.Job.NextRun);
        var json = JsonSerializer.SerializeToElement(c.Job);
        Assert.Equal(1, json.GetProperty("HairstyleAttempts").GetArrayLength());
        Assert.Equal(15000, c.Job.Rewards["experience"]);
    }

    [Fact]
    public void Shipped_recipe_repeats_weekly_until_legend_goal()
    {
        var r = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/recipes/perfect-hairstyle.v1.json"))), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.True(r.Repeatable);
        Assert.Equal(168, r.CooldownHours);
        Assert.DoesNotContain(r.OutcomeConditions, x => x.Contains("optional", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Resume_before_saved_deadline_sends_no_quest_submission_and_keeps_schedule()
    {
        var s = new PerfectHairstyleLiveTests.ReplaySession(true);
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-weekly-" + Guid.NewGuid()));
        var next = DateTimeOffset.UtcNow.AddDays(7);
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Quest = "perfect-hairstyle",
            Step = "preflight",
            NextRun = next,
            Repeat = true
        });
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Perfect Hairstyle", "fixture", [], [new("preflight", "hair-preflight", "eeva")], 168)
        {
            Repeatable = true
        };
        var runner = new QuestRunner(s, journal, recipe, new());
        await RunAttempt(runner);
        Assert.Equal(next, runner.Job.NextRun);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "visit" or "choose" or "next");
        Assert.True(runner.Job.Repeat);
    }

    [Fact]
    public async Task Actual_attempt_history_and_deadline_survive_restart_paused()
    {
        var s = new PerfectHairstyleLiveTests.ReplaySession(true);
        string folder = Path.Combine(Path.GetTempPath(), "DAQuester-weekly-" + Guid.NewGuid());
        var journal = new FileQuestJournal(folder);
        var c = new QuestContext(s, journal, new(), Pending());
        await new PerfectHairstyleActions().ExecuteAsync(c, new("confirm", "hair-confirm", "complete"), default);
        var restored = new FileQuestJournal(folder).Load("Nuclear", "perfect-hairstyle");
        Assert.Equal("Paused", restored.Status);
        Assert.Null(restored.CompletedAt);
        Assert.Equal(c.Job.NextRun, restored.NextRun);
        Assert.Equal(c.Job.LastAttemptAt, restored.LastAttemptAt);
        Assert.Single(restored.HairstyleAttempts);
        Assert.Equal(15000, restored.HairstyleAttempts[0].Rewards["experience"]);
    }

    [Fact]
    public async Task Existing_legend_finishes_goal_before_saved_weekly_deadline_without_quest_packets()
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            LegendsKnown = true,
            Legends = System.Collections.Immutable.ImmutableArray.Create(PerfectHairstyleEvidence.Legend)
        });
        var j = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-weekly-" + Guid.NewGuid()));
        await j.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = "perfect-hairstyle",
            Step = "preflight",
            Repeat = true,
            NextRun = DateTimeOffset.UtcNow.AddDays(7)
        });
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Perfect Hairstyle", "fixture", [], [new("preflight", "hair-preflight", "complete")], 168)
        {
            Repeatable = true
        };
        var r = new QuestRunner(s, j, recipe, new());
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.NotNull(r.Job.CompletedAt);
        Assert.Null(r.Job.NextRun);
        Assert.False(r.Job.Repeat);
        Assert.Empty(s.Commands);
    }
}
