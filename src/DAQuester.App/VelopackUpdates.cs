using DAQuester.Core;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace DAQuester.App;

internal sealed class VelopackUpdates : IUpdateBackend
{
    UpdateManager? manager;
    UpdateManager Manager => manager ??= new(new GithubSource(UpdateCoordinator.RepositoryUrl, null, false));

    UpdateInfo? update;
    public bool IsInstalled => VelopackLocator.IsCurrentSet && Manager.IsInstalled;
    public string? PendingVersion => IsInstalled ? Manager.UpdatePendingRestart?.Version.ToString() : null;

    public async Task<string?> CheckAsync()
    {
        update = await Manager.CheckForUpdatesAsync();
        return update?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync() => Manager.DownloadUpdatesAsync(update ?? throw new InvalidOperationException("Check for an update first."));
    public void PrepareExit() => PrepareExit(PendingVersion ?? throw new InvalidOperationException("Download an update first."));

    public void PrepareExit(string expectedVersion)
    {
        var pending = Manager.UpdatePendingRestart ?? throw new InvalidOperationException("Download an update first.");
        if (!string.Equals(pending.Version.ToString(), expectedVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("The downloaded update changed. Click Update again.");
        Manager.WaitExitThenApplyUpdates(pending, silent: true, restart: true);
    }
}
