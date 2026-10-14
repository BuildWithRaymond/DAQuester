using System.Buffers.Binary;
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

[Collection("Walking cadence")]
public class TravelNoticeTests
{
    [Theory]
    [InlineData("Your armaments are repelled from here.")]
    [InlineData("Your must be armed to enter here.")]
    [InlineData("You are not a Monk.")]
    public async Task First_active_entry_refusal_stops_before_another_move(string text)
    {
        await using var f=await MovingObstacleTests.Fixture.Create();
        int moves=0;
        f.Connection.ClientObserved+=(opcode,_,_)=> { if(opcode==(byte)ClientCommand.Move) moves++; };
        f.Connection.Tracker.Observe(0x0A, [3,(byte)(text.Length>>8),(byte)text.Length,..System.Text.Encoding.ASCII.GetBytes(text)]);
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var error=await Assert.ThrowsAsync<QuestBlockedException>(()=>f.Session.ExecuteAsync(new("navigate",f.Session.Snapshot.Map,new(3,1)),limit.Token));
        Assert.Contains(text,error.Message);
        Assert.Equal(0,moves);
    }

    static byte[] Payload(ServerMessage message)
    {
        var b = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try
        {
            message.Serialize(ref b);
            return b.ToPacket().Data;
        }
        finally
        {
            b.Dispose();
        }
    }

    static byte[] Warning(string text = "You can't bring weapons here!") => Payload(new ServerPursuitMessage { DialogType = DialogType.Popup, EntityType = EntityTypeFlags.Reactor, EntityId = 88, PursuitId = 204, StepId = 299, Name = "Guard", Content = text });
    static byte[] Roleplay() => Convert.FromBase64String(PerfectHairstyleLiveTests.Load().Events.Single(e => e.Revision == 711).Payload);
    static void Equip(MovingObstacleTests.Fixture f)
    {
        f.Connection.Tracker.Observe((byte)ServerCommand.AddEquip, Payload(new ServerAddEquipMessage { Slot = EquipmentSlot.Weapon, Name = "Stick" }));
        f.Connection.Tracker.Observe((byte)ServerCommand.UserReady, [0]);
    }

    static async Task Run(MovingObstacleTests.Fixture f, Func<ClientPacket, Task> response, GameCommand? command = null)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var decrypt = new ClientPacketEncryptor();
        var server = Task.Run(async () =>
        {
            while (true)
            {
                byte[] header = new byte[3];
                await f.Server.GetStream().ReadExactlyAsync(header, limit.Token);
                byte[] data = new byte[(header[1] << 8) | header[2]];
                await f.Server.GetStream().ReadExactlyAsync(data, limit.Token);
                await response((ClientPacket)decrypt.Decrypt(new ClientPacket(data[0], data[1..])));
            }
        });
        var travel = f.Session.ExecuteAsync(command ?? new("navigate", f.Session.Snapshot.Map, new(3, 1)), limit.Token);
        try
        {
            await Task.WhenAny(server, travel);
            if (server.IsFaulted)
                await server;
            await travel;
        }
        finally
        {
            limit.Cancel();
            try
            {
                await server;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    static Task Ack(MovingObstacleTests.Fixture f, ClientPacket packet)
    {
        var p = f.Session.Snapshot.Position!;
        byte[] ack = new byte[10];
        ack[0] = packet.Data[0];
        BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(1), (ushort)p.X);
        BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(3), (ushort)p.Y);
        return f.Send(11, ack);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Roleplay_is_dismissed_before_recovering_a_rejected_fast_step(bool rejected)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(3016, "Suomi Way", 5, 5, []));
        f.Session.FastMode = true;
        int moves = 0, nexts = 0;
        await Run(f, async packet =>
        {
            switch ((byte)packet.Command)
            {
                case 6:
                    if (++moves == 1)
                    {
                        await f.Send(0x30, Roleplay());
                        if (rejected)
                            await f.Position(new(1, 1));
                        break;
                    }

                    Assert.Equal(1, nexts);
                    await Ack(f, packet);
                    break;
                case 0x3A:
                    var next = new ClientPursuitMessage();
                    next.Deserialize(new NetworkPacketReader(packet));
                    Assert.Equal(2, next.StepId);
                    Assert.Equal(1, ++nexts);
                    await f.Send(0x30, [10, 0]);
                    break;
                case 0x38:
                    Assert.Equal(1, nexts);
                    await f.Position(f.Session.Snapshot.Position!);
                    break;
                default:
                    Assert.Fail("Unexpected travel command " + packet.Command);
                    break;
            }
        });
        Assert.Equal(1, nexts);
        Assert.Equal(new Tile(3, 1), f.Session.Snapshot.Position);
        Assert.Null(f.Session.Snapshot.Dialog);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Undine_warning_closes_then_unequips_and_waits_before_continuing(bool fast, bool duringWalk)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(504, "Undine Village", 5, 5, []));
        f.Session.FastMode = fast;
        Equip(f);
        if (!duringWalk)
            f.Connection.Tracker.Observe(0x30, Warning());
        int moves = 0, closes = 0, removals = 0;
        bool removed = false;
        await Run(f, async packet =>
        {
            switch ((byte)packet.Command)
            {
                case 6:
                    if (duringWalk && ++moves == 1)
                    {
                        await f.Send(0x30, Warning());
                        await f.Position(new(1, 1));
                        break;
                    }

                    Assert.True(removed, "Walking resumed before weapon removal was confirmed");
                    await Ack(f, packet);
                    break;
                case 0x3A:
                    var close = new ClientPursuitMessage();
                    close.Deserialize(new NetworkPacketReader(packet));
                    Assert.Equal(88u, close.EntityId);
                    Assert.Equal(299, close.StepId);
                    Assert.Equal(1, ++closes);
                    await f.Send(0x30, [10, 0]);
                    break;
                case 0x44:
                    Assert.Equal(1, closes);
                    var remove = new ClientRemoveEquipmentMessage();
                    remove.Deserialize(new NetworkPacketReader(packet));
                    Assert.Equal(EquipmentSlot.Weapon, remove.Slot);
                    Assert.Equal(1, ++removals);
                    await Task.Delay(180);
                    removed = true;
                    await f.Send((byte)ServerCommand.RemoveEquip, Payload(new ServerRemoveEquipMessage { Slot = EquipmentSlot.Weapon }));
                    break;
                case 0x38:
                    Assert.True(removed);
                    await f.Position(f.Session.Snapshot.Position!);
                    break;
                default:
                    Assert.Fail("Unexpected travel command " + packet.Command);
                    break;
            }
        });
        Assert.Equal(1, closes);
        Assert.Equal(1, removals);
        Assert.Equal(new Tile(3, 1), f.Session.Snapshot.Position);
        Assert.False(f.Session.Snapshot.Equipment.ContainsKey(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closing_entry_notice_can_finish_map_transfer_then_continue_destination_route(bool fast)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(504, "Synthetic entry", 5, 5, [new(new(2, 1), 3016, new(1, 1))]), additional: [new(3016, "Suomi Way", 5, 5, [])]);
        f.Session.FastMode = fast;
        int nexts = 0;
        await Run(f, async packet =>
        {
            if ((byte)packet.Command == 0x3A)
            {
                Assert.Equal(1, ++nexts);
                await f.Map(3016, 5, 5);
                await f.Position(new(1, 1));
                return;
            }

            Assert.Equal((byte)6, (byte)packet.Command);
            if (f.Session.Snapshot.Map == 504)
            {
                await f.Send(0x30, Roleplay());
                return;
            }

            Assert.Equal(1, nexts);
            await Ack(f, packet);
        }, new("navigate", 3016, new(3, 1)));
        Assert.Equal(1, nexts);
        Assert.Equal(3016, f.Session.Snapshot.Map);
        Assert.Equal(new Tile(3, 1), f.Session.Snapshot.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_weapon_removal_confirmation_never_walks_or_resends(bool full)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(504, "Undine Village", 5, 5, []));
        Equip(f);
        f.Connection.Tracker.Observe(0x30, Warning());
        if (full)
            for (byte slot = 1; slot <= 59; slot++)
                f.Connection.Tracker.Observe(15, Payload(new ServerAddInventoryMessage { Slot = slot, Name = "Item", Quantity = 1 }));
        int removals = 0;
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(f, async packet =>
        {
            if ((byte)packet.Command == 0x3A)
            {
                await f.Send(0x30, [10, 0]);
                return;
            }

            Assert.Equal((byte)0x44, (byte)packet.Command);
            Assert.Equal(1, ++removals);
        }));
        Assert.Equal(full ? 0 : 1, removals);
        Assert.Equal(new Tile(1, 1), f.Session.Snapshot.Position);
    }

    [Theory]
    [InlineData(500, "You can't bring weapons here!")]
    [InlineData(504, "Do you want to buy a weapon?")]
    public async Task Unrelated_popup_never_removes_equipment(int map, string text)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(map, "Other", 5, 5, []));
        Equip(f);
        f.Connection.Tracker.Observe(0x30, Warning(text));
        await Run(f, async packet =>
        {
            if ((byte)packet.Command == 0x3A)
            {
                await f.Send(0x30, [10, 0]);
                return;
            }

            Assert.Equal((byte)6, (byte)packet.Command);
            await Ack(f, packet);
        });
        Assert.True(f.Session.Snapshot.Equipment.ContainsKey(1));
        Assert.Equal(new Tile(3, 1), f.Session.Snapshot.Position);
    }

    [Fact]
    public async Task Resume_after_full_inventory_still_removes_weapon_after_warning_was_closed()
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(504, "Undine Village", 5, 5, []));
        Equip(f);
        f.Connection.Tracker.Observe(0x30, Warning());
        for (byte slot = 1; slot <= 59; slot++)
            f.Connection.Tracker.Observe(15, Payload(new ServerAddInventoryMessage { Slot = slot, Name = "Item", Quantity = 1 }));
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(f, async packet =>
        {
            Assert.Equal((byte)0x3A, (byte)packet.Command);
            await f.Send(0x30, [10, 0]);
        }));
        Assert.Null(f.Session.Snapshot.Dialog);
        f.Connection.Tracker.Observe(16, [59]);
        bool removed = false;
        await Run(f, async packet =>
        {
            if ((byte)packet.Command == 0x44)
            {
                removed = true;
                await f.Send((byte)ServerCommand.RemoveEquip, Payload(new ServerRemoveEquipMessage { Slot = EquipmentSlot.Weapon }));
                return;
            }

            Assert.True(removed, "Resume forgot the weapon warning after closing its popup");
            Assert.Equal((byte)6, (byte)packet.Command);
            await Ack(f, packet);
        });
        Assert.True(removed);
        Assert.Equal(new Tile(3, 1), f.Session.Snapshot.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Weapon_warning_is_remembered_when_map_transfer_clears_popup_before_walker_runs(bool fast)
    {
        await using var f = await MovingObstacleTests.Fixture.Create(record: new MapRecord(504, "Undine Village", 5, 5, []), additional: [new(3008, "Undine Village Way", 5, 5, [])]);
        f.Session.FastMode = fast;
        Equip(f);
        f.Connection.Tracker.Observe(0x30, Warning());
        long beforeTransfer = f.Session.Snapshot.Revision;
        await f.Map(3008, 5, 5);
        await f.Position(new(1, 1));
        await f.Session.WaitAsync(s => s.Map == 3008 && s.Position == new Tile(1, 1), beforeTransfer, TimeSpan.FromSeconds(3), default);
        Assert.Null(f.Session.Snapshot.Dialog);
        bool removed = false;
        await Run(f, async packet =>
        {
            if ((byte)packet.Command == 0x44)
            {
                Assert.False(removed);
                removed = true;
                await f.Send((byte)ServerCommand.RemoveEquip, Payload(new ServerRemoveEquipMessage { Slot = EquipmentSlot.Weapon }));
                return;
            }
            Assert.True(removed, "Map transfer lost the weapon restriction before walking resumed");
            Assert.Equal((byte)6, (byte)packet.Command);
            await Ack(f, packet);
        }, new("navigate", 3008, new(3, 1)));
        Assert.True(removed);
    }
}
