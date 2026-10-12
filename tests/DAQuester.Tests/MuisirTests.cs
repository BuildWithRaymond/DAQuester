using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class MuisirTests
{
    internal const string Id = "entered-muisir-village", Legend = "Entered Muisir Village", Mushroom = "Undine Mushroom", Potion = "Shrinking Potion";
    internal static QuestDefinition Recipe() => new(Id, 1, Legend, "https://www.vorlof.com/quests/muisirvillageentrance.html", ["navigate", "dialog", "inventory", "legend", "speech", "ground-item"], [new("preflight", "muisir-preflight", "gather"), new("gather", "muisir-gather", "kiril", new() { ["patrol"] = "[{\"X\":20,\"Y\":30},{\"X\":21,\"Y\":30}]" }), new("kiril", "muisir-dialog", "confirm"), new("confirm", "muisir-confirm", "complete")], 0)
    {
        Repeatable = false
    };
    internal static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    internal static SimulatedSession Session(int count = 0)
    {
        var s = new SimulatedSession();
        var caps = (HashSet<string>)s.Capabilities;
        caps.Add("legend");
        caps.Add("speech");
        caps.Add("ground-item");
        s.Set(s.Snapshot with
        {
            Level = 1,
            LegendsKnown = true,
            Inventory = count == 0 ? s.Snapshot.Inventory : s.Snapshot.Inventory.SetItem(6, new(6, Mushroom, (uint)count))
        });
        return s;
    }

    [Fact]
    public async Task Existing_own_legend_completes_without_navigation_or_claim()
    {
        var s = Session();
        s.Set(s.Snapshot with
        {
            Legends = ImmutableArray.Create(Legend + " - Deoch 223, Spring"),
            Gold = 100000
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync(true);
        Assert.Equal("Completed", r.Job.Status);
        Assert.NotNull(r.Job.CompletedAt);
        Assert.False(r.Job.Repeat);
        Assert.Null(r.Job.NextRun);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Unknown_or_invalid_level_never_walks(int? level)
    {
        var s = Session();
        s.Set(s.Snapshot with
        {
            Level = level
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Paused", r.Job.Status);
        Assert.Contains("level", r.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task Unknown_inventory_never_walks()
    {
        var s = Session();
        s.Set(s.Snapshot with
        {
            InventoryKnown = false
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Contains("inventory", r.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task Unregistered_legend_blocks_before_gathering()
    {
        var s = Session();
        s.Set(s.Snapshot with
        {
            Legends = ImmutableArray.Create("Fragile Chrysalis ((Unregistered))")
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Contains("registered", r.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(41)]
    [InlineData(99)]
    public async Task Any_level_from_one_can_gather_without_gold_gate(int level)
    {
        var s = Session();
        s.Set(s.Snapshot with
        {
            Level = level,
            Gold = 100000
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "collect-ground")
                s.Manual();
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await r.RunAsync(cancellation: limit.Token);
        Assert.Equal(504, s.Commands.Single(c => c.Kind == "collect-ground").Map);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "deposit");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Only_missing_mushrooms_are_gathered(int count)
    {
        var s = Session(count);
        s.OnCommand = c =>
        {
            if (c.Kind == "collect-ground")
                s.Set(s.Snapshot with
                {
                    Inventory = s.Snapshot.Inventory.SetItem(6, new(6, Mushroom, 5))
                });
            if (c.Kind == "visit")
                s.Manual();
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await r.RunAsync(cancellation: limit.Token);
        Assert.Equal(count == 5 ? 0 : 1, s.Commands.Count(c => c.Kind == "collect-ground"));
        Assert.All(s.Commands.Where(c => c.Kind == "collect-ground"), c => Assert.Equal(new InventoryGoal(Mushroom, 5), c.StopWhen));
        Assert.Equal(306, s.Commands.Single(c => c.Kind == "visit").Map);
    }

    [Fact]
    public async Task Dry_patrols_continue_until_five()
    {
        var s = Session(1);
        int walks = 0;
        s.OnCommand = c =>
        {
            if (c.Kind == "collect-ground" && ++walks == 9)
                s.Set(s.Snapshot with
                {
                    Inventory = s.Snapshot.Inventory.SetItem(6, new(6, Mushroom, 5))
                });
            if (c.Kind == "visit")
                s.Manual();
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await r.RunAsync(cancellation: limit.Token);
        Assert.Equal(9, walks);
        Assert.Equal(5, s.Snapshot.Count(Mushroom));
    }

    [Fact]
    public async Task Mushrooms_on_wizard_approach_count_before_patrol()
    {
        var s = Session(3);
        var recipe = Recipe();
        recipe = recipe with
        {
            Steps = recipe.Steps.Select(step => step.Action == "muisir-gather" ? step with { Arguments = new(step.Arguments!) { ["approach"] = "{\"X\":16,\"Y\":12}" } } : step).ToArray()
        };
        s.OnCommand = c =>
        {
            if (c.Kind == "collect-ground")
                s.Set(s.Snapshot with
                {
                    Inventory = s.Snapshot.Inventory.SetItem(6, new(6, Mushroom, 5))
                });
            if (c.Kind == "visit")
                s.Manual();
        };
        var r = new QuestRunner(s, Journal(), recipe, new());
        await r.RunAsync();
        Assert.Equal(new Tile(16, 12), s.Commands.Single(c => c.Kind == "collect-ground").Target);
        Assert.Equal(5, s.Snapshot.Count(Mushroom));
    }

    internal static QuestJob Pending(SimulatedSession s) => new()
    {
        Character = s.Snapshot.Name!,
        Quest = Id,
        Step = "confirm",
        Pending = new("muisir-turn-in", s.Snapshot.Generation, s.Snapshot.Revision, 0, 0, 0, DateTimeOffset.UtcNow)
        {
            EntityId = 8877,
            InventoryBaselineKnown = true,
            InventoryCounts = new()
            {
                [Mushroom] = 5,
                [Potion] = 2
            },
            Legends = []
        }
    };
    internal static void Reward(SimulatedSession s, int potions = 6, bool legend = true, int consumed = 5)
    {
        s.Set(s.Snapshot with
        {
            Inventory = s.Snapshot.Inventory.SetItem(6, new(6, Mushroom, (uint)(5 - consumed))).SetItem(8, new(8, Potion, (uint)potions)),
            Legends = legend ? ImmutableArray.Create(Legend + " - Deoch 223, Spring") : [],
            LegendsRevision = s.Snapshot.Revision + 1
        });
    }

    [Fact]
    public async Task Fresh_legend_and_five_consumed_mushrooms_and_four_new_potions_complete_once()
    {
        var s = Session(5);
        var j = Journal();
        await j.SaveAsync(Pending(s));
        Reward(s);
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync(true);
        Assert.Equal("Completed", r.Job.Status);
        Assert.Null(r.Job.Pending);
        Assert.Equal(4, r.Job.Rewards["item:" + Potion]);
        Assert.Equal(5, r.Job.Rewards["mushroomsConsumed"]);
        Assert.False(r.Job.Repeat);
        Assert.Null(r.Job.NextRun);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData(2, true, 5)]
    [InlineData(6, false, 5)]
    [InlineData(6, true, 0)]
    public async Task Existing_potions_or_missing_legend_or_unconsumed_mushrooms_cannot_complete(int potions, bool legend, int consumed)
    {
        var s = Session(5);
        var j = Journal();
        await j.SaveAsync(Pending(s));
        Reward(s, potions, legend, consumed);
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.Null(r.Job.CompletedAt);
        Assert.NotNull(r.Job.Pending);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "say" or "choose");
    }

    [Fact]
    public async Task Confirm_after_reconnect_uses_fresh_own_legend_without_resending()
    {
        var s = Session(5);
        var j = Journal();
        await j.SaveAsync(Pending(s));
        s.Set(s.Snapshot with
        {
            Generation = 2,
            Revision = 0,
            Name = "Nuclear"
        });
        Reward(s);
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task Ambiguous_claim_survives_restart_and_never_repeats_speech()
    {
        var s = Session(5);
        var j = Journal();
        await j.SaveAsync(Pending(s) with
        {
            Step = "kiril"
        });
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        r = new(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.NotNull(r.Job.Pending);
        Assert.Null(r.Job.CompletedAt);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "visit" or "say" or "choose");
    }

    [Fact]
    public async Task Two_characters_never_share_claim_evidence()
    {
        var a = Session(5);
        var b = Session(5);
        b.Set(b.Snapshot with
        {
            Name = "Clock"
        });
        var j = Journal();
        await j.SaveAsync(Pending(a));
        await j.SaveAsync(Pending(b));
        Reward(a);
        var r1 = new QuestRunner(a, j, Recipe(), new());
        var r2 = new QuestRunner(b, j, Recipe(), new());
        await Task.WhenAll(r1.RunAsync(), r2.RunAsync());
        Assert.Equal("Completed", r1.Job.Status);
        Assert.Null(r2.Job.CompletedAt);
        Assert.NotNull(r2.Job.Pending);
    }

    [Fact]
    public async Task Claim_speech_is_checkpointed_once_and_player_named_pages_are_followed()
    {
        var s = Session(5);
        var j = Journal();
        s.Set(s.Snapshot with
        {
            Entities = s.Snapshot.Entities.SetItem(8877, new(8877, "Kiril", new(5, 2), true))
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "visit")
                s.Set(s.Snapshot with
                {
                    Map = 306
                });
            if (c.Kind == "say")
            {
                Assert.Equal("muisir-turn-in", j.Load(s.Snapshot.Name!, Id).Pending?.Kind);
                Assert.Equal("Tiny Cave", c.Text);
                s.Set(s.Snapshot with
                {
                    Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 8877, 1, s.Snapshot.Name!, "Can you help me get inside?", true, 0, 1, 0, [], HasNext: true)
                });
            }

            if (c.Kind == "next")
            {
                Reward(s, potions: 4);
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
            }
        };
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.Single(s.Commands, c => c.Kind == "say");
        Assert.Single(s.Commands, c => c.Kind == "next");
    }

    [Fact]
    public async Task Fresh_character_with_five_finishes_introduction_then_claims_once()
    {
        var s = Session(5);
        s.Set(s.Snapshot with
        {
            Entities = s.Snapshot.Entities.SetItem(8877, new(8877, "Kiril", new(5, 2), true))
        });
        int speeches = 0;
        void Dialog(string text, bool next = true) => s.Set(s.Snapshot with
        {
            Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 8877, 1, "Kiril", text, true, 0, 1, 0, [], HasNext: next)
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "visit")
                s.Set(s.Snapshot with
                {
                    Map = 306
                });
            if (c.Kind == "say")
                Dialog(++speeches == 1 ? "Okay. I'll be right back." : "I told you it wasn't that hard to collect.");
            if (c.Kind == "next")
            {
                if (speeches == 1)
                    s.Set(s.Snapshot with
                    {
                        Dialog = null
                    });
                else
                {
                    Reward(s, potions: 4);
                    Dialog("Now go explore that cave! I gave you enough potion to explore the area several times.", false);
                }
            }

            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
            if (c.Kind == "inspect-self")
                s.Set(s.Snapshot with
                {
                    LegendsKnown = true,
                    LegendsRevision = s.Snapshot.Revision + 1
                });
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.Equal(2, speeches);
        Assert.Null(r.Job.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interrupted_introduction_reconciles_fresh_inventory_before_claiming(bool changed)
    {
        var s = Session(5);
        s.Set(s.Snapshot with
        {
            Inventory = s.Snapshot.Inventory.SetItem(8, new(8, Potion, 2)),
            Entities = s.Snapshot.Entities.SetItem(8877, new(8877, "Kiril", new(5, 2), true))
        });
        var job = Pending(s);
        job = job with
        {
            Step = "kiril",
            Pending = job.Pending! with
            {
                Kind = "muisir-briefing",
                TerminalSeen = true,
                TerminalRevision = s.Snapshot.Revision + 1
            }
        };
        var j = Journal();
        await j.SaveAsync(job);
        s.Set(s.Snapshot with
        {
            Inventory = s.Snapshot.Inventory.SetItem(6, new(6, Mushroom, changed ? 4u : 5u))
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "inspect-self")
                s.Set(s.Snapshot with
                {
                    LegendsRevision = s.Snapshot.Revision + 1
                });
            if (c.Kind == "visit")
                s.Set(s.Snapshot with
                {
                    Map = 306
                });
            if (c.Kind == "say")
            {
                Reward(s);
                s.Set(s.Snapshot with
                {
                    Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 8877, 1, "Kiril", "Now go explore that cave! I gave you enough potion to explore the area several times.", true, 0, 1, 0, [], HasNext: false)
                });
            }

            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
        };
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "inspect-self");
        if (changed)
        {
            Assert.Equal("Paused", r.Job.Status);
            Assert.NotNull(r.Job.Pending);
            Assert.DoesNotContain(s.Commands, c => c.Kind == "say");
        }
        else
        {
            Assert.Equal("Completed", r.Job.Status);
            Assert.Null(r.Job.Pending);
            Assert.Single(s.Commands, c => c.Kind == "say");
        }
    }

    [Fact]
    public async Task Repeated_introductions_are_bounded_and_preserved()
    {
        var s = Session(5);
        s.Set(s.Snapshot with
        {
            Entities = s.Snapshot.Entities.SetItem(8877, new(8877, "Kiril", new(5, 2), true))
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "say")
                s.Set(s.Snapshot with
                {
                    Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 8877, 1, "Kiril", "Okay. I'll be right back.", true, 0, 1, 0, [], HasNext: true)
                });
            if (c.Kind == "next")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
            if (c.Kind == "inspect-self")
                s.Set(s.Snapshot with
                {
                    LegendsRevision = s.Snapshot.Revision + 1
                });
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Paused", r.Job.Status);
        Assert.Equal(2, s.Commands.Count(c => c.Kind == "say"));
        Assert.Equal("muisir-briefing", r.Job.Pending?.Kind);
    }
}
