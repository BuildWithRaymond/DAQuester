using System.Reflection;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.Views;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public sealed class SchedulerTests
{
    static DesktopRuntime Runtime(bool preview = true)
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.ClientPath = "";
        settings.Characters = ["Elara"];
        settings.Save();
        return new(preview);
    }

    [Fact]
    public async Task Active_pending_login_is_launching_but_orphan_or_failure_still_requires_review()
    {
        var runtime = Runtime();
        try
        {
            var scheduler = (QuestScheduler)typeof(DesktopRuntime).GetField("scheduler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
            await scheduler.BeginLoginAsync("Elara");
            Assert.False(runtime.SchedulerLaunchInProgress("Elara"));
            void Set(string field, object? value) => typeof(DesktopRuntime).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runtime, value);
            Set("schedulerCharacter", "Elara");
            Set("schedulerQuest", "spare-a-stick");
            Set("schedulerPhase", "Launching");
            Assert.True(runtime.SchedulerLaunchInProgress("Elara"));
            Assert.NotNull(scheduler.LoginBlock("Elara"));
            await scheduler.BlockLoginAsync("Elara", "The account is already logged in.");
            Assert.False(runtime.SchedulerLaunchInProgress("Elara"));
            Set("schedulerCharacter", null);
            Set("schedulerQuest", null);
            Set("schedulerPhase", null);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Scheduler_navigation_preserves_saved_work_without_enabling_logins()
    {
        var runtime = Runtime();
        try
        {
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
            var vm = new MainWindowViewModel(runtime);
            var before = runtime.SchedulerState;
            vm.NavigateCommand.Execute("Scheduler");
            Assert.Equal("Scheduler", vm.PageTitle);
            Assert.True(vm.IsScheduler);
            Assert.Equal("Log in, run a quest, log out. Completely optional.", vm.PageSubtitle);
            Assert.False(vm.IsPlanner);
            Assert.False(vm.IsSettings);
            vm.NavigateCommand.Execute("Planner");
            vm.NavigateCommand.Execute("Scheduler");
            Assert.Equal(before, runtime.SchedulerState);
            Assert.False(runtime.SchedulerEnabled);
            Assert.False(runtime.LaunchPending("Elara"));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public void Scheduler_is_an_explicit_optional_runtime_feature()
    {
        var enabled = typeof(DesktopRuntime).GetProperty("SchedulerEnabled");
        Assert.NotNull(enabled);
        Assert.Equal(typeof(bool), enabled.PropertyType);
        Assert.NotNull(typeof(DesktopRuntime).GetMethod("SetSchedulerEnabledAsync"));
    }

    [Fact]
    public async Task Saving_schedule_never_enables_scheduler_and_preview_never_launches()
    {
        var runtime = Runtime();
        try
        {
            Assert.False(runtime.SchedulerEnabled);
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow, true, false));
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow, true, true);
            Assert.False(runtime.SchedulerEnabled);
            await runtime.SetSchedulerEnabledAsync(true, true);
            await runtime.TickSchedulerAsync();
            Assert.Equal("Armed", Assert.Single(runtime.SchedulerState.Entries).Status);
            Assert.False(runtime.LaunchPending("Elara"));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Schedule_time_cannot_bypass_verified_stick_or_altar_deadline()
    {
        var runtime = Runtime();
        try
        {
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            var at = DateTimeOffset.UtcNow;
            await journal.SaveAsync(new() { Character = "Elara", Quest = "spare-a-stick", Step = "complete", CompletedAt = at });
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", at, true, true);
            Assert.Equal(at.AddHours(8), Assert.Single(runtime.SchedulerState.Entries).Due);
            await journal.SaveAsync(new() { Character = "Elara", Quest = AltarWorshipEvidence.Id, AltarNoMarkAccepted = true, AltarOfferings = [new(at, 1, 750, "25 experience!", 0)] });
            await runtime.SaveScheduleAsync("Elara", AltarWorshipEvidence.Id, at, false, true);
            Assert.Equal(at.AddHours(3), runtime.SchedulerState.Entries.Single(e => e.Quest == AltarWorshipEvidence.Id).Due);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Scheduling_requires_saved_choices_and_never_repeats_one_time_goals()
    {
        var runtime = Runtime();
        try
        {
            Assert.True(runtime.SchedulerNeedsChoices("Elara", "mundane-birth"));
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SaveScheduleAsync("Elara", "mundane-birth", DateTimeOffset.UtcNow, false, true));
            await runtime.SaveSchedulerChoicesAsync("Elara", "mundane-birth", new(BirthOptions: new("Spring", 20, "Mileth")));
            await runtime.SaveScheduleAsync("Elara", "mundane-birth", DateTimeOffset.UtcNow, false, true);
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SaveScheduleAsync("Elara", "entered-muisir-village", DateTimeOffset.UtcNow, true, true));
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SaveScheduleAsync("Elara", "guide-only", DateTimeOffset.UtcNow, false, true));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Scheduler_reservations_block_launch_queue_quest_choices_and_account_owners()
    {
        var runtime = Runtime();
        typeof(DesktopRuntime).GetField("schedulerCharacter", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runtime, "Elara");
        try
        {
            Assert.True(runtime.SchedulerReserves("elara"));
            Assert.Contains("Scheduler", runtime.LaunchBlocker("Elara"));
            Assert.Contains("Scheduler", runtime.QueueStartReason("Elara", runtime.Queue("Elara")));
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.LaunchAsync("Elara"));
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.StartQuestAsync("Elara", "spare-a-stick"));
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SetRepeatAsync("Elara", "spare-a-stick", true));
        }
        finally
        {
            typeof(DesktopRuntime).GetField("schedulerCharacter", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runtime, null);
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Stop_all_turns_off_scheduler_and_keeps_saved_schedules()
    {
        var runtime = Runtime();
        try
        {
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
            await runtime.SetSchedulerEnabledAsync(true, true);
            await runtime.StopOwnersAsync();
            Assert.False(runtime.SchedulerEnabled);
            Assert.Single(runtime.SchedulerState.Entries);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Stop_all_cancels_launches_even_when_scheduler_checkpoint_is_locked()
    {
        var runtime = Runtime();
        using var launch = new CancellationTokenSource();
        var pending = (Dictionary<string, CancellationTokenSource>)typeof(DesktopRuntime).GetField("pendingLaunches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        try
        {
            await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
            await runtime.SetSchedulerEnabledAsync(true, true);
            pending["Elara"] = launch;
            using var locked = new FileStream(Path.Combine(AppSettings.DataPath, "scheduler.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await Assert.ThrowsAnyAsync<IOException>(() => runtime.StopOwnersAsync());
            Assert.True(launch.IsCancellationRequested);
        }
        finally { pending.Remove("Elara"); await runtime.ShutdownAsync(); }
    }

    [Fact]
    public void Warning_explicitly_explains_booting_logout_helpers_and_no_retry()
    {
        Assert.Contains("boot", SchedulerPage.Warning);
        Assert.Contains("closes", SchedulerPage.Warning);
        Assert.Contains("helpers", SchedulerPage.Warning);
        Assert.Contains("already logged in", SchedulerPage.Warning);
        Assert.Contains("No automatic retry", SchedulerPage.Warning);
    }

    [Fact]
    public async Task Graceful_shutdown_keeps_future_explicitly_enabled_schedules_for_restart()
    {
        var runtime = Runtime();
        await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
        await runtime.SetSchedulerEnabledAsync(true, true);
        await runtime.ShutdownAsync();
        var restored = new DesktopRuntime(preview: true);
        try
        {
            Assert.True(restored.SchedulerEnabled);
            Assert.Equal("Armed", Assert.Single(restored.SchedulerState.Entries).Status);
            await restored.TickSchedulerAsync();
            Assert.False(restored.LaunchPending("Elara"));
        }
        finally { await restored.ShutdownAsync(); }
    }
}
