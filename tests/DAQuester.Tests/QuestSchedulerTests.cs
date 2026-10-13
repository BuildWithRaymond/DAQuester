using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public sealed class QuestSchedulerTests
{
    readonly string path = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N"), "scheduler.json");
    readonly DateTimeOffset now = new(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);
    QuestSchedule Entry(string name = "Elara", DateTimeOffset? due = null) => new() { Character = name, Quest = "spare-a-stick", Profile = "synthetic", Due = due ?? now };

    [Fact]
    public async Task Disabled_and_unacknowledged_schedulers_never_dispatch()
    {
        int calls = 0;
        var scheduler = new QuestScheduler(new FileQuestSchedulerStore(path), (_, _) => { calls++; return Task.FromResult(new ScheduledQuestResult()); });
        await Assert.ThrowsAsync<QuestBlockedException>(() => scheduler.AddAsync(Entry(), false));
        await scheduler.AddAsync(Entry(), true);
        await scheduler.TickAsync(now);
        Assert.False(scheduler.State.Enabled);
        Assert.Equal(0, calls);
        await Assert.ThrowsAsync<QuestBlockedException>(() => scheduler.SetEnabledAsync(true, false));
    }

    [Fact]
    public async Task Due_order_and_overlapping_ticks_dispatch_exactly_once()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        var scheduler = new QuestScheduler(new FileQuestSchedulerStore(path), async (entry, ct) => { calls.Add(entry.Character); entered.SetResult(); await finish.Task.WaitAsync(ct); return new(); });
        await scheduler.AddAsync(Entry("Later", now.AddHours(1)), true);
        await scheduler.AddAsync(Entry("First", now.AddMinutes(-1)), true);
        await scheduler.SetEnabledAsync(true, true);
        var run = scheduler.TickAsync(now);
        await entered.Task;
        await scheduler.TickAsync(now);
        finish.SetResult();
        await run;
        await scheduler.TickAsync(now);
        Assert.Equal(new[] { "First" }, calls);
        Assert.Equal("Completed", scheduler.State.Entries.Single(e => e.Character == "First").Status);
    }

    [Fact]
    public async Task Login_block_survives_restart_edits_and_master_reenable()
    {
        int calls = 0;
        Task<ScheduledQuestResult> Fail(QuestSchedule entry, CancellationToken ct) { calls++; throw new ScheduledLoginException(entry.Character, "Account is already logged in."); }
        var scheduler = new QuestScheduler(new FileQuestSchedulerStore(path), Fail);
        await scheduler.AddAsync(Entry(), true);
        await scheduler.AddAsync(Entry("ELARA") with { Quest = "perfect-hairstyle" }, true);
        await scheduler.SetEnabledAsync(true, true);
        await scheduler.TickAsync(now);
        var restored = new QuestScheduler(new FileQuestSchedulerStore(path), Fail);
        await restored.SetEnabledAsync(true, true);
        await restored.AddAsync(Entry() with { Quest = "entered-muisir-village" }, true);
        await restored.TickAsync(now.AddDays(10));
        Assert.Equal(1, calls);
        Assert.False(restored.State.Entries[0].Resume);
        Assert.NotNull(restored.LoginBlock("elara"));
        await Assert.ThrowsAsync<QuestBlockedException>(() => restored.RearmAsync(restored.State.Entries[0].Id, false, now));
        await restored.RearmAsync(restored.State.Entries[0].Id, true, now);
        await restored.TickAsync(now);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Continuation_preserves_checkpoint_and_sleeps_logged_out_until_due()
    {
        int calls = 0;
        var scheduler = new QuestScheduler(new FileQuestSchedulerStore(path), (entry, ct) => { calls++; return Task.FromResult(calls == 1 ? new ScheduledQuestResult(now.AddHours(3), true) : new()); });
        var entry = Entry() with { Quest = "altar-worship" };
        await scheduler.AddAsync(entry, true);
        await scheduler.SetEnabledAsync(true, true);
        await scheduler.TickAsync(now);
        Assert.True(scheduler.State.Entries[0].Resume);
        await scheduler.TickAsync(now.AddHours(2));
        Assert.Equal(1, calls);
        await scheduler.TickAsync(now.AddHours(3));
        Assert.Equal(2, calls);
        Assert.Equal("Completed", scheduler.State.Entries[0].Status);
    }

    [Fact]
    public async Task Pause_all_cancels_run_and_prevents_future_dispatch()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new QuestScheduler(new FileQuestSchedulerStore(path), async (_, ct) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); return new(); });
        await scheduler.AddAsync(Entry(), true);
        await scheduler.SetEnabledAsync(true, true);
        var run = scheduler.TickAsync(now);
        await entered.Task;
        await scheduler.SetEnabledAsync(false, false);
        await run;
        Assert.False(scheduler.State.Enabled);
        Assert.Equal("Needs attention", scheduler.State.Entries[0].Status);
        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public async Task Pause_during_cleanup_never_rearms_a_recurring_entry()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new QuestScheduler(new FileQuestSchedulerStore(path), async (_, _) => { entered.SetResult(); await finish.Task; return new(now.AddHours(8)); });
        var entry = Entry() with { Recurring = true };
        await scheduler.AddAsync(entry, true);
        await scheduler.SetEnabledAsync(true, true);
        var run = scheduler.TickAsync(now);
        await entered.Task;
        await scheduler.PauseAsync(entry.Id);
        finish.SetResult();
        await run;
        Assert.Equal("Needs attention", scheduler.State.Entries[0].Status);
    }

    [Fact]
    public async Task Crash_checkpoint_restores_to_attention_without_launch()
    {
        var store = new FileQuestSchedulerStore(path);
        await store.SaveAsync(new() { Enabled = true, Entries = [Entry() with { Status = "Running" }] });
        int calls = 0;
        var scheduler = new QuestScheduler(store, (_, _) => { calls++; return Task.FromResult(new ScheduledQuestResult()); });
        await scheduler.TickAsync(now);
        Assert.Equal(0, calls);
        Assert.Equal("Needs attention", scheduler.State.Entries[0].Status);
    }

    [Fact]
    public async Task Suspension_preserves_consent_for_next_app_session_but_never_dispatches_in_current_session()
    {
        int calls = 0;
        var store = new FileQuestSchedulerStore(path);
        var scheduler = new QuestScheduler(store, (_, _) => { calls++; return Task.FromResult(new ScheduledQuestResult()); });
        await scheduler.AddAsync(Entry(), true);
        await scheduler.SetEnabledAsync(true, true);
        scheduler.Suspend();
        await scheduler.TickAsync(now);
        Assert.Equal(0, calls);
        var restored = new QuestScheduler(store, (_, _) => { calls++; return Task.FromResult(new ScheduledQuestResult()); });
        Assert.True(restored.State.Enabled);
        await restored.TickAsync(now.AddDays(5));
        await restored.TickAsync(now.AddDays(6));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Editing_a_blocked_schedule_cannot_clear_character_login_block()
    {
        int calls = 0;
        var scheduler = new QuestScheduler(new FileQuestSchedulerStore(path), (_, _) => { calls++; return Task.FromResult(new ScheduledQuestResult()); });
        var entry = Entry();
        await scheduler.AddAsync(entry, true);
        await scheduler.BlockLoginAsync("Elara", "Account already logged in");
        await scheduler.EditAsync(entry with { Due = now.AddHours(-1), Recurring = true }, true);
        await scheduler.SetEnabledAsync(true, true);
        await scheduler.TickAsync(now);
        Assert.Equal(0, calls);
        Assert.Equal("Needs attention", scheduler.State.Entries[0].Status);
        Assert.NotNull(scheduler.LoginBlock("Elara"));
    }

    [Fact]
    public async Task Failed_claim_write_never_launches_and_disables_dispatch()
    {
        var scheduler = new QuestScheduler(new FileQuestSchedulerStore(path), (_, _) => throw new Exception("Must not execute"));
        await scheduler.AddAsync(Entry(), true);
        await scheduler.SetEnabledAsync(true, true);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await Assert.ThrowsAnyAsync<IOException>(() => scheduler.TickAsync(now));
        Assert.False(scheduler.State.Enabled);
        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public async Task Unconfirmed_helper_login_survives_restart_and_requires_explicit_review()
    {
        var store = new FileQuestSchedulerStore(path);
        var scheduler = new QuestScheduler(store, (_, _) => Task.FromResult(new ScheduledQuestResult()));
        await scheduler.BeginLoginAsync("Helper");
        Assert.NotNull(scheduler.LoginBlock("helper"));
        var restored = new QuestScheduler(store, (_, _) => Task.FromResult(new ScheduledQuestResult()));
        Assert.NotNull(restored.LoginBlock("Helper"));
        await Assert.ThrowsAsync<QuestBlockedException>(() => restored.ReviewLoginAsync("Helper", false));
        await restored.ReviewLoginAsync("Helper", true);
        Assert.Null(restored.LoginBlock("Helper"));
        await restored.BeginLoginAsync("Helper");
        await restored.ConfirmLoginAsync("Helper");
        Assert.Null(restored.LoginBlock("Helper"));
    }
}
