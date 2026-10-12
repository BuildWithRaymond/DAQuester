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
    bool installRequested;
    public async Task StartAsync()
    {
        if (Available && AutomaticChecks)
            await CheckAsync();
    }

    public async Task CheckAsync()
    {
        RequireAvailable();
        if (Working)
            return;
        Working = true;
        Status = "Checking for updates…";
        Changed?.Invoke();
        try
        {
            AvailableVersion = await backend.CheckAsync();
            Status = PendingVersion is { } pending ? $"Version {pending} is downloaded and ready." : AvailableVersion is { } version ? $"Version {version} is available. Download when you're ready." : "You have the latest stable release.";
        }
        catch (Exception e)
        {
            AvailableVersion = null;
            Status = "Couldn't check for updates: " + e.Message;
        }
        finally
        {
            Working = false;
            Changed?.Invoke();
        }
    }

    public async Task DownloadAsync()
    {
        RequireAvailable();
        if (Working)
            return;
        if (AvailableVersion is null)
            throw new InvalidOperationException("Check for an update first.");
        Working = true;
        Status = "Downloading update…";
        Changed?.Invoke();
        try
        {
            await backend.DownloadAsync();
            Status = $"Version {PendingVersion} is ready. Finish quests and close game clients before installing.";
        }
        catch (Exception e)
        {
            Status = "Couldn't download the update: " + e.Message;
        }
        finally
        {
            Working = false;
            Changed?.Invoke();
        }
    }

    public void RequestInstall(bool busy, bool connected, bool launchedClient)
    {
        RequireAvailable();
        if (Working || busy || connected || launchedClient)
            throw new InvalidOperationException("Finish quests and close DAQuester-launched Dark Ages clients before installing. Your progress is saved.");
        if (PendingVersion is null)
            throw new InvalidOperationException("Download an update first.");
        installRequested = true;
    }

    public bool PrepareExit(bool busy, bool connected, bool launchedClient)
    {
        if (!Available || !installRequested || Working || busy || connected || launchedClient || PendingVersion is null)
            return false;
        try
        {
            backend.PrepareExit();
            return true;
        }
        catch (Exception e)
        {
            Status = "Couldn't start the update: " + e.Message;
            Changed?.Invoke();
            return false;
        }
    }

    void RequireAvailable()
    {
        if (!Available)
            throw new InvalidOperationException(preview ? "Updates are disabled in previews." : "Use the GitHub installer or portable package to enable updates.");
    }
}
