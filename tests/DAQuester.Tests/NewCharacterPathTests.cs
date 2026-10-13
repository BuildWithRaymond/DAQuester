using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;
using static DAQuester.Core.NewCharacterPath;

namespace DAQuester.Tests;

public class NewCharacterPathTests
{
    static CharacterSnapshot Peasant(int level, uint gold = 60_000, string[]? worn = null, string[]? bag = null)
    {
        var equipment = ImmutableDictionary<byte, string>.Empty;
        foreach (var name in worn ?? [])
            equipment = equipment.SetItem(Gear.First(g => g.Name == name).Slot, name);
        var inventory = ImmutableDictionary<byte, Item>.Empty;
        byte slot = 1;
        foreach (var name in bag ?? [])
            inventory = inventory.SetItem(slot, new Item(slot++, name, 1));
        return FoundationTests.Ready(level, gold) with { Name = "Kel", Equipment = equipment, Inventory = inventory };
    }

    static string[] Names(IEnumerable<StarterGear> gear) => gear.Select(g => g.Name).ToArray();

    [Fact]
    public void Gear_is_bought_at_its_level_the_belt_waits_for_the_necklace_and_the_dirk_is_skipped_once_a_saber_is_owned()
    {
        Assert.Empty(Due(Peasant(1)));
        Assert.Equal(["Dirk", "Wooden Shield"], Names(Due(Peasant(3))));
        Assert.Equal(["Wooden Shield"], Names(Due(Peasant(5, worn: ["Dirk"]))));
        Assert.Equal(["Wind Belt", "Sea Necklace"], Names(Due(Peasant(6, worn: ["Dirk", "Wooden Shield"]))));
        Assert.Equal(["Loures Saber"], Names(Due(Peasant(7, worn: ["Dirk", "Wooden Shield", "Wind Belt", "Sea Necklace"]))));
        // A Dirk from the tutorial in the bag counts as owned.
        Assert.DoesNotContain("Dirk", Names(Due(Peasant(4, bag: ["Dirk"]))));
        Assert.Empty(Due(Peasant(9, worn: ["Loures Saber", "Wooden Shield", "Wind Belt", "Sea Necklace"])));
    }

    [Fact]
    public void Owned_gear_is_equipped_once_the_level_allows_and_the_saber_replaces_the_dirk()
    {
        Assert.Equal(["Dirk", "Wooden Shield"], Names(ToEquip(Peasant(3, bag: ["Dirk", "Wooden Shield"]))));
        Assert.Empty(ToEquip(Peasant(2, bag: ["Wooden Shield"])));
        Assert.Equal(["Loures Saber"], Names(ToEquip(Peasant(7, worn: ["Dirk"], bag: ["Loures Saber"]))));
        Assert.Equal(["Loures Saber"], Names(ToEquip(Peasant(7, bag: ["Dirk", "Loures Saber"]))));
    }

    [Fact]
    public void Leveling_stops_at_each_shopping_level_then_at_seven()
    {
        Assert.Equal(2, NextStop(1));
        Assert.Equal(3, NextStop(2));
        Assert.Equal(6, NextStop(3));
        Assert.Equal(7, NextStop(6));
        Assert.Equal(7, NextStop(7));
    }

    [Fact]
    public void First_steps_needs_fifty_thousand_gold_and_crypt_climb_needs_the_first_steps_gear()
    {
        var poor = FirstStepsEligibility(Peasant(4, gold: 12_400));
        Assert.Equal(EligibilityKind.Blocked, poor.Kind);
        Assert.Contains("needs 50,000 gold; Kel has 12,400", poor.Reason);
        Assert.Equal(EligibilityKind.Eligible, FirstStepsEligibility(Peasant(4)).Kind);
        Assert.Equal(EligibilityKind.Blocked, FirstStepsEligibility(Peasant(8, worn: ["Loures Saber", "Wooden Shield", "Wind Belt", "Sea Necklace"])).Kind);

        var bare = ClimbEligibility(Peasant(8, worn: ["Dirk", "Wooden Shield"]));
        Assert.Equal(EligibilityKind.Blocked, bare.Kind);
        Assert.Equal("Missing Wind Belt, Sea Necklace, Loures Saber. Run Peasant's First Steps first.", bare.Reason);
        Assert.Equal(EligibilityKind.Eligible, ClimbEligibility(Peasant(8, worn: ["Loures Saber", "Wooden Shield", "Wind Belt"], bag: ["Sea Necklace"])).Kind);
        Assert.Equal(EligibilityKind.Blocked, ClimbEligibility(Peasant(11, worn: ["Loures Saber", "Wooden Shield", "Wind Belt", "Sea Necklace"])).Kind);
    }

    [Fact]
    public void Crypt_climb_warns_only_while_terror_of_the_alley_or_garden_is_still_possible()
    {
        var open = Peasant(8) with { LegendsKnown = true, Legends = ["Mileth mundane birth - Summer, Deoch, 25"] };
        Assert.Equal("Kel will reach level 11 and can never do Terror of the Alley / Garden. It can only be done up to level 9.", TerrorWarning("Kel", open));
        Assert.Null(TerrorWarning("Kel", open with { Legends = ["Slew Terror of the Garden (2)"] }));
        Assert.Null(TerrorWarning("Kel", open with { Level = 10 }));
        Assert.Null(TerrorWarning("Kel", open with { LegendsKnown = false }));
    }

    [Fact]
    public void Awakening_runs_only_in_the_tutorial_or_before_the_wake_up_rewards()
    {
        Assert.Equal(EligibilityKind.Eligible, AwakeningEvidence.Eligibility(Peasant(1) with { Map = AwakeningEvidence.TutorialMap }).Kind);
        Assert.Equal(EligibilityKind.Eligible, AwakeningEvidence.Eligibility(Peasant(3) with { Map = AwakeningEvidence.RoomMap }).Kind);
        Assert.Equal(EligibilityKind.Blocked, AwakeningEvidence.Eligibility(Peasant(4, bag: ["Dirk"]) with { Map = AwakeningEvidence.RoomMap }).Kind);
        Assert.Equal("Awakening only runs on a brand-new character still in the tutorial.", AwakeningEvidence.Eligibility(Peasant(4)).Reason);
    }

    [Fact]
    public void Shipped_recipes_match_the_catalog_and_the_runner()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content"));
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "catalog.json")));
        var entries = catalog.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        foreach (var (file, id, actions) in new[] { ("peasants-first-steps.v1.json", FirstStepsId, typeof(NewCharacterActions)), ("crypt-climb.v1.json", ClimbId, typeof(NewCharacterActions)), ("awakening.v1.json", AwakeningEvidence.Id, typeof(AwakeningActions)) })
        {
            var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root, "recipes", file)))!;
            Assert.Equal(id, recipe.Id);
            Assert.Single(entries, e => e.GetProperty("name").GetString() == recipe.Name);
            Assert.False(recipe.Repeatable);
            var runner = new QuestRunner(new MeleeBashingTests.Scripted(), new FileQuestJournal(Path.Combine(Path.GetTempPath(), "daq-" + Guid.NewGuid().ToString("N"))), recipe, new());
            Assert.All(recipe.Steps, s => Assert.IsType(actions, runner.Actions[s.Action]));
        }

        var terror = Assert.Single(entries, e => e.GetProperty("id").GetString()!.StartsWith("terror", StringComparison.Ordinal) && e.GetProperty("level").GetString() == "4 - 9");
        Assert.Equal("Terror of the Alley / Garden", terror.GetProperty("name").GetString());
    }
}
