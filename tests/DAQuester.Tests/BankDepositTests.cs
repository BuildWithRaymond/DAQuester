using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Xunit;

namespace DAQuester.Tests;

public class BankDepositTests
{
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    [Fact]
    public async Task Missing_deposit_menu_does_not_create_pending_money_transaction()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Gold = 1000
        });
        session.OnCommand = c =>
        {
            if (c.Kind == "prepare-deposit")
                throw new QuestBlockedException("Expected dialog option missing: Deposit Money");
        };
        var context = new QuestContext(session, Journal(), new(), new()
        {
            Character = "Nuclear"
        });
        await Assert.ThrowsAnyAsync<Exception>(() => new StickActions().ExecuteAsync(context, new("bank", "bank", null), default));
        Assert.Null(context.Job.Pending);
        Assert.DoesNotContain(session.Commands, c => c.Kind == "deposit");
    }

    [Fact]
    public async Task Deposit_baseline_is_saved_after_prompt_and_amount_uses_fresh_gold()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Gold = 2000
        });
        var context = new QuestContext(session, Journal(), new(), new()
        {
            Character = "Nuclear"
        });
        bool prepared = false;
        session.OnCommand = c =>
        {
            if (c.Kind == "prepare-deposit")
            {
                Assert.Null(context.Job.Pending);
                prepared = true;
                session.Set(session.Snapshot with
                {
                    Gold = 1200,
                    Dialog = new(1, session.Snapshot.Revision + 1, 7015, 1, "Cassidy", "How much more?", false, 82, 0, 2, [])
                });
            }

            if (c.Kind == "deposit")
            {
                Assert.True(prepared);
                Assert.Equal(201u, c.Amount);
                Assert.Equal(1200u, context.Job.Pending!.Gold);
                Assert.Equal(session.Snapshot.Dialog, c.Dialog);
                session.Set(session.Snapshot with
                {
                    Gold = 999
                });
            }
        };
        await new StickActions().ExecuteAsync(context, new("bank", "bank", null), default);
        Assert.Null(context.Job.Pending);
    }

    [Fact]
    public async Task Actual_Cassidy_menu_and_prompt_use_separate_live_pursuits()
    {
        string file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/evidence/datester-7.41/cassidy-deposit.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var events = doc.RootElement.GetProperty("Events").Deserialize<ReplayEvent[]>()!;
        var menu = events.First(e => e.Opcode == 47 && e.Snapshot.Dialog?.Type == 0);
        var prompt = events.First(e => e.Opcode == 47 && e.Snapshot.Dialog?.Type == 2);
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
        connection.Tracker.Identity("Awesome", 1100782);
        byte[] gold = new byte[25];
        gold[0] = 8;
        BinaryPrimitives.WriteUInt32BigEndian(gold.AsSpan(21), 1000);
        connection.Tracker.Observe(8, gold);
        connection.Tracker.Observe(47, Convert.FromBase64String(menu.Payload));
        string routes = Path.Combine(Path.GetTempPath(), "DAQuester-bank-routes-" + Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(routes, "[]");
        var session = new GameSession(connection, new MapRepository("", routes));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var prepare = session.ExecuteAsync(new("prepare-deposit"), timeout.Token);
        if (prepare.IsFaulted)
            await prepare; // Fail immediately for unsupported preparation on the old implementation.
        var select = Decode(await Read(server.GetStream(), timeout.Token));
        Assert.Equal(menu.Snapshot.Dialog!.EntityId, select.EntityId);
        Assert.Equal((ushort)66, select.PursuitId);
        Assert.Empty(select.Arguments);
        await server.GetStream().WriteAsync(new ServerPacketEncryptor().Encrypt(new ServerPacket(47, Convert.FromBase64String(prompt.Payload)), 1).ToArray(), timeout.Token);
        await prepare;
        await session.ExecuteAsync(new("deposit", Amount: 1, Dialog: session.Snapshot.Dialog), timeout.Token);
        var input = Decode(await Read(server.GetStream(), timeout.Token));
        Assert.Equal((ushort)82, input.PursuitId);
        Assert.Equal(new[] { "1" }, input.Arguments);
        Assert.Equal(select.EntityId, input.EntityId);
    }

    static ClientMerchantMessage Decode(byte[] frame)
    {
        var p = (ClientPacket)new ClientPacketEncryptor().Decrypt(new ClientPacket(frame[3], frame[4..]));
        Assert.Equal(0x39, (byte)p.Command);
        var m = new ClientMerchantMessage();
        m.Deserialize(new NetworkPacketReader(p));
        return m;
    }

    static async Task<byte[]> Read(NetworkStream stream, CancellationToken ct)
    {
        byte[] header = new byte[3];
        await stream.ReadExactlyAsync(header, ct);
        byte[] data = new byte[(header[1] << 8) | header[2]];
        await stream.ReadExactlyAsync(data, ct);
        return header.Concat(data).ToArray();
    }
}
