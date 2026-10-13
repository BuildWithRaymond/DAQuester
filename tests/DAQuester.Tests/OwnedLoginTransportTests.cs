using System.Net;
using System.Net.Sockets;
using System.Text;
using Arbiter.Net.Client;
using Arbiter.Net.Server;
using Arbiter.Net.Security;
using DAQuester.Core;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

public class OwnedLoginTransportTests
{
    static byte[] Payload(string name, string password) => new[]
    {
        (byte)name.Length
    }.Concat(Encoding.ASCII.GetBytes(name)).Concat(new[] { (byte)password.Length }).Concat(Encoding.ASCII.GetBytes(password)).Concat(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }).ToArray();
    static async Task<byte[]> Frame(NetworkStream stream, CancellationToken ct)
    {
        byte[] header = new byte[3];
        await stream.ReadExactlyAsync(header, ct);
        byte[] data = new byte[(header[1] << 8) | header[2]];
        await stream.ReadExactlyAsync(data, ct);
        return data;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Early_lobby_notice_and_redirect_survive_immediate_close_and_delayed_handler_setup(bool fragmented, bool delaySetup)
    {
        // Synthetic lobby/game servers only. No client executable or real login is used.
        using var lobby = new TcpListener(IPAddress.Loopback, 0);
        lobby.Start();
        using var game = new TcpListener(IPAddress.Loopback, 0);
        game.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var releaseSetup = new ManualResetEventSlim();
        var authenticated = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loginChecked = new TaskCompletionSource<DAQuester.DarkAges.Protocol.LoginResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var configured = new System.Collections.Concurrent.ConcurrentQueue<ProxyConnection>();
        var setups = 0;
        void Configure(ProxyConnection connection)
        {
            if (Interlocked.Increment(ref setups) == 1 && delaySetup && !releaseSetup.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Synthetic handler gate timed out.");
            configured.Enqueue(connection);
            connection.Authenticated += c => authenticated.TrySetResult(c);
            connection.LoginResponded += response => loginChecked.TrySetResult(response);
        }

        await using var host = new ProxyHost();
        int port = await host.StartOwnedAsync((IPEndPoint)lobby.LocalEndpoint, null, Configure);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        using var server = await lobby.AcceptTcpClientAsync(timeout.Token);
        var key = Encoding.ASCII.GetBytes("test-key!");
        var parameters = new NetworkEncryptionParameters(3, key);
        var crypto = new ServerPacketEncryptor(parameters);
        var greeting = new ServerPacket(0x7E, [65, 66, 10]).ToArray();
        var version = new ServerPacket(0x00, new byte[] { 0, 0, 0, 0, 0, 3, 9 }.Concat(key).ToArray()).ToArray();
        var notice = crypto.Encrypt(new ServerPacket(0x60, [0, 0, 0, 0, 1]), 0).ToArray();
        var loginResponse = crypto.Encrypt(new ServerPacket(0x02, [0, 0, 0]), 1).ToArray();
        int gamePort = ((IPEndPoint)game.LocalEndpoint).Port;
        byte[] transferMetadata = new byte[]
        {
            3,
            9
        }.Concat(key).Concat(new byte[] { 9 }).Concat(Encoding.ASCII.GetBytes("Synthetic")).Concat(new byte[] { 0, 0, 0, 42 }).ToArray();
        byte[] redirectPayload = new byte[]
        {
            1,
            0,
            0,
            127,
            (byte)(gamePort >> 8),
            (byte)gamePort,
            (byte)transferMetadata.Length
        }.Concat(transferMetadata).ToArray();
        var redirect = new ServerPacket(0x03, redirectPayload).ToArray();
        var burst = greeting.Concat(version).Concat(notice).Concat(loginResponse).Concat(redirect).ToArray();
        try
        {
            if (fragmented)
            {
                for (int offset = 0; offset < burst.Length; offset += 2)
                    await server.GetStream().WriteAsync(burst.AsMemory(offset, Math.Min(2, burst.Length - offset)), timeout.Token);
            }
            else
                await server.GetStream().WriteAsync(burst, timeout.Token);
            server.Client.Shutdown(SocketShutdown.Send); // EOF immediately after the last redirect byte.
        }
        finally
        {
            releaseSetup.Set();
        }

        Assert.Equal(greeting[3..], await Frame(client.GetStream(), timeout.Token));
        Assert.Equal(version[3..], await Frame(client.GetStream(), timeout.Token));
        Assert.Equal(notice[3..], await Frame(client.GetStream(), timeout.Token));
        Assert.Equal(loginResponse[3..], await Frame(client.GetStream(), timeout.Token));
        Assert.False((await loginChecked.Task.WaitAsync(timeout.Token)).Denied);
        var forwarded = await Frame(client.GetStream(), timeout.Token);
        Assert.Equal((byte)3, forwarded[0]);
        Assert.Equal(new byte[] { 1, 0, 0, 127 }, forwarded[1..5]);
        Assert.Equal(redirectPayload[6..], forwarded[7..]);
        int localPort = (forwarded[5] << 8) | forwarded[6];
        using var replacement = new TcpClient();
        await replacement.ConnectAsync(IPAddress.Loopback, localPort, timeout.Token);
        using var gameServer = await game.AcceptTcpClientAsync(timeout.Token);
        var handshake = new ClientPacket(0x10, transferMetadata).ToArray();
        await replacement.GetStream().WriteAsync(handshake, timeout.Token);
        Assert.Equal(handshake[3..], await Frame(gameServer.GetStream(), timeout.Token));
        var gameCrypto = new ServerPacketEncryptor(new NetworkEncryptionParameters(3, key, "Synthetic"));
        var identity = gameCrypto.Encrypt(new ServerPacket(0x05, [0, 0, 0, 42, 0, 0, 1, 0, 0]), 0).ToArray();
        await gameServer.GetStream().WriteAsync(identity, timeout.Token);
        Assert.Equal(identity[3..], await Frame(replacement.GetStream(), timeout.Token));
        var connection = await authenticated.Task.WaitAsync(timeout.Token);
        Assert.Equal("Synthetic", connection.Tracker.Snapshot.Name);
        Assert.Equal(2, configured.Count);
        Assert.NotEqual(configured.First().Tracker.Snapshot.Generation, connection.Tracker.Snapshot.Generation);
    }

    [Fact]
    public async Task Separate_owned_endpoints_only_rewrite_their_own_character_ticket()
    {
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        await using var host = new ProxyHost();
        var one = new SavedLoginTicket("First", () => "first-secret");
        var two = new SavedLoginTicket("Second", () => "second-secret");
        int a = await host.StartOwnedAsync((IPEndPoint)remote.LocalEndpoint, one);
        int b = await host.StartOwnedAsync((IPEndPoint)remote.LocalEndpoint, two);
        using var clientOne = new TcpClient();
        await clientOne.ConnectAsync(IPAddress.Loopback, a);
        using var serverOne = await remote.AcceptTcpClientAsync();
        using var clientTwo = new TcpClient();
        await clientTwo.ConnectAsync(IPAddress.Loopback, b);
        using var serverTwo = await remote.AcceptTcpClientAsync();
        var crypto = new ClientPacketEncryptor();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        await clientOne.GetStream().WriteAsync(crypto.Encrypt(new ClientPacket(3, Payload("First", one.Ticket)), 0).ToArray(), timeout.Token);
        byte[] frame = await Frame(serverOne.GetStream(), timeout.Token);
        Assert.Equal(Payload("First", "first-secret"), crypto.Decrypt(new ClientPacket(frame[0], frame[1..])).Data);
        Assert.True(one.WasSubmitted);
        Assert.False(two.WasSubmitted);
        await clientTwo.GetStream().WriteAsync(crypto.Encrypt(new ClientPacket(3, Payload("Second", two.Ticket)), 0).ToArray(), timeout.Token);
        frame = await Frame(serverTwo.GetStream(), timeout.Token);
        Assert.Equal(Payload("Second", "second-secret"), crypto.Decrypt(new ClientPacket(frame[0], frame[1..])).Data);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Login_denial_is_forwarded_to_client_and_only_its_owned_launch_observer(bool trailingData)
    {
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        await using var host = new ProxyHost();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var denied = new TaskCompletionSource<DAQuester.DarkAges.Protocol.LoginResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnostic = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        int otherResponses = 0;
        int first = await host.StartOwnedAsync((IPEndPoint)remote.LocalEndpoint, null, c =>
        {
            c.LoginResponded += response => denied.TrySetResult(response);
            c.Diagnostic += line =>
            {
                if (line.StartsWith("Server login response:"))
                    diagnostic.TrySetResult(line);
            };
        });
        int second = await host.StartOwnedAsync((IPEndPoint)remote.LocalEndpoint, null, c => c.LoginResponded += _ => Interlocked.Increment(ref otherResponses));
        using var client = new TcpClient();
        using var otherClient = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, first, timeout.Token);
        using var server = await remote.AcceptTcpClientAsync(timeout.Token);
        await otherClient.ConnectAsync(IPAddress.Loopback, second, timeout.Token);
        using var otherServer = await remote.AcceptTcpClientAsync(timeout.Token);
        const string text = "Account is already logged in.";
        var crypto = new ServerPacketEncryptor();
        var payload = new byte[] { 99, (byte)text.Length }.Concat(Encoding.ASCII.GetBytes(text)).ToArray();
        if (trailingData)
            payload = payload.Concat(new byte[] { 0 }).ToArray();
        await server.GetStream().WriteAsync(crypto.Encrypt(new ServerPacket(2, payload), 0).ToArray(), timeout.Token);
        var response = await denied.Task.WaitAsync(timeout.Token);
        Assert.True(response.Denied);
        Assert.Equal(text, response.Message);
        string shape = await diagnostic.Task.WaitAsync(timeout.Token);
        Assert.Contains($"{payload.Length} payload bytes", shape);
        Assert.DoesNotContain(text, shape);
        byte[] frame = await Frame(client.GetStream(), timeout.Token);
        Assert.Equal(payload, crypto.Decrypt(new ServerPacket(frame[0], frame[1..])).Data);
        Assert.Equal(0, otherResponses);
    }
}
