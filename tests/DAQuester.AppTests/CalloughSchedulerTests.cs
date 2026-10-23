using System.Collections.Immutable;
using System.Reflection;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class CalloughSchedulerTests
{
    static DesktopRuntime Create()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara"];
        settings.ClientPath = "";
        settings.Save();
        return new(preview: true);
    }
    static FileQuestJournal Journal => new(Path.Combine(AppSettings.DataPath, "journal"));
    static QuestJob Goal(DateTimeOffset at) => new()
    {
        Character = "Elara", Quest = "spare-a-stick", Step = "complete", Status = "PermanentlyCompleted", CompletedAt = at,
        PermanentCompletion = new(at, new(7, 12, 99, 1, "Callough", "You remind me of my own little child.", true, 177, 82, 0, []) { ObservedAt = at })
    };

    [Fact]
    public async Task Passive_completion_is_awaited_before_the_scheduler_reads_its_final_receipt()
    {
        var runtime = Create();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var journal = Journal;
            var at = DateTimeOffset.UtcNow;
            await journal.SaveAsync(Goal(at) with { PermanentCompletion = null, Step = "turn-in", Status = "Paused", Pending = new("turn-in", 7, 10, 0, 6, 0, at) });
            var running = journal.Load("Elara", "spare-a-stick");
            async Task Observe() { await release.Task; await journal.SaveAsync(Goal(at)); }
            var tasks = (Dictionary<string, Task>)typeof(DesktopRuntime).GetField("permanentTasks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
            tasks["Elara"] = Observe();
            var result = runtime.ScheduledQuestOutcomeAsync("Elara", running);
            Assert.False(result.IsCompleted);
            release.SetResult();
            Assert.Equal("PermanentlyCompleted", QueueAttemptOutcome.Evaluate(await result)?.Outcome);
        }
        finally { release.TrySetResult(); await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("changed-goal")]
    [InlineData("login-block")]
    [InlineData("write-failure")]
    public async Task Retry_never_discards_the_goal_or_bypasses_login_and_durability_guards(string scenario)
    {
        var runtime = Create();
        try
        {
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow, true, true);
            var entry = Assert.Single(runtime.SchedulerState.Entries);
            var at = DateTimeOffset.UtcNow;
            await Journal.SaveAsync(Goal(at));
            await runtime.TickSchedulerAsync();
            if (scenario == "login-block")
            {
                var scheduler = (QuestScheduler)typeof(DesktopRuntime).GetField("scheduler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
                await scheduler.BlockLoginAsync("Elara", "Already logged in elsewhere");
                await Assert.ThrowsAsync<ScheduledLoginException>(() => runtime.ResumeForMoreStickMarksAsync(entry.Id, at, true));
                Assert.Single(runtime.SchedulerState.LoginBlocks);
            }
            else if (scenario == "write-failure")
            {
                using var locked = new FileStream(Path.Combine(AppSettings.DataPath, "scheduler.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                await Assert.ThrowsAnyAsync<IOException>(() => runtime.ResumeForMoreStickMarksAsync(entry.Id, at, true));
            }
            else
                await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.ResumeForMoreStickMarksAsync(entry.Id, at.AddSeconds(-1), true));
            Assert.Null(Assert.Single(runtime.SchedulerState.Entries).CalloughRetryAfter);
            Assert.NotNull(Journal.Load("Elara", "spare-a-stick").PermanentCompletion);
            Assert.False(runtime.LaunchPending("Elara"));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task A_later_goal_stops_again_instead_of_reusing_old_approval_and_displays_only_observed_numbers()
    {
        var runtime = Create();
        try
        {
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow, true, true);
            var entry = Assert.Single(runtime.SchedulerState.Entries);
            var at = DateTimeOffset.UtcNow;
            await Journal.SaveAsync(Goal(at));
            Assert.Contains("not yet observed", runtime.SchedulerCalloughMark(entry)!.Description);
            runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 20, LegendsRevision = 20, LegendsKnown = true,
                Legends = ImmutableArray.Create("Loved by Mileth Mundanes (3) - Deoch 223, Summer") });
            Assert.Contains("Mundanes (3)", runtime.SchedulerCalloughMark(entry)!.Description);
            await runtime.ResumeForMoreStickMarksAsync(entry.Id, at, true);
            await Journal.SaveAsync(Goal(at.AddDays(1)));
            await runtime.TickSchedulerAsync();
            Assert.Equal("Completed", Assert.Single(runtime.SchedulerState.Entries).Status);
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.ResumeForMoreStickMarksAsync(entry.Id, at, true));
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
