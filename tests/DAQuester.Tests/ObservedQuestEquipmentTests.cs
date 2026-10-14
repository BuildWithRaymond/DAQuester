using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class ObservedQuestEquipmentTests
{
    [Fact]
    public async Task Glioca_removes_weapon_and_shield_with_separate_bag_receipts()
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Level = 99,
            Experience = 1000,
            LegendsKnown = true,
            Equipment = ImmutableDictionary<byte, string>.Empty.Add(1, "Oak Stick").Add(3, "Synthetic Shield")
        });
        s.OnCommand = c =>
        {
            if (c.Kind != "unequip")
                return;
            byte slot = c.Slot!.Value;
            string name = s.Snapshot.Equipment[slot];
            s.Set(s.Snapshot with
            {
                Equipment = s.Snapshot.Equipment.Remove(slot),
                Inventory = s.Snapshot.Inventory.SetItem((byte)(slot + 20), new((byte)(slot + 20), name, 1))
            });
        };
        var j = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await j.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true)
        });
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("glioca-unarmed", "unequip-armaments")]
        };
        var runner = new QuestRunner(s, j, recipe, new());
        await runner.RunAsync();
        Assert.Equal(new byte?[] { 1, 3 }, s.Commands.Where(c => c.Kind == "unequip").Select(c => c.Slot));
        Assert.Empty(s.Snapshot.Equipment);
        Assert.Null(runner.Job.Pending);
        Assert.Equal(1, runner.Job.Route?.Task);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unarmed_gate_requires_both_slot_removal_and_bag_receipt(bool bagReceipt)
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Level = 99,
            Experience = 1000,
            LegendsKnown = true,
            Equipment = ImmutableDictionary<byte, string>.Empty.Add(1, "Oak Stick")
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "unequip")
                s.Set(s.Snapshot with
                {
                    Equipment = ImmutableDictionary<byte, string>.Empty,
                    Inventory = bagReceipt ? s.Snapshot.Inventory.SetItem(3, new(3, "Oak Stick", 1)) : s.Snapshot.Inventory
                });
        };
        var j = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        await j.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true)
        });
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("glioca-unarmed", "unequip-weapon")]
        };
        var r = new QuestRunner(s, j, recipe, new());
        await r.RunAsync();
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "unequip");
        if (bagReceipt)
        {
            Assert.Null(r.Job.Pending);
            Assert.Equal(1, r.Job.Route?.Task);
        }
        else
        {
            Assert.NotNull(r.Job.Pending);
            Assert.Equal(0, r.Job.Route?.Task);
        }
        Assert.Null(r.Job.CompletedAt);
    }
}
