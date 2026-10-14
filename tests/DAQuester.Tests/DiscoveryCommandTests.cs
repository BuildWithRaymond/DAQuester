using System.Text.Json;
using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;

namespace DAQuester.Tests;

// Synthetic protocol peers only. Shapes follow the observed counter and Wine flow.
[Collection("Walking cadence")]
public class DiscoveryCommandTests
{
    static GameCommand Command(string json) => JsonSerializer.Deserialize<GameCommand>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    static byte[] Payload(ServerMessage message)
    {
        var builder = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try
        {
            message.Serialize(ref builder);
            return builder.ToPacket().Data;
        }
        finally { builder.Dispose(); }
    }

    static ServerScreenMenuMessage Stock() => new()
    {
        MenuType = DialogMenuType.ItemChoices,
        EntityType = EntityTypeFlags.Creature,
        EntityId = 77,
        Name = "Viveka",
        Content = "Synthetic stock",
        PursuitId = 74,
        ItemChoices = [new() { Name = "Wine", Price = 500 }]
    };

    static ServerScreenMenuMessage Menu(string npc, uint id = 77) => new()
    {
        MenuType = DialogMenuType.Menu,
        EntityType = EntityTypeFlags.Creature,
        EntityId = id,
        Name = npc,
        Content = "Synthetic fresh menu"
    };

    [Fact]
    public async Task Interact_opens_observed_NPC_from_public_approach_beyond_visit_anchor_distance()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new(500, "Synthetic counter", 10, 10, []),
            wall: (x, _) => x == 3, origin: new(7, 4));
        f.Connection.Tracker.Observe(7, Payload(new ServerDrawObjectsMessage
        {
            Entities = [new ServerCreatureEntity { Id = 77, X = 1, Y = 1, Name = "Viveka", CreatureType = CreatureType.Mundane }]
        }));
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0;
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode == (byte)ClientCommand.RequestObjectInfo)
            {
                requests++;
                opened.TrySetResult();
            }
        };
        var operation = f.Session.ExecuteAsync(Command("""{"kind":"interact","map":500,"target":{"x":7,"y":4},"text":"Viveka"}"""), default);
        await Task.WhenAny(operation, opened.Task);
        if (operation.IsCompleted)
            await operation;
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await f.Send(0x2F, Payload(Menu("Viveka")));
        await operation;
        Assert.Equal(1, requests);
        Assert.Equal(new Tile(7, 4), f.Session.Snapshot.Position);
        Assert.Equal("Viveka", f.Session.Snapshot.Dialog!.Name);
    }

    [Fact]
    public async Task Say_with_replyFrom_waits_for_the_intended_fresh_NPC_page()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Connection.ClientObserved += (opcode, _, _) => { if (opcode == 0x0E) sent.TrySetResult(); };
        var operation = f.Session.ExecuteAsync(Command("""{"kind":"say","text":"grapes","replyFrom":"Eeva"}"""), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(operation.IsCompleted, "Speech must wait for its requested NPC response.");
        long before = f.Session.Snapshot.Revision;
        await f.Send(0x2F, Payload(Menu("Other mundane", 88)));
        await f.Session.WaitAsync(s => s.Dialog?.Name == "Other mundane", before, TimeSpan.FromSeconds(2), default);
        Assert.False(operation.IsCompleted, "An unrelated NPC page cannot confirm speech.");
        await f.Send(0x2F, Payload(Menu("Eeva")));
        await operation;
        Assert.Equal("Eeva", f.Session.Snapshot.Dialog!.Name);
    }

    [Theory]
    [InlineData("expectedGeneration", "-1")]
    [InlineData("expectedDialogRevision", "-1")]
    public async Task Stale_command_guard_sends_nothing(string field, string value)
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        int sent = 0;
        f.Connection.ClientObserved += (_, _, _) => sent++;
        var command = Command("{\"kind\":\"say\",\"text\":\"wine\",\"" + field + "\":" + value + "}");
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(command, default));
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Quantity_purchase_submits_item_and_quantity_once_then_requires_both_receipts()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(8, Payload(new ServerStatusMessage { Fields = StatsFieldFlags.ExperienceGold, Gold = 10000 }));
        f.Connection.Tracker.Observe(0x2F, Payload(Stock()));
        var requests = new List<string[]>();
        var replies = new List<Task>();
        var quantitySent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Connection.ClientObserved += (opcode, payload, _) =>
        {
            if (opcode != 0x39)
                return;
            var message = new ClientMerchantMessage();
            message.Deserialize(new NetworkPacketReader(new ClientPacket(opcode, payload)));
            requests.Add(message.Arguments.ToArray());
            if (requests.Count == 1)
                replies.Add(f.Send(0x2F, Payload(new ServerScreenMenuMessage
                {
                    MenuType = DialogMenuType.TextInputWithArgs,
                    EntityType = EntityTypeFlags.Creature,
                    EntityId = 77,
                    Name = "Viveka",
                    Content = "How many do you need?",
                    Prompt = "Wine",
                    PursuitId = 76
                })));
            else
            {
                quantitySent.TrySetResult();
                replies.Add(f.Send(15, Payload(new ServerAddInventoryMessage { Slot = 1, Name = "Wine", Quantity = 5 })));
            }
        };
        var operation = f.Session.ExecuteAsync(Command("""{"kind":"buy","text":"Wine","quantity":5}"""), default);
        await quantitySent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await f.Session.WaitAsync(s => s.Count("Wine") == 5, 0, TimeSpan.FromSeconds(2), default);
        Assert.False(operation.IsCompleted, "Inventory alone cannot confirm the quoted payment.");
        await f.Send(8, Payload(new ServerStatusMessage { Fields = StatsFieldFlags.ExperienceGold, Gold = 7500 }));
        await operation;
        await Task.WhenAll(replies);
        Assert.Equal(2, requests.Count);
        Assert.Equal(new[] { "Wine" }, requests[0]);
        Assert.Equal(new[] { "Wine", "5" }, requests[1]);
        Assert.Equal(5, f.Session.Snapshot.Count("Wine"));
        Assert.Equal(7500u, f.Session.Snapshot.Gold);
    }

    [Fact]
    public async Task Quantity_total_quote_is_checked_before_selecting_any_item()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(8, Payload(new ServerStatusMessage { Fields = StatsFieldFlags.ExperienceGold, Gold = 1000 }));
        f.Connection.Tracker.Observe(0x2F, Payload(Stock()));
        int sent = 0;
        f.Connection.ClientObserved += (_, _, _) => sent++;
        await Assert.ThrowsAsync<ResourceUnavailableException>(() => f.Session.ExecuteAsync(Command("""{"kind":"buy","text":"Wine","quantity":5}"""), default));
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Unanswered_merchant_quantity_prompt_cannot_be_submitted_twice()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(0x2F, Payload(new ServerScreenMenuMessage
        {
            MenuType = DialogMenuType.TextInputWithArgs,
            EntityType = EntityTypeFlags.Creature,
            EntityId = 77,
            Name = "Viveka",
            Content = "How many do you need?",
            Prompt = "Wine",
            PursuitId = 76
        }));
        int sent = 0;
        f.Connection.ClientObserved += (opcode, _, _) => { if (opcode == 0x39) sent++; };
        await Assert.ThrowsAsync<TimeoutException>(() => f.Session.ExecuteAsync(new("input", Text: "5"), default));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("input", Text: "5"), default));
        Assert.Equal(1, sent);
    }

    [Fact]
    public async Task Enter_probes_one_adjacent_door_and_records_actual_unlisted_map_dimensions()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(wall: (x, y) => x == 1 && y == 0);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int moves = 0;
        f.Connection.ClientObserved += (opcode, payload, _) =>
        {
            if (opcode != (byte)ClientCommand.Move)
                return;
            moves++;
            Assert.Equal((byte)WorldDirection.Up, payload[0]);
            sent.TrySetResult();
        };
        var operation = f.Session.ExecuteAsync(new("enter", Target: new(1, 0)), default);
        await Task.WhenAny(operation, sent.Task);
        if (operation.IsCompleted)
            await operation;
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await f.Map(999, 8, 9);
        await f.Position(new(2, 3));
        await operation;
        Assert.Equal(1, moves);
        Assert.Equal(999, f.Session.Snapshot.Map);
        Assert.Equal(new Tile(2, 3), f.Session.Snapshot.Position);
        using var state = JsonDocument.Parse(JsonSerializer.Serialize(f.Session));
        var map = state.RootElement.GetProperty("ObservedMap");
        Assert.Equal(999, map.GetProperty("Id").GetInt32());
        Assert.Equal(8, map.GetProperty("Width").GetInt32());
        Assert.Equal(9, map.GetProperty("Height").GetInt32());
        Assert.Empty(map.GetProperty("Portals").EnumerateArray());
    }

    [Fact]
    public async Task Interact_does_not_open_a_same_named_NPC_after_an_unexpected_doorway_transition()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Session.FastMode = false;
        await f.Walk(new(3, 1), (_, _) => Task.FromResult(false));
        await f.Walk(new(1, 1), (_, _) => Task.FromResult(false));
        f.Session.FastMode = true;
        var requests = new List<byte>();
        var replies = new List<Task>();
        async Task UnexpectedLanding()
        {
            await f.Map(499, 5, 5);
            await f.Send(7, Payload(new ServerDrawObjectsMessage
            {
                Entities = [new ServerCreatureEntity { Id = 88, X = 4, Y = 4, Name = "Viveka", CreatureType = CreatureType.Mundane }]
            }));
            await f.Position(new(2, 1));
        }
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            requests.Add(opcode);
            if (opcode == (byte)ClientCommand.Move)
                replies.Add(UnexpectedLanding());
            if (opcode == (byte)ClientCommand.RequestObjectInfo)
                replies.Add(f.Send(0x2F, Payload(Menu("Viveka", 88))));
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("interact", 500, new(2, 1), "Viveka"), default));
        await Task.WhenAll(replies);
        Assert.DoesNotContain((byte)ClientCommand.RequestObjectInfo, requests);
        Assert.Equal(499, f.Session.Snapshot.Map);
    }

    [Fact]
    public async Task Enter_with_inward_target_requires_the_second_authoritative_position_before_returning()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(additional: [new(999, "Synthetic Inn", 8, 9, [])]);
        var doorSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inwardSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var directions = new List<byte>();
        f.Connection.ClientObserved += (opcode, payload, _) =>
        {
            if (opcode != (byte)ClientCommand.Move)
                return;
            directions.Add(payload[0]);
            if (directions.Count == 1)
                doorSent.TrySetResult();
            else
                inwardSent.TrySetResult();
        };
        var operation = f.Session.ExecuteAsync(Command("""{"kind":"enter","map":999,"target":{"x":1,"y":0},"inwardTarget":{"x":4,"y":5}}"""), default);
        await doorSent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await f.Map(999, 8, 9);
        await f.Position(new(4, 6));
        await f.Session.WaitAsync(s => s.Map == 999 && s.Position == new Tile(4, 6), 0, TimeSpan.FromSeconds(2), default);
        await Task.WhenAny(operation, inwardSent.Task);
        Assert.False(operation.IsCompleted, "The entrance landing alone must not finish an inward entry.");
        await inwardSent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await f.Position(new(4, 5));
        await operation;
        Assert.Equal(new byte[] { 0, 0 }, directions);
        Assert.Equal(new Tile(4, 5), f.Session.Snapshot.Position);
    }

    [Fact]
    public async Task Inward_entry_without_a_bound_destination_sends_nothing()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        int sent = 0;
        f.Connection.ClientObserved += (_, _, _) => sent++;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(Command("""{"kind":"enter","target":{"x":1,"y":0},"inwardTarget":{"x":4,"y":5}}"""), default));
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Armed_shrine_refusal_stops_at_the_first_rejection_without_refresh_or_equipment_change()
    {
        string file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/evidence/datester-7.41/danaan-live.json"));
        using var fixture = JsonDocument.Parse(File.ReadAllText(file));
        byte[] refusal = fixture.RootElement.GetProperty("Events").EnumerateArray()
            .Where(e => e.GetProperty("Opcode").GetInt32() == 10)
            .Select(e => Convert.FromBase64String(e.GetProperty("Payload").GetString()!))
            .First(p => System.Text.Encoding.ASCII.GetString(p.AsSpan(3)) == "Your must be armed to enter here.");
        var record = new MapRecord(3020, "Synthetic shrine approach", 5, 5, [new(new(3, 1), 3019, new(1, 1))]);
        await using var f = await MovingObstacleTests.Fixture.Create(record: record, additional: [new(3019, "Synthetic shrine", 5, 5, [])]);
        int rejections = 0;
        var error = await Assert.ThrowsAsync<EntranceRefusedException>(() => f.Walk(new(1, 1), async (_, next) =>
        {
            if (next != new Tile(3, 1))
                return false;
            rejections++;
            await f.Send(10, refusal);
            await f.Send(11, [4, 0, 2, 0, 1, 0, 0, 0, 0, 0]);
            return true;
        }, new("navigate", 3019, new(1, 1))));
        Assert.Equal("Your must be armed to enter here.", error.Message);
        Assert.Equal(1, rejections);
        Assert.DoesNotContain((byte)0x38, f.Commands);
        Assert.DoesNotContain((byte)0x1C, f.Commands);
        Assert.DoesNotContain((byte)0x44, f.Commands);
        Assert.Equal(new Tile(2, 1), f.Session.Snapshot.Position);
    }

    [Theory]
    [InlineData(998, 4)] // An unexpected map must not receive the inward movement.
    [InlineData(999, 5)] // A changed landing cannot authorize a nonadjacent tile.
    public async Task Inward_entry_stops_after_unexpected_map_or_nonadjacent_tile(int actualMap, int inwardX)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(additional: [new(999, "Synthetic Inn", 8, 9, [])]);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int moves = 0;
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode == (byte)ClientCommand.Move)
            {
                moves++;
                sent.TrySetResult();
            }
        };
        var operation = f.Session.ExecuteAsync(Command("{\"kind\":\"enter\",\"map\":999,\"target\":{\"x\":1,\"y\":0},\"inwardTarget\":{\"x\":" + inwardX + ",\"y\":5}}"), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await f.Map(actualMap, 8, 9);
        await f.Position(new(4, 6));
        await Assert.ThrowsAsync<QuestBlockedException>(() => operation);
        Assert.Equal(1, moves);
    }
}
