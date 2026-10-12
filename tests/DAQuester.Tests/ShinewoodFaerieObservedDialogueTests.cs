using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;
// Dialogue bodies and IDs below decode actual sanitized server packets. The
// runner, rewards, character, connection and transport remain synthetic.
public class ShinewoodFaerieObservedDialogueTests
{
    const string Pet = ShinewoodFaerieEvidence.Pet, Id = ShinewoodFaerieEvidence.Id;
    static QuestDefinition Recipe() => new(Id, 1, "Shinewood Faerie", "captured dialogue regression", ["navigate", "dialog", "inventory", "speech", "spell", "item"], [new("preflight", "faerie-preflight", "visit"), new("visit", "faerie-visit", "return"), new("return", "faerie-return", "complete")], 0)
    {
        Repeatable = false
    };
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    static CharacterSnapshot State(string cls = "Rogue") => new()
    {
        Name = "Synthetic",
        Id = 42,
        Connected = true,
        Generation = 1,
        Level = 99,
        Hp = 100,
        Mana = 100,
        Gold = 99,
        Weight = 1,
        MaxWeight = 100,
        Class = cls,
        IsMaster = false,
        IsPure = false,
        InventoryKnown = true,
        SpellsKnown = true,
        Map = 500,
        Position = new(20, 20),
        Inventory = ImmutableDictionary<byte, Item>.Empty.Add(9, new(9, "Rucesion Song", 11))
    };
    static Dictionary<ushort, Dialog> Pages()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/evidence/datester-7.41/shinewood-faerie-pet-wait.json"));
        using var fixture = JsonDocument.Parse(File.ReadAllText(path));
        var tracker = new StateTracker(1);
        tracker.Identity("Synthetic", 42);
        var pages = new Dictionary<ushort, Dialog>();
        foreach (var e in fixture.RootElement.GetProperty("Events").EnumerateArray())
        {
            tracker.Observe(e.GetProperty("Opcode").GetByte(), Convert.FromBase64String(e.GetProperty("Payload").GetString()!));
            if (tracker.Snapshot.Dialog is { } d)
                pages[d.Step] = d;
        }

        return pages;
    }

    static SimulatedSession Session(CharacterSnapshot state, Dictionary<ushort, Dialog> pages, FileQuestJournal journal, bool threshold = false)
    {
        var s = new SimulatedSession();
        foreach (var cap in Recipe().Capabilities)
            ((HashSet<string>)s.Capabilities).Add(cap);
        s.Set(state);
        void Page(ushort step) => s.Set(s.Snapshot with
        {
            Dialog = pages[step] with
            {
                Generation = s.Snapshot.Generation,
                Revision = s.Snapshot.Revision + 1
            }
        });
        s.OnCommand = c =>
        {
            switch (c.Kind)
            {
                case "visit":
                    s.Set(s.Snapshot with
                    {
                        Map = 992,
                        Position = ShinewoodFaerieEvidence.Approach,
                        Entities = []
                    });
                    break;
                case "say":
                    Assert.NotNull(journal.Load("Synthetic", Id).Pending);
                    Page(0);
                    break;
                case "choose":
                    Assert.Equal(s.Snapshot.Dialog, c.Dialog);
                    Page(c.Text == "I want a Faerie as a pet." ? (ushort)28 : (ushort)96);
                    break;
                case "next":
                    Page(c.Dialog!.Step switch
                    {
                        0 => 1,
                        28 => 32,
                        32 => 51,
                        51 => 54,
                        _ => throw new InvalidOperationException("Unexpected captured page")
                    });
                    break;
                case "close":
                    s.Set(s.Snapshot with
                    {
                        Dialog = null
                    });
                    break;
                case "use-item":
                    Assert.Equal("Rucesion Song", c.ExpectedResource);
                    s.Set(s.Snapshot with
                    {
                        Map = threshold ? 3082 : 505,
                        MapName = threshold ? "Rucesion Threshold" : "Rucesion Village",
                        Position = new(3, 1)
                    });
                    break;
            }
        };
        return s;
    }

    [Fact]
    public async Task Captured_pet_wait_closes_reply_and_uses_rucesion_song_without_claiming_pet()
    {
        var pages = Pages();
        var j = Journal();
        var s = Session(State(), pages, j, threshold: true);
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.True(r.Job.Status == "Completed", r.Job.Reason);
        Assert.Null(r.Job.Pending);
        Assert.NotNull(r.Job.FaerieReturn);
        Assert.Equal(0, s.Snapshot.Count(Pet));
        Assert.Empty(r.Job.FaerieReceipts);
        Assert.DoesNotContain(Pet, r.Job.Rewards.Keys);
        Assert.Contains(Pet, r.Job.FaerieUnavailable);
        Assert.Contains("one deoch", r.Job.CompletionText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pet Faerie verified", r.Job.CompletionText!);
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.DoesNotContain(s.Commands, c => c.Kind == "spell" || c.Text == "Do you know any spells you can teach me?");
        Assert.False(r.Job.Repeat);
        Assert.Null(r.Job.NextRun);
    }

    [Fact]
    public async Task Captured_nonpriest_spell_refusal_resolves_unknown_class_without_repeated_greetings()
    {
        var pages = Pages();
        var j = Journal();
        var state = State() with
        {
            Class = null,
            IsMaster = null,
            IsPure = null,
            Inventory = State().Inventory.Add(8, new(8, Pet, 1))
        };
        var s = Session(state, pages, j);
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.True(r.Job.Status == "Completed", r.Job.Reason);
        Assert.Single(s.Commands, c => c.Text == "Do you know any spells you can teach me?");
        Assert.Single(s.Commands, c => c.Kind == "say");
        Assert.Empty(s.Snapshot.Spells);
        Assert.Equal(6, r.Job.FaerieUnavailable.Length);
        Assert.Single(s.Commands, c => c.Kind == "use-item");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Saved_captured_pet_wait_reconciles_after_manual_close_and_manual_song_without_resubmission(long generation)
    {
        var page = Pages()[54];
        var j = Journal();
        var state = State() with
        {
            Generation = generation,
            Map = 3082,
            MapName = "Rucesion Threshold",
            Position = new(3, 1)
        };
        var pending = new Transaction("faerie-goal", 1, page.Revision - 1, 99, 0, 0, DateTimeOffset.UtcNow.AddMinutes(-1), TerminalSeen: true)
        {
            EntityId = page.EntityId,
            EntityType = page.EntityType,
            TerminalRevision = page.Revision,
            InventoryBaselineKnown = true,
            SpellsBaselineKnown = true
        };
        await j.SaveAsync(new()
        {
            Character = "Synthetic",
            Quest = Id,
            Step = "visit",
            FaerieGoals = [Pet],
            FaerieCurrentGoal = Pet,
            Pending = pending,
            CompletionDialog = page
        });
        var s = Session(state, Pages(), j);
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.True(r.Job.Status == "Completed", r.Job.Reason);
        Assert.Null(r.Job.Pending);
        Assert.Contains("one deoch", r.Job.CompletionText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_pet_wait_requires_the_connected_job_character(bool disconnected)
    {
        var page = Pages()[54];
        var j = Journal();
        var pending = new Transaction("faerie-goal", 1, page.Revision - 1, 99, 0, 0, DateTimeOffset.UtcNow, TerminalSeen: true)
        {
            EntityId = page.EntityId,
            EntityType = page.EntityType,
            TerminalRevision = page.Revision
        };
        await j.SaveAsync(new()
        {
            Character = "Synthetic",
            Quest = Id,
            Step = "visit",
            FaerieGoals = [Pet],
            FaerieCurrentGoal = Pet,
            Pending = pending,
            CompletionDialog = page
        });
        var s = Session(State() with
        {
            Map = 3082
        }, Pages(), j);
        var r = new QuestRunner(s, j, Recipe(), new());
        s.Set(s.Snapshot with
        {
            Name = disconnected ? "Synthetic" : "Other",
            Connected = !disconnected
        });
        await r.RunAsync();
        Assert.Equal("Paused", r.Job.Status);
        Assert.NotNull(r.Job.Pending);
        Assert.Null(r.Job.CompletedAt);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData("entity")]
    [InlineData("generation")]
    [InlineData("revision")]
    [InlineData("nonterminal")]
    public async Task Saved_pet_wait_requires_matching_terminal_submission(string mismatch)
    {
        var page = Pages()[54];
        var j = Journal();
        var pending = new Transaction("faerie-goal", 1, page.Revision - 1, 99, 0, 0, DateTimeOffset.UtcNow)
        {
            EntityId = page.EntityId,
            EntityType = page.EntityType,
            TerminalSeen = mismatch != "nonterminal",
            TerminalRevision = page.Revision
        };
        var saved = page with
        {
            EntityId = mismatch == "entity" ? 999 : page.EntityId,
            Generation = mismatch == "generation" ? 2 : 1,
            Revision = mismatch == "revision" ? pending.Revision : page.Revision
        };
        await j.SaveAsync(new()
        {
            Character = "Synthetic",
            Quest = Id,
            Step = "visit",
            FaerieGoals = [Pet],
            FaerieCurrentGoal = Pet,
            Pending = pending,
            CompletionDialog = saved
        });
        var s = Session(State() with
        {
            Map = 505
        }, Pages(), j);
        var r = new QuestRunner(s, j, Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Paused", r.Job.Status);
        Assert.NotNull(r.Job.Pending);
        Assert.Empty(s.Commands);
        Assert.Null(r.Job.CompletedAt);
    }

    [Fact]
    public void Rucesion_threshold_is_safe_but_woodland_threshold_is_not()
    {
        Assert.True(ShinewoodFaerieEvidence.SafeArrival(State() with
        {
            Map = 3082,
            MapName = "Rucesion Threshold"
        }));
        Assert.False(ShinewoodFaerieEvidence.SafeArrival(State() with
        {
            Map = 3080,
            MapName = "Woodlands Threshold"
        }));
    }
}
