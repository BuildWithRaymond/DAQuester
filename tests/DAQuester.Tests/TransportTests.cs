using Xunit;
using System.Net;
using System.Net.Sockets;
using DAQuester.DarkAges.Transport;

namespace DAQuester.Tests;

public class TransportTests
{
    [Fact]
    public async Task New_proxy_hosts_do_not_reuse_session_generations()
    {
        async Task<long> Generation()
        {
            using var remote = new TcpListener(IPAddress.Loopback, 0);
            remote.Start();
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var ready = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var host = new ProxyHost();
            host.Connected += c => ready.SetResult(c);
            await host.StartAsync(port, (IPEndPoint)remote.LocalEndpoint);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var server = await remote.AcceptTcpClientAsync();
            return (await ready.Task.WaitAsync(TimeSpan.FromSeconds(3))).Tracker.Snapshot.Generation;
        }

        long first = await Generation(), second = await Generation();
        Assert.NotEqual(first, second);
        Assert.InRange(second, 1, 9007199254740991L);
    }

    [Fact]
    public async Task Merchant_dismissal_is_visible_and_does_not_invent_server_evidence()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ready = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new ProxyHost();
        host.Connected += c => ready.SetResult(c);
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        await host.StartAsync(port, (IPEndPoint)listener.LocalEndpoint);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await listener.AcceptTcpClientAsync();
        var connection = await ready.Task;
        connection.Tracker.Identity("Nuclear", 42);
        var menu = new Arbiter.Net.Server.Messages.ServerScreenMenuMessage
        {
            EntityId = 99,
            EntityType = Arbiter.Net.Types.EntityTypeFlags.Creature,
            Name = "Cassidy",
            Content = "Welcome",
            MenuType = Arbiter.Net.Types.DialogMenuType.Menu
        };
        var builder = new Arbiter.Net.Serialization.NetworkPacketBuilder(Arbiter.Net.Server.ServerCommand.ScreenMenu);
        Arbiter.Net.Server.ServerPacket packet;
        try
        {
            menu.Serialize(ref builder);
            packet = (Arbiter.Net.Server.ServerPacket)builder.ToPacket();
        }
        finally
        {
            builder.Dispose();
        }

        var crypto = new Arbiter.Net.Server.ServerPacketEncryptor();
        // This watchdog includes temporary-file I/O and first-use JIT on CI.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await server.GetStream().WriteAsync(crypto.Encrypt(packet, 20).ToArray(), timeout.Token);
        await ReadFrame(client.GetStream(), timeout.Token);
        var before = connection.Tracker.Snapshot;
        Assert.NotNull(before.Dialog);
        string routes = Path.Combine(Path.GetTempPath(), "DAQuester-routes-" + Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(routes, "[]");
        var session = new DAQuester.DarkAges.GameSession(connection, new DAQuester.DarkAges.Navigation.MapRepository("", routes));
        await session.ExecuteAsync(new("close", Dialog: before.Dialog), timeout.Token);
        byte[] wire = await ReadFrame(client.GetStream(), timeout.Token);
        Assert.Equal((byte)48, wire[3]);
        Assert.Equal((byte)21, wire[4]);
        Assert.Equal(new byte[] { 10, 0 }, crypto.Decrypt(new Arbiter.Net.Server.ServerPacket(wire[3], wire[4..])).Data);
        Assert.Equal(before.Revision, session.Snapshot.Revision);
        Assert.Same(before.Dialog, session.Snapshot.Dialog);
        await server.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Server.ServerPacket(1, [0]), 21).ToArray(), timeout.Token);
        Assert.Equal((byte)22, (await ReadFrame(client.GetStream(), timeout.Token))[4]);
    }

    static async Task<byte[]> ReadFrame(NetworkStream stream, CancellationToken ct)
    {
        byte[] header = new byte[3];
        await stream.ReadExactlyAsync(header, ct);
        byte[] data = new byte[(header[1] << 8) | header[2]];
        await stream.ReadExactlyAsync(data, ct);
        return header.Concat(data).ToArray();
    }

    [Fact]
    public async Task Automated_walk_ack_is_forwarded_with_animation_and_ordered_following_packet()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var connected = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new ProxyHost();
        host.Connected += c => connected.TrySetResult(c);
        await host.StartAsync(port, (IPEndPoint)listener.LocalEndpoint);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await listener.AcceptTcpClientAsync();
        var connection = await connected.Task;
        connection.Tracker.Identity("Nuclear", 42);
        connection.Tracker.Observe(0x15, [1, 244, 100, 100, 0, 0, 0, 0, 0, 0]);
        connection.Tracker.Observe(4, [0, 20, 0, 20, 0, 0, 0, 0]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await connection.SendAsync(new Arbiter.Net.Client.ClientPacket(6, [1, 0]), connection.Tracker.Snapshot.Generation, timeout.Token);
        await ReadFrame(server.GetStream(), timeout.Token);
        var crypto = new Arbiter.Net.Server.ServerPacketEncryptor();
        byte[] ack = crypto.Encrypt(new Arbiter.Net.Server.ServerPacket(11, [1, 0, 20, 0, 20, 0, 0, 0, 0, 0]), 20).ToArray();
        await server.GetStream().WriteAsync(ack, timeout.Token);
        Assert.Equal(ack, await ReadFrame(client.GetStream(), timeout.Token));
        byte[] animation = await ReadFrame(client.GetStream(), timeout.Token);
        Assert.Equal((byte)12, animation[3]);
        Assert.Equal((byte)21, animation[4]);
        var presented = crypto.Decrypt(new Arbiter.Net.Server.ServerPacket(animation[3], animation[4..]));
        Assert.Equal(17, presented.Data.Length);
        await server.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Server.ServerPacket(1, [0]), 21).ToArray(), timeout.Token);
        byte[] next = await ReadFrame(client.GetStream(), timeout.Token);
        Assert.Equal((byte)22, next[4]);
        Assert.Equal(new byte[] { 0 }, crypto.Decrypt(new Arbiter.Net.Server.ServerPacket(next[3], next[4..])).Data);
        Assert.Equal(new DAQuester.Core.Tile(21, 20), connection.Tracker.Snapshot.Position);
        var clientCrypto = new Arbiter.Net.Client.ClientPacketEncryptor();
        await client.GetStream().WriteAsync(clientCrypto.Encrypt(new Arbiter.Net.Client.ClientPacket(6, [2, 0]), 0).ToArray(), timeout.Token);
        await ReadFrame(server.GetStream(), timeout.Token);
        await server.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Server.ServerPacket(11, [2, 0, 21, 0, 20, 0, 0, 0, 0, 0]), 22).ToArray(), timeout.Token);
        var manualAck = await ReadFrame(client.GetStream(), timeout.Token);
        Assert.Equal((byte)11, manualAck[3]);
        Assert.Equal((byte)23, manualAck[4]);
        await server.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Server.ServerPacket(1, [0]), 23).ToArray(), timeout.Token);
        var afterManual = await ReadFrame(client.GetStream(), timeout.Token);
        Assert.Equal((byte)1, afterManual[3]);
        Assert.Equal((byte)24, afterManual[4]);
        Assert.Equal(1, connection.VisibleMoves);
    }

    [Theory]
    [InlineData(0x06)]
    [InlineData(0x11)]
    [InlineData(0x39)]
    [InlineData(0x3A)]
    [InlineData(0x43)]
    public async Task Manual_client_actions_are_forwarded_without_interrupting_automation(byte opcode)
    {
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        await using var host = new ProxyHost();
        var connected = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Connected += c => connected.TrySetResult(c);
        int port = await host.StartOwnedAsync((IPEndPoint)remote.LocalEndpoint, null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        using var server = await remote.AcceptTcpClientAsync(timeout.Token);
        var connection = await connected.Task.WaitAsync(timeout.Token);
        var interruptions = new System.Collections.Concurrent.ConcurrentQueue<string>();
        connection.Interrupted += interruptions.Enqueue;
        byte[] data = opcode switch
        {
            0x06 => [2, 0],
            0x11 => [1],
            0x43 => [1, 0, 0, 0, 5],
            _ => [0, 0, 0, 42, 0, 1, 0]
        };
        var crypto = new Arbiter.Net.Client.ClientPacketEncryptor();
        await client.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Client.ClientPacket(opcode, data), 0).ToArray(), timeout.Token);
        var forwarded = await ReadFrame(server.GetStream(), timeout.Token);
        Assert.Equal(opcode, forwarded[3]);
        Assert.Equal(data, crypto.Decrypt(new Arbiter.Net.Client.ClientPacket(forwarded[3], forwarded[4..])).Data);
        Assert.Empty(interruptions);
        Assert.True(connection.Connected);
    }

    [Fact]
    public async Task Failing_interrupt_handler_is_reported_and_keeps_the_connection()
    {
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var connected = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new ProxyHost();
        host.Connected += c => connected.TrySetResult(c);
        await host.StartAsync(port, (IPEndPoint)remote.LocalEndpoint);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await remote.AcceptTcpClientAsync();
        var connection = await connected.Task;
        var diagnostics = new List<string>();
        connection.Diagnostic += m =>
        {
            lock (diagnostics)
                diagnostics.Add(m);
        };
        var interrupted = new List<string>();
        connection.Interrupted += reason =>
        {
            lock (interrupted)
                interrupted.Add(reason);
            throw new ObjectDisposedException("stale token");
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        int destination = ((IPEndPoint)remote.LocalEndpoint).Port;
        var redirect = new Arbiter.Net.Server.ServerPacket(3, new byte[] { 1, 0, 0, 127, (byte)(destination >> 8), (byte)destination }).ToArray();
        await server.GetStream().WriteAsync(redirect, timeout.Token);
        Assert.Equal((byte)3, (await ReadFrame(client.GetStream(), timeout.Token))[3]);
        Assert.True(connection.Connected);
        lock (interrupted)
            Assert.Equal(["Server redirect. Resume on replacement connection."], interrupted);
        lock (diagnostics)
        {
            Assert.Contains(diagnostics, m => m.StartsWith("Interrupt handler failed: ") && m.Contains("ObjectDisposedException") && m.Contains("stale token"));
            Assert.DoesNotContain(diagnostics, m => m.StartsWith("Transport failed"));
        }
    }

    [Fact]
    public async Task Focus_and_NPC_clicks_never_interrupt_automation()
    {
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var connected = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new ProxyHost();
        host.Connected += c => connected.TrySetResult(c);
        await host.StartAsync(port, (IPEndPoint)remote.LocalEndpoint);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await remote.AcceptTcpClientAsync();
        var connection = await connected.Task;
        connection.Tracker.Identity("Ferda", 0x01020304);
        var diagnostics = new List<string>();
        connection.Diagnostic += m =>
        {
            lock (diagnostics)
                diagnostics.Add(m);
        };
        var interrupted = new List<string>();
        connection.Interrupted += reason =>
        {
            lock (interrupted)
                interrupted.Add(reason);
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var crypto = new Arbiter.Net.Client.ClientPacketEncryptor();
        byte sequence = 0;
        async Task Click(byte[] data)
        {
            await client.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Client.ClientPacket(0x43, data), sequence++).ToArray(), timeout.Token);
            Assert.Equal((byte)0x43, (await ReadFrame(server.GetStream(), timeout.Token))[3]);
        }

        // Ground tile, then the character's own id: both are focus clicks.
        await Click([3, 0, 12, 0, 9]);
        await Click([1, 1, 2, 3, 4]);
        lock (interrupted)
            Assert.Empty(interrupted);
        await Click([1, 0, 0, 0x1A, 0x94]);
        lock (interrupted)
            Assert.Empty(interrupted);
        lock (diagnostics)
            Assert.DoesNotContain(diagnostics, m => m.Contains("interrupted automation"));
    }

    [Fact]
    public async Task Malformed_state_packet_still_passes_through()
    {
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        await using var host = new ProxyHost();
        await host.StartAsync(port, (IPEndPoint)remote.LocalEndpoint);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await remote.AcceptTcpClientAsync();
        var crypto = new Arbiter.Net.Server.ServerPacketEncryptor();
        byte[] wire = crypto.Encrypt(new Arbiter.Net.Server.ServerPacket(0x08, new byte[] { 0x20 }), 0, 512, 123).ToArray();
        await server.GetStream().WriteAsync(wire);
        var received = new byte[wire.Length];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await client.GetStream().ReadExactlyAsync(received, timeout.Token);
        Assert.Equal(wire, received);
    }

    [Fact]
    public async Task Proxy_passes_fragmented_ordinary_gameplay()
    {
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        await using var host = new ProxyHost();
        await host.StartAsync(port, (IPEndPoint)remote.LocalEndpoint);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await remote.AcceptTcpClientAsync();
        byte[] hello = [0xAA, 0, 4, 0x7E, 65, 66, 10];
        await server.GetStream().WriteAsync(hello.AsMemory(0, 2));
        await server.GetStream().WriteAsync(hello.AsMemory(2));
        byte[] received = new byte[hello.Length];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await client.GetStream().ReadExactlyAsync(received, timeout.Token);
        Assert.Equal(hello, received);
    }
}
