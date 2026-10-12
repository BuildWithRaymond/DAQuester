using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Buffers.Binary;
using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Serialization;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

public class WorldTravelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Suomi_entry_next_is_sent_once_before_walking_or_recovery(bool arrivesDuringWalk)
    {
        string folder = Path.Combine(Path.GetTempPath(), "DAQuester-notice-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, "maps"));
        await File.WriteAllBytesAsync(Path.Combine(folder, "maps", "lod3016.map"), new byte[5 * 5 * 6]);
        using (var w = new BinaryWriter(File.Create(Path.Combine(folder, "ia.dat"))))
        {
            w.Write(2);
            w.Write(25);
            w.Write(Encoding.ASCII.GetBytes("sotp.dat".PadRight(13, '\0')));
            w.Write(26);
            w.Write((byte)0);
        }

        string routes = Path.Combine(folder, "routes.json");
        await File.WriteAllTextAsync(routes, JsonSerializer.Serialize(new[] { new MapRecord(3016, "Suomi Way", 5, 5, []) }));
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var connected = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new ProxyHost();
        host.Connected += c => connected.SetResult(c);
        await host.StartAsync(port, (IPEndPoint)remote.LocalEndpoint);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await remote.AcceptTcpClientAsync();
        var connection = await connected.Task;
        connection.Tracker.Identity("Nuclear", 42);
        connection.Tracker.Observe(0x15, [11, 200, 5, 5, 0, 0, 0, 0, 0, 0]);
        connection.Tracker.Observe(4, [0, 1, 0, 1, 0, 0, 0, 0]);
        var notice = PerfectHairstyleLiveTests.Load().Events.Single(e => e.Revision == 711);
        byte[] payload = Convert.FromBase64String(notice.Payload);
        if (!arrivesDuringWalk)
            connection.Tracker.Observe(0x30, payload);
        var session = new GameSession(connection, new(folder, routes), 1);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var encrypt = new ServerPacketEncryptor();
        var decrypt = new ClientPacketEncryptor();
        byte ordinal = 20;
        int nexts = 0, moves = 0;
        bool dismissed = false;
        async Task Send(byte opcode, byte[] data) => await server.GetStream().WriteAsync(encrypt.Encrypt(new ServerPacket(opcode, data), ordinal++).ToArray(), limit.Token);
        var response = Task.Run(async () =>
        {
            while (true)
            {
                byte[] header = new byte[3];
                await server.GetStream().ReadExactlyAsync(header, limit.Token);
                byte[] bytes = new byte[(header[1] << 8) | header[2]];
                await server.GetStream().ReadExactlyAsync(bytes, limit.Token);
                var p = (ClientPacket)decrypt.Decrypt(new ClientPacket(bytes[0], bytes[1..]));
                if ((byte)p.Command == 0x3A)
                {
                    var m = new ClientPursuitMessage();
                    m.Deserialize(new NetworkPacketReader(p));
                    Assert.Equal((byte)4, (byte)m.EntityType);
                    Assert.Equal(11091u, m.EntityId);
                    Assert.Equal(0, m.PursuitId);
                    Assert.Equal(2, m.StepId);
                    Assert.Equal(Arbiter.Net.Types.DialogArgsType.None, m.ArgsType);
                    Assert.Equal(1, ++nexts);
                    dismissed = true;
                    await Send(0x30, [10, 0]);
                    continue;
                }

                Assert.Equal((byte)6, (byte)p.Command);
                moves++;
                if (arrivesDuringWalk && moves == 1)
                {
                    await Send(0x30, payload);
                    await Send(4, [0, 1, 0, 1, 0, 0, 0, 0]);
                    continue;
                }

                Assert.True(dismissed, "Movement attempted while town popup remained open");
                var before = session.Snapshot;
                byte[] ack = new byte[10];
                ack[0] = p.Data[0];
                BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(1), (ushort)before.Position!.X);
                BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(3), (ushort)before.Position.Y);
                await Send(11, ack);
            }
        });
        var navigation = session.ExecuteAsync(new("navigate", 3016, new(2, 2)), limit.Token);
        try
        {
            await Task.WhenAny(navigation, response);
            if (response.IsFaulted)
                await response;
            await navigation;
            Assert.Equal(new Tile(2, 2), session.Snapshot.Position);
            Assert.Equal(1, nexts);
            Assert.Null(session.Snapshot.Dialog);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Gateway_selection_uses_live_checksum_then_walks_to_interior(bool walkToField)
    {
        string folder = Path.Combine(Path.GetTempPath(), "DAQuester-world-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, "maps"));
        foreach (int id in new[]
        {
            500,
            3016,
            950
        }

        )
            await File.WriteAllBytesAsync(Path.Combine(folder, "maps", "lod" + id + ".map"), new byte[5 * 5 * 6]);
        using (var w = new BinaryWriter(File.Create(Path.Combine(folder, "ia.dat"))))
        {
            w.Write(2);
            w.Write(25);
            w.Write(Encoding.ASCII.GetBytes("sotp.dat".PadRight(13, '\0')));
            w.Write(26);
            w.Write((byte)0);
        }

        string routes = Path.Combine(folder, "routes.json");
        await File.WriteAllTextAsync(routes, JsonSerializer.Serialize(new[] { new MapRecord(500, "Start", 5, 5, [new(new(2, 1), 3016, new(1, 1), true)]), new MapRecord(3016, "Suomi Way", 5, 5, [new(new(2, 1), 950, new(1, 1))]), new MapRecord(950, "Inn", 5, 5, []) }));
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var connected = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new ProxyHost();
        host.Connected += c => connected.SetResult(c);
        await host.StartAsync(port, (IPEndPoint)remote.LocalEndpoint);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await remote.AcceptTcpClientAsync();
        var connection = await connected.Task;
        connection.Tracker.Identity("Nuclear", 42);
        connection.Tracker.Observe(0x15, [1, 244, 5, 5, 0, 0, 0, 0, 0, 0]);
        connection.Tracker.Observe(4, [0, 1, 0, 1, 0, 0, 0, 0]);
        var field = new ServerFieldMapMessage
        {
            FieldName = "Suomi",
            Locations = [new ServerWorldMapNode
            {
                MapId = 3016,
                MapX = 1,
                MapY = 1,
                Checksum = 9876,
                Name = "Suomi"
            }

            ]
        };
        byte[] Field()
        {
            var b = new NetworkPacketBuilder(ServerCommand.FieldMap);
            try
            {
                field.Serialize(ref b);
                return b.ToPacket().Data;
            }
            finally
            {
                b.Dispose();
            }
        }

        if (!walkToField)
            connection.Tracker.Observe(0x2E, Field());
        var session = new GameSession(connection, new(folder, routes), 1);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var encrypt = new ServerPacketEncryptor();
        var decrypt = new ClientPacketEncryptor();
        byte ordinal = 20;
        int selections = 0;
        async Task Send(byte opcode, byte[] data) => await server.GetStream().WriteAsync(encrypt.Encrypt(new ServerPacket(opcode, data), ordinal++).ToArray(), limit.Token);
        async Task Map(int map)
        {
            await Send(0x15, [(byte)(map >> 8), (byte)map, 5, 5, 0, 0, 0, 0, 0, 0]);
            await Send(4, [0, 1, 0, 1, 0, 0, 0, 0]);
        }

        var response = Task.Run(async () =>
        {
            while (true)
            {
                byte[] header = new byte[3];
                await server.GetStream().ReadExactlyAsync(header, limit.Token);
                byte[] bytes = new byte[(header[1] << 8) | header[2]];
                await server.GetStream().ReadExactlyAsync(bytes, limit.Token);
                var p = (ClientPacket)decrypt.Decrypt(new ClientPacket(bytes[0], bytes[1..]));
                if ((byte)p.Command == 0x3F)
                {
                    var m = new ClientFieldMapMessage();
                    m.Deserialize(new NetworkPacketReader(p));
                    Assert.Equal(3016, m.MapId);
                    Assert.Equal(9876, m.Checksum);
                    Assert.Equal(1, m.X);
                    Assert.Equal(1, m.Y);
                    selections++;
                    await Map(3016);
                    continue;
                }

                Assert.Equal((byte)6, (byte)p.Command);
                var before = session.Snapshot;
                var next = StateTracker.GridStep(before.Position!, p.Data[0]);
                if (before.Map == 500)
                {
                    await Send(0x2E, Field());
                    continue;
                }

                if (before.Map == 3016)
                {
                    await Map(950);
                    continue;
                }

                byte[] ack = new byte[10];
                ack[0] = p.Data[0];
                BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(1), (ushort)before.Position!.X);
                BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(3), (ushort)before.Position.Y);
                await Send(11, ack);
            }
        });
        var navigation = session.ExecuteAsync(new("navigate", 950, new(2, 2)), limit.Token);
        try
        {
            await Task.WhenAny(navigation, response);
            if (response.IsFaulted)
                await response;
            await navigation;
            Assert.Equal(950, session.Snapshot.Map);
            Assert.Equal(new Tile(2, 2), session.Snapshot.Position);
            Assert.Equal(1, selections);
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
}
