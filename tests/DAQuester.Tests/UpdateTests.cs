using DAQuester.Core;
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
        public Task<string?> CheckAsync()
        {
            Checks++;
            if (Failure is not null)
                throw Failure;
            return DelayedCheck?.Task ?? Task.FromResult(NextVersion);
        }

        public Task DownloadAsync()
        {
            Downloads++;
            if (Failure is not null)
                throw Failure;
            PendingVersion = NextVersion;
            return Task.CompletedTask;
        }

        public void PrepareExit()
        {
            if (Failure is not null)
                throw Failure;
            Installs++;
        }
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
