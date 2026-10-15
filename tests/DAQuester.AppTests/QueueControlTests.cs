using System.Net.Sockets;
using System.Reflection;
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

public class QueueControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queue_start_uses_existing_owner_and_never_toggles_a_running_queue(bool running)
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara"];
        settings.Save();
        var runtime = new DesktopRuntime(preview: true);
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 7, _ => throw new InvalidOperationException("Synthetic queue tests never connect."));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker,
            new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5 });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", flags)!.GetValue(runtime)!;
        var owners = (HashSet<string>)typeof(DesktopRuntime).GetField("previewQueueRunning", flags)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        if (running) owners.Add("Elara");
        try
        {
            var control = typeof(DesktopRuntime).GetMethod("ControlAsync", flags)!;
            Task<object?> Invoke() => (Task<object?>)control.Invoke(runtime, [new ControlRequest("queue-start", "Elara")])!;
            if (running)
            {
                Assert.NotNull(await Invoke());
                Assert.True(runtime.QueueRunning("Elara"));
            }
            else
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke());
                Assert.Equal("Preview does not run game queues.", error.Message);
            }
        }
        finally
        {
            sessions.Remove("Elara");
            owners.Clear();
            await runtime.ShutdownAsync();
        }
    }
}
