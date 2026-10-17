using System.Text.Json;
using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;

namespace DAQuester.Tests;

// Every connection here is a local synthetic peer, using a restricted observed popup.
[Collection("Walking cadence")]
public class ResearchArrivalTests
{
    static byte[] Payload(ServerMessage message)
    {
        var builder = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try { message.Serialize(ref builder); return builder.ToPacket().Data; }
        finally { builder.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Supply_departure_leaves_reactor_before_shop_or_returns_its_fresh_menu(bool popup)
    {
        var origin = new Tile(6, 9);
        await using var fixture = await MovingObstacleTests.Fixture.Create(record: new(401, "Synthetic research room", 15, 15, []), origin: origin);
        int moves = 0;
        await fixture.Walk(origin, async (_, _) =>
        {
            Assert.Equal(1, ++moves);
            if (!popup) return false; // Fixture acknowledges the requested step.
            await fixture.Send(0x30, Payload(new ServerPursuitMessage { DialogType = DialogType.Menu,
                EntityType = EntityTypeFlags.Reactor, EntityId = 77, PursuitId = 315,
                Content = WizardyResearchEvidence.Opening }));
            await fixture.Send(11, [4, 0, 6, 0, 9, 0, 0, 0, 0, 0]);
            return true;
        }, new("research-depart"));
        if (popup) Assert.Equal(WizardyResearchEvidence.Opening, fixture.Session.Snapshot.Dialog?.Text);
        else Assert.DoesNotContain(fixture.Session.Snapshot.Position!, WizardyResearchEvidence.Triggers);
        Assert.DoesNotContain((byte)ClientCommand.RequestObjectInfo, fixture.Commands);
        Assert.DoesNotContain((byte)0x3A, fixture.Commands);
    }

    [Fact]
    public async Task Unrelated_popup_stops_movement_and_remains_unselected()
    {
        var origin = new Tile(7, 9);
        await using var fixture = await MovingObstacleTests.Fixture.Create(record: new(401, "Synthetic research room", 15, 15, []), origin: origin);
        int moves = 0;
        await fixture.Walk(origin, async (_, _) =>
        {
            Assert.Equal(1, ++moves);
            await fixture.Send(0x30, Payload(new ServerPursuitMessage { DialogType = DialogType.Popup,
                EntityType = EntityTypeFlags.Reactor, EntityId = 88, PursuitId = 12, HasNextButton = true,
                Content = "Synthetic unrelated popup" }));
            await fixture.Send(11, [4, 0, 7, 0, 9, 0, 0, 0, 0, 0]);
            return true;
        }, new("research-trigger"));
        Assert.Equal("Synthetic unrelated popup", fixture.Session.Snapshot.Dialog?.Text);
        Assert.DoesNotContain((byte)0x3A, fixture.Commands);
        Assert.Equal(1, moves);
    }

    [Fact]
    public async Task Unexpected_room_change_stops_without_a_second_approach()
    {
        var origin = new Tile(7, 9);
        await using var fixture = await MovingObstacleTests.Fixture.Create(record: new(401, "Synthetic research room", 15, 15, []), origin: origin);
        int moves = 0;
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => fixture.Walk(origin, async (_, _) =>
        {
            Assert.Equal(1, ++moves);
            await fixture.Map(500, 5, 5);
            await fixture.Position(new(1, 1));
            return true;
        }, new("research-trigger")));
        Assert.Contains("changed connection or room", error.Message);
        Assert.Equal(1, moves);
        Assert.DoesNotContain((byte)0x3A, fixture.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Research_tile_login_can_reach_visible_Dar_without_an_artificial_doorway_departure(bool open)
    {
        var origin = new Tile(6, 9);
        await using var fixture = await MovingObstacleTests.Fixture.Create(record: new(401, "Synthetic research room", 15, 15, []), origin: origin);
        fixture.Connection.Tracker.Observe(7, Payload(new ServerDrawObjectsMessage
        {
            Entities = [new ServerCreatureEntity { Id = 55, X = 5, Y = 2, Name = "Dar", Sprite = 1, CreatureType = CreatureType.Mundane }]
        }));
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = Task.Run(async () =>
        {
            byte[] header = new byte[3];
            await fixture.Server.GetStream().ReadExactlyAsync(header, limit.Token);
            byte[] data = new byte[(header[1] << 8) | header[2]];
            await fixture.Server.GetStream().ReadExactlyAsync(data, limit.Token);
            var packet = (ClientPacket)new ClientPacketEncryptor().Decrypt(new ClientPacket(data[0], data[1..]));
            Assert.True(open, "Already-visible Dar must not cause walking to the idle tile.");
            Assert.Equal((byte)ClientCommand.RequestObjectInfo, (byte)packet.Command);
            await fixture.Send(0x30, Payload(new ServerPursuitMessage { DialogType = DialogType.Popup,
                EntityType = (EntityTypeFlags)1, EntityId = 55, Name = "Dar", Content = "Synthetic Dar reply", PursuitId = 0 }));
        });
        var command = fixture.Session.ExecuteAsync(open ? new("open", Text: "Dar")
            : new("navigate", 401, new(7, 9), StopAtVisibleNpc: "Dar"), limit.Token);
        try
        {
            await Task.WhenAny(command, response);
            if (response.IsFaulted) await response;
            await command;
            if (open) await response;
            Assert.Equal(origin, fixture.Session.Snapshot.Position);
        }
        finally
        {
            limit.Cancel();
            try { await response; }
            catch (OperationCanceledException) { }
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Fresh_research_popup_ends_approach_even_when_the_server_keeps_the_original_tile(bool departure, bool afterRefresh)
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../content/evidence/datester-7.41/wizardry-research-arrival.json"));
        using var capture = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        byte[] popup = Convert.FromBase64String(capture.RootElement.GetProperty("DialogPayload").GetString()!);
        var origin = departure ? new Tile(6, 9) : new Tile(7, 9);
        // Use the real recovery cadence: a 1 ms test cadence can exhaust route
        // refreshes between the separate position and popup network frames.
        await using var fixture = await MovingObstacleTests.Fixture.Create(cadence: 400, record: new(401, "Synthetic research room", 11, 11, []), origin: origin);
        fixture.Session.FastMode = false;
        int moves = 0;
        await fixture.Walk(origin, async (_, _) =>
        {
            Assert.Equal(1, ++moves);
            if (!afterRefresh) await fixture.Send(0x30, popup);
            // The server did not grant the requested step.
            await fixture.Send(11, [4, 0, (byte)origin.X, 0, (byte)origin.Y, 0, 0, 0, 0, 0]);
            return true;
        }, new("research-trigger"), refresh: async () =>
        {
            await fixture.Position(origin);
            if (afterRefresh) await fixture.Send(0x30, popup);
        });
        Assert.Equal(1, moves);
        Assert.Equal(origin, fixture.Session.Snapshot.Position);
        Assert.Equal(capture.RootElement.GetProperty("Dialog").GetProperty("Text").GetString(), fixture.Session.Snapshot.Dialog?.Text);
        Assert.DoesNotContain((byte)0x3A, fixture.Commands); // No automatic choice or dismissal.
        Assert.True(fixture.Commands.Count(c => c == 0x38) <= 1);
    }
}
