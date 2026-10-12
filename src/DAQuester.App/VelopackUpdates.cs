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
    public void PrepareExit() => Manager.WaitExitThenApplyUpdates(Manager.UpdatePendingRestart, silent: true, restart: false);
}
