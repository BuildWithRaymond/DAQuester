using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using Xunit;

namespace DAQuester.AppTests;

public class BackgroundSettingsTests
{
    sealed class Startup : ILoginStartup
    {
        public string? Command;
        public bool Reject;
        public List<string?> Writes = [];
        public string? LaunchCommand { get; init; } = "\"DAQuester.exe\" --background";
        public string? ReadCommand() => Command;
        public void WriteCommand(string? command)
        {
            if (Reject) throw new IOException("Synthetic startup registration failure");
            Command = command;
            Writes.Add(command);
        }
    }

    static DesktopRuntime Workspace(Startup startup)
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        return new(preview: true, loginStartup: startup);
    }

    [Fact]
    public async Task Development_build_preserves_an_existing_packaged_startup_registration()
    {
        var startup = new Startup { LaunchCommand = null, Command = "existing packaged startup" };
        var runtime = Workspace(startup);
        try
        {
            var vm = new MainWindowViewModel(runtime) { KeepRunningInTray = false };
            Assert.False(vm.CanStartWithWindows);
            await vm.SaveSettingsCommand.ExecuteAsync();
            Assert.Empty(vm.ErrorMessage);
            Assert.False(AppSettings.Load().KeepRunningInTray);
            Assert.Equal("existing packaged startup", startup.Command);
            Assert.Empty(startup.Writes);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public void Uninstall_removes_only_its_own_startup_registration()
    {
        var startup = new Startup();
        startup.Command = startup.LaunchCommand;
        WindowsLoginStartup.RemoveOwned(startup);
        Assert.Null(startup.Command);
        Assert.Single(startup.Writes);
        startup.Command = "another DAQuester installation";
        WindowsLoginStartup.RemoveOwned(startup);
        Assert.Equal("another DAQuester installation", startup.Command);
        Assert.Single(startup.Writes);
    }

    [Fact]
    public async Task Saving_background_settings_registers_startup_without_enabling_schedules()
    {
        var startup = new Startup();
        var runtime = Workspace(startup);
        try
        {
            var vm = new MainWindowViewModel(runtime);
            Assert.True(vm.KeepRunningInTray);
            Assert.False(vm.StartWithWindows);
            vm.KeepRunningInTray = false;
            vm.StartWithWindows = true;
            await vm.SaveSettingsCommand.ExecuteAsync();
            Assert.Equal(startup.LaunchCommand, startup.Command);
            Assert.False(AppSettings.Load().KeepRunningInTray);
            Assert.False(runtime.Settings.KeepRunningInTray);
            Assert.False(runtime.SchedulerEnabled);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Failed_startup_registration_preserves_saved_settings()
    {
        var startup = new Startup { Reject = true };
        var runtime = Workspace(startup);
        try
        {
            var vm = new MainWindowViewModel(runtime) { KeepRunningInTray = false, StartWithWindows = true };
            await vm.SaveSettingsCommand.ExecuteAsync();
            Assert.Contains("Synthetic startup registration failure", vm.ErrorMessage);
            Assert.True(AppSettings.Load().KeepRunningInTray);
            Assert.True(runtime.Settings.KeepRunningInTray);
            Assert.Null(startup.Command);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Failed_settings_write_restores_the_exact_startup_registration()
    {
        var startup = new Startup { Command = "original startup command" };
        var runtime = Workspace(startup);
        string profile = Path.Combine(AppSettings.DataPath, "profiles.json");
        try
        {
            var vm = new MainWindowViewModel(runtime) { KeepRunningInTray = false, StartWithWindows = false };
            File.SetAttributes(profile, FileAttributes.ReadOnly);
            await vm.SaveSettingsCommand.ExecuteAsync();
            Assert.NotEmpty(vm.ErrorMessage);
            Assert.Equal("original startup command", startup.Command);
            Assert.Equal(new string?[] { null, "original startup command" }, startup.Writes);
            Assert.True(runtime.Settings.KeepRunningInTray);
        }
        finally { File.SetAttributes(profile, FileAttributes.Normal); await runtime.ShutdownAsync(); }
    }
}
