using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using Xunit;

namespace DAQuester.AppTests;

public class AislingExchangeSettingsTests
{
    [Fact]
    public void New_profiles_explicitly_keep_optional_supplies_off()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new AppSettings(), AppSettings.Json));
        Assert.True(json.RootElement.TryGetProperty("AislingExchangeSupplies", out var enabled));
        Assert.False(enabled.GetBoolean());
    }

    [Fact]
    public async Task Saving_supply_opt_in_does_not_enable_scheduling_or_updates()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var runtime = new DesktopRuntime(preview: true);
        try
        {
            var vm = new MainWindowViewModel(runtime);
            var option = typeof(MainWindowViewModel).GetProperty("AislingExchangeSupplies");
            Assert.NotNull(option);
            Assert.False((bool)option.GetValue(vm)!);
            option.SetValue(vm, true);
            await vm.SaveSettingsCommand.ExecuteAsync();
            Assert.Empty(vm.ErrorMessage);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.DataPath, "profiles.json")));
            Assert.True(json.RootElement.GetProperty("AislingExchangeSupplies").GetBoolean());
            Assert.False(runtime.SchedulerEnabled);
            Assert.False(runtime.Settings.AutomaticUpdateChecks);
            option.SetValue(vm, false);
            await vm.SaveSettingsCommand.ExecuteAsync();
            using var disabled = JsonDocument.Parse(JsonSerializer.Serialize(AppSettings.Load(), AppSettings.Json));
            Assert.False(disabled.RootElement.GetProperty("AislingExchangeSupplies").GetBoolean());
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
