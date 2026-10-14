using System.Buffers.Binary;
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

// All menus, NPC IDs and arrival coordinates here are synthetic, not captures.
[Collection("Walking cadence")]
public class NpcTransferTests
{
    static byte[] Payload(ServerMessage message)
    {
        var builder = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try
        {
            message.Serialize(ref builder);
            return builder.ToPacket().Data;
        }
        finally
        {
            builder.Dispose();
        }
    }

    static MapRecord Source() => JsonSerializer.Deserialize<MapRecord>("""
        {"Id":10055,"Name":"Noam Village","Width":60,"Height":35,"Portals":[],
         "NpcTransfers":[{"Anchor":{"X":44,"Y":21},"Map":1960,
         "PlanningArrival":{"X":1,"Y":1},"Npc":"Habab","Service":"Carpet Merchant",
         "Destinations":["Tagor","Temuair"],"MinimumGold":10001}]}
        """)!;

    static async Task<MovingObstacleTests.Fixture> Create(uint? gold = 20000, bool npc = true, MapRecord? record = null, Tile? origin = null)
    {
        record ??= Source();
        var transfer = record.NpcTransfers.Single();
        var f = await MovingObstacleTests.Fixture.Create(record: record, origin: origin ?? new(44, 24),
            additional: [new(transfer.Map, "Synthetic arrival", 6, 5, [new(new(2, 1), 500, new(1, 1))])]);
        if (gold is not null)
            f.Connection.Tracker.Observe(8, Payload(new ServerStatusMessage { Fields = StatsFieldFlags.ExperienceGold, Gold = gold.Value }));
        if (npc)
            f.Connection.Tracker.Observe(7, Payload(new ServerDrawObjectsMessage
            {
                Entities = [new ServerCreatureEntity { Id = 77, X = (ushort)transfer.Anchor.X, Y = (ushort)transfer.Anchor.Y, Sprite = 1, CreatureType = CreatureType.Mundane, Name = transfer.Npc }]
            }));
        return f;
    }

    static byte[] Menu(string text, params string[] choices) => Payload(new ServerPursuitMessage
    {
        DialogType = DialogType.Menu,
        EntityType = EntityTypeFlags.Creature,
        EntityId = 77,
        PursuitId = 765,
        StepId = 20,
        Name = "Habab",
        Content = text,
        MenuChoices = [.. choices]
    });
    static byte[] Next(string text = "Synthetic carpet travel page", string name = "Habab") => Payload(new ServerPursuitMessage
    {
        DialogType = DialogType.Popup,
        EntityType = EntityTypeFlags.Creature,
        EntityId = 77,
        PursuitId = 876,
        StepId = 30,
        Name = name,
        Content = text,
        HasNextButton = true
    });

    static async Task Run(MovingObstacleTests.Fixture f, Func<ClientPacket, Task> respond, CancellationToken cancellation = default)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        stop.CancelAfter(TimeSpan.FromSeconds(15));
        var decrypt = new ClientPacketEncryptor();
        var response = Task.Run(async () =>
        {
            while (true)
            {
                var header = new byte[3];
                await f.Server.GetStream().ReadExactlyAsync(header, stop.Token);
                var data = new byte[(header[1] << 8) | header[2]];
                await f.Server.GetStream().ReadExactlyAsync(data, stop.Token);
                var packet = (ClientPacket)decrypt.Decrypt(new ClientPacket(data[0], data[1..]));
                f.Commands.Add((byte)packet.Command);
                await respond(packet);
            }
        });
        var travel = f.Session.ExecuteAsync(new("navigate", 500, new(1, 1)), stop.Token);
        try
        {
            await Task.WhenAny(travel, response);
            if (response.IsFaulted)
                await response;
            await travel;
        }
        finally
        {
            stop.Cancel();
            try
            {
                await response;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    static ClientPursuitMessage Pursuit(ClientPacket packet)
    {
        Assert.Equal(ClientCommand.Pursuit, packet.Command);
        var message = new ClientPursuitMessage();
        message.Deserialize(new NetworkPacketReader(packet));
        Assert.Equal(77u, message.EntityId);
        return message;
    }

    static async Task Land(MovingObstacleTests.Fixture f, int map = 1960)
    {
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(CharacterSnapshot state)
        {
            if (state.Map == map && state.Position == new Tile(4, 1))
                observed.TrySetResult();
        }
        f.Session.Changed += Changed;
        try
        {
            await f.Map(map, 6, 5);
            await f.Position(new(4, 1)); // Deliberately differs from the planning estimate.
            Changed(f.Session.Snapshot);
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            f.Session.Changed -= Changed;
        }
    }

    static async Task WalkAfterLanding(MovingObstacleTests.Fixture f, ClientPacket packet, int map = 1960)
    {
        Assert.Equal(ClientCommand.Move, packet.Command);
        Assert.Equal(map, f.Session.Snapshot.Map);
        var before = f.Session.Snapshot.Position!;
        var next = DAQuester.DarkAges.Protocol.StateTracker.GridStep(before, packet.Data[0]);
        if (next == new Tile(2, 1))
        {
            await f.Map(500, 5, 5);
            await f.Position(new(1, 1));
            return;
        }
        var ack = new byte[10];
        ack[0] = packet.Data[0];
        BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(1), (ushort)before.X);
        BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(3), (ushort)before.Y);
        await f.Send(11, ack);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Carpet_uses_fresh_service_destination_fare_and_Next_then_replans_actual_landing(bool merchant)
    {
        await using var f = await Create();
        int phase = 0;
        await Run(f, async packet =>
        {
            if (f.Session.Snapshot.Map == 1960)
            {
                await WalkAfterLanding(f, packet);
                return;
            }
            switch (phase++)
            {
                case 0:
                    Assert.Equal(ClientCommand.RequestObjectInfo, packet.Command);
                    await f.Send(merchant ? (byte)0x2F : (byte)0x30, merchant
                        ? Payload(new ServerScreenMenuMessage { MenuType = DialogMenuType.Menu, EntityType = EntityTypeFlags.Creature, EntityId = 77, Name = "Habab", Content = "Synthetic greeting", MenuChoices = [new() { Text = "Other", PursuitId = 12 }, new() { Text = "Carpet Merchant", PursuitId = 987 }] })
                        : Menu("Synthetic greeting", "Other", "Carpet Merchant"));
                    break;
                case 1:
                    if (merchant)
                    {
                        Assert.Equal(ClientCommand.Merchant, packet.Command);
                        var message = new ClientMerchantMessage();
                        message.Deserialize(new NetworkPacketReader(packet));
                        Assert.Equal(987, message.PursuitId);
                        Assert.Equal(77u, message.EntityId);
                    }
                    else
                    {
                        var message = Pursuit(packet);
                        Assert.Equal((byte?)2, message.MenuChoice);
                        Assert.Equal(765, message.PursuitId);
                        Assert.Equal(21, message.StepId);
                    }
                    await f.Send(0x30, Next());
                    break;
                case 2:
                    Assert.Equal(DialogArgsType.None, Pursuit(packet).ArgsType);
                    await f.Send(0x30, Menu("Where would you like to go?", "Asilon", "Renounce Tagor citizenship", "Temuair"));
                    break;
                case 3:
                    Assert.Equal((byte?)3, Pursuit(packet).MenuChoice);
                    await f.Send(0x30, Menu("The carpet fare is 10,000 gold. Would you like to travel?", "No", "Yes"));
                    break;
                case 4:
                    Assert.Equal((byte?)2, Pursuit(packet).MenuChoice);
                    await f.Send(0x30, Next("Synthetic: travel to Tagor Inn"));
                    break;
                case 5:
                    Assert.Equal(DialogArgsType.None, Pursuit(packet).ArgsType);
                    await Land(f);
                    break;
                default:
                    Assert.Fail("Repeated transfer submission");
                    break;
            }
        });
        Assert.Equal(6, phase);
        Assert.Equal(500, f.Session.Snapshot.Map);
        Assert.Equal(2, f.Commands.Count(c => c == (byte)ClientCommand.Move));
    }

    [Fact]
    public async Task Explicit_caravan_route_can_quote_coins_before_offering_destinations()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Source()))!;
        json["NpcTransfers"]![0]!["FareBeforeDestination"] = true;
        await using var f = await Create(record: json.Deserialize<MapRecord>()!);
        int phase = 0;
        await Run(f, async packet =>
        {
            if (f.Session.Snapshot.Map == 1960)
            {
                await WalkAfterLanding(f, packet);
                return;
            }
            switch (phase++)
            {
                case 0:
                    await f.Send(0x30, Menu("Synthetic greeting", "Carpet Merchant"));
                    break;
                case 1:
                    await f.Send(0x30, Menu("It will cost you 10000 coins to take the caravan. Would you like to go?", "Yes", "No, I'd rather walk"));
                    break;
                case 2:
                    Assert.Equal((byte?)1, Pursuit(packet).MenuChoice);
                    await f.Send(0x30, Menu("Where would you like to go?", "Temuair", "Asilon"));
                    break;
                case 3:
                    Assert.Equal((byte?)1, Pursuit(packet).MenuChoice);
                    await Land(f);
                    break;
                default:
                    Assert.Fail("Repeated fare or destination");
                    break;
            }
        });
        Assert.Equal(4, phase);
        Assert.Equal(500, f.Session.Snapshot.Map);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10000u)]
    public async Task Unknown_or_insufficient_gold_blocks_before_NPC_request(uint? gold)
    {
        await using var f = await Create(gold);
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("navigate", 500, new(1, 1)), default));
        Assert.Contains("gold", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(f.Server.GetStream().DataAvailable);
    }

    [Fact]
    public async Task Unknown_or_duplicate_service_is_preserved_without_submission()
    {
        await using var f = await Create();
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(f, async packet =>
        {
            Assert.Equal(ClientCommand.RequestObjectInfo, packet.Command);
            await f.Send(0x30, Menu("Synthetic greeting", "Carpet Merchant", "Carpet Merchant"));
        }));
        Assert.Single(f.Commands);
        Assert.NotNull(f.Session.Snapshot.Dialog);
    }

    [Fact]
    public async Task Missing_reply_resume_and_late_arrival_do_not_repeat_service()
    {
        await using var f = await Create();
        int submissions = 0;
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(f, async packet =>
        {
            if (packet.Command == ClientCommand.RequestObjectInfo)
                await f.Send(0x30, Menu("Synthetic greeting", "Carpet Merchant"));
            else
            {
                Assert.Equal((byte?)1, Pursuit(packet).MenuChoice);
                submissions++;
            }
        }));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("navigate", 500, new(1, 1)), default));
        Assert.Equal(1, submissions);
        Assert.False(f.Server.GetStream().DataAvailable);
        await Land(f);
        await Run(f, packet => WalkAfterLanding(f, packet));
        Assert.Equal(1, submissions);
        Assert.Equal(500, f.Session.Snapshot.Map);
    }

    [Theory]
    [InlineData("Habab")]
    [InlineData("Nuclear")]
    public async Task Unowned_open_transfer_dialog_is_preserved(string name)
    {
        await using var f = await Create();
        f.Connection.Tracker.Observe(0x30, Next(name: name));
        var dialog = f.Session.Snapshot.Dialog;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("navigate", 500, new(1, 1)), default));
        Assert.Same(dialog, f.Session.Snapshot.Dialog);
        Assert.False(f.Server.GetStream().DataAvailable);
    }

    [Theory]
    [InlineData(6926, 10028, "Quard", "Express Ship", "Asilon")]
    [InlineData(10000, 10999, "Lenoa", "Caravan to Noam", "Noam")]
    public async Task Ship_and_caravan_use_observed_destination_confirmation_and_accept_direct_landing(int sourceMap, int arrivalMap, string npc, string service, string destination)
    {
        var record = new MapRecord(sourceMap, "Synthetic departure", 14, 14, [])
        {
            NpcTransfers = [new(new(7, 7), arrivalMap, new(1, 1), npc, service, [destination])]
        };
        await using var f = await Create(record: record, origin: new(7, 10));
        int phase = 0;
        await Run(f, async packet =>
        {
            if (f.Session.Snapshot.Map == arrivalMap)
            {
                await WalkAfterLanding(f, packet, arrivalMap);
                return;
            }
            if (phase++ == 0)
            {
                Assert.Equal(ClientCommand.RequestObjectInfo, packet.Command);
                await f.Send(0x2F, Payload(new ServerScreenMenuMessage { MenuType = DialogMenuType.Menu, EntityType = EntityTypeFlags.Creature, EntityId = 77, Name = npc, Content = "Synthetic greeting", MenuChoices = [new() { Text = service, PursuitId = 987 }] }));
            }
            else if (phase == 2)
            {
                Assert.Equal(ClientCommand.Merchant, packet.Command);
                var message = new ClientMerchantMessage();
                message.Deserialize(new NetworkPacketReader(packet));
                Assert.Equal(987, message.PursuitId);
                await f.Send(0x30, Payload(new ServerPursuitMessage { DialogType = DialogType.Menu, EntityType = EntityTypeFlags.Creature, EntityId = 77, PursuitId = 876, StepId = 14, Name = npc, Content = "Would you like to travel to " + destination + "? The fare is 1,000 gold.", MenuChoices = ["No", "Yes"] }));
            }
            else
            {
                var message = Pursuit(packet);
                Assert.Equal((byte?)2, message.MenuChoice);
                Assert.Equal(876, message.PursuitId);
                Assert.Equal(15, message.StepId);
                await Land(f, arrivalMap);
            }
        });
        Assert.Equal(3, phase);
        Assert.Equal(500, f.Session.Snapshot.Map);
    }

    [Theory]
    [InlineData("Renounce Tagor citizenship", "Tagor registration")]
    [InlineData("Asilon", "Hwarone")]
    [InlineData("Temuair", "Travel to Tagor")]
    public async Task Unknown_or_ambiguous_destinations_are_preserved(string first, string second)
    {
        await using var f = await Create();
        int submissions = 0;
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(f, async packet =>
        {
            if (packet.Command == ClientCommand.RequestObjectInfo)
                await f.Send(0x30, Menu("Synthetic greeting", "Carpet Merchant"));
            else
            {
                Assert.Equal((byte?)1, Pursuit(packet).MenuChoice);
                Assert.Equal(1, ++submissions);
                await f.Send(0x30, Menu("Where would you like to go?", first, second));
            }
        }));
        Assert.Equal(1, submissions);
        Assert.Equal(first, f.Session.Snapshot.Dialog!.Choices[0].Text);
    }

    [Fact]
    public async Task Cancellation_after_service_preserves_pending_transfer()
    {
        await using var f = await Create();
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(f, async packet =>
        {
            if (packet.Command == ClientCommand.RequestObjectInfo)
                await f.Send(0x30, Menu("Synthetic greeting", "Carpet Merchant"));
            else
            {
                Pursuit(packet);
                cancel.Cancel();
            }
        }, cancel.Token));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("navigate", 500, new(1, 1)), default));
        Assert.Equal(2, f.Commands.Count);
        Assert.False(f.Server.GetStream().DataAvailable);
    }

    [Fact]
    public async Task Wrong_NPC_reply_never_gets_a_choice()
    {
        await using var f = await Create();
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(f, async packet =>
        {
            Assert.Equal(ClientCommand.RequestObjectInfo, packet.Command);
            await f.Send(0x30, Payload(new ServerPursuitMessage { DialogType = DialogType.Menu, EntityType = EntityTypeFlags.Creature, EntityId = 78, PursuitId = 765, StepId = 20, Name = "Habab", Content = "Synthetic wrong NPC", MenuChoices = ["Carpet Merchant"] }));
        }));
        Assert.Single(f.Commands);
        Assert.Equal(78u, f.Session.Snapshot.Dialog!.EntityId);
    }

    [Fact]
    public async Task Newly_observed_unaffordable_fare_is_preserved()
    {
        await using var f = await Create();
        int submissions = 0;
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => Run(f, async packet =>
        {
            if (packet.Command == ClientCommand.RequestObjectInfo)
                await f.Send(0x30, Menu("Synthetic greeting", "Carpet Merchant"));
            else
            {
                Pursuit(packet);
                submissions++;
                await f.Send(0x30, Menu("Would you like to travel to Tagor? The fare is 30,000 gold.", "No", "Yes"));
            }
        }));
        Assert.Contains("gold", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, submissions);
    }

    [Fact]
    public async Task Missing_NPC_pauses_without_request_or_fake_portal_movement()
    {
        await using var f = await Create(npc: false);
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("navigate", 500, new(1, 1)), default));
        Assert.Contains("Habab", error.Message);
        Assert.False(f.Server.GetStream().DataAvailable);
    }

    [Fact]
    public async Task Replacement_character_cannot_reconcile_previous_characters_transfer()
    {
        await using var f = await Create();
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(f, async packet =>
        {
            if (packet.Command == ClientCommand.RequestObjectInfo)
                await f.Send(0x30, Menu("Synthetic greeting", "Carpet Merchant"));
            else
            {
                Pursuit(packet);
                cancel.Cancel();
            }
        }, cancel.Token));
        f.Connection.Tracker.Identity("Replacement", 99);
        await Land(f);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("navigate", 1960, new(4, 1)), default));
        Assert.Equal(2, f.Commands.Count);
    }
}
