using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class PresentationRefreshTests
{
    [Fact]
    public async Task Background_scan_updates_external_saves_without_a_foreground_reload()
    {
        var seed = DashboardTests.Workspace();
        await seed.ShutdownAsync();
        var journal = new CountingJournal();
        var runtime = new DesktopRuntime(preview: true, journal: journal);
        try
        {
            runtime.SetPreviewQueue(new()
            {
                Character = "Elara",
                Quests = [SickChildEvidence.Id]
            });
            var vm = new MainWindowViewModel(runtime);
            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = SickChildEvidence.Id,
                Step = "pickup"
            });
            await runtime.RefreshPresentationAsync();
            Assert.Equal("Resume saved progress", vm.QueueItems[0].Detail);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Late_background_scan_cannot_restore_a_receipt_replaced_by_a_save()
    {
        using var source = new GatedJournal();
        var journal = new PresentationJournal(source);
        var before = journal.ReadForDisplay("Elara", SickChildEvidence.Id);
        source.BlockNextRead = true;
        var scan = journal.RefreshDisplayAsync();
        try
        {
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var after = before with
            {
                Step = "pickup"
            };
            await journal.SaveAsync(after);
            Assert.Same(after, journal.ReadForDisplay("Elara", SickChildEvidence.Id));
            source.Release.Set();
            await scan;
            Assert.Same(after, journal.ReadForDisplay("Elara", SickChildEvidence.Id));
        }
        finally { source.Release.Set(); await scan; }
    }

    [Fact]
    public async Task Failed_save_invalidates_display_but_never_publishes_the_proposed_receipt()
    {
        using var source = new GatedJournal();
        var journal = new PresentationJournal(source);
        var before = journal.ReadForDisplay("Elara", SickChildEvidence.Id);
        source.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => journal.SaveAsync(before with { Step = "complete" }));
        Assert.Same(before, journal.ReadForDisplay("Elara", SickChildEvidence.Id));
        Assert.Same(before, journal.Load("Elara", SickChildEvidence.Id));
    }

    [Fact]
    public async Task Movement_updates_reuse_display_receipts_until_an_explicit_refresh()
    {
        var seed = DashboardTests.Workspace();
        await seed.ShutdownAsync();
        var journal = new CountingJournal();
        var runtime = new DesktopRuntime(preview: true, journal: journal);
        try
        {
            runtime.SetPreviewQueue(new()
            {
                Character = "Elara",
                Quests = [SickChildEvidence.Id]
            });
            var vm = new MainWindowViewModel(runtime);
            journal.Loads.Clear();
            for (int revision = 3; revision < 53; revision++)
                runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with
                {
                    Revision = revision
                });

            Assert.Empty(journal.Loads);
            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = SickChildEvidence.Id,
                Step = "pickup"
            });
            runtime.Refresh();
            Assert.Equal("Resume saved progress", vm.QueueItems[0].Detail);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Burst_posts_one_display_refresh_and_reads_latest_state()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var pending = new Queue<Action>();
            runtime.Dispatch = pending.Enqueue;
            var seen = new List<long>();
            runtime.Changed += () => seen.Add(runtime.Snapshot("Elara").Revision);
            for (int revision = 3; revision <= 52; revision++)
                runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with
                {
                    Revision = revision
                });

            Assert.Single(pending);
            pending.Dequeue()();
            Assert.Equal([52L], seen);
            Assert.Empty(pending);
        }
        finally { runtime.Dispatch = action => action(); await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Display_batching_keeps_every_diagnostic_line_and_skips_callbacks_after_close()
    {
        var runtime = DashboardTests.Workspace();
        var pending = new Queue<Action>();
        runtime.DispatchPresentation = pending.Enqueue;
        int calls = 0;
        runtime.Changed += () => calls++;
        try
        {
            for (int index = 0; index < 50; index++)
            {
                runtime.Log("Synthetic receipt " + index);
                runtime.Refresh();
            }
            Assert.Equal(50, runtime.Logs.Count(line => line.Contains("Synthetic receipt ")));
            Assert.Equal(50, File.ReadLines(Path.Combine(AppSettings.DataPath, "diagnostics.log")).Count(line => line.Contains("Synthetic receipt ")));
            Assert.Single(pending);
            await runtime.ShutdownAsync();
            pending.Dequeue()();
            Assert.Equal(0, calls);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Change_during_display_refresh_gets_another_pass()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var pending = new Queue<Action>();
            runtime.Dispatch = pending.Enqueue;
            int calls = 0;
            runtime.Changed += () =>
            {
                if (++calls == 1)
                    runtime.Refresh();
            };
            runtime.Refresh();
            pending.Dequeue()();
            Assert.Single(pending);
            pending.Dequeue()();
            Assert.Equal(2, calls);
            Assert.Empty(pending);
        }
        finally { runtime.Dispatch = action => action(); await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Display_refresh_reads_each_saved_checkpoint_once_and_never_writes()
    {
        var seed = DashboardTests.Workspace();
        await seed.ShutdownAsync();
        var journal = new CountingJournal();
        var runtime = new DesktopRuntime(preview: true, journal: journal);
        try
        {
            runtime.SetPreviewQueue(new()
            {
                Character = "Elara",
                Quests = [NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId, SickChildEvidence.Id, AltarWorshipEvidence.Id]
            });
            var vm = new MainWindowViewModel(runtime);
            journal.Loads.Clear();
            vm.Refresh();

            Assert.NotEmpty(journal.Loads);
            Assert.All(journal.Loads.Values, count => Assert.Equal(1, count));
            Assert.Equal(0, journal.Writes);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Display_cache_expires_next_refresh_and_execution_reads_remain_fresh()
    {
        var seed = DashboardTests.Workspace();
        await seed.ShutdownAsync();
        var journal = new CountingJournal();
        var runtime = new DesktopRuntime(preview: true, journal: journal);
        try
        {
            runtime.SetPreviewQueue(new()
            {
                Character = "Elara",
                Quests = [SickChildEvidence.Id]
            });
            var vm = new MainWindowViewModel(runtime);
            Assert.Equal("One verified attempt", Assert.Single(vm.QueueItems).Detail);
            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = SickChildEvidence.Id,
                Step = "pickup"
            });

            Assert.Equal("pickup", runtime.Job("Elara", SickChildEvidence.Id).Step);
            Assert.Equal("One verified attempt", vm.QueueItems[0].Detail);
            vm.Refresh();
            Assert.Equal("Resume saved progress", vm.QueueItems[0].Detail);
            Assert.Equal(1, journal.Writes);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    sealed class GatedJournal : IQuestJournal, IDisposable
    {
        QuestJob saved = new() { Character = "Elara", Quest = SickChildEvidence.Id };
        public bool BlockNextRead
        {
            get; set;
        }
        public bool FailSave
        {
            get; set;
        }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();

        public QuestJob Load(string character, string quest)
        {
            var captured = saved;
            if (BlockNextRead)
            {
                BlockNextRead = false;
                Entered.SetResult();
                Release.Wait();
            }
            return captured;
        }

        public Task SaveAsync(QuestJob job, CancellationToken cancellation = default)
        {
            if (FailSave)
                throw new IOException("Synthetic checkpoint write failure");
            saved = job;
            return Task.CompletedTask;
        }

        public void Dispose() => Release.Dispose();
    }

    sealed class CountingJournal : IQuestJournal
    {
        readonly Dictionary<(string Character, string Quest), QuestJob> saved = [];
        public Dictionary<(string Character, string Quest), int> Loads { get; } = [];
        public int Writes
        {
            get; private set;
        }

        public QuestJob Load(string character, string quest)
        {
            var key = (character, quest);
            Loads[key] = Loads.GetValueOrDefault(key) + 1;
            return saved.GetValueOrDefault(key) ?? new()
            {
                Character = character,
                Quest = quest
            };
        }

        public Task SaveAsync(QuestJob job, CancellationToken cancellation = default)
        {
            Writes++;
            saved[(job.Character, job.Quest)] = job;
            return Task.CompletedTask;
        }
    }
}
