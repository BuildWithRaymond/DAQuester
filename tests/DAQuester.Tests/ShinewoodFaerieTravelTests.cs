using System.Text.Json;
using Arbiter.Net;
using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;

namespace DAQuester.Tests;
// All transport peers here are local synthetic servers; no game is connected.
[Collection("Walking cadence")]
public class ShinewoodFaerieTravelTests
{
    static void Observe(MovingObstacleTests.Fixture f, ServerMessage message)
    {
        var code = ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value;
        var b = new NetworkPacketBuilder(code);
        try
        {
            message.Serialize(ref b);
            f.Connection.Tracker.Observe((byte)code, b.ToPacket().Data);
        }
        finally
        {
            b.Dispose();
        }
    }

    static async Task<ClientPacket> Read(MovingObstacleTests.Fixture f, CancellationToken ct)
    {
        var h = new byte[3];
        await f.Server.GetStream().ReadExactlyAsync(h, ct);
        var data = new byte[(h[1] << 8) | h[2]];
        await f.Server.GetStream().ReadExactlyAsync(data, ct);
        return (ClientPacket)new ClientPacketEncryptor().Decrypt(new ClientPacket(data[0], data[1..]));
    }

    static GameCommand Command(string kind) => JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(new { Kind = kind, Amount = 3, Slot = 3, ExpectedResource = kind == "spell" ? "dachaidh" : "Mileth Song" }))!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Safe_return_departs_actual_shinewood_one_landing_before_settling(bool fast)
    {
        var landing = new Tile(2, 2);
        await using var f = await MovingObstacleTests.Fixture.Create(
            additional: [new(542, "Synthetic Shinewood 1", 5, 5, [])]);
        long before = f.Session.Snapshot.Revision;
        await f.Map(542, 5, 5);
        await f.Position(landing);
        await f.Session.WaitAsync(s => s.Map == 542 && s.Position == landing, before, TimeSpan.FromSeconds(3), default);
        f.Session.FastMode = fast;
        await f.Walk(new(2, 1), (_, next) =>
        {
            Assert.Equal(1, Math.Abs(next.X - landing.X) + Math.Abs(next.Y - landing.Y));
            return Task.FromResult(false);
        }, new("settle-faerie-return"));
        Assert.Equal(542, f.Session.Snapshot.Map);
        Assert.NotEqual(landing, f.Session.Snapshot.Position);
        Assert.Single(f.Commands, c => c == 6);
    }

    [Fact]
    public async Task Item_use_resolves_current_named_slot_after_inventory_reordering()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        Observe(f, new ServerAddInventoryMessage { Slot = 9, Name = "Mileth Song", Quantity = 1 });
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await f.Session.ExecuteAsync(Command("use-item"), stop.Token);
        var p = await Read(f, stop.Token);
        var item = new ClientUseMessage();
        item.Deserialize(new NetworkPacketReader(p));
        Assert.Equal(9, item.Slot);
    }

    [Fact]
    public async Task Missing_named_resource_is_rejected_before_any_packet_is_written()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        Observe(f, new ServerAddInventoryMessage { Slot = 3, Name = "Recall Scroll", Quantity = 1 });
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(Command("use-item"), default));
        Assert.False(f.Server.GetStream().DataAvailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" (Lev:100/100)")]
    public async Task Spell_use_resolves_current_named_slot_after_book_reordering(string rank)
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        Observe(f, new ServerAddSpellMessage { Slot = 9, Name = "dachaidh" + rank });
        Observe(f, new ServerStatusMessage { Fields = StatsFieldFlags.Vitals, Health = 100, Mana = 100 });
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await f.Session.ExecuteAsync(Command("spell"), stop.Token);
        var p = await Read(f, stop.Token);
        var spell = new ClientUseSpellMessage();
        spell.Deserialize(new NetworkPacketReader(p));
        Assert.Equal(9, spell.Slot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Documented_exit_uses_actual_landing_without_a_recorded_reverse_arrival(bool fast)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(547, "Synthetic Forest 10", 50, 50, []), origin: new(29, 48), additional: [new(546, "Synthetic Forest 9", 50, 50, [])]);
        f.Session.FastMode = fast;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        var response = Task.Run(async () =>
        {
            var p = await Read(f, stop.Token);
            Assert.Equal((byte)6, (byte)p.Command);
            Assert.Equal((byte)2, p.Data[0]);
            await f.Map(546, 50, 50);
            await f.Position(new(8, 7));
        });
        var walk = f.Session.ExecuteAsync(new("walk-exit", 546, new(29, 49)), stop.Token);
        await Task.WhenAny(walk, response);
        if (response.IsFaulted)
            await response;
        await walk;
        await response;
        Assert.Equal(546, f.Session.Snapshot.Map);
        Assert.Equal(new Tile(8, 7), f.Session.Snapshot.Position);
    }

    [Fact]
    public async Task Undocumented_exit_never_sends_movement()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("walk-exit", 546, new(2, 1)), default));
        Assert.False(f.Server.GetStream().DataAvailable);
    }

    [Fact]
    public async Task Faerie_reactor_approach_finishes_without_a_drawn_npc()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(992, "Synthetic Forest 11", 100, 100, []), origin: ShinewoodFaerieEvidence.Approach);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        await f.Session.ExecuteAsync(new("visit", 992, ShinewoodFaerieEvidence.Anchor, Text: "Faerie"), stop.Token);
        Assert.Equal(ShinewoodFaerieEvidence.Approach, f.Session.Snapshot.Position);
        Assert.Empty(f.Session.Snapshot.Entities);
        Assert.False(f.Server.GetStream().DataAvailable);
    }

    [Theory]
    [InlineData("visit")]
    [InlineData("navigate")]
    public async Task Popup_present_at_faerie_travel_dispatch_is_preserved_without_close_or_movement(string kind)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(992, "Synthetic Forest 11", 100, 100, []), origin: ShinewoodFaerieEvidence.Approach);
        Observe(f, new ServerScreenMenuMessage { MenuType = DialogMenuType.Menu, EntityType = EntityTypeFlags.Creature, EntityId = 888, Name = "Unrelated", Content = "Synthetic unknown offer" });
        var popup = f.Session.Snapshot.Dialog;
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var command = kind == "visit" ? new GameCommand("visit", 992, ShinewoodFaerieEvidence.Anchor, Text: "Faerie") : JsonSerializer.Deserialize<GameCommand>("""{"Kind":"navigate","Map":500,"Target":{"X":2,"Y":1},"PreservePopup":true}""")!;
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(command, stop.Token));
        Assert.Equal(popup, f.Session.Snapshot.Dialog);
        Assert.False(f.Server.GetStream().DataAvailable);
        Assert.False(f.Client.GetStream().DataAvailable);
    }
}
