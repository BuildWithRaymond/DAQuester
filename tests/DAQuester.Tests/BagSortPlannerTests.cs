using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class BagSortPlannerTests
{
    [Fact]
    public void EmptyBagNeedsNoMoves()
    {
        var plan = BagSortPlanner.Create(Bag());
        Assert.Empty(plan.Original);
        Assert.Empty(plan.Desired);
        Assert.Empty(plan.Swaps);
    }

    [Fact]
    public void MissingQuickItemsPackLeftInPriorityOrder()
    {
        var plan = BagSortPlanner.Create(Bag(I(22, "Dibenomum", 3), I(12, "Apple", 4), I(30, "Red Potion", 5), I(41, "Hemloch", 2)));
        Assert.Equal(new[] { "Red Potion", "Dibenomum", "Hemloch" }, At(plan, 1, 2, 3));
        Assert.Equal("Apple", plan.Desired[49].Name);
        AssertApplied(plan);
    }

    [Fact]
    public void ArrowsComeBeforeAllFiveQuickPotions()
    {
        var plan = BagSortPlanner.Create(Bag(I(31, "Hemloch"), I(30, "Dibenomum"), I(29, "Exkuranum"),
            I(28, "Red Potion"), I(27, "Komadium"), I(26, "Dual Crystal Arrows")));
        Assert.Equal(new[] { "Dual Crystal Arrows", "Komadium", "Red Potion", "Exkuranum", "Dibenomum", "Hemloch" }, At(plan, 1, 2, 3, 4, 5, 6));
        AssertApplied(plan);
    }

    [Fact]
    public void QuickStackAlreadyAtTargetWinsAndEveryDuplicateRemains()
    {
        var plan = BagSortPlanner.Create(Bag(I(1, "Komadium", 7), I(18, "Komadium", 3), I(30, "Komadium", 9), I(20, "Hemloch", 4)));
        Assert.Equal(7U, plan.Desired[1].Quantity);
        Assert.Equal("Hemloch", plan.Desired[2].Name);
        Assert.Equal(3U, plan.Desired[49].Quantity);
        Assert.Equal(9U, plan.Desired[50].Quantity);
        AssertApplied(plan);
    }

    [Fact]
    public void QuickMatchingTrimsAndIgnoresCase()
    {
        var plan = BagSortPlanner.Create(Bag(I(17, " red POTION "), I(18, "dUaL cRyStAl ArRoWs")));
        Assert.Equal(new[] { "dUaL cRyStAl ArRoWs", " red POTION " }, At(plan, 1, 2));
        AssertApplied(plan);
    }

    [Fact]
    public void AlphabeticalTrinketsFillTopRowFromRight()
    {
        var plan = BagSortPlanner.Create(Bag(I(21, "Wake Scroll"), I(22, "Glowing Stone"), I(23, "Nerve Stimulant"), I(31, "Red Potion")));
        Assert.Equal("Red Potion", plan.Desired[1].Name);
        Assert.Equal(new[] { "Glowing Stone", "Nerve Stimulant", "Wake Scroll" }, At(plan, 12, 11, 10));
        AssertApplied(plan);
    }

    [Fact]
    public void TrinketOverflowNeverReplacesQuickItems()
    {
        var inventory = Enumerable.Range(20, 13).Select(slot => I((byte)slot, "Wake Scroll", (uint)slot)).Append(I(1, "Red Potion")).ToDictionary(item => item.Slot);
        var plan = BagSortPlanner.Create(inventory);
        Assert.Equal("Red Potion", plan.Desired[1].Name);
        Assert.All(Enumerable.Range(2, 11), slot => Assert.Equal("Wake Scroll", plan.Desired[(byte)slot].Name));
        Assert.Equal(new[] { "Wake Scroll", "Wake Scroll" }, At(plan, 49, 50));
        Assert.Equal(20U, plan.Desired[12].Quantity);
        Assert.Equal(31U, plan.Desired[49].Quantity);
        Assert.Equal(32U, plan.Desired[50].Quantity);
        AssertApplied(plan);
    }

    [Fact]
    public void OrdinaryItemsUseCategoryOrderBeforeAlphabeticalNames()
    {
        string[] expected = ["Zinc Sword", "Iron Boots", "Gold Ring", "Blue Potion", "Emerald", "Book", "Fishing Rod", "Heart Effect", "Duckling", "Odd Object"];
        var plan = BagSortPlanner.Create(expected.Reverse().Select((name, i) => I((byte)(i + 1), name)).ToDictionary(item => item.Slot));
        Assert.Equal(expected, Enumerable.Range(49, expected.Length).Select(slot => plan.Desired[(byte)slot].Name));
        AssertApplied(plan);
    }

    [Fact]
    public void GearFamiliesStayAdjacentWithinTheirCategory()
    {
        var plan = BagSortPlanner.Create(Bag(I(10, "Copper Boots"), I(11, "Gold Shield"), I(12, "Iron Boots"), I(13, "Silver Shield")));
        Assert.Equal(new[] { "Copper Boots", "Iron Boots", "Gold Shield", "Silver Shield" }, At(plan, 49, 50, 51, 52));
        AssertApplied(plan);
    }

    [Fact]
    public void NamedClassificationPrecedesGenericWords()
    {
        var plan = BagSortPlanner.Create(Bag(I(1, "Dragon's Scale"), I(2, "Monster Cloak"), I(3, "Wake Scroll"),
            I(4, "Ruby Whip"), I(5, "Mushroom Cap"), I(6, "Silk White Dugon"), I(7, "Amusement Park Ticket")));
        Assert.Equal(new[] { "Dragon's Scale", "Monster Cloak", "Wake Scroll" }, At(plan, 12, 11, 10));
        Assert.Equal(new[] { "Ruby Whip", "Mushroom Cap", "Silk White Dugon", "Amusement Park Ticket" }, At(plan, 49, 50, 51, 52));
        AssertApplied(plan);
    }

    [Fact]
    public void OrdinaryItemsFillEveryBottomRowFromLeft()
    {
        var plan = BagSortPlanner.Create(Enumerable.Range(1, 25).Select(slot => I((byte)slot, $"Odd {slot:00}")).ToDictionary(item => item.Slot));
        Assert.Equal("Odd 01", plan.Desired[49].Name);
        Assert.Equal("Odd 11", plan.Desired[59].Name);
        Assert.Equal("Odd 12", plan.Desired[37].Name);
        Assert.Equal("Odd 23", plan.Desired[48].Name);
        Assert.Equal("Odd 24", plan.Desired[25].Name);
        Assert.Equal("Odd 25", plan.Desired[26].Name);
        AssertApplied(plan);
    }

    [Fact]
    public void FullBagRetainsAllStacksAndCanSortWithoutAnEmptySlot()
    {
        var inventory = Enumerable.Range(1, 59).Select(slot => I((byte)slot,
            slot == 17 ? "Red Potion" : slot <= 20 ? "Wake Scroll" : $"Odd {60 - slot:00}", (uint)slot,
            (ushort)(slot % 7), slot % 2 == 0)).ToDictionary(item => item.Slot);
        var plan = BagSortPlanner.Create(inventory);
        Assert.Equal(59, plan.Desired.Count);
        Assert.Equal("Red Potion", plan.Desired[1].Name);
        Assert.DoesNotContain((byte)60, plan.Desired.Keys);
        AssertApplied(plan);
    }

    [Fact]
    public void EqualNamesRetainOriginalSlotOrderAndExactAttributes()
    {
        var plan = BagSortPlanner.Create(Bag(I(8, "odd", 4, 90, true), I(9, "ODD", 5, 91), I(7, "Odd", 6, 92, true)));
        Assert.Equal(new[] { 6U, 4U, 5U }, new byte[] { 49, 50, 51 }.Select(slot => plan.Desired[slot].Quantity));
        Assert.Equal((ushort)92, plan.Desired[49].Sprite);
        Assert.True(plan.Desired[49].Stackable);
        AssertApplied(plan);
    }

    [Fact]
    public void SortedBagNeedsNoFurtherMoves()
    {
        var first = BagSortPlanner.Create(Bag(I(18, "Red Potion"), I(19, "Wake Scroll"), I(5, "Emerald"), I(6, "Iron Boots")));
        var second = BagSortPlanner.Create(first.Desired);
        Assert.Empty(second.Swaps);
        Assert.True(BagSortPlanner.Matches(first.Desired, second.Desired));
    }

    [Fact]
    public void FullBagOfIdenticalStacksNeedsNoObservableSwap()
    {
        var inventory = Enumerable.Range(1, 59).Select(slot => I((byte)slot, "Unknown")).ToDictionary(item => item.Slot);
        var plan = BagSortPlanner.Create(inventory);
        Assert.True(BagSortPlanner.Matches(plan.Original, plan.Desired));
        Assert.Empty(plan.Swaps);
    }

    [Fact]
    public void MixedDuplicateGroupsEmitOnlyObservableSwaps()
    {
        var inventory = Enumerable.Range(1, 59).Select(slot => I((byte)slot,
            slot == 1 ? "Zinc Sword" : slot >= 56 ? "Wake Scroll" : "Unknown")).ToDictionary(item => item.Slot);
        var plan = BagSortPlanner.Create(inventory);
        Assert.NotEmpty(plan.Swaps);
        var actual = plan.Original;
        foreach (var swap in plan.Swaps)
        {
            var next = BagSortPlanner.ApplySwap(actual, swap);
            Assert.False(BagSortPlanner.Matches(actual, next));
            actual = next;
        }
        Assert.True(BagSortPlanner.Matches(plan.Desired, actual));
        AssertApplied(plan);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DistinctDuplicatePayloadsStillRequireRealMoves(int difference)
    {
        var inventory = Enumerable.Range(1, 59).Select(slot => I((byte)slot, "Unknown")).ToDictionary(item => item.Slot);
        inventory[1] = difference switch
        {
            1 => inventory[1] with { Quantity = 2 },
            2 => inventory[1] with { Sprite = 12 },
            _ => inventory[1] with { Stackable = true }
        };
        var plan = BagSortPlanner.Create(inventory);
        Assert.NotEmpty(plan.Swaps);
        Assert.Equal(inventory[1] with { Slot = 49 }, plan.Desired[49]);
        AssertApplied(plan);
    }

    [Fact]
    public void ApplySwapExchangesWholeStacksAndNormalizesTheirSlots()
    {
        var a = I(1, "Apple", 4, 15, true);
        var b = I(2, "Sword", 1, 27);
        var result = BagSortPlanner.ApplySwap(Bag(a, b), new(1, 2));
        Assert.Equal(a with { Slot = 2 }, result[2]);
        Assert.Equal(b with { Slot = 1 }, result[1]);
    }

    [Fact]
    public void ApplySwapMovesIntoEmptySlotWithoutChangingSource()
    {
        var inventory = Bag(I(1, "Apple", 4, 15, true));
        var result = BagSortPlanner.ApplySwap(inventory, new(1, 59));
        Assert.Single(result);
        Assert.Equal((byte)59, result[59].Slot);
        Assert.Equal((byte)1, inventory[1].Slot);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(255)]
    public void InvalidItemSlotsCannotBePlannedOrMoved(byte slot)
    {
        var invalid = Bag(I(slot, "Apple"));
        Assert.Throws<ArgumentException>(() => BagSortPlanner.Create(invalid));
        Assert.Throws<ArgumentException>(() => BagSortPlanner.ApplySwap(invalid, new(slot, 1)));
    }

    [Fact]
    public void ItemSlotMustMatchItsInventoryKey()
    {
        Dictionary<byte, Item> invalid = new() { [1] = I(2, "Apple") };
        Assert.Throws<ArgumentException>(() => BagSortPlanner.Create(invalid));
        Assert.Throws<ArgumentException>(() => BagSortPlanner.ApplySwap(invalid, new(1, 2)));
        Assert.False(BagSortPlanner.Matches(invalid, invalid));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 60)]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    public void InvalidSwapCannotChangeInventory(byte source, byte target)
    {
        Assert.Throws<ArgumentException>(() => BagSortPlanner.ApplySwap(Bag(I(1, "Apple")), new(source, target)));
    }

    [Fact]
    public void MatchesRequiresCompleteExactInventory()
    {
        var original = Bag(I(1, "Apple", 4, 15, true));
        Assert.True(BagSortPlanner.Matches(original, Bag(original[1] with { })));
        Assert.False(BagSortPlanner.Matches(original, Bag()));
        Assert.False(BagSortPlanner.Matches(original, Bag(original[1] with { Quantity = 3 })));
        Assert.False(BagSortPlanner.Matches(original, Bag(original[1] with { Sprite = 16 })));
        Assert.False(BagSortPlanner.Matches(original, Bag(original[1] with { Stackable = false })));
        Assert.False(BagSortPlanner.Matches(original, Bag(I(2, "Apple", 4, 15, true))));
    }

    private static Item I(byte slot, string name, uint quantity = 1, ushort sprite = 0, bool stackable = false) =>
        new(slot, name, quantity) { Sprite = sprite, Stackable = stackable };

    private static Dictionary<byte, Item> Bag(params Item[] items) => items.ToDictionary(item => item.Slot);
    private static string[] At(BagSortPlan plan, params byte[] slots) => slots.Select(slot => plan.Desired[slot].Name).ToArray();

    private static void AssertApplied(BagSortPlan plan)
    {
        var actual = plan.Original;
        foreach (var swap in plan.Swaps)
        {
            actual = BagSortPlanner.ApplySwap(actual, swap);
            Assert.Equal(plan.Original.Count, actual.Count);
            Assert.All(actual, entry => Assert.Equal(entry.Key, entry.Value.Slot));
        }
        Assert.True(BagSortPlanner.Matches(plan.Desired, actual));
        var originalShapes = plan.Original.Values.Select(item => item with { Slot = 1 }).GroupBy(item => item).ToDictionary(group => group.Key, group => group.Count());
        var finalShapes = actual.Values.Select(item => item with { Slot = 1 }).GroupBy(item => item).ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(originalShapes.Count, finalShapes.Count);
        Assert.All(originalShapes, entry => Assert.Equal(entry.Value, finalShapes[entry.Key]));
    }
}
