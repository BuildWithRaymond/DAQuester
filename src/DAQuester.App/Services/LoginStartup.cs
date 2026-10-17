using Microsoft.Win32;
using Velopack.Locators;

namespace DAQuester.App.Services;

public interface ILoginStartup
{
    string? LaunchCommand { get; }
    string? ReadCommand();
    void WriteCommand(string? command);
}

internal sealed class PreviewLoginStartup : ILoginStartup
{
    string? command;
    public string? LaunchCommand => "\"DAQuester.exe\" --background";
    public string? ReadCommand() => command;
    public void WriteCommand(string? value) => command = value;
}

internal sealed class WindowsLoginStartup : ILoginStartup
{
    internal static void RemoveOwned(ILoginStartup startup)
    {
        if (startup.LaunchCommand is { } command &&
            string.Equals(startup.ReadCommand(), command, StringComparison.OrdinalIgnoreCase))
            startup.WriteCommand(null);
    }
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "DAQuester";
    public string? LaunchCommand { get; } = ResolveCommand(
        VelopackLocator.IsCurrentSet ? VelopackLocator.Current.RootAppDir : null, File.Exists);

    public string? ReadCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    public void WriteCommand(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new IOException("Windows sign-in startup settings are unavailable.");
        if (command is null) key.DeleteValue(ValueName, throwOnMissingValue: false);
        else key.SetValue(ValueName, command, RegistryValueKind.String);
    }

    internal static string? ResolveCommand(string? packageRoot, Func<string, bool> exists)
    {
        if (string.IsNullOrWhiteSpace(packageRoot) || !Path.IsPathFullyQualified(packageRoot) || packageRoot.Contains('"')) return null;
        string launcher = Path.Combine(packageRoot, "DAQuester.exe");
        return exists(launcher) ? '"' + launcher + "\" --background" : null;
    }
}
