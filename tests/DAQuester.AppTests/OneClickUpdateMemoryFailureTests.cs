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

public sealed class OneClickUpdateMemoryFailureTests
{
    [Fact]
    public async Task Repeated_memory_observations_share_one_inflight_flush_and_keep_bounded_retry_sources()
    {
        await using var fixture = new Fixture();
        await fixture.RecordFailedObservationAsync();
        var flush = Field<SemaphoreSlim>(fixture.Runtime, "characterMemoryFlush");
        await flush.WaitAsync();
        try
        {
            for (int revision = 11; revision <= 210; revision++)
                fixture.Observe(fixture.Observed with { Revision = revision });

            _ = Assert.Single(Field<HashSet<Task>>(fixture.Runtime, "characterMemoryWrites"));
            var pending = Assert.IsAssignableFrom<System.Collections.ICollection>(
                Field<object>(fixture.Runtime, "pendingCharacterMemorySaves"));
            Assert.InRange(pending.Count, 1, 4);
        }
        finally
        {
            flush.Release();
            await Task.WhenAll(Field<HashSet<Task>>(fixture.Runtime, "characterMemoryWrites").ToArray());
            fixture.RemoveSession();
        }
    }

    [Fact]
    public async Task Bounded_retry_preserves_old_actual_legends_when_a_new_login_only_observed_level_and_class()
    {
        await using var fixture = new Fixture();
        await fixture.RecordFailedObservationAsync();
        var newLogin = fixture.Observed with
        {
            Generation = 8, Revision = 1, Level = 50, Class = "Priest",
            LegendsKnown = false, LegendsRevision = 0, Legends = []
        };
        await fixture.RecordFailedObservationAsync(newLogin);
        File.Delete(fixture.MemoryDirectory);
        fixture.Backend.BeforeInstall = () =>
        {
            var saved = new FileCharacterMemory(fixture.MemoryDirectory, fixture.Runtime.Settings.ServerProfile).Load("Elara");
            Assert.Equal(newLogin.Level, saved.Level);
            Assert.Equal(newLogin.Class, saved.Class);
            Assert.Equal(fixture.Observed.Legends.ToArray(), saved.Legends);
            Assert.NotNull(saved.LegendsObservedAt);
        };
        Task? shutdown = null;
        fixture.Runtime.UpdateExitRequested += () => shutdown = fixture.Runtime.ShutdownAsync(true);

        await fixture.Runtime.UpdateAndRestartAsync();
        Assert.NotNull(shutdown);
        await shutdown!;

        Assert.Equal(1, fixture.Backend.Installs);
    }

    [Fact]
    public async Task An_actual_character_memory_write_failure_prevents_the_updater_and_restores_admission()
    {
        await using var fixture = new Fixture();
        await fixture.RecordFailedObservationAsync();
        Task? shutdown = null;
        fixture.Runtime.UpdateExitRequested += () => shutdown = fixture.Runtime.ShutdownAsync(true);

        await fixture.Runtime.UpdateAndRestartAsync();
        Assert.NotNull(shutdown);
        await Assert.ThrowsAsync<IOException>(() => shutdown!);

        Assert.Equal(0, fixture.Backend.Installs);
        Assert.False(fixture.Runtime.Updates.InstallRequested);
        Assert.False(fixture.Runtime.TryBeginUpdateExit());
        Assert.False(Field<bool>(fixture.Runtime, "closing"));
        Assert.False(Field<bool>(fixture.Runtime, "shuttingDown"));
        Assert.True(fixture.Host.TryPauseForUpdate());
        fixture.Host.ResumeAfterUpdateFailure();
        Assert.True(File.Exists(fixture.MemoryDirectory));
    }

    [Fact]
    public async Task A_fresh_click_after_repair_retries_the_exact_failed_own_memory_before_the_helper()
    {
        await using var fixture = new Fixture();
        await fixture.RecordFailedObservationAsync();
        Task? shutdown = null;
        int exits = 0;
        fixture.Runtime.UpdateExitRequested += () =>
        {
            exits++;
            shutdown = fixture.Runtime.ShutdownAsync(true);
        };
        await fixture.Runtime.UpdateAndRestartAsync();
        await Assert.ThrowsAsync<IOException>(() => shutdown!);
        Assert.Equal(0, fixture.Backend.Installs);
        File.Delete(fixture.MemoryDirectory);
        fixture.Backend.BeforeInstall = () =>
        {
            var saved = new FileCharacterMemory(fixture.MemoryDirectory, fixture.Runtime.Settings.ServerProfile).Load("Elara");
            Assert.Equal(fixture.Observed.Level, saved.Level);
            Assert.Equal(fixture.Observed.Class, saved.Class);
            Assert.Equal(fixture.Observed.Legends.ToArray(), saved.Legends);
            Assert.NotNull(saved.LegendsObservedAt);
        };

        await fixture.Runtime.UpdateAndRestartAsync();
        await shutdown!;

        Assert.Equal(2, exits);
        Assert.Equal(1, fixture.Backend.Downloads);
        Assert.Equal(1, fixture.Backend.Installs);
        Assert.True(Field<bool>(fixture.Runtime, "closing"));
    }

    static T Field<T>(DesktopRuntime runtime, string name)
        => (T)typeof(DesktopRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;

    sealed class Fixture : IAsyncDisposable
    {
        readonly TcpClient client = new();
        readonly TcpClient remote = new();
        readonly ProxyConnection connection;
        readonly GameSession session;
        public Backend Backend { get; } = new();
        public DesktopRuntime Runtime { get; }
        public string MemoryDirectory { get; }
        public ProxyHost Host => Field<ProxyHost>(Runtime, "host");
        public CharacterSnapshot Observed { get; } = new()
        {
            Name = "Elara", Id = 77, Connected = true, Generation = 7, Revision = 10,
            Level = 99, Class = "Mage", LegendsKnown = true, LegendsRevision = 10,
            Legends = ImmutableArray.Create("A synthetic own character memory mark")
        };

        public Fixture()
        {
            AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
            var settings = AppSettings.Load();
            settings.Characters = ["Elara"];
            settings.ClientPath = "";
            settings.AutomaticUpdateChecks = false;
            settings.Save();
            Runtime = new(updateBackend: Backend);
            MemoryDirectory = Path.Combine(AppSettings.DataPath, "characters");
            // A file at the required parent directory forces a genuine, bounded
            // filesystem failure without touching the user's application data.
            File.WriteAllText(MemoryDirectory, "Synthetic character-memory directory conflict");
            connection = new(client, remote, Observed.Generation,
                _ => throw new InvalidOperationException("Synthetic memory tests never connect."));
            typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, Observed);
            session = new GameSession(connection, new MapRepository(AppSettings.DataPath,
                Path.Combine(AppSettings.Content, "routes.json")));
            Field<Dictionary<string, GameSession>>(Runtime, "sessions").Add("Elara", session);
        }

        public Task RecordFailedObservationAsync() => RecordFailedObservationAsync(Observed);

        public async Task RecordFailedObservationAsync(CharacterSnapshot snapshot)
        {
            Observe(snapshot);
            await Task.WhenAll(Field<HashSet<Task>>(Runtime, "characterMemoryWrites").ToArray());
            Assert.Contains(Runtime.Logs, line => line.Contains("character marks could not be saved", StringComparison.Ordinal));
            Assert.True(File.Exists(MemoryDirectory));
            RemoveSession();
        }

        public void Observe(CharacterSnapshot snapshot)
        {
            Field<Dictionary<string, GameSession>>(Runtime, "sessions")["Elara"] = session;
            typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, snapshot);
            var remember = typeof(DesktopRuntime).GetMethod("RememberCharacter", BindingFlags.Instance | BindingFlags.NonPublic,
                null, [typeof(string), typeof(GameSession), typeof(CharacterSnapshot)], null)!;
            remember.Invoke(Runtime, ["Elara", session, snapshot]);
        }

        public void RemoveSession() => Field<Dictionary<string, GameSession>>(Runtime, "sessions").Clear();

        public async ValueTask DisposeAsync()
        {
            Field<Dictionary<string, GameSession>>(Runtime, "sessions").Clear();
            if (File.Exists(MemoryDirectory)) File.Delete(MemoryDirectory);
            await Runtime.ShutdownAsync();
            await connection.DisposeAsync();
            client.Dispose();
            remote.Dispose();
        }
    }

    sealed class Backend : IUpdateBackend
    {
        public bool IsInstalled => true;
        public string? PendingVersion { get; private set; }
        public int Downloads, Installs;
        public Action? BeforeInstall;
        public Task<string?> CheckAsync() => Task.FromResult<string?>("1.8.1");
        public Task DownloadAsync()
        {
            Downloads++;
            PendingVersion = "1.8.1";
            return Task.CompletedTask;
        }
        public void PrepareExit()
        {
            BeforeInstall?.Invoke();
            Installs++;
        }
    }
}
