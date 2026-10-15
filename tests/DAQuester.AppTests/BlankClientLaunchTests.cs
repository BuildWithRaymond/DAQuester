using System.Diagnostics;
using System.Reflection;
using DAQuester.App;
using DAQuester.App.Services;
using Xunit;

namespace DAQuester.AppTests;

public class BlankClientLaunchTests
{
    [Fact]
    public async Task New_blank_client_checks_its_image_instead_of_focusing_the_existing_manual_slot()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.ClientPath = Path.Combine(AppSettings.DataPath, "unsupported-synthetic.exe");
        File.WriteAllText(settings.ClientPath, "Synthetic input, never executed.");
        settings.Save();
        var runtime = new DesktopRuntime();
        typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.Ready))!.SetValue(runtime, true);
        var owned = (Dictionary<string, Process>)typeof(DesktopRuntime).GetField("ownedClients", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        using var process = Process.GetCurrentProcess();
        owned.Add("(manual)", process);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.LaunchBlankClientAsync());
            Assert.Contains("supported 7.41 client", error.Message);
            Assert.False(runtime.QuestActionsRunning);
        }
        finally
        {
            owned.Remove("(manual)");
            await runtime.ShutdownAsync();
        }
    }
}
