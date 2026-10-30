using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public string FastModeCharacterLabel => CharacterName.Length > 0 ? "Movement for " + CharacterName : "Movement";
    public bool CanSetFastMode => CharacterName.Length > 0;
    public bool FastMode
    {
        get => Runtime.Settings.FastModeEnabled(CharacterName);
        set
        {
            if (!refreshing && CharacterName.Length > 0 && value != FastMode)
                try
                {
                    Runtime.SetFastMode(CharacterName, value);
                }
                catch (Exception e)
                {
                    ReportError(e);
                }
        }
    }

    public string FastModeLabel => "Fast mode · " + (FastMode ? Runtime.FastModeDescription(CharacterName) : "normal walking");

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
    public bool KeepRunningInTray
    {
        get => keepRunningInTray;
        set => Set(ref keepRunningInTray, value);
    }
    public bool StartWithWindows
    {
        get => startWithWindows;
        set => Set(ref startWithWindows, value);
    }
    public bool CanStartWithWindows => Runtime.LoginStartup.LaunchCommand is not null;
    public string StartupDescription => CanStartWithWindows
        ? "Open DAQuester when you sign in to Windows. Starts in the tray when background mode is on. Keep a portable folder in the same location."
        : "Start with Windows is available in the installed or portable build.";
    public string UpdateStatus => Runtime.Updates.Status;
    public string? AvailableUpdate => Runtime.Updates.PendingVersion ?? Runtime.Updates.AvailableVersion;
    public bool CanCheckUpdates => Runtime.Updates.Available && !Runtime.Updates.Working && !Runtime.Updates.InstallRequested;
    public bool CanUpdateAndRestart => CanCheckUpdates;

    void SaveSettings()
    {
        var settings = Runtime.Settings;
        string oldPath = settings.ClientPath;
        bool oldChecks = settings.AutomaticUpdateChecks;
        bool oldTray = settings.KeepRunningInTray;
        bool oldSupplies = settings.AislingExchangeSupplies;
        string? oldStartup = Runtime.LoginStartup.ReadCommand();
        string? desiredStartup = !CanStartWithWindows ? oldStartup
            : StartWithWindows ? Runtime.LoginStartup.LaunchCommand : null;
        bool startupChanged = false;
        settings.ClientPath = ClientPath.Trim();
        settings.AutomaticUpdateChecks = AutomaticUpdateChecks;
        settings.KeepRunningInTray = KeepRunningInTray;
        settings.AislingExchangeSupplies = AislingExchangeSupplies;
        try
        {
            var persisted = AppSettings.Load();
            persisted.ClientPath = settings.ClientPath;
            persisted.AutomaticUpdateChecks = AutomaticUpdateChecks;
            persisted.KeepRunningInTray = KeepRunningInTray;
            persisted.AislingExchangeSupplies = AislingExchangeSupplies;
            if (oldStartup != desiredStartup)
            {
                Runtime.LoginStartup.WriteCommand(desiredStartup);
                startupChanged = true;
            }
            persisted.Save();
            Runtime.Updates.AutomaticChecks = AutomaticUpdateChecks;
            Runtime.Refresh();
            NoticeMessage = oldPath != settings.ClientPath ? "Settings saved. Restart DAQuester to apply the game client path." : "Settings saved.";
        }
        catch
        {
            settings.ClientPath = oldPath;
            settings.AutomaticUpdateChecks = oldChecks;
            settings.KeepRunningInTray = oldTray;
            settings.AislingExchangeSupplies = oldSupplies;
            if (startupChanged)
                Runtime.LoginStartup.WriteCommand(oldStartup);
            throw;
        }
    }
}
