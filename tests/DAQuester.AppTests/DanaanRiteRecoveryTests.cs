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

public class DanaanRiteRecoveryTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData("complete", true)]
    [InlineData("missing-next", false)]
    [InlineData("newer-conflict", false)]
    public async Task Preparing_saved_rite_recovers_original_replay_without_a_pending_page_or_game_commands(string scenario, bool expected)
    {
        var runtime = DashboardTests.Workspace();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence"))) root = root.Parent;
        using var capture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root!.FullName,
            "content/evidence/datester-7.41/danaan-greim-rotten-apple.json")));
        var job = capture.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()! with { Character = "Elara" };
        var states = capture.RootElement.GetProperty("Snapshots").Deserialize<CharacterSnapshot[]>()!
            .Select(s => s with { Name = "Elara" }).ToArray();
        var actions = capture.RootElement.GetProperty("Actions").Deserialize<RecordedJourneyAction[]>()!
            .Select(a => a with { Character = "Elara" }).ToArray();
        using var client = new TcpClient();
        using var remote = new TcpClient();
        await using var connection = new ProxyConnection(client, remote, 2,
            _ => throw new InvalidOperationException("Synthetic replay recovery never connects."));
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker,
            new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 2, Revision = 5,
                Map = 720, Position = new(14, 11), Level = 99, Hp = 10 });
        var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
        var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", Private)!.GetValue(runtime)!;
        sessions.Add("Elara", session);
        var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
        await journal.SaveAsync(job);
        string directory = Path.Combine(AppSettings.DataPath, "replays");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "original.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 3, ServerProfile = "datester-7.41", Sanitized = true,
            Events = states.Select(s => new { Character = s.Name, s.Generation, s.Revision, Direction = "S", Snapshot = s }),
            Actions = scenario == "missing-next" ? actions[..^1] : actions
        }));
        if (scenario == "newer-conflict")
        {
            var changed = states[^1] with { Revision = 14454, Gold = 1 };
            string newer = Path.Combine(directory, "newer.json");
            await File.WriteAllTextAsync(newer, JsonSerializer.Serialize(new
            {
                SchemaVersion = 3, ServerProfile = "datester-7.41", Sanitized = true,
                Events = new[] { new { Character = changed.Name, changed.Generation, changed.Revision, Direction = "S", Snapshot = changed } },
                Actions = Array.Empty<RecordedJourneyAction>()
            }));
            File.SetLastWriteTimeUtc(newer, File.GetLastWriteTimeUtc(path).AddSeconds(1));
        }
        try
        {
            var prepare = typeof(DesktopRuntime).GetMethod("PrepareRunnerCoreAsync", Private)!;
            var runner = await (Task<QuestRunner>)prepare.Invoke(runtime,
                ["Elara", runtime.Recipes[DanaanEvidence.Id], null, null, false, false, null])!;
            Assert.Equal(expected ? 15 : 14, runner.Job.Route?.Task);
            Assert.Equal(expected, runner.Job.RiteRecovery is not null);
            Assert.Equal(runner.Job.Route?.Task, journal.Load("Elara", DanaanEvidence.Id).Route?.Task);
            Assert.False(runner.IsRunning);
            Assert.False(connection.Connected);
            Assert.Null(runner.Job.CompletedAt);
        }
        finally
        {
            sessions.Remove("Elara");
            await runtime.ShutdownAsync();
        }
    }
}
