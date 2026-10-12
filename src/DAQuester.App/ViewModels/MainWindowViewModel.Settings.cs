using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public string ClientPath
    {
        get => clientPath;
        set => Set(ref clientPath, value);
    }
    public bool AutomaticUpdateChecks
    {
        get => automaticUpdateChecks;
        set => Set(ref automaticUpdateChecks, value);
    }
    public string VersionText => "DAQuester " + UpdateVersion.Current;
    public string UpdateStatus => Runtime.Updates.Status;
    public string? AvailableUpdate => Runtime.Updates.PendingVersion ?? Runtime.Updates.AvailableVersion;
    public bool CanCheckUpdates => Runtime.Updates.Available && !Runtime.Updates.Working;
    public bool CanDownloadUpdate => CanCheckUpdates && Runtime.Updates.AvailableVersion is not null && Runtime.Updates.PendingVersion is null;
    public bool CanInstallUpdate => CanCheckUpdates && Runtime.Updates.PendingVersion is not null && !Runtime.QuestActionsRunning && !Runtime.ConnectedGame && !Runtime.LaunchedClientRunning;

    void SaveSettings()
    {
        var settings = Runtime.Settings;
        string oldPath = settings.ClientPath;
        bool oldChecks = settings.AutomaticUpdateChecks;
        settings.ClientPath = ClientPath.Trim();
        settings.AutomaticUpdateChecks = AutomaticUpdateChecks;
        try
        {
            var persisted = AppSettings.Load();
            persisted.ClientPath = settings.ClientPath;
            persisted.AutomaticUpdateChecks = AutomaticUpdateChecks;
            persisted.Save();
            Runtime.Updates.AutomaticChecks = AutomaticUpdateChecks;
            NoticeMessage = oldPath != settings.ClientPath ? "Settings saved. Restart DAQuester to apply the game client path." : "Settings saved.";
        }
        catch
        {
            settings.ClientPath = oldPath;
            settings.AutomaticUpdateChecks = oldChecks;
            throw;
        }
    }
}
