using System.Text.Json;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class PacketRecordingTests
{
    static byte[] Payload(ServerMessage message)
    {
        var builder = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try
        {
            message.Serialize(ref builder);
            return ((ServerPacket)builder.ToPacket()).Data;
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void Both_directions_are_recorded_with_their_source_and_chat_text_stays_out()
    {
        var recorder = new ReplayRecorder();
        var tracker = new StateTracker(42);
        tracker.Identity("Aria", 7);
        tracker.Observed += (o, p) => recorder.Record(o, p, tracker.Snapshot);
        tracker.Observe(0x13, [1, 2, 3]);
        tracker.Observe(0x6F, [0]);
        tracker.Observe(0x0D, [0, 0, 0, 0, 0]);
        recorder.RecordClient(0x47, [8], false, tracker.Snapshot);
        recorder.RecordClient(0x13, [0], true, tracker.Snapshot);
        recorder.RecordClient(0x19, [1, 2, 3], false, tracker.Snapshot);
        string path = Path.GetTempFileName();
        recorder.Export(path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var events = doc.RootElement.GetProperty("Events").EnumerateArray().Select(e => (Opcode: e.GetProperty("Opcode").GetByte(), Direction: e.GetProperty("Direction").GetString(), Source: e.GetProperty("Source").ValueKind == JsonValueKind.Null ? null : e.GetProperty("Source").GetString())).ToArray();
        Assert.Equal(new (byte, string?, string?)[] { (0x13, "S", null), (0x6F, "S", null), (0x47, "C", "client"), (0x13, "C", "bot"), (0x19, "C", "client") }, events);
        Assert.Equal("", doc.RootElement.GetProperty("Events")[4].GetProperty("Payload").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("SchemaVersion").GetInt32());
    }

    [Fact]
    public void Credentials_never_leave_the_proxy()
    {
        Assert.All(new byte[] { 0x02, 0x03, 0x26 }, o => Assert.True(ProxyConnection.CarriesCredentials(o)));
        Assert.False(ProxyConnection.CarriesCredentials(0x47));
    }

    [Fact]
    public async Task Steps_sent_by_DAQuester_are_reported_as_bot_packets()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        var seen = new List<(byte Opcode, bool Bot)>();
        f.Connection.ClientObserved += (o, _, bot) => seen.Add((o, bot));
        await f.Walk(new(2, 1), (_, _) => Task.FromResult(false));
        Assert.Contains(((byte)0x06, true), seen);
    }

    [Fact]
    public async Task Raising_CON_sends_the_verified_0x47_0x10_packet()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        var seen = new List<(byte Opcode, byte[] Payload)>();
        f.Connection.ClientObserved += (o, p, _) => seen.Add((o, p));
        await f.Session.ExecuteAsync(new("raise-stat", Amount: 16), default);
        var sent = Assert.Single(seen, s => s.Opcode == 0x47);
        Assert.Equal(new byte[] { 0x10 }, sent.Payload);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("raise-stat", Amount: 3), default));
    }

    [Fact]
    public void Walking_back_onto_the_tile_just_left_three_times_is_a_bounce()
    {
        var guard = new DAQuester.DarkAges.BounceGuard(3);
        Tile a = new(8, 40), b = new(8, 39);
        guard.Stepped(1, a);
        Assert.False(guard.Bounced(1, a));
        guard.Stepped(1, b);
        Assert.False(guard.Bounced(1, b));
        guard.Stepped(1, a);
        Assert.True(guard.Bounced(1, a));
        guard.Stepped(1, a);
        Assert.False(guard.Bounced(1, new(9, 40)));
    }

    // Carlos's real reply to a repair (Rucesion Armor Shop, 2026-10-12).
    static readonly byte[] repaired = Convert.FromHexString("000100002d3501401e0001401e0000064361726c6f73001e596f7572206974656d732068617665206265656e2072657061697265642e0107476f206261636b0037");

    static byte[] MerchantSays(string text)
    {
        var said = System.Text.Encoding.ASCII.GetBytes(text);
        return [.. repaired[..22], (byte)(said.Length >> 8), (byte)said.Length, .. said, .. repaired[(24 + 30)..]];
    }

    [Fact]
    public void Durability_messages_are_tracked_and_a_repair_clears_them()
    {
        var tracker = new StateTracker(42);
        tracker.Identity("Aria", 7);
        var text = System.Text.Encoding.ASCII.GetBytes("The durability of Ragged Shirt is now 25%");
        tracker.Observe(0x0A, [3, (byte)(text.Length >> 8), (byte)text.Length, .. text]);
        Assert.Equal(25, tracker.Snapshot.Durability["ragged shirt"]);
        Assert.Equal(25, tracker.Snapshot.WorstDurability);
        tracker.Observe(0x2F, repaired);
        Assert.Null(tracker.Snapshot.WorstDurability);
        Assert.Equal("Your items have been repaired.", tracker.Snapshot.Dialog?.Text);
    }

    [Fact]
    public async Task Repair_sends_pursuit_92_and_reads_the_merchants_answer()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(7, Payload(new ServerDrawObjectsMessage { Entities = [new ServerCreatureEntity { Id = 0x2D35, X = 1, Y = 3, Name = "Carlos" }] }));
        var sent = new List<(byte Opcode, byte[] Payload)>();
        f.Connection.ClientObserved += (o, p, _) => sent.Add((o, p));
        var repair = f.Session.ExecuteAsync(new("repair", EntityId: 0x2D35), default);
        await Task.Delay(100);
        await f.Send(0x2F, repaired);
        await repair;
        Assert.Equal(new byte[] { 1, 0, 0, 0x2D, 0x35, 0, 0x5C }, sent.Single(s => s.Opcode == 0x39).Payload);

        var broke = f.Session.ExecuteAsync(new("repair", EntityId: 0x2D35), default);
        await Task.Delay(100);
        await f.Send(0x2F, MerchantSays("Come back when you have money."));
        var e = await Assert.ThrowsAsync<ResourceUnavailableException>(() => broke);
        Assert.Contains("Come back when you have money.", e.Message);
    }

    [Fact]
    public void The_full_stat_block_is_kept()
    {
        var tracker = new StateTracker(42);
        tracker.Identity("Aria", 7);
        tracker.Observe(0x08, Payload(new ServerStatusMessage { Fields = StatsFieldFlags.Stats, Level = 5, AbilityLevel = 0, MaxHealth = 940, MaxMana = 306, Strength = 5, Intelligence = 3, Wisdom = 3, Constitution = 9, Dexterity = 3, HasStatPoints = false, StatPoints = 0, MaxWeight = 52, Weight = 18, Unknown = 0 }));
        Assert.Equal(new CharacterStats(5, 3, 3, 9, 3, 0), tracker.Snapshot.Stats);
        Assert.Equal(940u, tracker.Snapshot.MaxHp);
    }
}
