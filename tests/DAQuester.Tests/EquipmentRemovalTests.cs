using System.Collections.Immutable;
using System.Reflection;
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
public class EquipmentRemovalTests
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

    static void Set(MovingObstacleTests.Fixture f, CharacterSnapshot snapshot) =>
        typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(f.Connection.Tracker, snapshot);

    static CharacterSnapshot Equipped(MovingObstacleTests.Fixture f) => f.Session.Snapshot with
    {
        InventoryKnown = true,
        Equipment = ImmutableDictionary<byte, string>.Empty.Add(1, "Oak Stick")
    };

    [Theory]
    [InlineData("unknown")]
    [InlineData("full")]
    [InlineData("absent")]
    [InlineData("changed")]
    public async Task Invalid_removal_sends_nothing(string scenario)
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        var snapshot = Equipped(f);
        snapshot = scenario switch
        {
            "unknown" => snapshot with { InventoryKnown = false },
            "full" => snapshot with { Inventory = Enumerable.Range(1, 59).ToImmutableDictionary(i => (byte)i, i => new Item((byte)i, "Synthetic item", 1)) },
            "absent" => snapshot with { Equipment = snapshot.Equipment.Remove(1) },
            "changed" => snapshot with { Equipment = snapshot.Equipment.SetItem(1, "Different weapon") },
            _ => snapshot
        };
        Set(f, snapshot);
        int sends = 0;
        f.Connection.ClientObserved += (_, _, _) => sends++;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("unequip", Slot: 1, Text: "Oak Stick"), default));
        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task Removal_requires_equipment_and_matching_bag_receipt()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        Set(f, Equipped(f));
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int removes = 0;
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode == (byte)ClientCommand.RemoveEquipment)
            {
                removes++;
                sent.TrySetResult();
            }
        };
        var operation = f.Session.ExecuteAsync(new("unequip", Slot: 1, Text: "Oak Stick"), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await f.Send((byte)ServerCommand.RemoveEquip, Payload(new ServerRemoveEquipMessage { Slot = EquipmentSlot.Weapon }));
        await f.Session.WaitAsync(s => !s.Equipment.ContainsKey(1), 0, TimeSpan.FromSeconds(2), default);
        Assert.False(operation.IsCompleted, "Equipment disappearance alone is not an item receipt.");
        await f.Send((byte)ServerCommand.AddInventory, Payload(new ServerAddInventoryMessage { Slot = 2, Name = "Oak Stick", Quantity = 1 }));
        await operation;
        Assert.Equal(1, removes);
        Assert.Equal(1, f.Session.Snapshot.Count("Oak Stick"));
    }

    [Fact]
    public async Task Interrupted_removal_cannot_send_again_without_receipts()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        Set(f, Equipped(f));
        using var cancellation = new CancellationTokenSource();
        int removes = 0;
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode == (byte)ClientCommand.RemoveEquipment)
            {
                removes++;
                cancellation.Cancel();
            }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Session.ExecuteAsync(new("unequip", Slot: 1), cancellation.Token));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("unequip", Slot: 1), default));
        Assert.Equal(1, removes);
    }
}
