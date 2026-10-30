using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Arbiter.Net.Client;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

public class DaydreamerSubmissionTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public async Task Submission_uses_current_page_ushort_pursuit_and_returns_after_write_without_waiting_for_dialogue()
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        var before = f.Session.Snapshot;

        var receipt = await session.SubmitDaydreamerAsync(f.Page, f.Timeout.Token);
        var wire = await Frame(f.ServerPeer.GetStream(), f.Timeout.Token);
        var packet = new ClientPacketEncryptor().Decrypt(new ClientPacket(wire[3], wire[4..]));

        Assert.Equal((byte)0x3A, wire[3]);
        Assert.Equal(new byte[] { 1, 1, 2, 3, 4, 0x12, 0x35, 0, 8 }, packet.Data);
        Assert.Equal(f.Page, receipt.Page);
        Assert.Equal(before.Revision, receipt.BaselineRevision);
        Assert.InRange(receipt.SentAt, before.Dialog!.ObservedAt!.Value, DateTimeOffset.UtcNow);
        Assert.Same(f.Page, f.Session.Snapshot.Dialog);
    }

    [Fact]
    public async Task A_count_arriving_while_the_send_is_queued_is_older_than_the_submission_baseline()
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        var sendGate = (SemaphoreSlim)typeof(ProxyConnection).GetField("toServer", Private)!.GetValue(f.Connection)!;
        await sendGate.WaitAsync(f.Timeout.Token);
        var submission = session.SubmitDaydreamerAsync(f.Page, f.Timeout.Token);
        Assert.False(submission.IsCompleted);
        f.Connection.Tracker.Observe(0x0A, DaydreamerProtocolTests.Message(3, "This is your 30th consecutive Login!"));
        var unsolicited = f.Session.Snapshot.DaydreamerStatus!;
        sendGate.Release();

        var receipt = await submission;

        Assert.True(unsolicited.Revision <= receipt.BaselineRevision);
        f.Connection.Tracker.Observe(0x0A, DaydreamerProtocolTests.Message(3, "This is your 2nd consecutive Login!"));
        var observed = await f.Session.WaitAsync(s => s.DaydreamerStatus is { } n && n.Revision > receipt.BaselineRevision,
            receipt.BaselineRevision, TimeSpan.FromSeconds(2), f.Timeout.Token);
        Assert.Equal("This is your 2nd consecutive Login!", observed.DaydreamerStatus!.Text);
    }

    [Theory]
    [InlineData("page")]
    [InlineData("trade")]
    [InlineData("trade-finished")]
    [InlineData("identity")]
    [InlineData("npc")]
    [InlineData("registration")]
    public async Task A_replaced_page_trade_identity_or_missing_npc_prevents_a_queued_submission(string change)
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        var sendGate = (SemaphoreSlim)typeof(ProxyConnection).GetField("toServer", Private)!.GetValue(f.Connection)!;
        await sendGate.WaitAsync(f.Timeout.Token);
        int sent = 0;
        f.Connection.ClientObserved += (_, _, bot) => { if (bot) sent++; };
        var submission = session.SubmitDaydreamerAsync(f.Page, f.Timeout.Token);
        Assert.False(submission.IsCompleted);
        var s = f.Session.Snapshot;
        if (change == "registration") f.Connection.Tracker.ClearLoginRegistration();
        else f.Set(change switch
        {
            "page" => s with { Dialog = f.Page with { Revision = s.Revision + 1, Text = "Another page" } },
            "trade" => s with { Exchange = new(s.Generation, s.Revision + 1, 55, "Synthetic") },
            "trade-finished" => s with { Exchange = new(s.Generation, s.Revision + 1, 55, "Synthetic") { Cancelled = true } },
            "identity" => s with { Name = "Another", Id = 99 },
            _ => s with { Entities = ImmutableDictionary<uint, Entity>.Empty }
        });
        sendGate.Release();

        await Assert.ThrowsAsync<QuestBlockedException>(() => submission);
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task An_active_count_during_prewrite_observers_cannot_qualify_as_a_submitted_reply()
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        f.Connection.ClientObserved += (opcode, _, bot) =>
        {
            if (!bot || opcode != 0x3A) return;
            f.Connection.Tracker.Observe(0x0A, DaydreamerProtocolTests.Message(3, "This is your 30th consecutive Login!"));
            f.Connection.Tracker.Observe(0x0A, DaydreamerProtocolTests.Message(3, "An unrelated active status"));
        };

        var receipt = await session.SubmitDaydreamerAsync(f.Page, f.Timeout.Token);
        Assert.True(f.Session.Snapshot.DaydreamerStatus!.Revision <= receipt.BaselineRevision);
    }

    [Fact]
    public async Task An_immediate_server_reply_after_reading_the_wire_remains_available_after_submission_returns()
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        async Task Reply()
        {
            await Frame(f.ServerPeer.GetStream(), f.Timeout.Token);
            f.Connection.Tracker.Observe(0x0A, DaydreamerProtocolTests.Message(3, "This is your 30th consecutive Login!"));
            f.Connection.Tracker.Observe(0x0A, DaydreamerProtocolTests.Message(3, "An unrelated active status"));
        }
        var response = Reply();
        var receipt = await session.SubmitDaydreamerAsync(f.Page, f.Timeout.Token);
        await response;
        var proof = await f.Session.WaitAsync(s => s.DaydreamerStatus is { } n && n.Generation == receipt.Page.Generation
            && n.Revision > receipt.BaselineRevision, receipt.BaselineRevision, TimeSpan.FromSeconds(2), f.Timeout.Token);

        Assert.Equal(30, DaydreamerEvidence.Count(proof.DaydreamerStatus!));
        Assert.True(proof.DaydreamerStatus!.Revision > receipt.BaselineRevision);
    }

    [Theory]
    [InlineData("page")]
    [InlineData("manual")]
    [InlineData("trade")]
    [InlineData("registration")]
    public async Task Queued_Celesta_selection_revalidates_page_manual_actions_and_trade_before_sending(string change)
    {
        await using var f = await Fixture.Create();
        var menu = f.Page with { Text = "Welcome", IsPursuit = false, HasNext = false,
            Choices = [new(" Avid Daydreamer ", 0x1234, 1)] };
        f.Set(f.Session.Snapshot with { Dialog = menu });
        var sendGate = (SemaphoreSlim)typeof(ProxyConnection).GetField("toServer", Private)!.GetValue(f.Connection)!;
        await sendGate.WaitAsync(f.Timeout.Token);
        var selection = f.Session.ExecuteAsync(new("choose", Text: " Avid Daydreamer ", Dialog: menu,
            ExpectedGeneration: menu.Generation, ExpectedDialogRevision: menu.Revision, ExpectedPageText: menu.Text), f.Timeout.Token);
        Assert.False(selection.IsCompleted);
        var current = f.Session.Snapshot;
        if (change == "page") f.Set(current with { Dialog = menu with { Revision = current.Revision + 1, Text = "Other page" } });
        else if (change == "trade") f.Set(current with { Exchange = new(current.Generation, current.Revision + 1, 55, "Synthetic") });
        else if (change == "registration") f.Connection.Tracker.ClearLoginRegistration();
        else typeof(ProxyConnection).GetMethod("ReportClient", Private)!.Invoke(f.Connection, [(byte)0x06, new byte[] { 2, 0 }, false]);
        sendGate.Release();

        await Assert.ThrowsAsync<QuestBlockedException>(() => selection);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_page_replaced_by_a_prewrite_observer_prevents_the_next_without_consuming_a_wire_ordinal()
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        f.Connection.ClientObserved += (opcode, _, bot) =>
        {
            if (bot && opcode == 0x3A)
                f.Set(f.Session.Snapshot with { Dialog = f.Page with { Revision = f.Page.Revision + 1, Text = "Changed page" } });
        };

        await Assert.ThrowsAsync<QuestBlockedException>(() => session.SubmitDaydreamerAsync(f.Page, f.Timeout.Token));
        Assert.Equal(0, f.ServerPeer.Available);
        await f.Connection.SendAsync(new ClientPacket(0x11, [1]), 17, f.Timeout.Token);
        var wire = await Frame(f.ServerPeer.GetStream(), f.Timeout.Token);
        Assert.Equal((byte)0x11, wire[3]);
        Assert.Equal((byte)0, wire[4]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_revoked_by_a_prewrite_observer_prevents_Celestas_choice_or_next(bool selection)
    {
        await using var f = await Fixture.Create();
        var page = selection ? f.Page with
        {
            Text = "Welcome", IsPursuit = false, HasNext = false,
            Choices = [new(" Avid Daydreamer ", 0x1234, 1)]
        } : f.Page;
        f.Set(f.Session.Snapshot with { Dialog = page });
        f.Connection.ClientObserved += (opcode, _, bot) =>
        {
            if (bot && opcode == (selection ? 0x39 : 0x3A))
                f.Connection.Tracker.ClearLoginRegistration();
        };

        if (selection)
            await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("choose", Text: " Avid Daydreamer ",
                Dialog: page, ExpectedGeneration: page.Generation, ExpectedDialogRevision: page.Revision,
                ExpectedPageText: page.Text), f.Timeout.Token));
        else
            await Assert.ThrowsAsync<QuestBlockedException>(() => ((IDaydreamerSession)f.Session).SubmitDaydreamerAsync(page, f.Timeout.Token));

        Assert.Equal(0, f.ServerPeer.Available);
        Assert.Null(f.Session.Snapshot.Registration);
        await f.Connection.SendAsync(new ClientPacket(0x11, [1]), 17, f.Timeout.Token);
        var wire = await Frame(f.ServerPeer.GetStream(), f.Timeout.Token);
        Assert.Equal((byte)0x11, wire[3]);
        Assert.Equal((byte)0, wire[4]);
    }

    [Fact]
    public async Task Cancellation_during_prewrite_observers_prevents_a_not_yet_written_next()
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        using var stop = new CancellationTokenSource();
        f.Connection.ClientObserved += (opcode, _, bot) => { if (bot && opcode == 0x3A) stop.Cancel(); };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SubmitDaydreamerAsync(f.Page, stop.Token));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Manual_client_movement_while_awaiting_action_ownership_prevents_the_stale_next()
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        var actionGate = (SemaphoreSlim)typeof(GameSession).GetField("actions", Private)!.GetValue(f.Session)!;
        await actionGate.WaitAsync(f.Timeout.Token);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Connection.ClientObserved += (opcode, _, bot) => { if (!bot && opcode == 0x06) observed.TrySetResult(); };
        f.StartPump();
        var submission = session.SubmitDaydreamerAsync(f.Page, f.Timeout.Token);
        var crypto = new ClientPacketEncryptor();
        await f.ClientPeer.GetStream().WriteAsync(crypto.Encrypt(new ClientPacket(0x06, [2, 0]), 0).ToArray(), f.Timeout.Token);
        await observed.Task.WaitAsync(f.Timeout.Token);
        await Frame(f.ServerPeer.GetStream(), f.Timeout.Token);
        actionGate.Release();

        await Assert.ThrowsAsync<QuestBlockedException>(() => submission);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 65535)]
    public async Task No_next_or_exhausted_step_cannot_send_a_submission(bool hasNext, ushort step)
    {
        await using var f = await Fixture.Create();
        var session = Assert.IsAssignableFrom<IDaydreamerSession>(f.Session);
        var page = f.Page with { HasNext = hasNext, Step = step };
        f.Set(f.Session.Snapshot with { Dialog = page });

        await Assert.ThrowsAsync<QuestBlockedException>(() => session.SubmitDaydreamerAsync(page, f.Timeout.Token));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    static async Task<byte[]> Frame(NetworkStream stream, CancellationToken ct)
    {
        byte[] header = new byte[3];
        await stream.ReadExactlyAsync(header, ct);
        byte[] body = new byte[(header[1] << 8) | header[2]];
        await stream.ReadExactlyAsync(body, ct);
        return header.Concat(body).ToArray();
    }

    sealed class Fixture : IAsyncDisposable
    {
        public ProxyConnection Connection { get; }
        public GameSession Session { get; }
        public TcpClient ClientPeer { get; }
        public TcpClient ServerPeer { get; }
        public CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(10));
        public Dialog Page { get; }
        Task? pump;

        Fixture(ProxyConnection connection, TcpClient clientPeer, TcpClient serverPeer)
        {
            Connection = connection;
            ClientPeer = clientPeer;
            ServerPeer = serverPeer;
            Page = new(17, 5, 0x01020304, 1, "Celesta", "Thank you for dropping by. I'll make sure to check you in!",
                true, 0x1235, 7, 0, [], HasNext: true) { ObservedAt = DateTimeOffset.UtcNow };
            Set(new()
            {
                Name = "Elara", Id = 7, Connected = true, Generation = 17, Revision = 5,
                Map = DaydreamerEvidence.Map, Position = DaydreamerEvidence.Approach, Dialog = Page,
                Registration = new(17, 1, 3, "This account is on auto-renewal", DateTimeOffset.UtcNow),
                Entities = ImmutableDictionary<uint, Entity>.Empty.Add(Page.EntityId, new(Page.EntityId, "Celesta", new(49, 18), true))
            });
            var routes = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/routes.json"));
            Session = new(connection, new MapRepository("", routes));
        }

        public void Set(CharacterSnapshot snapshot) => typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(Connection.Tracker, snapshot);
        public void StartPump() => pump = Connection.RunAsync(Timeout.Token);
        public static async Task<Fixture> Create()
        {
            var (client, clientPeer) = await Pair();
            var (remote, serverPeer) = await Pair();
            return new(new(client, remote, 17, _ => throw new InvalidOperationException("Synthetic tests never redirect.")), clientPeer, serverPeer);
        }
        static async Task<(TcpClient, TcpClient)> Pair()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var connection = new TcpClient();
            await connection.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            return (connection, await listener.AcceptTcpClientAsync());
        }
        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            if (pump is not null) await pump;
            ClientPeer.Dispose();
            ServerPeer.Dispose();
            Timeout.Dispose();
        }
    }
}
