using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class QuestCompletionTrackerTests
{
    static readonly DateTimeOffset At = new(2026, 10, 12, 17, 0, 0, TimeSpan.Zero);
    static QuestJob Completed(string character = "Elara") => new()
    {
        Character = character, Quest = "spare-a-stick", Step = "complete",
        Status = "Completed", CompletedAt = At
    };
    static QuestCompletionTracker Tracker(QuestJob initial) => new(initial);
    static QuestCompletion? Observe(QuestCompletionTracker tracker, QuestJob job) => tracker.Observe(job);

    [Fact]
    public void Loaded_history_and_duplicate_status_updates_do_not_replay()
    {
        var completed = Completed();
        var tracker = Tracker(completed);
        Assert.Null(Observe(tracker, completed));
        Assert.Null(Observe(tracker, completed with { Reason = "Progress saved" }));
        Assert.NotNull(Observe(tracker, completed with { CompletedAt = At.AddHours(8) }));
        Assert.Null(Observe(tracker, completed with { CompletedAt = At.AddHours(8) }));
    }

    [Fact]
    public void Matching_timestamps_for_different_characters_are_independent()
    {
        var tracker = Tracker(new QuestJob());
        Assert.NotNull(Observe(tracker, Completed("Elara")));
        Assert.NotNull(Observe(tracker, Completed("Wren")));
        Assert.Null(Observe(tracker, Completed("ELARA")));
    }

    [Fact]
    public void A_timestamp_without_a_verified_terminal_outcome_does_not_celebrate()
    {
        var tracker = Tracker(new QuestJob());
        Assert.Null(Observe(tracker, Completed() with { Step = "turn-in", Status = "Running" }));
        Assert.Null(Observe(tracker, Completed() with { Step = "turn-in" }));
        Assert.Null(Observe(tracker, Completed() with { Pending = new("turn-in", 1, 2, 0, 6, 0, At) }));
        Assert.NotNull(Observe(tracker, Completed()));
    }

    [Fact]
    public void Loaded_paused_terminal_receipt_does_not_replay_when_resumed()
    {
        var tracker = Tracker(Completed() with { Status = "Paused" });
        Assert.Null(Observe(tracker, Completed()));
    }

    [Theory]
    [InlineData("Running", "confirm")]
    [InlineData("Scheduled", "preflight")]
    public void Verified_weekly_receipt_celebrates_without_claiming_the_final_legend(string status, string step)
    {
        var reply = new Dialog(1, 12, 42, 1, "Eeva", PerfectHairstyleEvidence.Completion, true, 1, 1, 0, []) { ObservedAt = At };
        var hair = new QuestJob
        {
            Character = "Elara", Quest = "perfect-hairstyle", Step = step, Status = status,
            LastAttemptAt = At, LegendReward = false, HairstyleCompletion = reply,
            HairstyleAttempts = [new(At, reply, new() { ["experience"] = 15000 }, false)]
        };
        var tracker = Tracker(new QuestJob());
        Assert.Null(Observe(tracker, hair with { HairstyleAttempts = [] }));
        var result = Observe(tracker, hair);
        Assert.NotNull(result);
        Assert.Equal("AttemptCompleted", result.Status);
        Assert.Null(Observe(tracker, hair with { Status = "AttemptCompleted" }));
        Assert.Null(Observe(Tracker(hair), hair with { Status = "AttemptCompleted" }));
    }

    [Fact]
    public async Task Runtime_completion_event_follows_the_durable_runner_save()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var runtime = new DesktopRuntime(preview: true);
        try
        {
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            var recipe = new QuestDefinition("spare-a-stick", 1, "Synthetic receipt", "", [], [new("preflight", "synthetic-result", null)]) { Repeatable = false };
            var runner = new QuestRunner(new NoGameSession(), journal, recipe, runtime.Calibration);
            runner.Actions["synthetic-result"] = new CompleteAction();
            runtime.ObserveRunner("Elara", runner);
            runtime.ObserveRunner("Elara", runner);
            var events = new List<QuestCompletion>();
            runtime.QuestCompleted += completed =>
            {
                Assert.Equal("complete", journal.Load(completed.Character, completed.Quest).Step);
                Assert.Equal(completed.At, journal.Load(completed.Character, completed.Quest).CompletedAt);
                events.Add(completed);
            };
            await runner.RunAsync();
            Assert.True(runner.Job.Status == "Completed", runner.Job.Status + ": " + runner.Job.Reason);
            runtime.Refresh();
            Assert.Single(events);
            Assert.Equal("Elara", events[0].Character);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    sealed class CompleteAction : IQuestAction
    {
        public Task ExecuteAsync(QuestContext context, QuestStep step, CancellationToken cancellation) =>
            context.SaveAsync(context.Job with { Step = "complete", CompletedAt = At });
    }

    sealed class NoGameSession : IGameSession
    {
        public CharacterSnapshot Snapshot { get; } = new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Level = 6, Hp = 10 };
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
        public event Action<CharacterSnapshot>? Changed { add { } remove { } }
        public event Action<string>? Interrupted { add { } remove { } }
        public Task ExecuteAsync(GameCommand command, CancellationToken cancellation) => throw new InvalidOperationException("No real game actions in receipt tests.");
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation) => throw new InvalidOperationException("No real game waits in receipt tests.");
    }
}
