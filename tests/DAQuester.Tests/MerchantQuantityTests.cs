using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// These server responses are synthetic, shaped by the observed Wine quantity prompt.
[Collection("Walking cadence")]
public class MerchantQuantityTests
{
    static byte[] Payload(ServerMessage message)
    {
        var builder = new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
        try
        {
            message.Serialize(ref builder);
            return builder.ToPacket().Data;
        }
        finally { builder.Dispose(); }
    }

    [Fact]
    public async Task Buy_returns_the_bound_quantity_prompt_without_claiming_an_item_receipt()
    {
        await using var fixture = await MovingObstacleTests.Fixture.Create();
        fixture.Connection.Tracker.Observe(8, Payload(new ServerStatusMessage { Fields = StatsFieldFlags.ExperienceGold, Gold = 1000 }));
        fixture.Connection.Tracker.Observe(0x2F, Payload(new ServerScreenMenuMessage
        {
            MenuType = DialogMenuType.ItemChoices,
            EntityType = EntityTypeFlags.Creature,
            EntityId = 77,
            Name = "Viveka",
            Content = "Synthetic stock",
            PursuitId = 74,
            ItemChoices = [new() { Name = "Wine", Price = 500 }]
        }));
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int selections = 0;
        fixture.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode == 0x39)
            {
                selections++;
                sent.TrySetResult();
            }
        };
        var purchase = fixture.Session.ExecuteAsync(new("buy", Text: "Wine"), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.Send(0x2F, Payload(new ServerScreenMenuMessage
        {
            MenuType = DialogMenuType.TextInputWithArgs,
            EntityType = EntityTypeFlags.Creature,
            EntityId = 77,
            Name = "Viveka",
            Content = "How many do you need?",
            Prompt = "Wine",
            PursuitId = 76
        }));
        await purchase;
        Assert.Equal(1, selections);
        Assert.Equal("Wine", fixture.Session.Snapshot.Dialog!.Prompt);
        Assert.Equal(3, fixture.Session.Snapshot.Dialog.Type);
        Assert.Equal(0, fixture.Session.Snapshot.Count("Wine"));
        Assert.Equal(1000u, fixture.Session.Snapshot.Gold);
    }
}
