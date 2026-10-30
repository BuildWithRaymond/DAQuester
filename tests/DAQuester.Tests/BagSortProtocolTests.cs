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

public class BagSortProtocolTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public async Task Sort_sends_one_native_inventory_move_and_waits_for_the_complete_server_bag()
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3) { Sprite = 12, Stackable = true });
        var sorting = f.Sort();
        Assert.False(sorting.IsCompleted);
        var packet = await f.ReadPacket();
        Assert.Equal((byte)0x30, (byte)packet.Command);
        Assert.Equal(new byte[] { 0, 7, 49 }, packet.Data);
        Assert.False(sorting.IsCompleted);

        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        Assert.False(sorting.IsCompleted);
        f.Publish(f.Session.Snapshot with { Inventory = Bag(new Item(49, "Synthetic item", 3) { Sprite = 12, Stackable = true }) });
        await sorting;
        Assert.Equal(0, f.ServerPeer.Available);
        Assert.Equal((byte)49, Assert.Single(f.Session.Snapshot.Inventory.Values).Slot);
    }

    [Fact]
    public async Task An_occupied_swap_requires_both_slots_and_preserves_quantities()
    {
        await using var f = await Fixture.Create(new Item(1, "Hemloch", 5), new Item(2, "Red Potion", 10));
        var sorting = f.Sort();
        var packet = await f.ReadPacket();
        Assert.Equal((byte)0x30, (byte)packet.Command);
        Assert.Equal(new byte[] { 1, 2 }, packet.Data[1..].Order().ToArray());
        f.Publish(f.Session.Snapshot with { Inventory = Bag(new Item(1, "Red Potion", 10), new Item(2, "Red Potion", 10)) });
        Assert.False(sorting.IsCompleted);
        f.Publish(f.Session.Snapshot with { Inventory = Bag(new Item(1, "Red Potion", 10), new Item(2, "Hemloch", 5)) });
        await sorting;
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("identity")]
    [InlineData("unknown")]
    [InlineData("dialog")]
    [InlineData("exchange")]
    public async Task A_changed_owner_or_unready_bag_sends_nothing(string change)
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        var s = f.Session.Snapshot;
        f.Publish(change switch
        {
            "generation" => s with { Generation = 18 },
            "identity" => s with { Id = null },
            "unknown" => s with { InventoryKnown = false },
            "dialog" => s with { Dialog = new(17, 6, 3, 1, "Synthetic", "A menu", false, 1, 1, 0, []) },
            _ => s with { Exchange = new(17, 6, 3, "Synthetic") }
        });
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Sort());
        Assert.DoesNotContain("Unsupported", error.Message);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData((byte)0x06, new byte[] { 2, 0 })]
    [InlineData((byte)0x30, new byte[] { 0, 1, 2 })]
    [InlineData((byte)0x1C, new byte[] { 7 })]
    public async Task Manual_input_while_waiting_for_action_ownership_invalidates_the_sort(byte opcode, byte[] payload)
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        var gate = (SemaphoreSlim)typeof(GameSession).GetField("actions", Private)!.GetValue(f.Session)!;
        await gate.WaitAsync(f.Timeout.Token);
        var sorting = f.Sort();
        f.Manual(opcode, payload);
        gate.Release();
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => sorting);
        Assert.DoesNotContain("Unsupported", error.Message);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task The_full_bag_is_revalidated_after_waiting_for_the_transport_send_lock()
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        var gate = (SemaphoreSlim)typeof(ProxyConnection).GetField("toServer", Private)!.GetValue(f.Connection)!;
        await gate.WaitAsync(f.Timeout.Token);
        var sorting = f.Sort();
        f.Publish(f.Session.Snapshot with { Inventory = Bag(new Item(7, "Synthetic item", 4)) });
        gate.Release();
        await Assert.ThrowsAsync<QuestBlockedException>(() => sorting);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_prewrite_observer_cannot_change_the_bag_after_validation_and_still_send()
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        f.Connection.ClientObserved += (opcode, _, bot) =>
        {
            if (opcode == 0x30 && bot)
                f.Publish(f.Session.Snapshot with { Inventory = Bag(new Item(7, "Another item", 3)) });
        };
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Sort());
        Assert.DoesNotContain("Unsupported", error.Message);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData((byte)0x07)]
    [InlineData((byte)0x08)]
    [InlineData((byte)0x0F)]
    [InlineData((byte)0x13)]
    [InlineData((byte)0x1A)]
    [InlineData((byte)0x1C)]
    [InlineData((byte)0x29)]
    [InlineData((byte)0x30)]
    [InlineData((byte)0x39)]
    [InlineData((byte)0x3A)]
    [InlineData((byte)0x3E)]
    [InlineData((byte)0x41)]
    [InlineData((byte)0x44)]
    [InlineData((byte)0x4A)]
    [InlineData((byte)0x55)]
    public async Task Conflicting_player_input_stops_waiting_without_interrupting_ordinary_quest_semantics(byte opcode)
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        int interruptions = 0;
        f.Session.Interrupted += _ => interruptions++;
        var sorting = f.Sort();
        await f.ReadPacket();
        f.Manual(opcode, 0, 1, 2);
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => sorting.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.DoesNotContain("Unsupported", error.Message);
        Assert.Equal(0, interruptions);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task An_unrelated_inventory_change_stops_the_sort_without_a_second_packet()
    {
        await using var f = await Fixture.Create(new Item(7, "Alpha", 3), new Item(8, "Bravo", 1));
        var sorting = f.Sort();
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(9, new Item(9, "Unexpected", 1)) });
        await Assert.ThrowsAsync<QuestBlockedException>(() => sorting.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_timed_out_swap_is_not_retried_by_the_command_or_a_new_hold()
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        var sorting = f.Sort();
        await f.ReadPacket();
        await Assert.ThrowsAsync<QuestBlockedException>(() => sorting.WaitAsync(TimeSpan.FromSeconds(6)));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Sort());
        Assert.Equal(0, f.ServerPeer.Available);

        f.Publish(f.Session.Snapshot with { Inventory = Bag(new Item(49, "Synthetic item", 3)) });
        await f.Sort();
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_cancelled_write_wait_does_not_repeat_the_uncertain_swap()
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        using var stop = new CancellationTokenSource();
        var sorting = f.Sort(stop.Token);
        await f.ReadPacket();
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sorting);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Sort());
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task An_already_sorted_bag_completes_without_any_wire_packet()
    {
        await using var f = await Fixture.Create(new Item(49, "Synthetic item", 3));
        await f.Sort();
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Sorting_requires_an_explicit_connection_generation()
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        var error = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("sort-bag"), f.Timeout.Token));
        Assert.DoesNotContain("Unsupported", error.Message);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_disconnect_while_awaiting_receipt_stops_without_retry()
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        var sorting = f.Sort();
        await f.ReadPacket();
        f.Connection.Tracker.Disconnect();
        await Assert.ThrowsAsync<QuestBlockedException>(() => sorting.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_second_command_waits_until_the_whole_sort_has_authoritative_receipts()
    {
        await using var f = await Fixture.Create(new Item(7, "Alpha", 3), new Item(8, "Bravo", 1));
        var sorting = f.Sort();
        var first = await f.ReadPacket();
        Assert.Equal((byte)0x30, (byte)first.Command);
        var other = f.Session.ExecuteAsync(new("unsupported-after-sort"), f.Timeout.Token);
        Assert.False(other.IsCompleted);
        f.ApplyObservedSwap(first);
        var second = await f.ReadPacket();
        Assert.Equal((byte)0x30, (byte)second.Command);
        Assert.False(other.IsCompleted);
        f.ApplyObservedSwap(second);
        await sorting;
        await Assert.ThrowsAsync<QuestBlockedException>(() => other);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public async Task Rearranging_another_pane_does_not_cancel_bag_sorting(byte pane)
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        var sorting = f.Sort();
        var packet = await f.ReadPacket();
        f.Manual(0x30, pane, 1, 2);
        f.ApplyObservedSwap(packet);
        await sorting;
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task An_inflight_manual_observer_cannot_fail_the_connection_after_sort_cleanup()
    {
        await using var f = await Fixture.Create(new Item(7, "Synthetic item", 3));
        var sorting = f.Sort();
        var packet = await f.ReadPacket();
        var inFlight = Assert.IsType<Action<byte, byte[], bool>>(typeof(ProxyConnection).GetField("ClientObserved", Private)!.GetValue(f.Connection));
        f.ApplyObservedSwap(packet);
        await sorting;
        f.Manual(0x1C, 49);
        Assert.Null(Record.Exception(() => inFlight(0x1C, [49], false)));
    }

    static ImmutableDictionary<byte, Item> Bag(params Item[] items) => items.ToImmutableDictionary(i => i.Slot);

    sealed class Fixture : IAsyncDisposable
    {
        public ProxyConnection Connection { get; }
        public GameSession Session { get; }
        public TcpClient ClientPeer { get; }
        public TcpClient ServerPeer { get; }
        public CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(12));

        Fixture(ProxyConnection connection, TcpClient clientPeer, TcpClient serverPeer, Item[] items)
        {
            Connection = connection;
            ClientPeer = clientPeer;
            ServerPeer = serverPeer;
            typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, new CharacterSnapshot
            {
                Name = "Synthetic", Id = 7, Connected = true, Generation = 17, Revision = 5,
                InventoryKnown = true, Inventory = Bag(items)
            });
            string routes = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/routes.json"));
            Session = new(connection, new MapRepository("", routes));
        }

        public Task Sort(CancellationToken? ct = null) => Session.ExecuteAsync(new("sort-bag", ExpectedGeneration: 17), ct ?? Timeout.Token);
        public void Publish(CharacterSnapshot snapshot) => typeof(StateTracker).GetMethod("Publish", Private)!.Invoke(Connection.Tracker, [snapshot]);
        public void Manual(byte opcode, params byte[] payload) => typeof(ProxyConnection).GetMethod("ReportClient", Private)!.Invoke(Connection, [opcode, payload, false]);
        public void ApplyObservedSwap(ClientPacket packet)
        {
            var bag = Session.Snapshot.Inventory.ToBuilder();
            byte source = packet.Data[1], target = packet.Data[2];
            var item = bag[source];
            bag.Remove(source);
            if (bag.TryGetValue(target, out var displaced)) bag[source] = displaced with { Slot = source };
            bag[target] = item with { Slot = target };
            Publish(Session.Snapshot with { Inventory = bag.ToImmutable() });
        }
        public async Task<ClientPacket> ReadPacket()
        {
            byte[] header = new byte[3];
            await ServerPeer.GetStream().ReadExactlyAsync(header, Timeout.Token);
            byte[] body = new byte[(header[1] << 8) | header[2]];
            await ServerPeer.GetStream().ReadExactlyAsync(body, Timeout.Token);
            return (ClientPacket)new ClientPacketEncryptor().Decrypt(new ClientPacket(body[0], body[1..]));
        }
        public static async Task<Fixture> Create(params Item[] items)
        {
            var (client, clientPeer) = await Pair();
            var (remote, serverPeer) = await Pair();
            return new(new(client, remote, 17, _ => throw new InvalidOperationException("Synthetic tests never redirect.")), clientPeer, serverPeer, items);
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
            Timeout.Cancel();
            await Connection.DisposeAsync();
            ClientPeer.Dispose();
            ServerPeer.Dispose();
            Timeout.Dispose();
        }
    }
}
