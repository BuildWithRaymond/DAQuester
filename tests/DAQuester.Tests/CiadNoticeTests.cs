using System.Buffers.Binary;
using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class CiadNoticeTests
{
    // Exact observed page; the entity and connection IDs in each fixture are synthetic.
    const string Greeting = "Greetings there, adventurous Aisling.  So, you lookin' for an adventure are you?";

    static byte[] Popup(string text = Greeting, ushort pursuit = 434, ushort step = 54)
    {
        var message = new ServerPursuitMessage
        {
            DialogType = DialogType.Popup, EntityType = EntityTypeFlags.Reactor,
            EntityId = 88, PursuitId = pursuit, StepId = step,
            Content = text, HasNextButton = true
        };
        var builder = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try { message.Serialize(ref builder); return builder.ToPacket().Data; }
        finally { builder.Dispose(); }
    }

    [Theory]
    [InlineData("captured")]
    [InlineData("unknown-text")]
    [InlineData("choice")]
    [InlineData("prompt")]
    [InlineData("npc")]
    [InlineData("stale-generation")]
    [InlineData("pursuit")]
    [InlineData("step")]
    [InlineData("merchant")]
    [InlineData("items")]
    [InlineData("map")]
    [InlineData("name")]
    [InlineData("no-next")]
    public async Task Only_the_observed_Ciad_greeting_is_eligible_for_cancellation(string variant)
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(0x30, Popup());
        var current = f.Session.Snapshot;
        var d = current.Dialog!;
        d = variant switch
        {
            "unknown-text" => d with { Text = Greeting + " Will you accept?" },
            "choice" => d with { Choices = [new("Yes", 434, 1)] },
            "prompt" => d with { Prompt = "Enter name" },
            "npc" => d with { EntityType = 1 },
            "stale-generation" => d with { Generation = current.Generation + 1 },
            "pursuit" => d with { Pursuit = 435 },
            "step" => d with { Step = 55 },
            "merchant" => d with { IsPursuit = false },
            "items" => d with { Items = [new("Synthetic item", 1)] },
            "name" => d with { Name = "Synthetic other quest" },
            "no-next" => d with { HasNext = false },
            _ => d
        };
        if (variant == "map") current = current with { Map = 501 };
        Assert.Equal(variant == "captured", EventNotices.Recognized(current with { Dialog = d }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bound_notice_cancellation_closes_once_before_movement_settlement(bool redMoon)
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(0x30, redMoon ? Popup(EventNoticeTests.RedMoon, 0, 1) : Popup());
        var expectedClose = DialogPackets.Close(f.Session.Snapshot, f.Session.Snapshot.Dialog!).Data.ToArray();
        int closes = 0;
        await Run(f, async packet =>
        {
            Assert.Equal((byte)0x3A, (byte)packet.Command);
            Assert.Equal(expectedClose, packet.Data.ToArray());
            Assert.Equal(1, ++closes);
            await f.Send(0x30, [10, 0]);
        }, ct => EventNotices.DismissAsync(f.Session, ct));
        Assert.Equal(1, closes);
        Assert.Null(f.Session.Snapshot.Dialog);
    }

    [Fact]
    public async Task Uncertain_greeting_cancellation_preserves_a_submitted_quest_transaction()
    {
        var session = new SimulatedSession();
        var before = session.Snapshot;
        session.Set(before with { Dialog = new(before.Generation, before.Revision + 1, 88, 4, "", Greeting,
            true, 434, 54, 0, []) { HasNext = true } });
        session.OnCommand = _ => throw new TimeoutException("Synthetic unconfirmed Ciad cancellation");
        var pending = new Transaction("synthetic-handoff", before.Generation, before.Revision, 99, 0, 0, DateTimeOffset.UtcNow);
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new() { Character = before.Name!, Quest = "notice-fixture", Pending = pending });
        var runner = new QuestRunner(session, journal,
            new("notice-fixture", 1, "Notice fixture", "Synthetic", [], [new("preflight", "probe", null)]), new());
        await runner.RunAsync();
        Assert.Equal("close", Assert.Single(session.Commands).Kind);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(pending), System.Text.Json.JsonSerializer.Serialize(runner.Job.Pending));
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Null(runner.Job.CompletedAt);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Ciad_greeting_during_travel_is_cancelled_before_the_next_step(bool fast, bool preservePopup)
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Session.FastMode = fast;
        int moves = 0, closes = 0;
        byte[]? expectedClose = null;
        await Run(f, async packet =>
        {
            switch ((byte)packet.Command)
            {
                case 6:
                    if (++moves == 1)
                    {
                        await f.Send(0x30, Popup());
                        await f.Session.WaitAsync(s => s.Dialog?.Text == Greeting,
                            f.Session.Snapshot.Revision - 1, TimeSpan.FromSeconds(2), default);
                        expectedClose = DialogPackets.Close(f.Session.Snapshot, f.Session.Snapshot.Dialog!).Data.ToArray();
                        await f.Position(new(1, 1));
                        break;
                    }
                    Assert.Equal(1, closes);
                    await Ack(f, packet);
                    break;
                case 0x3A:
                    Assert.Equal(expectedClose, packet.Data.ToArray());
                    Assert.Equal(1, ++closes);
                    await f.Send(0x30, [10, 0]);
                    break;
                case 0x38:
                    Assert.Equal(1, closes);
                    await f.Position(f.Session.Snapshot.Position!);
                    break;
                default:
                    Assert.Fail("Unexpected travel command " + packet.Command);
                    break;
            }
        }, ct => f.Session.ExecuteAsync(new("navigate", 500, new(3, 1), PreservePopup: preservePopup), ct));
        Assert.Equal(1, closes);
        Assert.Null(f.Session.Snapshot.Dialog);
        Assert.Equal(new Tile(3, 1), f.Session.Snapshot.Position);
    }

    static Task Ack(MovingObstacleTests.Fixture f, ClientPacket packet)
    {
        var here = f.Session.Snapshot.Position!;
        byte[] ack = new byte[10];
        ack[0] = packet.Data[0];
        BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(1), (ushort)here.X);
        BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(3), (ushort)here.Y);
        return f.Send(11, ack);
    }

    static async Task Run(MovingObstacleTests.Fixture f, Func<ClientPacket, Task> response, Func<CancellationToken, Task> action)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var decrypt = new ClientPacketEncryptor();
        var server = Task.Run(async () =>
        {
            while (true)
            {
                byte[] header = new byte[3];
                await f.Server.GetStream().ReadExactlyAsync(header, limit.Token);
                byte[] data = new byte[(header[1] << 8) | header[2]];
                await f.Server.GetStream().ReadExactlyAsync(data, limit.Token);
                await response((ClientPacket)decrypt.Decrypt(new ClientPacket(data[0], data[1..])));
            }
        });
        var running = action(limit.Token);
        try
        {
            await Task.WhenAny(server, running);
            if (server.IsFaulted) await server;
            await running;
        }
        finally
        {
            limit.Cancel();
            try { await server; }
            catch (OperationCanceledException) { }
        }
    }
}
