using DAQuester.Core;
using System.Reflection;
using Xunit;

namespace DAQuester.Tests;

public class UpdateTests
{
    sealed class Backend : IUpdateBackend
    {
        public bool IsInstalled { get;
        set; } = true;
        public string? PendingVersion
        {
            get;
        set;
        }
        public string? NextVersion { get;
        set; } = "1.1.0";

        public int Checks, Downloads, Installs;
        public Exception? Failure;
        public TaskCompletionSource<string?>? DelayedCheck;
        public TaskCompletionSource? DelayedDownload;
        public TaskCompletionSource DownloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action? AfterDownload;
        public Task<string?> CheckAsync()
        {
            Checks++;
            if (Failure is not null)
                throw Failure;
            return DelayedCheck?.Task ?? Task.FromResult(NextVersion);
        }

        public async Task DownloadAsync()
        {
            Downloads++;
            if (Failure is not null)
                throw Failure;
            DownloadStarted.TrySetResult();
            if (DelayedDownload is not null) await DelayedDownload.Task;
            PendingVersion = NextVersion;
            AfterDownload?.Invoke();
        }

        public void PrepareExit()
        {
            if (Failure is not null)
                throw Failure;
            Installs++;
        }
    }

    static Task Update(UpdateCoordinator updates)
    {
        var method = typeof(UpdateCoordinator).GetMethod("UpdateAsync");
        Assert.NotNull(method);
        try { return (Task)method.Invoke(updates, null)!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { return Task.FromException(error.InnerException); }
    }

    static bool Requested(UpdateCoordinator updates)
    {
        var property = typeof(UpdateCoordinator).GetProperty("InstallRequested");
        Assert.NotNull(property);
        return (bool)property.GetValue(updates)!;
    }

    static bool CanPrepare(UpdateCoordinator updates, bool busy = false, bool connected = false, bool launched = false)
    {
        var method = typeof(UpdateCoordinator).GetMethod("CanPrepareExit");
        Assert.NotNull(method);
        return (bool)method.Invoke(updates, [busy, connected, launched])!;
    }

    static void Cancel(UpdateCoordinator updates)
    {
        var method = typeof(UpdateCoordinator).GetMethod("CancelInstallRequest");
        Assert.NotNull(method);
        method.Invoke(updates, null);
    }

    static void PrepareExpected(IUpdateBackend backend, string version)
    {
        var method = typeof(IUpdateBackend).GetMethod("PrepareExit", [typeof(string)]);
        Assert.NotNull(method);
        try { method.Invoke(backend, [version]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }

    sealed class VersionBackend : IUpdateBackend
    {
        public bool IsInstalled => true;
        public string? PendingVersion => "1.1.0";
        public int LegacyCalls;
        public string? ExpectedVersion;
        public Task<string?> CheckAsync() => throw new InvalidOperationException("The synthetic pending package needs no check.");
        public Task DownloadAsync() => throw new InvalidOperationException("The synthetic pending package needs no download.");
        public void PrepareExit() => LegacyCalls++;
        public void PrepareExit(string expectedVersion) => ExpectedVersion = expectedVersion;
    }

    sealed class ChangedPendingBackend : IUpdateBackend
    {
        public bool IsInstalled => true;
        public bool ChangeAtHandoff;
        public int Reads, LegacyCalls;
        public string? PendingVersion => !ChangeAtHandoff || ++Reads == 1 ? "1.1.0" : "9.9.9";
        public Task<string?> CheckAsync() => throw new InvalidOperationException("The synthetic pending package needs no check.");
        public Task DownloadAsync() => throw new InvalidOperationException("The synthetic pending package needs no download.");
        public void PrepareExit() => LegacyCalls++;
    }

    [Fact]
    public async Task The_backend_receives_the_exact_version_authorized_by_the_update_click()
    {
        var backend = new VersionBackend();
        var updates = new UpdateCoordinator(backend);
        await Update(updates);

        Assert.True(updates.PrepareExit(false, false, false));

        Assert.Equal("1.1.0", backend.ExpectedVersion);
        Assert.Equal(0, backend.LegacyCalls);
    }

    [Theory]
    [InlineData("1.1.0", true)]
    [InlineData("9.9.9", false)]
    [InlineData(null, false)]
    public void A_legacy_backend_default_handoff_validates_the_expected_pending_version(string? pending, bool accepted)
    {
        var backend = new Backend { PendingVersion = pending };
        if (accepted) PrepareExpected(backend, "1.1.0");
        else Assert.Throws<InvalidOperationException>(() => PrepareExpected(backend, "1.1.0"));

        Assert.Equal(accepted ? 1 : 0, backend.Installs);
    }

    [Fact]
    public async Task A_pending_package_replaced_after_the_core_safety_read_cannot_reach_the_legacy_helper()
    {
        var backend = new ChangedPendingBackend();
        var updates = new UpdateCoordinator(backend);
        await Update(updates);
        backend.ChangeAtHandoff = true;

        Assert.False(updates.PrepareExit(false, false, false));

        Assert.Equal(0, backend.LegacyCalls);
        Assert.False(Requested(updates));
        Assert.Contains("Couldn't start", updates.Status);
    }

    [Fact]
    public async Task Cancelling_after_a_handoff_failure_preserves_its_precise_status()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await Update(updates);
        backend.Failure = new IOException("Locked");
        Assert.False(updates.PrepareExit(false, false, false));
        string failure = updates.Status;

        Cancel(updates);

        Assert.Equal(failure, updates.Status);
        Assert.False(Requested(updates));
        Assert.Equal("1.1.0", updates.PendingVersion);
    }

    [Fact]
    public async Task One_update_action_checks_downloads_and_authorizes_the_verified_package()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);

        await Update(updates);

        Assert.Equal((1, 1, 0), (backend.Checks, backend.Downloads, backend.Installs));
        Assert.True(Requested(updates));
        Assert.True(CanPrepare(updates));
        Assert.False(updates.Working);
        Assert.True(updates.PrepareExit(false, false, false));
        Assert.Equal(1, backend.Installs);
    }

    [Fact]
    public async Task A_known_available_update_does_not_require_another_check()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await updates.CheckAsync();

        await Update(updates);

        Assert.Equal((1, 1), (backend.Checks, backend.Downloads));
        Assert.True(Requested(updates));
    }

    [Fact]
    public async Task A_previous_pending_package_needs_one_fresh_click_without_checking_or_downloading_again()
    {
        var backend = new Backend { PendingVersion = "1.0.5" };
        var updates = new UpdateCoordinator(backend);
        await updates.StartAsync();
        Assert.False(Requested(updates));
        Assert.False(CanPrepare(updates));

        await Update(updates);

        Assert.True(Requested(updates));
        Assert.Equal((0, 0), (backend.Checks, backend.Downloads));
        Assert.True(updates.PrepareExit(false, false, false));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task One_click_intent_waits_for_safety_and_later_prepares_without_another_click(bool busy, bool connected, bool launched)
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await Update(updates);

        Assert.True(Requested(updates));
        Assert.False(CanPrepare(updates, busy, connected, launched));
        Assert.False(updates.PrepareExit(busy, connected, launched));
        Assert.Equal(0, backend.Installs);
        Assert.True(CanPrepare(updates));
        Assert.True(updates.PrepareExit(false, false, false));
        Assert.Equal((1, 1), (backend.Downloads, backend.Installs));
    }

    [Fact]
    public async Task No_release_or_check_failure_never_arms_installation()
    {
        var backend = new Backend { NextVersion = null };
        var updates = new UpdateCoordinator(backend);
        await Update(updates);
        Assert.False(Requested(updates));
        Assert.Equal(0, backend.Downloads);

        backend.Failure = new IOException("Offline");
        await Update(updates);
        Assert.False(Requested(updates));
        Assert.Contains("Offline", updates.Status);
        Assert.False(updates.Working);
        Assert.False(updates.PrepareExit(false, false, false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("9.9.9")]
    public async Task A_download_without_the_exact_verified_pending_version_never_arms(string? pending)
    {
        var backend = new Backend();
        backend.AfterDownload = () => backend.PendingVersion = pending;
        var updates = new UpdateCoordinator(backend);

        await Update(updates);

        Assert.False(Requested(updates));
        Assert.False(CanPrepare(updates));
        Assert.False(updates.PrepareExit(false, false, false));
        Assert.Equal(0, backend.Installs);
        Assert.Contains("Couldn't", updates.Status);
    }

    [Fact]
    public async Task A_failed_download_can_be_retried_by_one_new_update_action()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await updates.CheckAsync();
        backend.Failure = new IOException("Interrupted");
        await Update(updates);
        Assert.False(Requested(updates));
        Assert.False(updates.PrepareExit(false, false, false));
        backend.Failure = null;

        await Update(updates);

        Assert.True(Requested(updates));
        Assert.Equal((1, 2), (backend.Checks, backend.Downloads));
    }

    [Fact]
    public async Task Cancelling_intent_keeps_the_download_for_a_fresh_click()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await Update(updates);

        Cancel(updates);

        Assert.False(Requested(updates));
        Assert.False(CanPrepare(updates));
        Assert.False(updates.PrepareExit(false, false, false));
        Assert.Equal("1.1.0", updates.PendingVersion);
        Assert.Contains("Click Update", updates.Status);
        Assert.DoesNotContain("will install", updates.Status);
        await Update(updates);
        Assert.True(Requested(updates));
        Assert.Equal(1, backend.Downloads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelling_while_check_or_download_is_pending_cannot_rearm_when_it_finishes(bool downloading)
    {
        var backend = new Backend
        {
            DelayedCheck = downloading ? null : new(TaskCreationOptions.RunContinuationsAsynchronously),
            DelayedDownload = downloading ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null
        };
        var updates = new UpdateCoordinator(backend);
        var work = Update(updates);
        if (downloading) await backend.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(updates.Working);

        Cancel(updates);
        if (downloading) backend.DelayedDownload!.SetResult();
        else backend.DelayedCheck!.SetResult("1.1.0");
        await work;

        Assert.False(Requested(updates));
        Assert.False(CanPrepare(updates));
        Assert.False(updates.PrepareExit(false, false, false));
        Assert.False(updates.Working);
        if (downloading) Assert.Equal("1.1.0", updates.PendingVersion);
        else Assert.Equal(0, backend.Downloads);
    }

    [Fact]
    public async Task The_whole_check_download_chain_excludes_duplicate_update_and_legacy_calls()
    {
        var backend = new Backend
        {
            DelayedCheck = new(TaskCreationOptions.RunContinuationsAsynchronously),
            DelayedDownload = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var updates = new UpdateCoordinator(backend);
        var work = Update(updates);
        await Update(updates);
        await updates.CheckAsync();
        await updates.DownloadAsync();
        Assert.Throws<InvalidOperationException>(() => updates.RequestInstall(false, false, false));
        Assert.Equal((1, 0), (backend.Checks, backend.Downloads));
        backend.DelayedCheck.SetResult("1.1.0");
        await backend.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Update(updates);
        await updates.CheckAsync();
        await updates.DownloadAsync();
        Assert.True(updates.Working);
        Assert.Equal((1, 1), (backend.Checks, backend.Downloads));
        backend.DelayedDownload.SetResult();
        await work;
        Assert.True(Requested(updates));
    }

    [Fact]
    public async Task A_replaced_pending_version_cannot_inherit_previous_update_consent()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await Update(updates);
        backend.PendingVersion = "9.9.9";

        Assert.False(CanPrepare(updates));
        Assert.False(updates.PrepareExit(false, false, false));
        Assert.Equal(0, backend.Installs);
    }

    [Fact]
    public async Task A_failed_handoff_disarms_before_notifying_and_can_retry_from_the_cached_package()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await Update(updates);
        bool notifiedDisarmed = false;
        updates.Changed += () =>
        {
            if (updates.Status.Contains("Couldn't start")) notifiedDisarmed = !Requested(updates);
        };
        backend.Failure = new IOException("Locked");

        Assert.False(updates.PrepareExit(false, false, false));

        Assert.True(notifiedDisarmed);
        Assert.False(Requested(updates));
        Assert.Equal("1.1.0", updates.PendingVersion);
        backend.Failure = null;
        await Update(updates);
        Assert.True(updates.PrepareExit(false, false, false));
        Assert.Equal((1, 1), (backend.Downloads, backend.Installs));
    }

    [Fact]
    public async Task A_successful_handoff_is_prepared_only_once()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await Update(updates);

        Assert.True(updates.PrepareExit(false, false, false));
        Assert.False(CanPrepare(updates));
        Assert.True(updates.PrepareExit(false, false, false));
        Assert.Equal(1, backend.Installs);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task One_click_update_is_disabled_for_preview_and_unpackaged_builds(bool preview, bool installed)
    {
        var backend = new Backend { IsInstalled = installed };
        var updates = new UpdateCoordinator(backend, preview);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Update(updates));

        Assert.Equal((0, 0, 0), (backend.Checks, backend.Downloads, backend.Installs));
        Assert.False(Requested(updates));
        Assert.False(CanPrepare(updates));
    }

    [Fact]
    public async Task Updates_default_to_off_and_startup_does_not_contact_GitHub()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        Assert.False(updates.AutomaticChecks);
        await updates.StartAsync();
        Assert.Equal(0, backend.Checks);
    }

    [Fact]
    public async Task Automatic_checks_never_download_or_install()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend)
        {
            AutomaticChecks = true
        };
        await updates.StartAsync();
        Assert.Equal("1.1.0", updates.AvailableVersion);
        Assert.Equal(1, backend.Checks);
        Assert.Equal(0, backend.Downloads);
        Assert.False(updates.PrepareExit(false, false, false));
    }

    [Fact]
    public async Task Manual_download_does_not_implicitly_authorize_install()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await updates.CheckAsync();
        await updates.DownloadAsync();
        Assert.Equal("1.1.0", updates.PendingVersion);
        Assert.False(updates.PrepareExit(false, false, false));
        Assert.Equal(0, backend.Installs);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Installation_is_blocked_by_quests_connections_or_launched_clients(bool busy, bool connected, bool launched)
    {
        var updates = new UpdateCoordinator(new Backend());
        await updates.CheckAsync();
        await updates.DownloadAsync();
        Assert.Throws<InvalidOperationException>(() => updates.RequestInstall(busy, connected, launched));
        Assert.False(updates.PrepareExit(busy, connected, launched));
    }

    [Fact]
    public async Task Explicit_install_waits_for_safe_exit_and_rechecks_safety()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await updates.CheckAsync();
        await updates.DownloadAsync();
        updates.RequestInstall(false, false, false);
        Assert.False(updates.PrepareExit(false, true, false));
        Assert.Equal(0, backend.Installs);
        Assert.True(updates.PrepareExit(false, false, false));
        Assert.Equal(1, backend.Installs);
    }

    [Fact]
    public void Pending_download_from_previous_run_still_requires_install_action()
    {
        var backend = new Backend
        {
            PendingVersion = "1.1.0"
        };
        var updates = new UpdateCoordinator(backend);
        Assert.False(updates.PrepareExit(false, false, false));
        updates.RequestInstall(false, false, false);
        Assert.True(updates.PrepareExit(false, false, false));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Preview_and_unpackaged_builds_cannot_update(bool preview, bool installed)
    {
        var backend = new Backend
        {
            IsInstalled = installed
        };
        var updates = new UpdateCoordinator(backend, preview);
        Assert.False(updates.Available);
        await updates.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => updates.CheckAsync());
        Assert.Throws<InvalidOperationException>(() => updates.RequestInstall(false, false, false));
        Assert.Equal(0, backend.Checks);
    }

    [Fact]
    public async Task Network_failure_is_recoverable_and_does_not_download()
    {
        var backend = new Backend
        {
            Failure = new IOException("Offline")
        };
        var updates = new UpdateCoordinator(backend);
        await updates.CheckAsync();
        Assert.Contains("Offline", updates.Status);
        Assert.False(updates.Working);
        Assert.Null(updates.AvailableVersion);
        backend.Failure = null;
        await updates.CheckAsync();
        Assert.Equal("1.1.0", updates.AvailableVersion);
        Assert.Equal(0, backend.Downloads);
    }

    [Fact]
    public async Task Failed_download_never_authorizes_install()
    {
        var backend = new Backend();
        var updates = new UpdateCoordinator(backend);
        await updates.CheckAsync();
        backend.Failure = new IOException("Checksum mismatch");
        await updates.DownloadAsync();
        Assert.Contains("Checksum mismatch", updates.Status);
        Assert.False(updates.Working);
        Assert.False(updates.PrepareExit(false, false, false));
        Assert.Throws<InvalidOperationException>(() => updates.RequestInstall(false, false, false));
    }

    [Fact]
    public async Task Duplicate_checks_and_installs_are_blocked_while_working()
    {
        var backend = new Backend
        {
            DelayedCheck = new()
        };
        var updates = new UpdateCoordinator(backend);
        var check = updates.CheckAsync();
        await updates.CheckAsync();
        Assert.Equal(1, backend.Checks);
        Assert.True(updates.Working);
        Assert.Throws<InvalidOperationException>(() => updates.RequestInstall(false, false, false));
        backend.DelayedCheck.SetResult("1.1.0");
        await check;
        Assert.False(updates.Working);
    }

    [Fact]
    public async Task No_new_release_disables_download()
    {
        var updates = new UpdateCoordinator(new Backend { NextVersion = null });
        await updates.CheckAsync();
        Assert.Null(updates.AvailableVersion);
        Assert.Contains("latest", updates.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => updates.DownloadAsync());
    }

    [Fact]
    public void Installer_launch_failure_leaves_app_data_and_status_available()
    {
        var backend = new Backend
        {
            PendingVersion = "1.1.0"
        };
        var updates = new UpdateCoordinator(backend);
        updates.RequestInstall(false, false, false);
        backend.Failure = new IOException("Locked");
        Assert.False(updates.PrepareExit(false, false, false));
        Assert.Contains("Locked", updates.Status);
        Assert.Equal(0, backend.Installs);
    }
}
