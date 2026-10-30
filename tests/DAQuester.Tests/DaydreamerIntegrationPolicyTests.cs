using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DaydreamerIntegrationPolicyTests
{
    const string Id = "avid-daydreamer";
    static readonly DateTimeOffset At = DateTimeOffset.UtcNow.AddDays(-2);
    static QuestDefinition Recipe => new(Id, 1, "Avid Temuairian Daydreamer", "Synthetic integration", [],
        [new("preflight", "daydreamer-preflight", "check-in"), new("check-in", "daydreamer-check-in", null)], 24);

    internal static QuestJob Receipt(int count, bool repeat = false, bool firstThirty = false, bool cleanup = false)
    {
        var status = new ServerNotice(1, 12, 3, $"This is your {count}th consecutive Login!", At);
        var goal = firstThirty ? new ServerNotice(1, 8, 3, "This is your 30th consecutive Login!", At.AddDays(-1)) : count >= 30 ? status : null;
        return JsonSerializer.Deserialize<QuestJob>(JsonSerializer.Serialize(new
        {
            Character = "Nuclear", Quest = Id, Step = "complete", Status = "Completed", CompletedAt = At,
            LastAttemptAt = At, Repeat = repeat, NextRun = At.AddHours(24),
            Daydreamer = new
            {
                Streak = count, LastCountReceipt = status, LastAcknowledgement = new { At, Status = status },
                NextCheckInAt = At.AddHours(24), FirstThirtyReceipt = goal, CleanupPending = cleanup
            }
        }))!;
    }

    [Fact]
    public void Daily_receipt_advances_the_queue_without_finishing_the_goal()
    {
        var job = Receipt(29);
        Assert.False(QuestRunPolicy.Finished(Recipe, job));
        Assert.Equal("AttemptCompleted", QueueAttemptOutcome.Evaluate(job)!.Outcome);
        Assert.Equal(job.NextRun, QuestRunPolicy.NextAttempt(Recipe, job));
    }

    [Fact]
    public void Already_done_acknowledges_the_day_without_inventing_a_new_count()
    {
        var at = DateTimeOffset.UtcNow;
        var prior = Receipt(19);
        var job = prior with
        {
            CompletedAt = at,
            Daydreamer = prior.Daydreamer! with
            {
                LastAcknowledgement = new(at, new(1, 13, 3, "You have already checked in today.", at)),
                NextCheckInAt = at.AddHours(24)
            }
        };

        Assert.Equal("AttemptCompleted", QueueAttemptOutcome.Evaluate(job)!.Outcome);
        Assert.False(QuestRunPolicy.Finished(Recipe, job));
        Assert.Equal(19, job.Daydreamer.Streak);
        Assert.Equal(prior.Daydreamer.LastCountReceipt, job.Daydreamer.LastCountReceipt);
        Assert.Equal(at.AddHours(24), QuestRunPolicy.NextAttempt(Recipe, job));
    }

    [Fact]
    public void First_thirty_stops_by_default_and_explicit_continuation_preserves_completion_evidence()
    {
        var goal = Receipt(30);
        Assert.True(QuestRunPolicy.Finished(Recipe, goal));
        Assert.Equal("Completed", QueueAttemptOutcome.Evaluate(goal)!.Outcome);
        Assert.Null(QuestRunPolicy.NextAttempt(Recipe, goal));

        var continued = Receipt(1, repeat: true, firstThirty: true);
        Assert.False(QuestRunPolicy.Finished(Recipe, continued));
        Assert.Equal("AttemptCompleted", QueueAttemptOutcome.Evaluate(continued)!.Outcome);
        Assert.Equal(continued.NextRun, QuestRunPolicy.NextAttempt(Recipe, continued));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_submission_or_popup_cleanup_owns_the_checkpoint_before_deadlines(bool pending)
    {
        var job = Receipt(30, cleanup: !pending) with
        {
            Pending = pending ? new("daydreamer-check-in", 1, 11, 0, 0, 0, At) : null,
            NextRun = DateTimeOffset.UtcNow.AddDays(1)
        };
        Assert.False(QuestRunPolicy.Finished(Recipe, job));
        Assert.Null(QuestRunPolicy.NextAttempt(Recipe, job));
        Assert.Null(QueueAttemptOutcome.Evaluate(job));
        Assert.False(QuestRunPolicy.StartNewScheduledAttempt(Recipe, job, new() { Due = DateTimeOffset.UtcNow, Resume = false }));
        Assert.False(QuestRunPolicy.StartNewScheduledAttempt(Recipe, job, new() { Due = DateTimeOffset.UtcNow, Resume = true }));
    }

    [Fact]
    public async Task Even_a_direct_repeat_run_yields_after_one_verified_daily_attempt()
    {
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe, new());
        var probe = new DailyReceipt();
        runner.Actions["daydreamer-preflight"] = new NoOp();
        runner.Actions["daydreamer-check-in"] = probe;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await runner.RunAsync(repeat: true, cancellation: stop.Token, startNewAttempt: true);

        Assert.False(stop.IsCancellationRequested);
        Assert.Equal(1, probe.Calls);
        Assert.Equal("AttemptCompleted", runner.Job.Status);
        Assert.True(runner.Job.Repeat);
        Assert.Equal("AttemptCompleted", QueueAttemptOutcome.Evaluate(runner.Job)!.Outcome);
    }

    [Fact]
    public async Task Scheduler_run_does_not_disable_saved_continuation_after_the_first_mark()
    {
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        var previous = Receipt(30, repeat: true);
        await journal.SaveAsync(previous);
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe, new());
        runner.Actions["daydreamer-preflight"] = new NoOp();
        var probe = new DailyReceipt();
        runner.Actions["daydreamer-check-in"] = probe;
        await runner.RunAsync(repeat: false, startNewAttempt: true, singleAttempt: true, yieldScheduledWaits: true);

        Assert.Equal(1, probe.Calls);
        Assert.True(runner.Job.Repeat);
        Assert.Equal(previous.Daydreamer!.FirstThirtyReceipt, runner.Job.Daydreamer!.FirstThirtyReceipt);
        Assert.Equal("AttemptCompleted", runner.Job.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_runs_before_a_saved_goal_or_future_daily_deadline(bool pending)
    {
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        var saved = Receipt(30, cleanup: !pending) with
        {
            Pending = pending ? new("daydreamer-check-in", 1, 11, 0, 0, 0, At) : null,
            NextRun = DateTimeOffset.UtcNow.AddDays(1)
        };
        await journal.SaveAsync(saved);
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe, new());
        var recovery = new Recovery();
        runner.Actions["daydreamer-check-in"] = recovery;

        await runner.RunAsync(startNewAttempt: true);

        Assert.Equal(1, recovery.Calls);
        Assert.Equal(saved.CompletedAt, runner.Job.CompletedAt);
        Assert.Equal(saved.Daydreamer!.FirstThirtyReceipt, runner.Job.Daydreamer!.FirstThirtyReceipt);
        Assert.Null(runner.Job.Pending);
        Assert.Equal("Completed", runner.Job.Status);
    }

    sealed class NoOp : IQuestAction
    {
        public Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct) => Task.CompletedTask;
    }

    sealed class DailyReceipt : IQuestAction
    {
        public int Calls { get; private set; }
        public Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
        {
            Calls++;
            var at = DateTimeOffset.UtcNow;
            var status = new ServerNotice(1, 12, 3, "This is your 7th consecutive Login!", at);
            var receipt = Receipt(7, c.Job.Repeat) with { CompletedAt = at, LastAttemptAt = at, NextRun = at.AddHours(24) };
            var progress = receipt.Daydreamer! with
            {
                LastCountReceipt = status, LastAcknowledgement = new(at, status),
                NextCheckInAt = at.AddHours(24), FirstThirtyReceipt = c.Job.Daydreamer?.FirstThirtyReceipt
            };
            return c.SaveAsync(c.Job with
            {
                Step = "complete", CompletedAt = at, LastAttemptAt = at, NextRun = at.AddHours(24), Daydreamer = progress
            });
        }
    }

    sealed class Recovery : IQuestAction
    {
        public int Calls { get; private set; }
        public Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
        {
            Calls++;
            return c.SaveAsync(c.Job with
            {
                Pending = null, Step = "complete",
                Daydreamer = c.Job.Daydreamer! with { CleanupPending = false, Submission = null }
            });
        }
    }
}
