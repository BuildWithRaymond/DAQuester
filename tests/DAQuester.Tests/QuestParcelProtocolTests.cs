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

public class QuestParcelProtocolTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public async Task Parcel_input_stamp_reads_both_native_manual_epochs()
    {
        await using var f = await Fixture.Create();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var initial = port.InputStamp;
        f.Manual(0x08, 4, 0, 1);
        var dropped = port.InputStamp;
        Assert.Equal(initial.ManualOperation, dropped.ManualOperation);
        Assert.Equal(initial.ManualBagOperation + 1, dropped.ManualBagOperation);
        f.Manual(0x11, 2);
        var turned = port.InputStamp;
        Assert.Equal(dropped.ManualOperation + 1, turned.ManualOperation);
        Assert.Equal(dropped.ManualBagOperation, turned.ManualBagOperation);
    }

    [Fact]
    public async Task Receive_without_a_visible_parcel_choice_journals_once_and_needs_sender_bound_reply_plus_inventory()
    {
        await using var f = await Fixture.Create();
        f.OrdinaryMenu();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var receiving = port.ReceiveParcelAsync(f.Intent(QuestParcelDirection.Receive), f.Journal, f.Policy, f.Timeout.Token);
        Assert.False(receiving.IsCompleted);
        var packet = await f.ReadPacket();
        Assert.Equal((byte)0x39, (byte)packet.Command);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 42, 0, 0x64 }, packet.Data);
        Assert.True(Assert.Single(f.Journal.Entries.Values).WriteIntent);
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(4, new(4, "Mold", 1)) });
        Assert.False(receiving.IsCompleted);
        f.Active("Synthetic receipt: Exchanger|Mold");
        var result = await receiving;
        Assert.Equal(QuestParcelOutcome.Received, result.Result?.Outcome);
        Assert.Equal("Exchanger", result.Result?.Sender);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_single_item_return_addresses_Exchanger_and_needs_item_fee_and_source_ack()
    {
        await using var f = await Fixture.Create(new Item(4, "Mold", 1));
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var sending = port.SendParcelAsync(f.Intent(QuestParcelDirection.Send), f.Journal, f.Policy, f.Timeout.Token);
        var packet = await f.ReadPacket();
        Assert.Equal((byte)0x39, (byte)packet.Command);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 42, 0, 0x63, 2, 4, 0x31, 9 }.Concat("Exchanger".Select(c => (byte)c)), packet.Data);
        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        f.Active("Synthetic sent to Exchanger");
        Assert.False(sending.IsCompleted);
        f.Publish(f.Session.Snapshot with { Gold = 4000 });
        var result = await sending;
        Assert.Equal(QuestParcelOutcome.SourceAccepted, result.Result?.Outcome);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task An_unqualified_policy_emits_no_packet()
    {
        await using var f = await Fixture.Create();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        await Assert.ThrowsAsync<QuestBlockedException>(() => port.ReceiveParcelAsync(f.Intent(QuestParcelDirection.Receive), f.Journal,
            UnqualifiedQuestParcelReceiptPolicy.Instance, f.Timeout.Token));
        Assert.Empty(f.Journal.Entries);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_request_rejected_inside_the_send_lock_is_durably_known_not_submitted()
    {
        await using var f = await Fixture.Create();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var gate = (SemaphoreSlim)typeof(ProxyConnection).GetField("toServer", Private)!.GetValue(f.Connection)!;
        await gate.WaitAsync(f.Timeout.Token);
        var receiving = port.ReceiveParcelAsync(f.Intent(QuestParcelDirection.Receive), f.Journal, f.Policy, f.Timeout.Token);
        f.Publish(f.Session.Snapshot with { Gold = 4999 });
        gate.Release();
        await Assert.ThrowsAsync<QuestBlockedException>(() => receiving);
        var checkpoint = Assert.Single(f.Journal.Entries.Values);
        Assert.Equal(QuestParcelOutcome.NotSubmitted, checkpoint.Result?.Outcome);
        Assert.False(checkpoint.Uncertain);
        var restored = await port.ReconcileParcelAsync(checkpoint, f.Journal, f.Policy, f.Timeout.Token);
        Assert.Equal(QuestParcelOutcome.NotSubmitted, restored.Result?.Outcome);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Receiving_an_item_without_a_sender_bound_reply_stays_uncertain_and_never_retries()
    {
        await using var f = await Fixture.Create();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var intent = f.Intent(QuestParcelDirection.Receive);
        var receiving = port.ReceiveParcelAsync(intent, f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(4, new(4, "Mold", 1)) });
        var result = await receiving;
        Assert.True(result.Uncertain);
        Assert.Null(result.Result);
        var retried = await port.ReceiveParcelAsync(intent, f.Journal, f.Policy, f.Timeout.Token);
        Assert.True(retried.Uncertain);
        Assert.Null(retried.Result);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(3u)]
    public async Task Direct_return_without_visible_parcel_choices_sends_exactly_one_from_any_quantity_and_reconciles_read_only(uint quantity)
    {
        await using var f = await Fixture.Create(new Item(4, "Mold", quantity));
        f.OrdinaryMenu();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var sending = port.SendParcelAsync(f.Intent(QuestParcelDirection.Send), f.Journal, f.Policy, f.Timeout.Token);
        Assert.False(sending.IsCompleted);
        var packet = await f.ReadPacket();
        Assert.Equal(new byte[] { 1, 0, 0, 0, 42, 0, 0x63, 2, 4, 0x31, 9 }.Concat("Exchanger".Select(c => (byte)c)), packet.Data);
        var inventory = quantity == 1 ? f.Session.Snapshot.Inventory.Remove(4)
            : f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", quantity - 1));
        f.Publish(f.Session.Snapshot with { Inventory = inventory, Gold = 4000 });
        f.Active("Synthetic sent to Exchanger");
        var receipt = await sending;
        Assert.Equal(QuestParcelOutcome.SourceAccepted, receipt.Result?.Outcome);
        Assert.Equal(new[] { "Recipient" }, receipt.WrittenStages);
        f.Publish(f.Session.Snapshot with { Generation = 18, Id = 8, Dialog = null, ActiveStatus = null });
        var reconciled = await port.ReconcileParcelAsync(receipt, f.Journal, f.Policy, f.Timeout.Token);
        Assert.Equal(QuestParcelOutcome.SourceAccepted, reconciled.Result?.Outcome);
        Assert.False(reconciled.Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    public async Task A_direct_one_unit_return_never_accepts_a_whole_stack_or_multiple_item_debit(uint remaining)
    {
        await using var f = await Fixture.Create(new Item(4, "Mold", 3));
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var sending = port.SendParcelAsync(f.Intent(QuestParcelDirection.Send), f.Journal, f.Policy, f.Timeout.Token);
        Assert.Equal((byte)0x63, (await f.ReadPacket()).Data[6]);
        f.Publish(f.Session.Snapshot with { Inventory = remaining == 0 ? f.Session.Snapshot.Inventory.Remove(4)
            : f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", remaining)), Gold = 4000 });
        f.Active("Synthetic sent to Exchanger");
        var result = await sending;
        Assert.True(result.Uncertain);
        Assert.Null(result.Result);
        Assert.True((await port.ReconcileParcelAsync(result, f.Journal, f.Policy, f.Timeout.Token)).Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Old_generation_durable_proof_reconciles_without_a_packet_on_the_new_connection()
    {
        await using var f = await Fixture.Create();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var receiving = port.ReceiveParcelAsync(f.Intent(QuestParcelDirection.Receive), f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(4, new(4, "Mold", 1)) });
        f.Active("Synthetic receipt: Exchanger|Mold");
        var receipt = await receiving;
        f.Publish(f.Session.Snapshot with { Generation = 18, ActiveStatus = null, Id = 8 });
        var result = await port.ReconcileParcelAsync(receipt, f.Journal, f.Policy, f.Timeout.Token);
        Assert.Equal(QuestParcelOutcome.Received, result.Result?.Outcome);
        Assert.False(result.Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData((byte)0x1C)]
    [InlineData((byte)0x30)]
    public async Task Manual_inventory_actions_while_waiting_for_ownership_invalidate_the_original_intent(byte opcode)
    {
        await using var f = await Fixture.Create();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var gate = (SemaphoreSlim)typeof(GameSession).GetField("actions", Private)!.GetValue(f.Session)!;
        await gate.WaitAsync(f.Timeout.Token);
        var receiving = port.ReceiveParcelAsync(f.Intent(QuestParcelDirection.Receive), f.Journal, f.Policy, f.Timeout.Token);
        f.Manual(opcode, 0, 4, 5);
        gate.Release();
        await Assert.ThrowsAsync<QuestBlockedException>(() => receiving);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)4)]
    public async Task Nonactive_chat_cannot_acknowledge_a_receive(byte messageType)
    {
        await using var f = await Fixture.Create();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var receiving = port.ReceiveParcelAsync(f.Intent(QuestParcelDirection.Receive), f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(4, new(4, "Mold", 1)),
            ActiveStatus = new(17, f.Session.Snapshot.Revision + 1, messageType, "Synthetic receipt: Exchanger|Mold", DateTimeOffset.UtcNow) });
        var result = await receiving;
        Assert.Null(result.Result);
        Assert.True(result.Uncertain);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_mismatched_fee_prevents_every_return_packet(bool known)
    {
        await using var f = await Fixture.Create(new Item(4, "Mold", 1));
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        f.Policy.Fee = known ? 2000u : null;
        await Assert.ThrowsAsync<QuestBlockedException>(() => port.SendParcelAsync(f.Intent(QuestParcelDirection.Send), f.Journal, f.Policy, f.Timeout.Token));
        Assert.Equal(0, f.ServerPeer.Available);
        Assert.Empty(f.Journal.Entries);
    }

    [Fact]
    public async Task A_quote_for_another_items_fee_cannot_authorize_a_native_return()
    {
        await using var f = await Fixture.Create(new Item(4, "Apple", 1));
        f.Policy.ItemFees = new(StringComparer.OrdinalIgnoreCase) { ["Mold"] = 1000, ["Apple"] = 17 };
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var intent = f.Intent(QuestParcelDirection.Send) with { Item = "Apple", QuotedFeeGold = 1000 };

        await Assert.ThrowsAsync<QuestBlockedException>(() => port.SendParcelAsync(intent, f.Journal, f.Policy, f.Timeout.Token));
        Assert.Empty(f.Journal.Entries);
        Assert.Equal(0, f.ServerPeer.Available);
        Assert.Equal(5000u, f.Session.Snapshot.Gold);
    }

    [Fact]
    public async Task A_changed_item_fee_before_wire_leaves_the_native_return_known_unsent()
    {
        await using var f = await Fixture.Create(new Item(4, "Mold", 1));
        f.Policy.ItemFees = new(StringComparer.OrdinalIgnoreCase) { ["Mold"] = 1000 };
        f.Journal.OnSave = checkpoint =>
        {
            if (checkpoint.WriteIntent) f.Policy.ItemFees["Mold"] = 17;
        };
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);

        await Assert.ThrowsAsync<QuestBlockedException>(() => port.SendParcelAsync(f.Intent(QuestParcelDirection.Send), f.Journal, f.Policy, f.Timeout.Token));
        var checkpoint = Assert.Single(f.Journal.Entries.Values);
        Assert.Equal(QuestParcelOutcome.NotSubmitted, checkpoint.Result?.Outcome);
        Assert.False(checkpoint.Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task An_explicitly_qualified_zero_fee_is_distinct_from_an_unknown_fee()
    {
        await using var f = await Fixture.Create(new Item(4, "Mold", 1));
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        f.Policy.Fee = 0;
        var sending = port.SendParcelAsync(f.Intent(QuestParcelDirection.Send) with { QuotedFeeGold = 0 }, f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        f.Active("Synthetic sent to Exchanger");
        Assert.Equal(QuestParcelOutcome.SourceAccepted, (await sending).Result?.Outcome);
    }

    [Fact]
    public async Task Protocol_interruption_cancels_a_direct_parcel_session_without_an_automatic_retry()
    {
        await using var f = await Fixture.Create();
        var port = Assert.IsAssignableFrom<IQuestParcelSession>(f.Session);
        var receiving = port.ReceiveParcelAsync(f.Intent(QuestParcelDirection.Receive), f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        typeof(ProxyConnection).GetMethod("Interrupt", Private)!.Invoke(f.Connection, ["Synthetic protocol interruption"]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receiving);
        Assert.True(Assert.Single(f.Journal.Entries.Values).Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    internal sealed class NativePolicy : IQuestParcelReceiptPolicy
    {
        public uint? Fee { get; set; } = 1000;
        public Dictionary<string, uint>? ItemFees;
        public bool SupportsReceiveAll(string profile) => profile == "synthetic-profile";
        public bool SupportsSend(string profile, bool fromStack) => profile == "synthetic-profile";
        public uint? SendFeeGold(string profile, string item) => profile != "synthetic-profile" ? null
            : ItemFees is null ? Fee : ItemFees.TryGetValue(item, out var fee) ? fee : null;
        public QuestParcelRoute? Route(string profile) => profile == "synthetic-profile" ? new(100, "Postmaster", new(2, 3)) : null;
        public TimeSpan ReplyTimeout => TimeSpan.FromMilliseconds(500);
        public TimeSpan ReceiveSpacing => TimeSpan.Zero;
        public int MaximumReceiveAttempts => 5;
        public QuestParcelReply? Interpret(QuestParcelObservation observation, QuestParcelCheckpoint checkpoint)
        {
            var status = observation.ActiveStatus;
            if (status is not { Type: 3 } || status.Generation != checkpoint.Before.Generation) return null;
            if (status.Text.StartsWith("Synthetic receipt: ", StringComparison.Ordinal))
            {
                var parts = status.Text[19..].Split('|');
                return new(QuestParcelOutcome.Received, QuestParcelReplySource.ActiveStatus, status.Revision, parts[0], parts[1], 1, 0);
            }
            if (status.Text == "Synthetic sent to Exchanger")
                return new(QuestParcelOutcome.SourceAccepted, QuestParcelReplySource.ActiveStatus, status.Revision, FeeGold: Fee);
            if (status.Text == "Synthetic accepted")
                return new(QuestParcelOutcome.SourceAccepted, QuestParcelReplySource.ActiveStatus, status.Revision);
            if (status.Text.StartsWith("Synthetic paid ", StringComparison.Ordinal) && uint.TryParse(status.Text[15..], out var paid))
                return new(QuestParcelOutcome.SourceAccepted, QuestParcelReplySource.ActiveStatus, status.Revision, FeeGold: paid);
            if (status.Text == "Synthetic empty") return new(QuestParcelOutcome.Empty, QuestParcelReplySource.ActiveStatus, status.Revision);
            return null;
        }
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public ProxyConnection Connection { get; }
        public GameSession Session { get; }
        public TcpClient ClientPeer { get; }
        public TcpClient ServerPeer { get; }
        public ParcelJournal Journal { get; } = new();
        public NativePolicy Policy { get; } = new();
        public CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(10));
        Fixture(ProxyConnection connection, TcpClient clientPeer, TcpClient serverPeer, Item[] items)
        {
            Connection = connection; ClientPeer = clientPeer; ServerPeer = serverPeer;
            typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, new CharacterSnapshot
            {
                Name = "Synthetic", Id = 7, Connected = true, Generation = 17, Revision = 5, Map = 100, Position = new(2, 3),
                InventoryKnown = true, Inventory = items.ToImmutableDictionary(i => i.Slot), Gold = 5000, Weight = 0, MaxWeight = 100,
                Entities = ImmutableDictionary<uint, Entity>.Empty.Add(42, new(42, "Postmaster", new(2, 4), true) { Kind = 2 }),
                Dialog = new(17, 5, 42, 1, "Postmaster", "Synthetic menu", false, 0, 0, 0,
                    [new("Receive Parcel", 0x64, 1), new("Send Parcel", 0x60, 2)])
            });
            string routes = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/routes.json"));
            Session = new(connection, new MapRepository("", routes, "synthetic-profile"));
        }
        public QuestParcelIntent Intent(QuestParcelDirection direction) => new("attempt-1", Guid.NewGuid(), "unit-1", direction,
            "Synthetic", "synthetic-profile", 17, direction == QuestParcelDirection.Send ? "Mold" : null, 1, direction == QuestParcelDirection.Send ? 1000u : 0u);
        public void Publish(CharacterSnapshot state) => typeof(StateTracker).GetMethod("Publish", Private)!.Invoke(Connection.Tracker, [state]);
        public void Gold(uint gold)
        {
            var message = new Arbiter.Net.Server.Messages.ServerStatusMessage { Fields = Arbiter.Net.Types.StatsFieldFlags.ExperienceGold, Gold = gold };
            var builder = new Arbiter.Net.Serialization.NetworkPacketBuilder(Arbiter.Net.Server.ServerCommand.Status);
            try { message.Serialize(ref builder); Connection.Tracker.Observe(8, builder.ToPacket().Data); }
            finally { builder.Dispose(); }
        }
        public void Active(string text) => Publish(Session.Snapshot with { ActiveStatus = new(17, Session.Snapshot.Revision + 1, 3, text, DateTimeOffset.UtcNow) });
        public void Manual(byte opcode, params byte[] payload) => typeof(ProxyConnection).GetMethod("ReportClient", Private)!.Invoke(Connection, [opcode, payload, false]);
        public void OrdinaryMenu() => Publish(Session.Snapshot with { Dialog = Session.Snapshot.Dialog! with
            { Revision = Session.Snapshot.Revision + 1, Choices = [new("Ordinary conversation", 0x21, 1)] } });
        public void Menu(byte type, ushort action, string? echo = null, byte[]? slots = null) => Publish(Session.Snapshot with
        {
            Dialog = new(17, Session.Snapshot.Revision + 1, 42, 1, "Postmaster", "Synthetic parcel prompt", false, action, 0, type, [], echo)
                { InventorySlots = (slots ?? []).ToImmutableArray() }
        });
        public async Task<ClientPacket> ReadPacket()
        {
            byte[] header = new byte[3]; await ServerPeer.GetStream().ReadExactlyAsync(header, Timeout.Token);
            byte[] body = new byte[(header[1] << 8) | header[2]]; await ServerPeer.GetStream().ReadExactlyAsync(body, Timeout.Token);
            return (ClientPacket)new ClientPacketEncryptor().Decrypt(new ClientPacket(body[0], body[1..]));
        }
        public static async Task<Fixture> Create(params Item[] items)
        {
            var (client, clientPeer) = await Pair(); var (remote, serverPeer) = await Pair();
            return new(new(client, remote, 17, _ => throw new InvalidOperationException("Synthetic tests never redirect.")), clientPeer, serverPeer, items);
        }
        static async Task<(TcpClient, TcpClient)> Pair()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var client = new TcpClient(); await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            return (client, await listener.AcceptTcpClientAsync());
        }
        public async ValueTask DisposeAsync()
        {
            Timeout.Cancel(); await Connection.DisposeAsync(); ClientPeer.Dispose(); ServerPeer.Dispose(); Timeout.Dispose();
        }
    }
}
