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
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public class StickReplayRecoveryTests
{
    [Theory]
    [InlineData(false, "valid", true)]
    [InlineData(true, "valid", true)]
    [InlineData(false, "unknown-reply", false)]
    [InlineData(false, "other-generation", false)]
    [InlineData(false, "other-character", false)]
    [InlineData(false, "other-server", false)]
    [InlineData(false, "missing-rewards", false)]
    [InlineData(false, "stale-reply", false)]
    [InlineData(false, "other-entity", false)]
    [InlineData(false, "unsanitized", false)]
    [InlineData(false, "invalid-json", false)]
    public async Task Preparing_stick_recovers_only_bound_terminal_and_rewards_from_saved_replay(bool existingRunner, string scenario, bool recover)
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara"];
        settings.ClientPath = "";
        settings.Save();
        var runtime = new DesktopRuntime();
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 2, _ => throw new InvalidOperationException("Synthetic recovery never connects."));
        typeof(DAQuester.DarkAges.Protocol.StateTracker).GetProperty("Snapshot")!.SetValue(connection.Tracker,
            new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 2, Revision = 5, Level = 11, Hp = 10 });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", privateInstance)!.GetValue(runtime)!;
        var runners = (Dictionary<string, QuestRunner>)typeof(DesktopRuntime).GetField("runners", privateInstance)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
        var observed = DateTimeOffset.UtcNow.AddHours(-9);
        await journal.SaveAsync(new()
        {
            Character = "Elara",
            Step = "confirm",
            Accepted = true,
            AcceptedGeneration = 1,
            Pending = new("turn-in", 1, 10, 50, 6, 1000, observed.AddSeconds(-1)) { EntityId = 99 }
        });
        if (existingRunner)
            runners["Elara"] = new(session, journal, runtime.Recipes["spare-a-stick"], runtime.Calibration);
        long replayGeneration = scenario == "other-generation" ? 2 : 1;
        string replayCharacter = scenario == "other-character" ? "Wren" : "Elara";
        var reward = new CharacterSnapshot
        {
            Name = replayCharacter,
            Generation = replayGeneration,
            Revision = 12,
            Connected = true,
            InventoryKnown = true,
            Inventory = scenario == "missing-rewards" ? ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Branch", 6)) : ImmutableDictionary<byte, Item>.Empty,
            Experience = scenario == "missing-rewards" ? 1000u : 4500u,
            Gold = 50,
            Dialog = new(replayGeneration, scenario == "stale-reply" ? 10 : 11, scenario == "other-entity" ? 100u : 99u, 1, "Callough", scenario == "unknown-reply" ? "Unrecognized reply" : "Ah thank ya, you've done well.", true, 177, 73, 0, []) { ObservedAt = observed }
        };
        var replayDirectory = Path.Combine(AppSettings.DataPath, "replays");
        Directory.CreateDirectory(replayDirectory);
        File.WriteAllText(Path.Combine(replayDirectory, "capture.json"), JsonSerializer.Serialize(new
        {
            SchemaVersion = 2,
            ServerProfile = scenario == "other-server" ? "another-server" : "datester-7.41",
            Sanitized = scenario != "unsanitized",
            Events = new[] { new ReplayEvent(observed, replayCharacter, replayGeneration, reward.Revision, 0x30, "", reward) }
        }));
        if (scenario == "invalid-json")
            File.WriteAllText(Path.Combine(replayDirectory, "capture.json"), "{ interrupted export");
        try
        {
            var prepare = typeof(DesktopRuntime).GetMethod("PrepareRunnerAsync", privateInstance)!;
            var runner = await (Task<QuestRunner>)prepare.Invoke(runtime, ["Elara", runtime.Recipes["spare-a-stick"], null, null, false])!;

            Assert.Equal(recover, runner.Job.Pending is null);
            Assert.Equal(recover ? "complete" : "confirm", runner.Job.Step);
            Assert.False(runner.IsRunning);
            if (recover)
            {
                Assert.Equal(observed, runner.Job.CompletedAt);
                Assert.Equal(observed.AddHours(8), runner.Job.NextRun);
                Assert.Equal(3500, runner.Job.Rewards["experience"]);
                Assert.Equal(6, runner.Job.Rewards["branchesConsumed"]);
                Assert.Null(journal.Load("Elara", "spare-a-stick").Pending);
            }
            Assert.False(connection.Connected);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }
}
