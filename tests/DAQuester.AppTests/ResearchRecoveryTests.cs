using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public class ResearchRecoveryTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData("complete", true)]
    [InlineData("new-generation", false)]
    [InlineData("client", false)]
    [InlineData("profile", false)]
    [InlineData("actor", false)]
    [InlineData("missing-status", false)]
    public async Task Prepare_recovers_only_original_server_receipts_from_existing_automatic_archive(string scenario, bool expected)
    {
        var runtime = DashboardTests.Workspace();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence"))) root = root.Parent;
        using var proof = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root!.FullName,
            "content/evidence/datester-7.41/wizardry-research-status-result.json")));
        var captured = proof.RootElement;
        var source = captured.GetProperty("Source").Deserialize<Dialog>()!;
        var pending = new Transaction("research-submit", source.Generation, source.Revision - 1, 9500, 0, 0,
            captured.GetProperty("SubmittedAt").GetDateTimeOffset())
        {
            EntityId = source.EntityId, EntityType = source.EntityType, InventoryBaselineKnown = true,
            InventoryCounts = new() { ["Parchment Scroll"] = 1 }
        };
        var job = new QuestJob { Character = "Elara", Quest = WizardyResearchEvidence.Id, ResearchOptions = new(),
            Research = new() { Stage = 4, Source = source }, Pending = pending };
        var after = captured.GetProperty("After").Deserialize<CharacterSnapshot>()! with { Name = "Elara" };
        after = scenario switch
        {
            "new-generation" => after with { Generation = 2 },
            "actor" => after with { Name = "Someoneelse" },
            "missing-status" => after with { ActiveStatus = null },
            _ => after
        };
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 2,
            _ => throw new InvalidOperationException("Synthetic recovery never connects."));
        // Current balances resemble the reward but cannot prove an old submission.
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker,
            new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 2, Revision = 5,
                Map = 401, Position = new(7, 9), Class = "Wizard", Level = 99, Hp = 100, Gold = 9500,
                InventoryKnown = true, Inventory = after.Inventory });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", Private)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
        await journal.SaveAsync(job);
        string directory = Path.Combine(AppSettings.DataPath, "replays");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "automatic.json"), JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = scenario == "profile" ? "other-server" : "datester-7.41", Sanitized = true,
            Events = new[] { new { Character = after.Name, after.Generation, after.Revision,
                Direction = scenario == "client" ? "C" : "S", Snapshot = after } }, Actions = Array.Empty<RecordedJourneyAction>()
        }));
        try
        {
            var prepare = typeof(DesktopRuntime).GetMethod("PrepareRunnerCoreAsync", Private)!;
            var runner = await (Task<QuestRunner>)prepare.Invoke(runtime,
                ["Elara", runtime.Recipes[WizardyResearchEvidence.Id], null, null, false, false, null])!;
            Assert.Equal(expected, runner.Job.Pending is null);
            Assert.Equal(expected ? 3 : 0, runner.Job.Rewards.GetValueOrDefault("item:Piet Song"));
            Assert.Equal(expected ? 1 : 0, runner.Job.Research!.Attempts.Length);
            Assert.Equal(expected, journal.Load("Elara", WizardyResearchEvidence.Id).Pending is null);
            Assert.False(runner.IsRunning);
            Assert.False(connection.Connected);
            Assert.Null(runner.Job.CompletedAt);
            Assert.Null(runner.Job.NextRun);
        }
        finally
        {
            sessions.Remove("Elara");
            await runtime.ShutdownAsync();
        }
    }
}
