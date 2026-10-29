using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

// Synthetic loopback peers only; these tests never connect to a game server.
public class AltarResumeTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 4)]
    [InlineData(false, 4)]
    public async Task Saved_approval_or_verified_offerings_resume_without_rechecking_class(bool accepted, int offerings)
    {
        await using var f = await Fixture.Create(accepted, offerings: offerings);
        f.Runtime.ChooseQuestAsync = (_, _, _, _) => throw new InvalidOperationException("Approved Altar journeys must not request reward acknowledgement again.");

        Assert.True(await f.Configure());
        f.RequireDispatch();

        Assert.Null(f.Runtime.Snapshot("Elara").Class);
        Assert.False(f.Runtime.SchedulerNeedsChoices("Elara", AltarWorshipEvidence.Id));
        Assert.Equal(0, f.Inspections);
        var saved = f.Journal.Load("Elara", AltarWorshipEvidence.Id);
        Assert.Equal(accepted, saved.AltarNoMarkAccepted);
        Assert.Equal(f.Saved.AltarOfferings, saved.AltarOfferings);
        Assert.Equal(f.Saved.NextRun, saved.NextRun);
        Assert.Equal(AltarWorshipEvidence.NextOffering(f.Saved), AltarWorshipEvidence.NextOffering(saved));
        Assert.Null(saved.Pending);
    }

    [Fact]
    public async Task Fresh_non_priest_class_still_requires_and_saves_reward_acknowledgement()
    {
        await using var f = await Fixture.Create(accepted: false);
        var blocked = Assert.IsType<QuestBlockedException>(Assert.Throws<TargetInvocationException>(f.RequireDispatch).InnerException);
        Assert.Contains("Fresh class", blocked.Message);
        int prompts = 0;
        f.Runtime.ChooseQuestAsync = (_, _, _, _) =>
        {
            Assert.Equal("Warrior", f.Runtime.Snapshot("Elara").Class);
            prompts++;
            return Task.FromResult<QuestChoiceResult?>(new(AltarNoMarkAccepted: true));
        };

        Assert.True(await f.Configure());
        f.RequireDispatch();

        Assert.Equal(1, f.Inspections);
        Assert.Equal(1, prompts);
        var saved = f.Journal.Load("Elara", AltarWorshipEvidence.Id);
        Assert.True(saved.AltarNoMarkAccepted);
        Assert.Equal(f.Saved.AltarOfferings, saved.AltarOfferings);
        Assert.Equal(f.Saved.NextRun, saved.NextRun);
    }

    [Fact]
    public async Task Fresh_priest_class_resumes_without_a_reward_warning()
    {
        await using var f = await Fixture.Create(accepted: false, replyClass: CharacterClass.Priest);
        f.Runtime.ChooseQuestAsync = (_, _, _, _) => throw new InvalidOperationException("Priests do not need the no-mark warning.");

        Assert.True(await f.Configure());
        f.RequireDispatch();

        Assert.Equal("Priest", f.Runtime.Snapshot("Elara").Class);
        Assert.Equal(1, f.Inspections);
        Assert.False(f.Journal.Load("Elara", AltarWorshipEvidence.Id).AltarNoMarkAccepted);
    }

    [Theory]
    [InlineData("Priest", false)]
    [InlineData("Warrior", true)]
    public async Task Known_class_resumes_without_another_inspection(string characterClass, bool accepted)
    {
        await using var f = await Fixture.Create(accepted, knownClass: characterClass);
        f.Runtime.ChooseQuestAsync = (_, _, _, _) => throw new InvalidOperationException("Saved reward acknowledgement must not be requested again.");

        Assert.True(await f.Configure());
        f.RequireDispatch();

        Assert.Equal(0, f.Inspections);
    }

    sealed class Fixture : IAsyncDisposable
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly TcpClient clientPeer, serverPeer;
        readonly ProxyConnection connection;
        readonly Dictionary<string, GameSession> sessions;
        public DesktopRuntime Runtime { get; }
        public FileQuestJournal Journal { get; }
        public QuestJob Saved { get; }
        public int Inspections { get; private set; }

        Fixture(DesktopRuntime runtime, FileQuestJournal journal, QuestJob saved, ProxyConnection connection,
            TcpClient clientPeer, TcpClient serverPeer, CharacterClass replyClass)
        {
            Runtime = runtime;
            Journal = journal;
            Saved = saved;
            this.connection = connection;
            this.clientPeer = clientPeer;
            this.serverPeer = serverPeer;
            sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", Private)!.GetValue(runtime)!;
            sessions.Add("Elara", new(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json"))));
            connection.ClientObserved += (opcode, _, _) =>
            {
                Assert.Equal((byte)0x2D, opcode);
                Inspections++;
                var message = new ServerSelfLookMessage { Class = replyClass };
                var builder = new NetworkPacketBuilder(ServerCommand.SelfLook);
                try
                {
                    message.Serialize(ref builder);
                    connection.Tracker.Observe((byte)ServerCommand.SelfLook, builder.ToPacket().Data);
                }
                finally { builder.Dispose(); }
            };
        }

        public static async Task<Fixture> Create(bool accepted, CharacterClass replyClass = CharacterClass.Warrior, string? knownClass = null, int offerings = 0)
        {
            AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
            var settings = AppSettings.Load();
            settings.Characters = ["Elara"];
            settings.ClientPath = "";
            settings.Save();
            var runtime = new DesktopRuntime(preview: true);
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            var last = DateTimeOffset.UtcNow.AddHours(-4);
            var saved = new QuestJob
            {
                Character = "Elara", Quest = AltarWorshipEvidence.Id, Step = offerings > 0 ? "run" : "preflight", Status = "Paused",
                AltarNoMarkAccepted = accepted, NextRun = offerings > 0 ? last + AltarWorshipEvidence.Interval : null,
                AltarOfferings = Enumerable.Range(0, offerings).Select(i =>
                    new AltarOffering(last.AddHours(3 * (i - offerings + 1)), 1, 750, "25 experience!", 0)).ToArray()
            };
            await journal.SaveAsync(saved);
            var (client, clientPeer) = await Pair();
            var (remote, serverPeer) = await Pair();
            var connection = new ProxyConnection(client, remote, 7,
                _ => throw new InvalidOperationException("Synthetic Altar tests never redirect."));
            typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker,
                new CharacterSnapshot
                {
                    Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5,
                    Level = 99, Hp = 100, Gold = 4500, InventoryKnown = true, Class = knownClass
                });
            return new(runtime, journal, saved, connection, clientPeer, serverPeer, replyClass);
        }

        static async Task<(TcpClient Connection, TcpClient Peer)> Pair()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var client = new TcpClient();
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            return (client, await listener.AcceptTcpClientAsync());
        }

        public Task<bool> Configure() => (Task<bool>)typeof(DesktopRuntime).GetMethod("ConfigureQuestAsync", Private)!
            .Invoke(Runtime, ["Elara", Runtime.Recipes[AltarWorshipEvidence.Id], true, false])!;

        public void RequireDispatch() => typeof(DesktopRuntime).GetMethod("RequireQueueDispatch", Private)!
            .Invoke(Runtime, ["Elara", AltarWorshipEvidence.Id, true, 7L]);

        public async ValueTask DisposeAsync()
        {
            sessions.Remove("Elara");
            await Runtime.ShutdownAsync();
            await connection.DisposeAsync();
            clientPeer.Dispose();
            serverPeer.Dispose();
        }
    }
}
