using System.Collections.Immutable;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public class ResearchHealingReplayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData("complete", true)]
    [InlineData("generation", false)]
    [InlineData("no-debit", false)]
    [InlineData("not-full", false)]
    [InlineData("unknown-max", false)]
    public async Task Original_archive_can_settle_a_healing_dose_after_research_is_already_reconciled(string gap, bool expected)
    {
        var runtime = DashboardTests.Workspace();
        var job = new QuestJob { Character = "Elara", Quest = WizardyResearchEvidence.Id, ResearchOptions = new(),
            Research = new() { Healing = new(1, 10, 30, 1742, 2), HealthCheckRequired = true } };
        var after = new CharacterSnapshot { Name = "Elara", Connected = true, Generation = 1, Revision = 12, Map = 401,
            Hp = 3950, MaxHp = 3950, InventoryKnown = true,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(2, new(2, "Exkuranum", 29)) };
        after = gap switch
        {
            "generation" => after with { Generation = 2 },
            "no-debit" => after with { Inventory = after.Inventory.SetItem(2, new(2, "Exkuranum", 30)) },
            "not-full" => after with { Hp = 1742 },
            "unknown-max" => after with { MaxHp = null },
            _ => after
        };
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 2,
            _ => throw new InvalidOperationException("Synthetic recovery never connects."));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker,
            new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 2, Revision = 5,
                Map = 401, Position = new(7, 9), Class = "Wizard", Hp = 3950, MaxHp = 3950, InventoryKnown = true,
                Inventory = after.Inventory });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", Private)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(job);
        string directory = Path.Combine(AppSettings.DataPath, "replays");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "healing.json"), JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = "datester-7.41", Sanitized = true,
            Events = new[] { new { Character = after.Name, after.Generation, after.Revision, Direction = "S", Snapshot = after } },
            Actions = Array.Empty<RecordedJourneyAction>()
        }));
        try
        {
            var prepare = typeof(DesktopRuntime).GetMethod("PrepareRunnerCoreAsync", Private)!;
            var runner = await (Task<QuestRunner>)prepare.Invoke(runtime,
                ["Elara", runtime.Recipes[WizardyResearchEvidence.Id], null, null, false, false, null])!;

            Assert.Equal(!expected, runner.Job.Research!.Healing is not null);
            Assert.True(runner.Job.Research.HealthCheckRequired);
            Assert.False(runner.IsRunning);
            Assert.False(connection.Connected);
            Assert.Null(runner.Job.Pending);
        }
        finally
        {
            sessions.Remove("Elara");
            await runtime.ShutdownAsync();
        }
    }
}
