using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Xunit;

namespace DAQuester.Tests;

// Synthetic execution stitched from independently retained MCP snapshots. Actor,
// connection/revisions and timer clock are normalized; no live game is operated.
public class FiskCapturedJourneyTests
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    static string Repository()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "AGENTS.md")))
                return d.FullName;
        throw new InvalidOperationException("Repository not found.");
    }
    [Fact]
    public async Task Complete_secrecy_journey_matches_actual_dialogue_and_resource_snapshots()
    {
        string root = Repository();
        var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root, "content/recipes/fisk-secret.v1.json")), Json)!;
        using var capture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/evidence/datester-7.41/fisk-danaan-research.json")));
        var records = capture.RootElement.GetProperty("Observations").EnumerateArray().ToDictionary(x => x.GetProperty("Sequence").GetInt32(), x => x.Clone());
        using var packets = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/evidence/datester-7.41/fisk-secret-live.json")));
        var ranges = new Dictionary<string, (int First, int Last)>
        {
            ["fisk-promise"] = (6, 13),
            ["thulin-request"] = (18, 22),
            ["water-lilies"] = (65, 69),
            ["fisk-lilies"] = (71, 77),
            ["eeva-request"] = (81, 85),
            ["petunias"] = (109, 113),
            ["fisk-petunias"] = (115, 121),
            ["pink-rose"] = (123, 129),
            ["flower-bundle"] = (131, 141),
            ["bouquet-acceptance"] = (143, 146),
            ["bouquet-collection"] = (164, 167),
            ["fisk-letter"] = (169, 172),
            ["bertil-delivery"] = (174, 180),
            ["fisk-reward"] = (182, 188)
        };
        JsonElement Snapshot(int number)
        {
            var result = records[number].GetProperty("Result");
            return result.TryGetProperty("snapshot", out var own) ? own : result.GetProperty("characters")[0].GetProperty("snapshot");
        }
        var s = new SimulatedSession();
        ((HashSet<string>)s.Capabilities).UnionWith(recipe.Capabilities);
        s.Set(s.Snapshot with
        {
            Name = "ReplayA",
            Level = 99,
            Gold = 1000000,
            Experience = 319308866,
            MaxHp = 4108,
            LegendsKnown = true,
            LegendsRevision = 1,
            MaxWeight = 127
        });
        void Apply(int number, bool legendOnly = false)
        {
            if (number == 183)
            {
                var tracker = new StateTracker(1);
                tracker.Identity("ReplayA", 42);
                // This fixture retains quest packets only; map/identity are
                // normalized from its independent own-state capture.
                var builder = new NetworkPacketBuilder(ServerCommand.MapSize);
                new ServerMapSizeMessage { MapId = (ushort)Snapshot(number).GetProperty("map").GetInt32(), Width = 20, Height = 20 }.Serialize(ref builder);
                tracker.Observe((byte)ServerCommand.MapSize, builder.ToPacket().Data);
                builder.Dispose();
                foreach (var packet in packets.RootElement.GetProperty("Events").EnumerateArray().Where(e => e.GetProperty("Opcode").GetInt32() == 10 && e.GetProperty("Revision").GetInt64() is 2346 or 2347))
                {
                    tracker.Observe(10, Convert.FromBase64String(packet.GetProperty("Payload").GetString()!));
                    var reward = tracker.Snapshot.Notice! with
                    {
                        Revision = s.Snapshot.Revision + 1
                    };
                    s.Set(s.Snapshot with
                    {
                        Notice = reward
                    });
                }
            }
            var observed = Snapshot(number).Deserialize<CharacterSnapshot>(Json)!;
            long rev = s.Snapshot.Revision + 1;
            var d = observed.Dialog;
            if (d is not null)
                d = d with
                {
                    Generation = 1,
                    Revision = rev,
                    Name = d.Name == "Fast" ? "ReplayA" : d.Name,
                    ObservedAt = d.Text == FiskEvidence.BouquetAccepted ? DateTimeOffset.UtcNow.AddHours(-2) : DateTimeOffset.UtcNow
                };
            var notice = observed.Notice is { } n ? n with
            {
                Generation = 1,
                Revision = rev
            } : null;
            if (legendOnly)
            {
                s.Set(s.Snapshot with
                {
                    Legends = observed.Legends,
                    LegendsKnown = true,
                    LegendsRevision = rev
                });
                return;
            }
            s.Set(observed with
            {
                Name = "ReplayA",
                Id = 42,
                Generation = 1,
                MaxHp = 4108,
                Gold = observed.Gold == 0 ? s.Snapshot.Gold : observed.Gold,
                Dialog = d,
                Notice = notice,
                LegendsRevision = rev
            });
        }
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        QuestRunner? runner = null;
        string? active = null;
        Queue<int> frames = new();
        s.OnCommand = command =>
        {
            string task = recipe.Journey[runner!.Job.Route!.Task].Id;
            if (active != task)
            {
                active = task;
                frames = ranges.TryGetValue(task, out var range) ? new(Enumerable.Range(range.First, range.Last - range.First + 1)) : new();
            }
            if (command.Kind == "navigate")
            {
                s.Set(s.Snapshot with
                {
                    Map = command.Map,
                    Position = command.Target
                });
                return;
            }
            if (command.Kind == "inspect-self")
            {
                Apply(189, legendOnly: true);
                return;
            }
            if (task is "wine" or "grapes")
            {
                if (command.Kind == "interact")
                {
                    var d = new Dialog(1, s.Snapshot.Revision + 1, 55, 1, task == "wine" ? "Viveka" : "Alvar", "Hello.  What can I do for you?", false, 0, 0, 0, [new("Buy", 64, 1)]);
                    s.Set(s.Snapshot with
                    {
                        Dialog = d
                    });
                    return;
                }
                if (command.Kind == "choose")
                {
                    var d = s.Snapshot.Dialog! with
                    {
                        Revision = s.Snapshot.Revision + 1,
                        Type = 4,
                        Choices = [],
                        Items = [new(task == "wine" ? "Wine" : "Grapes", task == "wine" ? 500u : 10u)]
                    };
                    s.Set(s.Snapshot with
                    {
                        Dialog = d
                    });
                    return;
                }
                if (command.Kind == "buy")
                {
                    Apply(task == "wine" ? 62 : 106);
                    return;
                }
                if (command.Kind == "close")
                {
                    s.Set(s.Snapshot with
                    {
                        Dialog = null
                    });
                    return;
                }
            }
            if (command.Kind is "interact" or "say")
            {
                while (frames.Count > 0 && Snapshot(frames.Peek()).GetProperty("dialog").ValueKind == JsonValueKind.Null)
                    frames.Dequeue();
                Apply(frames.Dequeue());
                return;
            }
            if (command.Kind == "close" && frames.Count == 0)
            {
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
                return;
            }
            while (frames.Count > 0)
            {
                int index = frames.Dequeue();
                var args = records[index].GetProperty("Arguments");
                if (!args.TryGetProperty("kind", out var kind))
                    continue;
                Assert.Equal(kind.GetString(), command.Kind);
                if (command.Kind == "choose")
                    Assert.Equal(args.GetProperty("text").GetString(), command.Text);
                Apply(index);
                return;
            }
            throw new InvalidOperationException("Unexpected synthetic action " + command.Kind + " at " + task);
        };
        runner = new(s, journal, recipe, new());
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await runner.RunAsync(cancellation: cancel.Token);
        Assert.True(runner.Job.Status == "Completed", runner.Job.Reason);
        Assert.Equal(967400u, s.Snapshot.Gold);
        Assert.Equal(320058866u, s.Snapshot.Experience);
        Assert.Equal(0, s.Snapshot.Count(FiskEvidence.Bouquet));
        Assert.Contains(FiskEvidence.Legend, runner.Job.CompletionLegend!);
        Assert.Null(s.Snapshot.Dialog);
        Assert.DoesNotContain(s.Commands, c => c.Text?.Contains("Fisk needs", StringComparison.Ordinal) == true);
    }
}
