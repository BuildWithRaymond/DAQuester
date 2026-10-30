using System.Diagnostics;
using System.Net;
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

public sealed class OneClickUpdateRuntimeTests
{
    [Fact]
    public async Task Residual_cleanup_failure_after_handoff_is_reported_without_blocking_exit()
    {
        var errors = new List<Exception>();
        await AwaitCleanup(Task.FromException(new IOException("Synthetic disposal failure")), TimeSpan.FromSeconds(1), errors.Add);
        Assert.IsType<IOException>(Assert.Single(errors));
    }

    [Fact]
    public async Task Residual_cleanup_after_handoff_has_a_bounded_wait()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = new List<Exception>();
        await AwaitCleanup(pending.Task, TimeSpan.Zero, errors.Add);
        Assert.IsType<TimeoutException>(Assert.Single(errors));
        pending.SetResult();
    }

    [Fact]
    public async Task A_diagnostic_failure_after_handoff_cannot_prevent_exit()
    {
        await AwaitCleanup(Task.FromException(new IOException("Synthetic disposal failure")), TimeSpan.FromSeconds(1),
            _ => throw new IOException("Synthetic diagnostic failure"));
    }

    [Fact]
    public async Task Direct_install_shutdown_refuses_busy_state_before_cancelling_any_owner()
    {
        await using var fixture = new Fixture();
        using var owner = new CancellationTokenSource();
        fixture.Owners.Add("Elara", owner);
        await CoreUpdate(fixture.Runtime);
        var shutdown = fixture.Runtime.ShutdownAsync(true);
        try
        {
            Assert.False(owner.IsCancellationRequested);
            await Assert.ThrowsAsync<InvalidOperationException>(() => shutdown);
            Assert.Equal(0, fixture.Backend.Installs);
            Assert.False(InstallRequested(fixture.Runtime));
        }
        finally
        {
            fixture.Owners.Clear();
            try { await shutdown; }
            catch (InvalidOperationException) { }
        }
    }

    [Fact]
    public async Task Ordinary_shutdown_never_prepares_an_armed_core_update()
    {
        await using var fixture = new Fixture();
        await CoreUpdate(fixture.Runtime);

        await fixture.Runtime.ShutdownAsync();

        Assert.Equal(0, fixture.Backend.Installs);
        Assert.False(InstallRequested(fixture.Runtime));
    }

    [Fact]
    public async Task One_click_downloads_while_busy_without_stopping_the_owner_then_claims_once_idle()
    {
        await using var fixture = new Fixture();
        using var owner = new CancellationTokenSource();
        fixture.Owners.Add("Elara", owner);
        int exits = 0;
        OnExit(fixture.Runtime, () => exits++);

        await Update(fixture.Runtime);

        Assert.Equal(1, fixture.Backend.Downloads);
        Assert.Equal(0, exits);
        Assert.False(owner.IsCancellationRequested);
        Assert.False(Field<bool>(fixture.Runtime, "shuttingDown"));
        Assert.False(TryBegin(fixture.Runtime));
        fixture.Owners.Clear();

        Assert.True(TryBegin(fixture.Runtime));
        Assert.True(TryBegin(fixture.Runtime));
        Assert.Equal(1, exits);
        Assert.True(Field<bool>(fixture.Runtime, "shuttingDown"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runtime.LaunchBlankClientAsync());
        Assert.Contains("closing", error.Message);
        Assert.Throws<InvalidOperationException>(() => { _ = fixture.Runtime.StopOwnersAsync(); });
    }

    [Fact]
    public async Task A_launched_process_defers_the_update_without_closing_it()
    {
        await using var fixture = new Fixture();
        using var observedProcess = Process.GetCurrentProcess();
        var launched = Field<List<Process>>(fixture.Runtime, "launchedClients");
        launched.Add(observedProcess);
        int exits = 0;
        OnExit(fixture.Runtime, () => exits++);

        await Update(fixture.Runtime);

        Assert.False(TryBegin(fixture.Runtime));
        Assert.False(observedProcess.HasExited);
        Assert.Equal(0, exits);
        launched.Clear();
        Assert.True(TryBegin(fixture.Runtime));
        Assert.Equal(1, exits);
    }

    [Fact]
    public async Task A_supply_plan_save_lease_defers_the_update_without_dropping_the_lease()
    {
        await using var fixture = new Fixture();
        var leases = Field<Dictionary<string, HashSet<object>>>(fixture.Runtime, "supplyPlanMutations");
        var savedLease = new object();
        leases.Add("Elara", [savedLease]);
        int exits = 0;
        OnExit(fixture.Runtime, () => exits++);
        try
        {
            await Update(fixture.Runtime);

            Assert.Equal(0, exits);
            Assert.Contains(savedLease, leases["Elara"]);
            leases.Clear();
            Assert.True(TryBegin(fixture.Runtime));
            Assert.Equal(1, exits);
        }
        finally { leases.Clear(); }
    }

    [Fact]
    public async Task A_connected_character_defers_the_update_without_touching_its_session()
    {
        await using var fixture = new Fixture();
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 7,
            _ => throw new InvalidOperationException("Synthetic update tests never connect."));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker,
            new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 7 });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        Field<Dictionary<string, GameSession>>(fixture.Runtime, "sessions").Add("Elara", session);
        int exits = 0;
        OnExit(fixture.Runtime, () => exits++);

        await Update(fixture.Runtime);

        Assert.False(TryBegin(fixture.Runtime));
        Assert.True(session.Snapshot.Connected);
        Assert.Equal(0, exits);
        Assert.Equal(0, fixture.Backend.Installs);
    }

    [Fact]
    public async Task A_pending_owned_proxy_connection_defers_the_update_before_authentication()
    {
        await using var fixture = new Fixture();
        await fixture.Host.StartOwnedAsync(new(IPAddress.Loopback, 9), null);
        int exits = 0;
        OnExit(fixture.Runtime, () => exits++);

        await Update(fixture.Runtime);

        Assert.False(TryBegin(fixture.Runtime));
        Assert.False(Field<bool>(fixture.Runtime, "shuttingDown"));
        Assert.Equal(0, exits);
        Assert.Equal(0, fixture.Backend.Installs);
    }

    [Fact]
    public async Task A_previous_download_needs_a_new_click_but_is_not_downloaded_twice()
    {
        await using var fixture = new Fixture();
        fixture.Backend.PendingVersion = "1.8.1";
        int exits = 0;
        OnExit(fixture.Runtime, () => exits++);

        Assert.False(TryBegin(fixture.Runtime));
        await Update(fixture.Runtime);

        Assert.Equal(0, fixture.Backend.Downloads);
        Assert.Equal(1, exits);
    }

    [Fact]
    public async Task A_missing_exit_subscriber_never_seals_the_runtime()
    {
        await using var fixture = new Fixture();
        await Update(fixture.Runtime);

        Assert.False(TryBegin(fixture.Runtime));
        Assert.False(Field<bool>(fixture.Runtime, "shuttingDown"));
        Assert.True(PauseHost(fixture.Host));
        ResumeHost(fixture.Host);
    }

    [Fact]
    public async Task Update_handoff_waits_for_durable_saves_and_coalesces_shutdown()
    {
        await using var fixture = new Fixture();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Writes.Add(saved.Task);
        Task? shutdown = null;
        OnExit(fixture.Runtime, () => shutdown = fixture.Runtime.ShutdownAsync(true));
        try
        {
            await Update(fixture.Runtime);

            Assert.NotNull(shutdown);
            Assert.False(shutdown.IsCompleted);
            Assert.Same(shutdown, fixture.Runtime.ShutdownAsync(true));
            Assert.Equal(0, fixture.Backend.Installs);
            saved.SetResult();
            await shutdown;
            Assert.Equal(1, fixture.Backend.Installs);
            Assert.True(Field<bool>(fixture.Runtime, "closing"));
        }
        finally { saved.TrySetResult(); }
    }

    [Fact]
    public async Task A_final_receipt_already_queued_on_the_dispatcher_is_saved_before_handoff()
    {
        await using var fixture = new Fixture();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = new Queue<Action>();
        fixture.Runtime.Dispatch = callbacks.Enqueue;
        callbacks.Enqueue(() => fixture.Writes.Add(saved.Task));
        Task? shutdown = null;
        OnExit(fixture.Runtime, () => shutdown = fixture.Runtime.ShutdownAsync(true));
        try
        {
            await Update(fixture.Runtime);
            Assert.NotNull(shutdown);
            Assert.Equal(0, fixture.Backend.Installs);
            while (callbacks.TryDequeue(out var callback)) callback();
            Assert.False(shutdown.IsCompleted);
            Assert.Equal(0, fixture.Backend.Installs);
            saved.SetResult();
            await shutdown;
            Assert.Equal(1, fixture.Backend.Installs);
        }
        finally { saved.TrySetResult(); fixture.Runtime.Dispatch = action => action(); }
    }

    [Fact]
    public async Task A_failed_save_disarms_before_handoff_and_restores_admission()
    {
        await using var fixture = new Fixture();
        var failedWrite = Task.FromException(new IOException("Synthetic save failure"));
        fixture.Writes.Add(failedWrite);
        Task? shutdown = null;
        int exits = 0;
        OnExit(fixture.Runtime, () => { exits++; shutdown = fixture.Runtime.ShutdownAsync(true); });

        await Update(fixture.Runtime);
        await Assert.ThrowsAsync<IOException>(() => shutdown!);

        Assert.Equal(0, fixture.Backend.Installs);
        Assert.False(InstallRequested(fixture.Runtime));
        Assert.False(TryBegin(fixture.Runtime));
        Assert.False(Field<bool>(fixture.Runtime, "closing"));
        Assert.False(Field<bool>(fixture.Runtime, "shuttingDown"));
        Assert.True(PauseHost(fixture.Host));
        ResumeHost(fixture.Host);
        Assert.Equal(1, exits);
        fixture.Writes.Remove(failedWrite);

        await Update(fixture.Runtime);
        await shutdown!;
        Assert.Equal(2, exits);
        Assert.Equal(1, fixture.Backend.Installs);
    }

    [Fact]
    public async Task Backend_handoff_failure_does_not_loop_and_a_fresh_click_can_retry()
    {
        await using var fixture = new Fixture();
        fixture.Backend.ApplyFailure = new IOException("Synthetic updater failure");
        Task? shutdown = null;
        int exits = 0;
        OnExit(fixture.Runtime, () => { exits++; shutdown = fixture.Runtime.ShutdownAsync(true); });

        await Update(fixture.Runtime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => shutdown!);

        Assert.False(InstallRequested(fixture.Runtime));
        Assert.False(TryBegin(fixture.Runtime));
        Assert.False(Field<bool>(fixture.Runtime, "closing"));
        Assert.True(PauseHost(fixture.Host));
        ResumeHost(fixture.Host);
        Assert.Equal(1, exits);
        fixture.Backend.ApplyFailure = null;

        await Update(fixture.Runtime);
        await shutdown!;
        Assert.Equal(2, exits);
        Assert.Equal(2, fixture.Backend.Installs);
        Assert.Equal(1, fixture.Backend.Downloads);
    }

    [Fact]
    public async Task A_late_owner_is_not_cancelled_or_allowed_to_reach_the_updater()
    {
        await using var fixture = new Fixture();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Writes.Add(saved.Task);
        Task? shutdown = null;
        OnExit(fixture.Runtime, () => shutdown = fixture.Runtime.ShutdownAsync(true));
        using var lateOwner = new CancellationTokenSource();
        try
        {
            await Update(fixture.Runtime);
            fixture.Owners.Add("Elara", lateOwner);
            saved.SetResult();
            await Assert.ThrowsAsync<InvalidOperationException>(() => shutdown!);

            Assert.False(lateOwner.IsCancellationRequested);
            Assert.Equal(0, fixture.Backend.Installs);
            Assert.False(InstallRequested(fixture.Runtime));
            Assert.True(PauseHost(fixture.Host));
            ResumeHost(fixture.Host);
        }
        finally { saved.TrySetResult(); fixture.Owners.Clear(); }
    }

    [Fact]
    public async Task Ordinary_exit_does_not_consume_an_update_click_intent()
    {
        await using var fixture = new Fixture();
        using var owner = new CancellationTokenSource();
        fixture.Owners.Add("Elara", owner);
        OnExit(fixture.Runtime, () => throw new InvalidOperationException("Busy update must not request exit."));
        await Update(fixture.Runtime);
        fixture.Owners.Clear();

        await fixture.Runtime.ShutdownAsync();

        Assert.Equal(0, fixture.Backend.Installs);
        Assert.False(InstallRequested(fixture.Runtime));
    }

    [Fact]
    public async Task A_download_finishing_after_ordinary_exit_cannot_request_an_update_exit()
    {
        await using var fixture = new Fixture();
        fixture.Backend.DelayedDownload = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int exits = 0;
        OnExit(fixture.Runtime, () => exits++);
        var updating = Update(fixture.Runtime);
        try
        {
            Assert.False(updating.IsCompleted);
            await fixture.Runtime.ShutdownAsync();
            fixture.Backend.DelayedDownload.SetResult();
            await updating;

            Assert.False(InstallRequested(fixture.Runtime));
            Assert.Equal(0, fixture.Backend.Installs);
            Assert.Equal(0, exits);
        }
        finally { fixture.Backend.DelayedDownload.TrySetResult(); }
    }

    [Fact]
    public async Task Preview_cannot_download_or_request_exit_even_with_an_injected_backend()
    {
        await using var fixture = new Fixture(preview: true);
        int exits = 0;
        OnExit(fixture.Runtime, () => exits++);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Update(fixture.Runtime));

        Assert.False(TryBegin(fixture.Runtime));
        Assert.Equal(0, fixture.Backend.Checks);
        Assert.Equal(0, fixture.Backend.Downloads);
        Assert.Equal(0, fixture.Backend.Installs);
        Assert.Equal(0, exits);
    }

    static Task Update(DesktopRuntime runtime)
    {
        var method = typeof(DesktopRuntime).GetMethod("UpdateAndRestartAsync");
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(runtime, null));
    }

    static Task CoreUpdate(DesktopRuntime runtime)
    {
        var method = typeof(UpdateCoordinator).GetMethod("UpdateAsync");
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(runtime.Updates, null));
    }

    static Task AwaitCleanup(Task cleanup, TimeSpan timeout, Action<Exception> report)
    {
        var method = typeof(DesktopRuntime).GetMethod("AwaitUpdateCleanupAsync", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(null, [cleanup, timeout, report]));
    }

    static bool TryBegin(DesktopRuntime runtime)
    {
        var method = typeof(DesktopRuntime).GetMethod("TryBeginUpdateExit");
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(runtime, null));
    }

    static void OnExit(DesktopRuntime runtime, Action action)
    {
        var notification = typeof(DesktopRuntime).GetEvent("UpdateExitRequested");
        Assert.NotNull(notification);
        notification.AddEventHandler(runtime, action);
    }

    static bool InstallRequested(DesktopRuntime runtime)
        => Assert.IsType<bool>(typeof(UpdateCoordinator).GetProperty("InstallRequested")!.GetValue(runtime.Updates));

    static bool PauseHost(ProxyHost host)
        => Assert.IsType<bool>(typeof(ProxyHost).GetMethod("TryPauseForUpdate")!.Invoke(host, null));

    static void ResumeHost(ProxyHost host)
        => typeof(ProxyHost).GetMethod("ResumeAfterUpdateFailure")!.Invoke(host, null);

    static T Field<T>(DesktopRuntime runtime, string name)
        => (T)typeof(DesktopRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;

    sealed class Fixture : IAsyncDisposable
    {
        public Backend Backend { get; } = new();
        public DesktopRuntime Runtime { get; }
        public Dictionary<string, CancellationTokenSource> Owners => Field<Dictionary<string, CancellationTokenSource>>(Runtime, "controlActions");
        public HashSet<Task> Writes => Field<HashSet<Task>>(Runtime, "characterMemoryWrites");
        public ProxyHost Host => Field<ProxyHost>(Runtime, "host");

        public Fixture(bool preview = false)
        {
            AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
            var settings = AppSettings.Load();
            settings.Characters = ["Elara"];
            settings.ClientPath = "";
            settings.AutomaticUpdateChecks = false;
            settings.Save();
            Runtime = new(preview: preview, updateBackend: Backend);
        }

        public async ValueTask DisposeAsync()
        {
            Owners.Clear();
            foreach (var write in Writes.Where(write => write.IsFaulted)) _ = write.Exception;
            Writes.Clear();
            Field<List<Process>>(Runtime, "launchedClients").Clear();
            Field<Dictionary<string, GameSession>>(Runtime, "sessions").Clear();
            await Runtime.ShutdownAsync();
        }
    }

    sealed class Backend : IUpdateBackend
    {
        public bool IsInstalled => true;
        public string? PendingVersion { get; set; }
        public int Checks, Downloads, Installs;
        public Exception? ApplyFailure;
        public TaskCompletionSource? DelayedDownload;
        public Task<string?> CheckAsync() { Checks++; return Task.FromResult<string?>("1.8.1"); }
        public async Task DownloadAsync()
        {
            Downloads++;
            if (DelayedDownload is not null) await DelayedDownload.Task;
            PendingVersion = "1.8.1";
        }
        public void PrepareExit()
        {
            Installs++;
            if (ApplyFailure is not null) throw ApplyFailure;
        }
    }
}
