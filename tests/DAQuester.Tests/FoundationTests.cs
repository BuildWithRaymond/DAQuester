using Xunit;
using DAQuester.Core;
using System.Collections.Immutable;

namespace DAQuester.Tests;

public class FoundationTests
{
    public static CharacterSnapshot Ready(int? level = 1, uint gold = 999) => new()
    {
        Name = "Nuclear",
        Id = 42,
        Connected = true,
        Generation = 1,
        Level = level,
        Gold = gold,
        Hp = 100,
        Weight = 1,
        MaxWeight = 100,
        InventoryKnown = true,
        Map = 500,
        Position = new(20, 20)
    };
    [Theory]
    [InlineData(1, EligibilityKind.Eligible)]
    [InlineData(11, EligibilityKind.Eligible)]
    [InlineData(12, EligibilityKind.Blocked)]
    [InlineData(null, EligibilityKind.Unknown)]
    public void Level_gate(int? level, EligibilityKind expected) => Assert.Equal(expected, StickEligibility.Check(Ready(level), null, DateTimeOffset.UtcNow).Kind);
    [Fact]
    public void Gold_excess_requires_banking()
    {
        Assert.Equal(EligibilityKind.Blocked, StickEligibility.Check(Ready(1, 1000), null, DateTimeOffset.UtcNow).Kind);
    }

    [Fact]
    public void Unknown_inventory_waits()
    {
        Assert.Equal(EligibilityKind.Unknown, StickEligibility.Check(Ready() with
        {
            InventoryKnown = false
        }, null, DateTimeOffset.UtcNow).Kind);
    }

    [Fact]
    public void Cooldown_blocks_until_eight_hours()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(EligibilityKind.Blocked, StickEligibility.Check(Ready(), now.AddHours(-7), now).Kind);
        Assert.Equal(EligibilityKind.Eligible, StickEligibility.Check(Ready(), now.AddHours(-8), now).Kind);
    }

    [Fact]
    public void Stack_updates_are_absolute_and_case_insensitive()
    {
        var s = Ready() with
        {
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Branch", 4)).Add(2, new(2, "branch", 2))
        };
        Assert.Equal(6, s.Count("Branch"));
    }
}
