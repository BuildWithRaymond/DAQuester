using System.Collections.Immutable;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Control;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public class ControlPurchaseTests
{
    [Fact]
    public async Task Unequip_is_admitted_but_unknown_inventory_blocks_before_submission()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara"];
        settings.ClientPath = "";
        settings.Save();
        var runtime = new DesktopRuntime(preview: true);
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 7, _ => throw new InvalidOperationException("Synthetic equipment tests never connect."));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, new CharacterSnapshot
        {
            Name = "Elara",
            Id = 1,
            Connected = true,
            Generation = 7,
            Revision = 5,
            InventoryKnown = false,
            Equipment = ImmutableDictionary<byte, string>.Empty.Add(1, "Oak Stick")
        });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", privateInstance)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        try
        {
            var request = new ControlRequest("command", "Elara", JsonSerializer.SerializeToElement(new GameCommand("unequip", Slot: 1, Text: "Oak Stick")));
            var control = typeof(DesktopRuntime).GetMethod("ControlAsync", privateInstance)!;
            var operation = (Task<object?>)control.Invoke(runtime, [request])!;
            var error = await Assert.ThrowsAsync<QuestBlockedException>(() => operation);
            Assert.Contains("known inventory", error.Message);
            Assert.Empty(runtime.ControlActions);
        }
        finally
        {
            sessions.Remove("Elara");
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Read_dialog_returns_own_transcript_and_resources_without_snapshot_entities()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara"];
        settings.ClientPath = "";
        settings.Save();
        var runtime = new DesktopRuntime(preview: true);
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 7, _ => throw new InvalidOperationException("Synthetic dialogue tests never connect."));
        var observed = new Dialog(7, 5, 77, 1, "Synthetic NPC", "Permanent terms", true, 1, 1, 2, [new("Choose an outcome", 1, 1)]);
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, new CharacterSnapshot
        {
            Name = "Elara",
            Id = 1,
            Connected = true,
            Generation = 7,
            Revision = 5,
            InventoryKnown = true,
            Gold = 1234,
            Dialog = observed
        });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", privateInstance)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        try
        {
            var request = new ControlRequest("command", "Elara", JsonSerializer.SerializeToElement(new GameCommand("read-dialog")));
            var control = typeof(DesktopRuntime).GetMethod("ControlAsync", privateInstance)!;
            var operation = (Task<object?>)control.Invoke(runtime, [request])!;
            using var result = JsonDocument.Parse(JsonSerializer.Serialize(await operation));
            var report = result.RootElement.GetProperty("discovery");
            Assert.Equal("choice", report.GetProperty("StopReason").GetString());
            Assert.Equal("Permanent terms", report.GetProperty("Pages")[0].GetProperty("Text").GetString());
            Assert.Equal(1234, report.GetProperty("Before").GetProperty("Gold").GetInt32());
            Assert.False(report.GetProperty("After").TryGetProperty("Entities", out _));
            Assert.False(result.RootElement.TryGetProperty("snapshot", out _));
            Assert.Empty(runtime.ControlActions);
            Assert.Same(observed, session.Snapshot.Dialog);
        }
        finally
        {
            sessions.Remove("Elara");
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Buy_is_admitted_but_caller_cannot_replace_the_observed_shop_price()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara"];
        settings.ClientPath = "";
        settings.Save();
        var runtime = new DesktopRuntime(preview: true);
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 7, _ => throw new InvalidOperationException("Synthetic purchase tests never connect."));
        var observed = new Dialog(7, 5, 77, 1, "Synthetic shop", "Synthetic stock", false, 74, 0, 4, [])
        {
            Items = [new("Wine", 500)]
        };
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, new CharacterSnapshot
        {
            Name = "Elara",
            Id = 1,
            Connected = true,
            Generation = 7,
            Revision = 5,
            InventoryKnown = true,
            Gold = 0,
            Dialog = observed,
            Inventory = ImmutableDictionary<byte, Item>.Empty
        });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", privateInstance)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        try
        {
            var forged = observed with
            {
                EntityId = 999,
                Items = [new("Wine", 0)]
            };
            var request = new ControlRequest("command", "Elara", JsonSerializer.SerializeToElement(new GameCommand("buy", Text: "Wine", Dialog: forged)));
            var control = typeof(DesktopRuntime).GetMethod("ControlAsync", privateInstance)!;
            var operation = (Task<object?>)control.Invoke(runtime, [request])!;
            var error = await Assert.ThrowsAsync<ResourceUnavailableException>(() => operation);
            Assert.Contains("Not enough gold for Wine", error.Message);
            Assert.Empty(runtime.ControlActions);
            Assert.Same(observed, session.Snapshot.Dialog);
        }
        finally
        {
            sessions.Remove("Elara");
            await runtime.ShutdownAsync();
        }
    }
}
