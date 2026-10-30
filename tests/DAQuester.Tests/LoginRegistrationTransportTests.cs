using System.Net;
using System.Net.Sockets;
using System.Text;
using Arbiter.Net.Client;
using Arbiter.Net.Security;
using Arbiter.Net.Server;
using DAQuester.Core;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

public class LoginRegistrationTransportTests
{
    static async Task<byte[]> ReadFrame(NetworkStream stream, CancellationToken cancellation)
    {
        var header = new byte[3];
        await stream.ReadExactlyAsync(header, cancellation);
        var data = new byte[(header[1] << 8) | header[2]];
        await stream.ReadExactlyAsync(data, cancellation);
        return data;
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("fragmented", true)]
    [InlineData("trailing-null", true)]
    [InlineData("denied", false)]
    [InlineData("malformed-login-response", false)]
    [InlineData("revoked-login", false)]
    [InlineData("no-registration", false)]
    [InlineData("no-login-request", false)]
    [InlineData("stale-attempt", false)]
    [InlineData("wrong-name", false)]
    [InlineData("wrong-key", false)]
    [InlineData("wrong-cookie", false)]
    [InlineData("duplicate-handshake", false)]
    public async Task Registration_follows_only_the_same_successful_login_redirect(string mode, bool expected)
    {
        // Both servers, credentials and actors are synthetic. No game executable or live login.
        using var lobby = new TcpListener(IPAddress.Loopback, 0);
        lobby.Start();
        using var game = new TcpListener(IPAddress.Loopback, 0);
        game.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = deadline.Token;
        var authenticated = new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var configured = new System.Collections.Concurrent.ConcurrentQueue<ProxyConnection>();
        var observedSource = new TaskCompletionSource<ServerNotice>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new ProxyHost();
        int port = await host.StartOwnedAsync((IPEndPoint)lobby.LocalEndpoint, null, c =>
        {
            configured.Enqueue(c);
            c.Authenticated += world => authenticated.TrySetResult(world);
            c.Tracker.Changed += s =>
            {
                if (s.Registration is { } notice)
                    observedSource.TrySetResult(notice);
            };
        });
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, ct);
        using var server = await lobby.AcceptTcpClientAsync(ct);
        byte[] key = Encoding.ASCII.GetBytes("test-key!");
        var parameters = new NetworkEncryptionParameters(3, key);
        var serverCrypto = new ServerPacketEncryptor(parameters);
        var clientCrypto = new ClientPacketEncryptor(parameters);
        byte seq = 0;

        async Task Forward(byte[] wire)
        {
            if (mode == "fragmented")
                foreach (byte value in wire)
                    await server.GetStream().WriteAsync(new[] { value }, ct);
            else
                await server.GetStream().WriteAsync(wire, ct);
            await ReadFrame(client.GetStream(), ct);
        }

        async Task Login()
        {
            byte[] name = Encoding.ASCII.GetBytes("Synthetic"), password = Encoding.ASCII.GetBytes("unused-test-value");
            var payload = new[] { (byte)name.Length }.Concat(name).Concat(new[] { (byte)password.Length }).Concat(password).ToArray();
            await client.GetStream().WriteAsync(clientCrypto.Encrypt(new ClientPacket(3, payload), 0).ToArray(), ct);
            await ReadFrame(server.GetStream(), ct);
        }

        await Forward(new ServerPacket(0, new byte[] { 0, 0, 0, 0, 0, 3, 9 }.Concat(key).ToArray()).ToArray());
        if (mode != "no-login-request")
            await Login();
        byte[] loginReply = mode == "denied" ? new byte[] { 15, 0, 0 }
            : mode == "malformed-login-response" ? new byte[] { 0, 20, 65 } : new byte[] { 0, 0, 0 };
        await Forward(serverCrypto.Encrypt(new ServerPacket(2, loginReply), seq++).ToArray());
        if (mode != "no-registration")
            await Forward(serverCrypto.Encrypt(new ServerPacket(0x0A,
                DaydreamerProtocolTests.Message(3, "Your expiration date is 10-29")), seq++).ToArray());
        if (mode == "stale-attempt")
        {
            await Login();
            await Forward(serverCrypto.Encrypt(new ServerPacket(2, new byte[] { 0, 0, 0 }), seq++).ToArray());
        }
        if (mode == "revoked-login")
            await Forward(serverCrypto.Encrypt(new ServerPacket(2, new byte[] { 15, 0, 0 }), seq++).ToArray());
        byte[] metadata = new byte[] { 3, 9 }.Concat(key).Concat(new byte[] { 9 }).Concat(Encoding.ASCII.GetBytes("Synthetic"))
            .Concat(new byte[] { 0, 0, 0, 42 }).ToArray(); // Connection cookie42 is deliberately NOT world avatar id77.
        int semanticLength = metadata.Length;
        if (mode == "trailing-null")
            metadata = metadata.Concat(new byte[] { 0 }).ToArray();
        int gamePort = ((IPEndPoint)game.LocalEndpoint).Port;
        byte[] redirect = new byte[] { 1, 0, 0, 127, (byte)(gamePort >> 8), (byte)gamePort, (byte)semanticLength }.Concat(metadata).ToArray();
        await server.GetStream().WriteAsync(new ServerPacket(3, redirect).ToArray(), ct);
        server.Client.Shutdown(SocketShutdown.Send); // Evidence must survive expected lobby EOF.
        byte[] rewritten = await ReadFrame(client.GetStream(), ct);
        int replacementPort = (rewritten[5] << 8) | rewritten[6];
        using var replacement = new TcpClient();
        await replacement.ConnectAsync(IPAddress.Loopback, replacementPort, ct);
        using var worldServer = await game.AcceptTcpClientAsync(ct);
        byte[] actual = metadata.ToArray();
        if (mode == "wrong-name")
            Encoding.ASCII.GetBytes("Different").CopyTo(actual, 12);
        if (mode == "wrong-key")
            actual[2] ^= 1;
        if (mode == "wrong-cookie")
            actual[semanticLength - 1] ^= 1;
        var handshake = new ClientPacket(0x10, actual).ToArray();
        await replacement.GetStream().WriteAsync(handshake, ct);
        await ReadFrame(worldServer.GetStream(), ct);
        if (mode == "duplicate-handshake")
        {
            await replacement.GetStream().WriteAsync(handshake, ct);
            await ReadFrame(worldServer.GetStream(), ct);
        }
        string actualName = mode == "wrong-name" ? "Different" : "Synthetic";
        byte[] actualKey = actual[2..11];
        var worldCrypto = new ServerPacketEncryptor(new NetworkEncryptionParameters(3, actualKey, actualName));
        await worldServer.GetStream().WriteAsync(worldCrypto.Encrypt(new ServerPacket(5, new byte[] { 0, 0, 0, 77, 0, 0, 1, 0, 0 }), 0).ToArray(), ct);
        var world = await authenticated.Task.WaitAsync(ct);
        var snapshot = world.Tracker.Snapshot with { Hp = 100, Map = DaydreamerEvidence.Map, Position = DaydreamerEvidence.Approach };
        var eligibility = DaydreamerEvidence.Eligibility(snapshot, new QuestJob { Character = actualName, Quest = DaydreamerEvidence.Id });
        Assert.Equal(expected, eligibility.Kind == EligibilityKind.Eligible);
        Assert.Equal((uint)77, snapshot.Id);
        Assert.NotEqual(configured.First().Tracker.Snapshot.Generation, snapshot.Generation);
        if (expected)
        {
            var source = await observedSource.Task.WaitAsync(ct);
            Assert.Equal("Your expiration date is 10-29", source.Text);
            Assert.Equal((byte)3, source.Type);
            Assert.Null(snapshot.DaydreamerStatus);
            Assert.Null(snapshot.ActiveStatus);
        }
    }
}
