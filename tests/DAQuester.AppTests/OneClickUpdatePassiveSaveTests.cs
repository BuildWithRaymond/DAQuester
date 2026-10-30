using System.Collections.Immutable;
using System.Net.Sockets;
using System.Reflection;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public sealed class OneClickUpdatePassiveSaveTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, Private)!.GetValue(owner)!;

    static void SetSnapshot(ProxyConnection connection, CharacterSnapshot snapshot) =>
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, snapshot);

    [Fact]
    public async Task A_new_memory_observation_from_flush_completion_is_saved_without_a_third_observation()
    {
        await using var fixture = new Fixture(new Journal(new() { Character = "Elara" }));
        await using var connection = new ProxyConnection(new TcpClient(), new TcpClient(), 7,
            _ => throw new InvalidOperationException("Synthetic memory tests never redirect."));
        var first = new CharacterSnapshot
        {
            Name = "Elara", Id = 77, Connected = true, Generation = 7, Revision = 10, Level = 10, Class = "Mage"
        };
        var next = first with { Revision = 11, Level = 50 };
        SetSnapshot(connection, first);
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath,
            Path.Combine(AppSettings.Content, "routes.json")));
        Field<Dictionary<string, GameSession>>(fixture.Runtime, "sessions").Add("Elara", session);
        var remember = typeof(DesktopRuntime).GetMethod("RememberCharacter", Private, null,
            [typeof(string), typeof(GameSession), typeof(CharacterSnapshot)], null)!;
        bool observedNext = false;
        fixture.Runtime.Changed += () =>
        {
            if (observedNext || fixture.Runtime.Memory("Elara").Level != first.Level) return;
            observedNext = true;
            SetSnapshot(connection, next);
            remember.Invoke(fixture.Runtime, ["Elara", session, next]);
        };
        var flush = Field<SemaphoreSlim>(fixture.Runtime, "characterMemoryFlush");
        await flush.WaitAsync(fixture.Deadline.Token);
        try
        {
            remember.Invoke(fixture.Runtime, ["Elara", session, first]);
        }
        finally
        {
            flush.Release();
        }

        var writes = Field<HashSet<Task>>(fixture.Runtime, "characterMemoryWrites");
        while (writes.Any(write => !write.IsCompleted))
            await Task.WhenAll(writes.ToArray()).WaitAsync(fixture.Deadline.Token);

        Assert.True(observedNext);
        var durable = new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"),
            fixture.Runtime.Settings.ServerProfile).Load("Elara");
        Assert.Equal(next.Level, durable.Level);
        Assert.Equal(0, fixture.Backend.Installs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task A_passive_reward_journal_write_finishes_before_the_updater_is_armed(int failures)
    {
        var observedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        var reply = new Dialog(7, 2, 99, 1, "Callough", "Done", true, 177, 92, 0, []) { ObservedAt = observedAt };
        var journal = new Journal(new()
        {
            Character = "Elara", Quest = "spare-a-stick", Step = "confirm",
            Pending = new("turn-in", 7, 1, 100, 6, 1000, observedAt, TerminalSeen: true)
            {
                TerminalRevision = 2, EntityId = 99
            },
            CompletionDialog = reply
        }, blockSaves: true, failures: failures);
        await using var fixture = new Fixture(journal);
        await using var connection = new ProxyConnection(new TcpClient(), new TcpClient(), 7,
            _ => throw new InvalidOperationException("Synthetic passive-save tests never redirect."));
        var before = new CharacterSnapshot
        {
            Name = "Elara", Id = 77, Connected = true, Generation = 7, Revision = 3,
            Gold = 100, Experience = 1000, InventoryKnown = true,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Branch", 6)),
            LegendsKnown = true, LegendsRevision = 1
        };
        SetSnapshot(connection, before);
        var bind = typeof(DesktopRuntime).GetMethod("BindAsync", Private)!;
        await (Task)bind.Invoke(fixture.Runtime, [connection])!;
        var session = Field<Dictionary<string, GameSession>>(fixture.Runtime, "sessions")["Elara"];
        var calibration = new Calibration
        {
            Validated = true, Patrol = [new(1, 1)], Accepted = ["Accepted"], Completed = ["Done"], EvidenceFiles = ["synthetic"]
        };
        typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.Calibration))!.SetValue(fixture.Runtime, calibration);
        var recipe = new QuestDefinition("spare-a-stick", 1, "Spare a Stick", "", [], [new("confirm", "confirm", null)]);
        var runner = new QuestRunner(session, journal, recipe, calibration);
        Field<Dictionary<string, QuestRunner>>(fixture.Runtime, "runners").Add("Elara", runner);
        var reward = before with { Revision = 4, Experience = 4500, Inventory = ImmutableDictionary<byte, Item>.Empty };

        // Exercise the real subscribed session callback, not an injected pending Task.
        typeof(StateTracker).GetMethod("Publish", Private)!.Invoke(connection.Tracker, [reward]);
        await journal.SaveStarted.Task.WaitAsync(fixture.Deadline.Token);
        Assert.False(runner.IsRunning);
        SetSnapshot(connection, session.Snapshot with { Connected = false });
        try
        {
            await fixture.Runtime.UpdateAndRestartAsync();

            Assert.Equal(0, fixture.Backend.Installs);
            Assert.False(journal.Saved.Task.IsCompleted);
            journal.AllowSave.TrySetResult();
            await journal.FirstAttemptFinished.Task.WaitAsync(fixture.Deadline.Token);
            if (failures == 2)
            {
                Assert.NotNull(await Record.ExceptionAsync(fixture.FinishUpdate));
                Assert.Equal(0, fixture.Backend.Installs);
                Assert.False(fixture.Runtime.Updates.InstallRequested);
                Assert.False(fixture.Runtime.TryBeginUpdateExit());
                Assert.Equal(2, journal.SaveAttempts);
                var host = Field<ProxyHost>(fixture.Runtime, "host");
                Assert.True(host.TryPauseForUpdate());
                host.ResumeAfterUpdateFailure();
                journal.FailuresRemaining = 0;
                await fixture.Runtime.UpdateAndRestartAsync();
            }
            await fixture.FinishUpdate();
            Assert.Equal(1, fixture.Backend.Installs);
            Assert.True(journal.Saved.Task.IsCompleted);
            Assert.Equal(failures + 1, journal.SaveAttempts);
            Assert.Equal("Completed", journal.Load("Elara", "spare-a-stick").Status);
            Assert.Null(journal.Load("Elara", "spare-a-stick").Pending);
        }
        finally
        {
            journal.AllowSave.TrySetResult();
            await journal.FirstAttemptFinished.Task.WaitAsync(fixture.Deadline.Token);
        }
    }

    [Fact]
    public async Task A_scheduler_passive_checkpoint_blocks_update_before_it_becomes_an_active_run()
    {
        await using var fixture = new Fixture(new Journal(PermanentJob()));
        var scheduler = Field<QuestScheduler>(fixture.Runtime, "scheduler");
        var goal = Schedule("spare-a-stick");
        typeof(QuestScheduler).GetProperty(nameof(QuestScheduler.State))!.SetValue(scheduler,
            new QuestSchedulerState { Enabled = false, Entries = [goal] });
        var writes = Field<SemaphoreSlim>(scheduler, "writes");
        await writes.WaitAsync(fixture.Deadline.Token);
        Task? tick = null;
        bool held = true;
        try
        {
            tick = fixture.Runtime.TickSchedulerAsync();
            Assert.False(tick.IsCompleted);
            Assert.False(scheduler.IsRunning);

            await fixture.Runtime.UpdateAndRestartAsync();

            Assert.Equal(0, fixture.Backend.Installs);
            writes.Release();
            held = false;
            await tick.WaitAsync(fixture.Deadline.Token);
            Assert.Equal("Completed", scheduler.State.Entries.Single().Status);
            await fixture.FinishUpdate();
            Assert.Equal(1, fixture.Backend.Installs);
        }
        finally
        {
            if (held) writes.Release();
            if (tick is not null) await tick.WaitAsync(fixture.Deadline.Token);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_scheduler_tick_resuming_after_quiescence_cannot_persist_a_new_running_attempt(bool queuedConsent)
    {
        await using var fixture = new Fixture(new Journal(PermanentJob()));
        var scheduler = Field<QuestScheduler>(fixture.Runtime, "scheduler");
        var goal = Schedule("spare-a-stick");
        var due = Schedule(DaydreamerEvidence.Id);
        typeof(QuestScheduler).GetProperty(nameof(QuestScheduler.State))!.SetValue(scheduler,
            new QuestSchedulerState { Enabled = true, Entries = [goal, due] });
        var writes = Field<SemaphoreSlim>(scheduler, "writes");
        await writes.WaitAsync(fixture.Deadline.Token);
        Task? tick = null;
        bool held = true;
        try
        {
            tick = fixture.Runtime.TickSchedulerAsync();
            Assert.False(tick.IsCompleted);
            Assert.False(scheduler.IsRunning);
            if (queuedConsent)
                await fixture.Runtime.Updates.UpdateAsync();
            else
                // This is the same admission-sealed phase entered by an idle update claim.
                typeof(DesktopRuntime).GetField("shuttingDown", Private)!.SetValue(fixture.Runtime, true);
            writes.Release();
            held = false;
            await tick.WaitAsync(fixture.Deadline.Token);

            Assert.Equal("Armed", scheduler.State.Entries.Single(entry => entry.Id == due.Id).Status);
            Assert.Equal("", scheduler.State.Entries.Single(entry => entry.Id == due.Id).Reason);
            Assert.Equal(0, fixture.Backend.Installs);
        }
        finally
        {
            if (held) writes.Release();
            if (tick is not null) await tick.WaitAsync(fixture.Deadline.Token);
            typeof(DesktopRuntime).GetField("shuttingDown", Private)!.SetValue(fixture.Runtime, false);
        }
    }

    static QuestSchedule Schedule(string quest) => new()
    {
        Character = "Elara", Quest = quest, Profile = "datester-7.41", Connection = "synthetic",
        Due = DateTimeOffset.UtcNow.AddMinutes(-1)
    };

    static QuestJob PermanentJob()
    {
        var at = DateTimeOffset.UtcNow.AddMinutes(-1);
        var reply = new Dialog(7, 2, 99, 1, "Callough", "You remind me of a child I once knew.", true, 177, 82, 0, []) { ObservedAt = at };
        return new() { Character = "Elara", Quest = "spare-a-stick", Status = "PermanentlyCompleted", CompletedAt = at, PermanentCompletion = new(at, reply) };
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Backend Backend { get; } = new();
        public DesktopRuntime Runtime { get; }
        public CancellationTokenSource Deadline { get; } = new(TimeSpan.FromSeconds(10));
        Task? shutdown;

        public Fixture(IQuestJournal journal)
        {
            AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
            var settings = AppSettings.Load();
            settings.Characters = ["Elara"];
            settings.ClientPath = Path.Combine(AppSettings.DataPath, "synthetic-client-never-executed.exe");
            settings.AutomaticUpdateChecks = false;
            settings.Save();
            Runtime = new(updateBackend: Backend, journal: journal);
            typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.Ready))!.SetValue(Runtime, true);
            Runtime.UpdateExitRequested += () => shutdown = Runtime.ShutdownAsync(true);
        }

        public async Task FinishUpdate()
        {
            while (!Runtime.TryBeginUpdateExit()) await Task.Delay(10, Deadline.Token);
            Assert.NotNull(shutdown);
            await shutdown.WaitAsync(Deadline.Token);
        }

        public async ValueTask DisposeAsync()
        {
            Field<Dictionary<string, GameSession>>(Runtime, "sessions").Clear();
            Field<Dictionary<string, QuestRunner>>(Runtime, "runners").Clear();
            await Runtime.ShutdownAsync();
            Deadline.Dispose();
        }
    }

    sealed class Journal(QuestJob initial, bool blockSaves = false, int failures = 0) : IQuestJournal
    {
        QuestJob saved = initial;
        public int FailuresRemaining = failures;
        public int SaveAttempts;
        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Saved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstAttemptFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public QuestJob Load(string character, string quest) => quest == saved.Quest ? saved : new() { Character = character, Quest = quest };

        public async Task SaveAsync(QuestJob job, CancellationToken cancellation = default)
        {
            int attempt = Interlocked.Increment(ref SaveAttempts);
            SaveStarted.TrySetResult();
            try
            {
                if (blockSaves) await AllowSave.Task.WaitAsync(cancellation);
                if (Interlocked.Decrement(ref FailuresRemaining) >= 0)
                    throw new IOException("Synthetic passive receipt save failure.");
                saved = job;
                Saved.TrySetResult();
            }
            finally
            {
                if (attempt == 1) FirstAttemptFinished.TrySetResult();
            }
        }
    }

    sealed class Backend : IUpdateBackend
    {
        public bool IsInstalled => true;
        public string? PendingVersion { get; private set; }
        public int Installs;
        public Task<string?> CheckAsync() => Task.FromResult<string?>("1.8.1");
        public Task DownloadAsync() { PendingVersion = "1.8.1"; return Task.CompletedTask; }
        public void PrepareExit() => Installs++;
    }
}
