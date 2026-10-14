using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class ObservedQuestGatherTests
{
    [Theory]
    [InlineData("Mold", true)]
    [InlineData("Spider's Eye", false)]
    public async Task Shared_ground_sprite_requires_actual_item_name_and_never_discards_wrong_item(string actual, bool expected)
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Level = 99,
            Experience = 1000,
            LegendsKnown = true,
            Map = 1,
            Position = new(5, 10),
            GroundItems = ImmutableDictionary<uint, GroundItem>.Empty.Add(88, new(88, 338, new(4, 10)))
        });
        s.OnCommand = c => { if (c.Kind == "pickup") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, actual, 1)), GroundItems = ImmutableDictionary<uint, GroundItem>.Empty }); };
        var j = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        await j.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true)
        });
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("mold", "gather", 1, "", new(5, 10)) { Item = "Mold", Sprite = 338, Monster = 7 }]
        };
        var r = new QuestRunner(s, j, recipe, new());
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "pickup");
        Assert.Equal(1, s.Snapshot.Count(actual));
        Assert.DoesNotContain(s.Commands, c => c.Kind == "drop");
        Assert.Null(r.Job.Pending);
        Assert.Equal(expected ? 1 : 0, r.Job.Route?.Task);
        Assert.Null(r.Job.CompletedAt);
    }
}
