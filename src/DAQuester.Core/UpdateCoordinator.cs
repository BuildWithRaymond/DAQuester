namespace DAQuester.Core;

public interface IUpdateBackend
{
    bool IsInstalled
    {
        get;
    }

    string? PendingVersion
    {
        get;
    }

    Task<string?> CheckAsync();
    Task DownloadAsync();
    void PrepareExit();
    void PrepareExit(string expectedVersion)
    {
        if (string.IsNullOrEmpty(expectedVersion) || !string.Equals(PendingVersion, expectedVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("The downloaded update changed. Click Update again.");
        PrepareExit();
    }
}

/// <summary>Checks never imply download or install consent. Exit safety is checked again at handoff.</summary>
public sealed class UpdateCoordinator(IUpdateBackend backend, bool preview = false)
{
    public const string RepositoryUrl = "https://github.com/BuildWithRaymond/DAQuester";
    public bool Available => !preview && backend.IsInstalled;
    public bool AutomaticChecks
    {
        get;
        set;
    }
    public bool Working
    {
        get; private set;
    }
    public string? AvailableVersion
    {
        get; private set;
    }
    public string? PendingVersion => Available ? backend.PendingVersion : null;
    public string Status { get; private set; } = preview ? "Updates are disabled in previews." : "Check for a newer stable release.";

    public event Action? Changed;
    readonly object gate = new();
    string? requestedVersion;
    long requestEpoch;
    bool handoffPrepared;
    public bool InstallRequested { get { lock (gate) return requestedVersion is not null; } }
    public async Task StartAsync()
    {
        if (Available && AutomaticChecks)
            await CheckAsync();
    }

    public async Task CheckAsync()
    {
        RequireAvailable();
        lock (gate)
        {
            if (Working || handoffPrepared) return;
            Working = true;
        }
        try { await CheckCoreAsync(); }
        finally { FinishWork(); }
    }

    async Task<bool> CheckCoreAsync()
    {
        Status = "Checking for updates…";
        Changed?.Invoke();
        try
        {
            AvailableVersion = await backend.CheckAsync();
            Status = PendingVersion is { } pending ? $"Version {pending} is downloaded and ready." : AvailableVersion is { } version ? $"Version {version} is available. Download when you're ready." : "You have the latest stable release.";
            return true;
        }
        catch (Exception e)
        {
            AvailableVersion = null;
            Status = "Couldn't check for updates: " + e.Message;
            return false;
        }
    }

    public async Task DownloadAsync()
    {
        RequireAvailable();
        string version;
        lock (gate)
        {
            if (Working || handoffPrepared) return;
            version = AvailableVersion ?? throw new InvalidOperationException("Check for an update first.");
            Working = true;
            ClearRequest();
        }
        try { await DownloadCoreAsync(version); }
        finally { FinishWork(); }
    }

    async Task<bool> DownloadCoreAsync(string version)
    {
        Status = "Downloading update…";
        Changed?.Invoke();
        try
        {
            await backend.DownloadAsync();
            if (!string.Equals(PendingVersion, version, StringComparison.Ordinal))
                throw new InvalidOperationException("The downloaded update could not be verified. Try Update again.");
            Status = $"Version {PendingVersion} is ready. Finish quests and close game clients before installing.";
            return true;
        }
        catch (Exception e)
        {
            Status = "Couldn't download the update: " + e.Message;
            return false;
        }
    }

    public async Task UpdateAsync()
    {
        RequireAvailable();
        long epoch;
        lock (gate)
        {
            if (Working || handoffPrepared) return;
            Working = true;
            ClearRequest();
            epoch = requestEpoch;
        }
        try
        {
            string? version = PendingVersion;
            if (version is null)
            {
                if (AvailableVersion is null && !await CheckCoreAsync()) return;
                lock (gate) if (epoch != requestEpoch) return;
                version = PendingVersion ?? AvailableVersion;
                if (version is null) return;
                if (PendingVersion is null && !await DownloadCoreAsync(version)) return;
            }
            lock (gate)
            {
                if (epoch != requestEpoch) return;
                if (!string.Equals(PendingVersion, version, StringComparison.Ordinal))
                {
                    Status = "Couldn't verify the downloaded update. Try Update again.";
                    return;
                }
                requestedVersion = version;
                Status = $"Version {version} is ready. It will install and restart when quests and game clients are closed.";
            }
        }
        finally { FinishWork(epoch); }
    }

    void FinishWork(long? epoch = null)
    {
        lock (gate)
        {
            if (epoch is not null && epoch != requestEpoch)
                Status = PendingVersion is { } version ? $"Version {version} is downloaded. Click Update to install and restart." : "Update cancelled.";
            Working = false;
        }
        Changed?.Invoke();
    }

    void ClearRequest()
    {
        requestedVersion = null;
        requestEpoch++;
    }

    public void CancelInstallRequest()
    {
        lock (gate)
        {
            bool wasRequested = requestedVersion is not null;
            ClearRequest();
            if (wasRequested && !Working && !Status.StartsWith("Couldn't", StringComparison.Ordinal))
                Status = PendingVersion is { } version ? $"Version {version} is downloaded. Click Update to install and restart." : "Update cancelled.";
        }
        Changed?.Invoke();
    }

    public void RequestInstall(bool busy, bool connected, bool launchedClient)
    {
        RequireAvailable();
        lock (gate)
        {
            if (Working || busy || connected || launchedClient)
                throw new InvalidOperationException("Finish quests and close DAQuester-launched Dark Ages clients before installing. Your progress is saved.");
            var pending = PendingVersion ?? throw new InvalidOperationException("Download an update first.");
            ClearRequest();
            requestedVersion = pending;
        }
    }

    bool SafeToPrepare(bool busy, bool connected, bool launchedClient) => Available && requestedVersion is not null &&
        !Working && !busy && !connected && !launchedClient && string.Equals(PendingVersion, requestedVersion, StringComparison.Ordinal);

    public bool CanPrepareExit(bool busy, bool connected, bool launchedClient)
    {
        lock (gate) return !handoffPrepared && SafeToPrepare(busy, connected, launchedClient);
    }

    public bool PrepareExit(bool busy, bool connected, bool launchedClient)
    {
        lock (gate)
        {
            if (!SafeToPrepare(busy, connected, launchedClient)) return false;
            if (handoffPrepared) return true;
            try
            {
                backend.PrepareExit(requestedVersion!);
                handoffPrepared = true;
                return true;
            }
            catch (Exception e)
            {
                ClearRequest();
                Status = "Couldn't start the update: " + e.Message;
            }
        }
        Changed?.Invoke();
        return false;
    }

    void RequireAvailable()
    {
        if (!Available)
            throw new InvalidOperationException(preview ? "Updates are disabled in previews." : "Use the GitHub installer or portable package to enable updates.");
    }
}
