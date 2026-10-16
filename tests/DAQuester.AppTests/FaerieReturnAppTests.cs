using System.Collections.Immutable;
using System.Net.Sockets;
using System.Reflection;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public class FaerieReturnAppTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Safe_saved_return_resumes_without_a_walking_warning(bool petOwned)
    {
        var runtime = DashboardTests.Workspace();
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 7,
            _ => throw new InvalidOperationException("Synthetic return never connects."));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker,
            new CharacterSnapshot
            {
                Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5,
                Level = 20, Hp = 100, Class = "Warrior", Map = 542, Position = new(17, 11),
                InventoryKnown = true, SpellsKnown = true,
                Inventory = petOwned ? ImmutableDictionary<byte, Item>.Empty.Add(8, new(8, "Pet Faerie", 1)) : []
            });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", flags)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        runtime.ChooseQuestAsync = (_, _, _, _) => throw new InvalidOperationException("No walking warning after safe return.");
        try
        {
            await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(new()
            {
                Character = "Elara", Quest = ShinewoodFaerieEvidence.Id, Step = "return"
            });
            var configure = typeof(DesktopRuntime).GetMethod("ConfigureQuestAsync", flags)!;
            Assert.True(await (Task<bool>)configure.Invoke(runtime, ["Elara", runtime.Recipes[ShinewoodFaerieEvidence.Id], true, false])!);
            var prepare = typeof(DesktopRuntime).GetMethod("PrepareRunnerCoreAsync", flags)!;
            var runner = await (Task<QuestRunner>)prepare.Invoke(runtime,
                ["Elara", runtime.Recipes[ShinewoodFaerieEvidence.Id], null, null, false, false, null])!;
            Assert.Equal("return", runner.Job.Step);
        }
        finally
        {
            sessions.Remove("Elara");
            await runtime.ShutdownAsync();
        }
    }
}
