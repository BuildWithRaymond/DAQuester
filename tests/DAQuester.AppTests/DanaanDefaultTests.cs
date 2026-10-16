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

public class DanaanDefaultTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Desktop_and_scheduler_default_without_a_popup_and_preserve_saved_choices(bool scheduled, bool savedOtherRoute)
    {
        var runtime = DashboardTests.Workspace();
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 7,
            _ => throw new InvalidOperationException("Synthetic defaults never connect."));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker,
            new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5 });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", Private)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        runtime.ChooseQuestAsync = (_, _, _, _) => throw new InvalidOperationException("Danaan must not ask questions.");
        try
        {
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            if (savedOtherRoute)
                await journal.SaveAsync(new()
                {
                    Character = "Elara", Quest = DanaanEvidence.Id, Step = "run",
                    DanaanOptions = new("other-route", false), Route = new() { Task = 1 }
                });
            if (!scheduled)
            {
                var configure = typeof(DesktopRuntime).GetMethod("ConfigureQuestAsync", Private)!;
                Assert.True(await (Task<bool>)configure.Invoke(runtime, ["Elara", runtime.Recipes[DanaanEvidence.Id], false, false])!);
            }
            var prepare = typeof(DesktopRuntime).GetMethod("PrepareRunnerCoreAsync", Private)!;
            if (savedOtherRoute)
            {
                var attempt = (Task<QuestRunner>)prepare.Invoke(runtime,
                    ["Elara", runtime.Recipes[DanaanEvidence.Id], null, null, false, scheduled, null])!;
                await Assert.ThrowsAsync<QuestBlockedException>(() => attempt);
                Assert.Equal(new DanaanOptions("other-route", false), journal.Load("Elara", DanaanEvidence.Id).DanaanOptions);
                return;
            }
            var runner = await (Task<QuestRunner>)prepare.Invoke(runtime,
                ["Elara", runtime.Recipes[DanaanEvidence.Id], null, null, false, scheduled, null])!;
            Assert.Equal(new DanaanOptions("cail", true), runner.Job.DanaanOptions);
            var saved = journal.Load("Elara", DanaanEvidence.Id);
            Assert.Equal(runner.Job.DanaanOptions, saved.DanaanOptions);
            Assert.Null(saved.Pending);
        }
        finally
        {
            sessions.Remove("Elara");
            await runtime.ShutdownAsync();
        }
    }
}
