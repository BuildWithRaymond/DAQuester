using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class MovingObstacleTests
{
    [Fact]
    public async Task Ground_collection_picks_only_matching_sprite_and_stops_on_inventory_evidence()
    {
        await using var f = await Fixture.Create();
        f.Connection.Tracker.Observe(8, Packet(new ServerStatusMessage { Fields = Arbiter.Net.Types.StatsFieldFlags.Vitals, Health = 100 }).Data);
        f.Connection.Tracker.Observe(7, Packet(new ServerDrawObjectsMessage { Entities = [new Arbiter.Net.Server.Types.ServerItemEntity { Id = 77, X = 2, Y = 1, Sprite = 569 }, new Arbiter.Net.Server.Types.ServerItemEntity { Id = 78, X = 3, Y = 1, Sprite = 2286 }] }).Data);
        await f.Walk(new(4, 1), (_, _) => Task.FromResult(false), new("collect-ground", 500, new(4, 1), Amount: 569, StopWhen: new("Undine Mushroom", 5)), async packet =>
        {
            var get = new Arbiter.Net.Client.Messages.ClientGetMessage();
            get.Deserialize(new NetworkPacketReader(packet));
            Assert.Equal(2, get.X);
            Assert.Equal(1, get.Y);
            await f.Send(15, Packet(new ServerAddInventoryMessage { Slot = get.Slot, Name = "Undine Mushroom", Sprite = 569, Quantity = 5 }).Data);
            await f.Send(14, [0, 0, 0, 77]);
        });
        Assert.Equal(5, f.Session.Snapshot.Count("Undine Mushroom"));
        Assert.Single(f.Commands, c => c == 7);
        Assert.Equal(new Tile(2, 1), f.Session.Snapshot.Position);
        Assert.Contains(78u, f.Session.Snapshot.GroundItems.Keys);
    }

    static ServerPacket Packet(Arbiter.Net.Server.Messages.ServerMessage message)
    {
        var b = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try
        {
            message.Serialize(ref b);
            return (ServerPacket)b.ToPacket();
        }
        finally
        {
            b.Dispose();
        }
    }

    [Fact]
    public async Task Mushrooms_appearing_during_approach_are_picked_before_arrival()
    {
        await using var f = await Fixture.Create();
        f.Connection.Tracker.Observe(8, Packet(new ServerStatusMessage { Fields = Arbiter.Net.Types.StatsFieldFlags.Vitals, Health = 100 }).Data);
        await f.Walk(new(4, 1), (moves, _) =>
        {
            if (moves == 1)
                f.Connection.Tracker.Observe(7, Packet(new ServerDrawObjectsMessage { Entities = [new Arbiter.Net.Server.Types.ServerItemEntity { Id = 77, X = 3, Y = 1, Sprite = 569 }] }).Data);
            return Task.FromResult(false);
        }, new("collect-ground", 500, new(4, 1), Amount: 569, StopWhen: new("Undine Mushroom", 5)), async packet =>
        {
            var get = new Arbiter.Net.Client.Messages.ClientGetMessage();
            get.Deserialize(new NetworkPacketReader(packet));
            Assert.Equal(3, get.X);
            await f.Send(15, Packet(new ServerAddInventoryMessage { Slot = get.Slot, Name = "Undine Mushroom", Sprite = 569, Quantity = 5 }).Data);
            await f.Send(14, [0, 0, 0, 77]);
        });
        Assert.Equal(5, f.Session.Snapshot.Count("Undine Mushroom"));
        Assert.Equal(new Tile(3, 1), f.Session.Snapshot.Position);
        Assert.Single(f.Commands, c => c == 7);
    }

    [Fact]
    public async Task Another_player_forces_pickup_detour_and_item_removal_alone_never_counts_as_collection()
    {
        await using var f = await Fixture.Create();
        f.Connection.Tracker.Observe(8, Packet(new ServerStatusMessage { Fields = Arbiter.Net.Types.StatsFieldFlags.Vitals, Health = 100 }).Data);
        f.Connection.Tracker.Observe(0x33, Human(99, new(2, 1)).Data);
        f.Connection.Tracker.Observe(7, Packet(new ServerDrawObjectsMessage { Entities = [new Arbiter.Net.Server.Types.ServerItemEntity { Id = 77, X = 3, Y = 1, Sprite = 569 }] }).Data);
        var visited = new List<Tile>();
        await f.Walk(new(4, 1), (_, next) =>
        {
            Assert.NotEqual(new Tile(2, 1), next);
            visited.Add(next);
            return Task.FromResult(false);
        }, new("collect-ground", 500, new(4, 1), Amount: 569, StopWhen: new("Undine Mushroom", 1)), async _ => await f.Send(14, [0, 0, 0, 77]));
        Assert.Contains(visited, t => t.Y != 1);
        Assert.Equal(0, f.Session.Snapshot.Count("Undine Mushroom"));
        Assert.Equal(new Tile(4, 1), f.Session.Snapshot.Position);
        Assert.Single(f.Commands, c => c == 7);
    }

    [Fact]
    public void Player_appearance_movement_removal_and_self_are_tracked_as_obstacles()
    {
        var tracker = new StateTracker(1);
        tracker.Identity("Nuclear", 42);
        tracker.Observe(0x33, Human(99, new(2, 1)).Data);
        Assert.True(tracker.Snapshot.Entities[99].Blocks);
        Assert.Equal(new Tile(2, 1), tracker.Snapshot.Entities[99].Position);
        tracker.Observe(0x0C, [0, 0, 0, 99, 0, 2, 0, 1, 2, 0]);
        Assert.Equal(new Tile(2, 2), tracker.Snapshot.Entities[99].Position);
        tracker.Observe(0x33, Human(42, new(1, 1)).Data);
        Assert.DoesNotContain(tracker.Snapshot.Entities.Values, e => e.Id == 42 && e.Blocks);
        tracker.Observe(0x0E, [0, 0, 0, 99]);
        Assert.False(tracker.Snapshot.Entities.ContainsKey(99));
    }

    [Fact]
    public async Task Known_player_in_straight_path_is_routed_around()
    {
        await using var fixture = await Fixture.Create();
        fixture.Connection.Tracker.Observe(0x33, Human(99, new(2, 1)).Data);
        var visited = new List<Tile>();
        await fixture.Walk(new(4, 1), (n, next) =>
        {
            Assert.NotEqual(new Tile(2, 1), next);
            visited.Add(next);
            return Task.FromResult(false);
        });
        Assert.Contains(visited, p => p.Y != 1);
        Assert.Equal(new Tile(4, 1), fixture.Session.Snapshot.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Player_rejection_with_authoritative_position_replans_without_an_unanswered_refresh(bool fast)
    {
        await using var f = await Fixture.Create(fast ? 160 : 1);
        if (fast)
        {
            f.Session.FastMode = false;
            await f.Walk(new(1, 2), (_, _) => Task.FromResult(false));
            await f.Walk(new(1, 1), (_, _) => Task.FromResult(false));
        }
        f.Session.FastMode = fast;
        var occupied = new Tile(2, 1);
        await f.Walk(new(4, 1), async (moves, next) =>
        {
            if (moves == 1)
            {
                await f.Send(0x33, Human(99, occupied).Data);
                await f.Position(new(1, 1));
                return true;
            }
            Assert.NotEqual(occupied, next);
            return false;
        }, refresh: () => Task.CompletedTask);
        Assert.Equal(new Tile(4, 1), f.Session.Snapshot.Position);
        Assert.DoesNotContain((byte)0x38, f.Commands);
    }

    [Fact]
    public async Task Player_arriving_during_movement_cadence_is_routed_around()
    {
        await using var fixture = await Fixture.Create(200);
        var occupied = new Tile(3, 1);
        Task arrival = Task.CompletedTask;
        await fixture.Walk(new(4, 1), (n, next) =>
        {
            if (n == 1)
                arrival = Task.Run(async () =>
                {
                    await Task.Delay(30);
                    fixture.Connection.Tracker.Observe(0x33, Human(99, occupied).Data);
                });
            if (n > 1)
                Assert.NotEqual(occupied, next);
            return Task.FromResult(false);
        });
        await arrival;
        Assert.Equal(new Tile(4, 1), fixture.Session.Snapshot.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Blocked_move_refreshes_without_looking_at_sign_and_detours(bool rejectionAck)
    {
        await using var fixture = await Fixture.Create();
        var blocked = new Tile(2, 1);
        await fixture.Walk(new(4, 1), async (n, next) =>
        {
            if (n == 1)
            {
                Assert.Equal(blocked, next);
                if (rejectionAck)
                    await fixture.Send(11, [4, 0, 1, 0, 1, 0, 0, 0, 0, 0]);
                return true;
            }

            Assert.NotEqual(blocked, next);
            return false;
        });
        if (rejectionAck)
            Assert.DoesNotContain((byte)0x38, fixture.Commands);
        else
            Assert.Contains((byte)0x38, fixture.Commands);
        Assert.DoesNotContain((byte)0x09, fixture.Commands);
        Assert.Equal(new Tile(4, 1), fixture.Session.Snapshot.Position);
    }

    [Fact]
    public async Task Occupied_patrol_tile_is_skipped_and_next_patrol_tile_remains_reachable()
    {
        await using var fixture = await Fixture.Create();
        fixture.Connection.Tracker.Observe(0x33, Human(99, new(2, 1)).Data);
        await fixture.Session.ExecuteAsync(new("navigate", 500, new(2, 1), StopWhen: new("Branch", 6)), default);
        Assert.Equal(new Tile(1, 1), fixture.Session.Snapshot.Position);
        await fixture.Walk(new(4, 1), (n, next) =>
        {
            Assert.NotEqual(new Tile(2, 1), next);
            return Task.FromResult(false);
        });
    }

    [Fact]
    public async Task Same_map_refresh_during_move_waits_for_position_instead_of_counting_corrections()
    {
        await using var f = await Fixture.Create();
        await f.Walk(new(4, 1), async (_, next) =>
        {
            await f.Send(21, [1, 244, 5, 5, 0, 0, 0, 0, 0, 0]);
            await Task.Delay(40);
            await f.Position(next);
            return true;
        }, refresh: () =>
        {
            Assert.Fail("A valid same-map reload was treated as a rejected movement.");
            return Task.CompletedTask;
        });
        Assert.Equal(new Tile(4, 1), f.Session.Snapshot.Position);
        Assert.DoesNotContain((byte)0x38, f.Commands);
    }

    [Fact]
    public async Task Lag_recovery_blocks_steps_until_authoritative_refresh_position()
    {
        await using var f = await Fixture.Create();
        bool refreshing = false;
        Task reply = Task.CompletedTask;
        try
        {
            await f.Walk(new(4, 1), async (n, _) =>
            {
                Assert.False(refreshing, "A new step was sent before refresh position arrived.");
                if (n == 1)
                {
                    return true;
                }

                return false;
            }, refresh: () =>
            {
                refreshing = true;
                reply = Task.Run(async () =>
                {
                    await Task.Delay(200);
                    await f.Send(11, [4, 0, 1, 0, 1, 0, 0, 0, 0, 0]);
                    await Task.Delay(250);
                    refreshing = false;
                    await f.Position(new(1, 1));
                });
                return Task.CompletedTask;
            });
            Assert.Equal(new Tile(4, 1), f.Session.Snapshot.Position);
            Assert.Single(f.Commands, c => c == 0x38);
        }
        finally
        {
            await reply;
        }
    }

    [Theory]
    [InlineData(3079, 14, false)]
    [InlineData(3080, 7, false)]
    [InlineData(3079, 14, true)]
    [InlineData(3080, 7, true)]
    public async Task Threshold_route_uses_recorded_same_map_transfer_without_walking_through_walls(int map, int width, bool fast)
    {
        var record = JsonSerializer.Deserialize<MapRecord>("{\"Id\":" + map + ",\"Name\":\"Threshold\",\"Width\":" + width + ",\"Height\":7,\"Portals\":[{\"From\":{\"X\":4,\"Y\":6},\"Map\":500,\"To\":{\"X\":1,\"Y\":1}}],\"InternalTransfers\":[{\"From\":{\"X\":3,\"Y\":2},\"To\":{\"X\":3,\"Y\":5}}]}")!;
        await using var f = await Fixture.Create(record: record, origin: new(3, 1), wall: (_, y) => y is 3 or 4);
        f.Session.FastMode = fast;
        int crossings = 0;
        await f.Walk(new(4, 1), async (_, next) =>
        {
            if (f.Session.Snapshot.Map != map)
                return false;
            Assert.DoesNotContain(next.Y, new[] { 3, 4 });
            if (next == new Tile(3, 2))
            {
                crossings++;
                await f.Map(map, width, 7);
                await Task.Delay(30);
                await f.Position(new(3, 5));
                return true;
            }

            if (next == new Tile(4, 6))
            {
                await f.Map(500, 5, 5);
                await f.Position(new(1, 1));
                return true;
            }

            return false;
        });
        Assert.Equal(1, crossings);
        Assert.Equal(500, f.Session.Snapshot.Map);
        Assert.Equal(new Tile(4, 1), f.Session.Snapshot.Position);
        Assert.DoesNotContain((byte)0x38, f.Commands);
    }

    [Fact]
    public async Task Stop_cancels_refresh_wait_without_sending_another_step()
    {
        await using var f = await Fixture.Create();
        using var stop = new CancellationTokenSource(3500);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Walk(new(4, 1), async (_, _) =>
        {
            await Task.CompletedTask;
            return true;
        }, refresh: () => Task.CompletedTask, cancellation: stop.Token));
        Assert.Single(f.Commands, c => c == 6);
        Assert.Single(f.Commands, c => c == 0x38);
    }

    internal static ServerPacket Human(uint id, Tile tile)
    {
        var message = new ServerDrawHumanObjectsMessage
        {
            EntityId = id,
            X = (ushort)tile.X,
            Y = (ushort)tile.Y,
            Name = id == 42 ? "Nuclear" : "OtherPlayer"
        };
        var builder = new NetworkPacketBuilder(ServerCommand.DrawHumanObjects);
        try
        {
            message.Serialize(ref builder);
            return (ServerPacket)builder.ToPacket();
        }
        finally
        {
            builder.Dispose();
        }
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public required ProxyHost Host;
        public required TcpClient Client, Server;
        public required ProxyConnection Connection;
        public required GameSession Session;
        readonly ServerPacketEncryptor encrypt = new();
        readonly ClientPacketEncryptor decrypt = new();
        // A projected walk can finish while the fake server has read only part of
        // its frame. Keep those bytes for the next Walk instead of losing alignment.
        readonly FrameBuffer serverFrames = new();
        readonly byte[] serverBytes = new byte[8192];
        byte ordinal = 20;
        readonly SemaphoreSlim serverWrites = new(1, 1);
        public List<byte> Commands { get; } = [];

        public static async Task<Fixture> Create(int cadence = 1, MapRecord? record = null, Tile? origin = null, Func<int, int, bool>? wall = null, MapRecord[]? additional = null)
        {
            record ??= new(500, "Mileth", 5, 5, []);
            origin ??= new(1, 1);
            string folder = Path.Combine(Path.GetTempPath(), "DAQuester-moving-" + Guid.NewGuid());
            Directory.CreateDirectory(Path.Combine(folder, "maps"));
            await File.WriteAllBytesAsync(Path.Combine(folder, "maps", "lod500.map"), new byte[5 * 5 * 6]);
            var asset = new byte[record.Width * record.Height * 6];
            for (int y = 0; y < record.Height; y++)
                for (int x = 0; x < record.Width; x++)
                    if (wall?.Invoke(x, y) == true)
                        asset[(y * record.Width + x) * 6 + 2] = 1;
            await File.WriteAllBytesAsync(Path.Combine(folder, "maps", "lod" + record.Id + ".map"), asset);
            using (var writer = new BinaryWriter(File.Create(Path.Combine(folder, "ia.dat"))))
            {
                writer.Write(2);
                writer.Write(25);
                writer.Write(Encoding.ASCII.GetBytes("sotp.dat".PadRight(13, '\0')));
                writer.Write(26);
                writer.Write((byte)15);
            }

            var records = new[]
            {
                record
            }.Concat(additional ?? []).DistinctBy(r => r.Id).ToList();
            if (records.All(r => r.Id != 500))
                records.Add(new(500, "Mileth", 5, 5, []));
            foreach (var extra in records.Where(r => r.Id != record.Id))
                await File.WriteAllBytesAsync(Path.Combine(folder, "maps", "lod" + extra.Id + ".map"), new byte[extra.Width * extra.Height * 6]);
            string routes = Path.Combine(folder, "routes.json");
            await File.WriteAllTextAsync(routes, JsonSerializer.Serialize(records));
            using var remote = new TcpListener(IPAddress.Loopback, 0);
            remote.Start();
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var connected = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = new ProxyHost();
            host.Connected += c => connected.SetResult(c);
            await host.StartAsync(port, (IPEndPoint)remote.LocalEndpoint);
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            var server = await remote.AcceptTcpClientAsync();
            var connection = await connected.Task;
            connection.Tracker.Identity("Nuclear", 42);
            connection.Tracker.Observe(0x15, [(byte)(record.Id >> 8), (byte)record.Id, (byte)record.Width, (byte)record.Height, 0, 0, 0, 0, 0, 0]);
            connection.Tracker.Observe(4, [0, (byte)origin.X, 0, (byte)origin.Y, 0, 0, 0, 0]);
            connection.Tracker.Observe(0x58, [0]);
            return new()
            {
                Host = host,
                Client = client,
                Server = server,
                Connection = connection,
                Session = new(connection, new(folder, routes), cadence)
            };
        }

        public async Task Send(byte opcode, byte[] payload)
        {
            await serverWrites.WaitAsync();
            try
            {
                await Server.GetStream().WriteAsync(encrypt.Encrypt(new ServerPacket(opcode, payload), ordinal++).ToArray());
            }
            finally
            {
                serverWrites.Release();
            }
        }

        public Task Position(Tile p)
        {
            byte[] position = new byte[8];
            BinaryPrimitives.WriteUInt16BigEndian(position, (ushort)p.X);
            BinaryPrimitives.WriteUInt16BigEndian(position.AsSpan(2), (ushort)p.Y);
            return Send(4, position);
        }

        public Task Map(int map, int width, int height) => Send(21, [(byte)(map >> 8), (byte)map, (byte)width, (byte)height, 0, 0, 0, 0, 0, 0]);
        public async Task Walk(Tile target, Func<int, Tile, Task<bool>> intercept, GameCommand? command = null, Func<ClientPacket, Task>? pickup = null, Func<Task>? refresh = null, CancellationToken cancellation = default, bool projectedServer = false)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            limit.CancelAfter(TimeSpan.FromSeconds(12));
            int moves = 0;
            var serverPosition = Session.Snapshot.Position!;
            var response = Task.Run(async () =>
            {
                while (true)
                {
                    byte[]? frame;
                    while ((frame = serverFrames.Read()) is null)
                    {
                        int read = await Server.GetStream().ReadAsync(serverBytes, limit.Token);
                        if (read == 0)
                            throw new EndOfStreamException("Synthetic game connection closed before its next request.");
                        serverFrames.Append(serverBytes[..read]);
                    }
                    var packet = (ClientPacket)decrypt.Decrypt(new ClientPacket(frame[3], frame[4..]));
                    byte opcode = (byte)packet.Command;
                    Commands.Add(opcode);
                    if (opcode == 7 && pickup is not null)
                    {
                        await pickup(packet);
                        continue;
                    }

                    if (opcode != 6)
                    {
                        Assert.Equal((byte)0x38, opcode);
                        if (refresh is not null)
                            await refresh();
                        else
                            await Position(Session.Snapshot.Position ?? serverPosition);
                        continue;
                    }

                    // A map packet clears client position while an earlier move can still be
                    // in flight. The simulated server retains its position during that gap.
                    byte direction = packet.Data[0];
                    var before = projectedServer ? serverPosition : Session.Snapshot.Position ?? serverPosition;
                    var next = StateTracker.GridStep(before, direction);
                    serverPosition = next;
                    if (await intercept(++moves, next))
                        continue;
                    byte[] ack = new byte[10];
                    ack[0] = direction;
                    BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(1), (ushort)before.X);
                    BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(3), (ushort)before.Y);
                    await Send(11, ack);
                }
            });
            var navigation = Session.ExecuteAsync(command ?? new("navigate", 500, target), limit.Token);
            try
            {
                await Task.WhenAny(navigation, response);
                if (response.IsFaulted)
                    await response;
                await navigation;
            }
            finally
            {
                limit.Cancel();
                try
                {
                    await response;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            Server.Dispose();
            await Host.DisposeAsync();
        }
    }
}
