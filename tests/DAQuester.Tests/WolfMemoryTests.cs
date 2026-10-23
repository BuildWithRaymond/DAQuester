using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class WolfMemoryTests
{
    const string Id = "wolf-of-west-woodlands";
    const string Legend = "Hunted down a wolf of West Woodland";

    static CharacterMemory Memory(params string[] legends) => new()
    {
        Character = "Hunter", Level = 70, Legends = legends,
        LegendsObservedAt = DateTimeOffset.UnixEpoch
    };

    [Fact]
    public void Remembered_exact_wolf_mark_blocks_repeat_claim()
    {
        var memory = Memory(Legend);
        Assert.Equal(Legend, memory.CompletedLegend(Id));
        Assert.Contains("Already completed", memory.SelectionRestriction(Id)!.Reason);
    }

    [Fact]
    public void Similar_or_unobserved_wolf_marks_do_not_claim_completion()
    {
        Assert.Null(Memory("Never " + Legend, Legend + " apprentice", Legend + " - Deoch 223, Autumn").CompletedLegend(Id));
        Assert.Null((Memory(Legend) with { LegendsObservedAt = null }).CompletedLegend(Id));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(69, true)]
    [InlineData(70, false)]
    [InlineData(99, false)]
    public void Remembered_wolf_level_gate_retains_guide_uncertainty(int? level, bool blocked)
    {
        var restriction = (Memory() with { Level = level }).SelectionRestriction(Id);
        Assert.Equal(blocked, restriction is not null);
        if (restriction is not null)
        {
            Assert.Contains("70+", restriction.Reason);
            Assert.Contains("guide", restriction.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Current_own_fresh_marks_replace_memory_but_foreign_marks_do_not()
    {
        var memory = Memory();
        var current = new CharacterSnapshot
        {
            Name = "Hunter", Id = 1, Connected = true, Generation = 1,
            Revision = 5, LegendsKnown = true, LegendsRevision = 5,
            Legends = ImmutableArray.Create(Legend)
        };
        Assert.Equal(Legend, memory.CompletedLegend(Id, current));
        Assert.Null(memory.CompletedLegend(Id, current with { Name = "Other" }));
        Assert.Null(memory.CompletedLegend(Id, current with { LegendsRevision = 6 }));
    }
}
