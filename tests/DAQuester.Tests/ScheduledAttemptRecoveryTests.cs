using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class ScheduledAttemptRecoveryTests
{
    static readonly DateTimeOffset Due = new(2026, 10, 16, 22, 33, 27, TimeSpan.Zero);
    static QuestDefinition Recipe => new("spare-a-stick", 1, "Stick", "Synthetic", [], [new("preflight", "probe", null)]) { Repeatable = true };
    static QuestJob Previous => new() { Character = "Nuclear", Quest = "spare-a-stick", Step = "complete", CompletedAt = Due.AddDays(-1), LastAttemptAt = Due.AddDays(-1), NextRun = Due.AddHours(-16), Repeat = true };
    static QuestSchedule Entry => new() { Character = "Nuclear", Quest = "spare-a-stick", Due = Due, Recurring = true, Resume = true };

    [Fact]
    public async Task Recovering_a_stale_resume_flag_keeps_the_verified_eight_hour_deadline()
    {
        var now = DateTimeOffset.UtcNow;
        var next = now.AddHours(7);
        var previous = Previous with { CompletedAt = now.AddHours(-1), LastAttemptAt = now.AddHours(-1), NextRun = next };
        var entry = Entry with { Due = now.AddMinutes(-1) };
        var session = new SimulatedSession();
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(previous);
        var runner = new QuestRunner(session, journal, Recipe, new());
        var probe = new Probe();
        runner.Actions["probe"] = probe;
        await runner.RunAsync(startNewAttempt: QuestRunPolicy.StartNewScheduledAttempt(Recipe, runner.Job, entry), singleAttempt: true, yieldScheduledWaits: true);
        Assert.Equal(0, probe.Calls);
        Assert.Equal("Scheduled", runner.Job.Status);
        Assert.Equal(next, runner.Job.NextRun);
        Assert.Equal(previous.LastAttemptAt, runner.Job.LastAttemptAt);
    }

    [Fact]
    public async Task Resumed_schedule_with_yesterdays_completed_stick_runs_a_new_attempt()
    {
        var session = new SimulatedSession();
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(Previous);
        var runner = new QuestRunner(session, journal, Recipe, new());
        var probe = new Probe();
        runner.Actions["probe"] = probe;
        await runner.RunAsync(startNewAttempt: QuestRunPolicy.StartNewScheduledAttempt(Recipe, runner.Job, Entry), singleAttempt: true, yieldScheduledWaits: true);
        Assert.Equal(1, probe.Calls);
        Assert.True(runner.Job.CompletedAt > Previous.CompletedAt);
        Assert.True(runner.Job.NextRun > DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("in-progress")]
    [InlineData("pending")]
    [InlineData("current-receipt")]
    [InlineData("cleanup")]
    [InlineData("permanent")]
    [InlineData("one-time")]
    public void Existing_work_and_current_receipts_stay_resumable(string state)
    {
        var job = state switch
        {
            "in-progress" => Previous with { Step = "gather", CompletedAt = null },
            "pending" => Previous with { Pending = new("turn-in", 1, 1, 0, 6, 0, Due) },
            "current-receipt" => Previous with { CompletedAt = Due },
            "cleanup" => Previous with { Route = new() { CleanupPending = true } },
            "permanent" => Previous with { PermanentCompletion = new(Due, new(1, 1, 77, 1, "Callough", "Synthetic permanent goal", true, 0, 1, 0, [])) },
            _ => Previous
        };
        var recipe = state == "one-time" ? Recipe with { Repeatable = false } : Recipe;
        Assert.False(QuestRunPolicy.StartNewScheduledAttempt(recipe, job, Entry));
    }

    sealed class Probe : IQuestAction
    {
        public int Calls;
        public Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
        {
            Calls++;
            var now = DateTimeOffset.UtcNow;
            return c.SaveAsync(c.Job with { Step = "complete", CompletedAt = now, LastAttemptAt = now, NextRun = now.AddHours(8) });
        }
    }
}
