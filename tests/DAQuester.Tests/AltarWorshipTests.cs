using System.Collections.Immutable;
using System.Text.Json;
using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using Xunit;
using static DAQuester.Core.AltarWorshipEvidence;
namespace DAQuester.Tests;

public class AltarWorshipTests
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString());
    // Torrance's shop list as Ferda received it on 2026-10-12.
    const string ShopPacket = "BAEAACHzAUAeAAFAHgAACFRvcnJhbmNlADdQZXJoYXBzIEkgY2FuIG9mZmVyIHlvdSBzb21ldGhpbmcgeW91IGFyZSBsb29raW5nIGZvcj8gAEoADoBcAAAAAGQNV29vZGVuIFNoaWVsZB1Cb3RoIEFsbCBMZXYgMyAoQUMgIC0zKSwgV3QgNIC8AAAAAu4OTGVhdGhlciBTaGllbGQdQm90aCBBbGwgTGV2MTUgKEFDICAtNSksIFd0IDWAvQAAAA6mDUJyb256ZSBTaGllbGQeQm90aCBBbGwgTGV2MzEgKEFDICAtOCksIFd0IDEwgOMAAAAH0BBMZWF0aGVyIEdhdW50bGV0HUFsbChleGNlcHQgTW9uayksIExldiA5LCBXdCAygEwAAAAD6AVCb290cx1BbGwoZXhjZXB0IE1vbmspLCBMZXYgMSwgV3QgMoBKAAAAE4gLQ3VyZWQgQm9vdHMeQWxsKGV4Y2VwdCBNb25rKSwgTGV2MjQsIERleCAxgHIAAAAnEA5TaGFncmVlbiBCb290cx5BbGwoZXhjZXB0IE1vbmspLCBMZXYzMywgRGV4IDKAcAAAACcQCkdyZXkgQm9vdHMdQWxsKGV4Y2VwdCBNb25rKSwgTGV2MTEsIFd0IDKA6gAAAB9ADkNvcmFsIEVhcnJpbmdzEUFsbCwgTGV2IDgsIEludCAygOgAAAALuA5CZXJ5bCBFYXJyaW5ncxFBbGwsIExldiA4LCBJbnQgMYDJAAAABdwNQm9uZSBOZWNrbGFjZRhBbGwsIExldjE1LCBIaXQgNSwgRGFtIDGAzAAAAAZADkFtYmVyIE5lY2tsYWNlEkFsbCwgTGV2MTQsIERhbSAyIIDSAAAATiAJUnVieSBSaW5nEkFsbCwgTGV2IDYsIEhwIDIwMIDRAAAAw1ANQW1ldGh5c3QgUmluZxtBbGwsIExldiAxMiwgSHAgMjAwLCBNcCAxMDA=";

    sealed class Scripted(string name, string? characterClass) : IGameSession
    {
        public CharacterSnapshot Snapshot { get; private set; } = FoundationTests.Ready(20, 5000) with { Name = name, Id = 7, Map = 500, Position = new(20, 20) };
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string> { "navigate", "dialog", "inventory", "legend", "status-message" };
        public event Action<CharacterSnapshot>? Changed;
        public event Action<string>? Interrupted
        {
            add
            {
            }
            remove
            {
            }
        }
        public List<GameCommand> Commands { get; } = [];
        public bool AltarTakes { get; set; } = true;
        public string? Reward
        {
            get; set;
        }
        public int Drops
        {
            get; set;
        }
        public void Set(CharacterSnapshot s)
        {
            Snapshot = s with
            {
                Revision = Snapshot.Revision + 1
            };
            Changed?.Invoke(Snapshot);
        }
        public Task ExecuteAsync(GameCommand c, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            Commands.Add(c);
            var s = Snapshot;
            CharacterSnapshot? next = c.Kind switch
            {
                "inspect-self" => s with { Class = characterClass, LegendsKnown = true, LegendsRevision = s.Revision + 1, Legends = Reward is { } mark && Drops >= Goal ? [mark] : [] },
                "visit" => s with { Map = ShopMap, Position = new(4, 6), Entities = s.Entities.SetItem(8691, new(8691, Torrance, new(3, 5), true)) },
                "open" => s with { Dialog = new(s.Generation, s.Revision + 1, 8691, 1, Torrance, "Hello.  What can I do for you?", false, 0, 0, 0, [new(Buy, 0x40, 1), new("Sell", 0x41, 2)]) },
                "choose" when c.Text == Buy => s with { Dialog = new Dialog(s.Generation, s.Revision + 1, 8691, 1, Torrance, "Perhaps I can offer you something you are looking for?", false, 74, 0, 4, []) { Items = [new("Wooden Shield", 100), new(Shield, ShieldPrice)] } },
                "buy" => s with { Gold = s.Gold - ShieldPrice, Inventory = s.Inventory.SetItem(14, new(14, Shield, 1)), Dialog = s.Dialog! with { Revision = s.Revision + 1 } },
                "close" => s with { Dialog = null },
                "navigate" => s with { Map = c.Map, Position = c.Target, Dialog = null },
                "drop" when AltarTakes => Dropped(s, c.Slot!.Value),
                _ => null
            };
            if (next is not null)
                Set(next);
            return Task.CompletedTask;
        }
        CharacterSnapshot Dropped(CharacterSnapshot s, byte slot)
        {
            Drops++;
            var after = s with
            {
                Inventory = s.Inventory.Remove(slot),
                Notice = new(s.Generation, s.Revision + 1, 3, Touch, DateTimeOffset.UtcNow)
            };
            return Drops >= Goal && Reward is not null && !IsPriest(characterClass) ? after with
            {
                Inventory = after.Inventory.SetItem(30, new(30, "Red Jade Ring", 1))
            } : after;
        }
        public async Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken token)
        {
            var end = DateTimeOffset.UtcNow.AddMilliseconds(Math.Min(200, timeout.TotalMilliseconds));
            do
            {
                token.ThrowIfCancellationRequested();
                if (Snapshot.Revision > after && test(Snapshot))
                    return Snapshot;
                await Task.Delay(2, token);
            } while (DateTimeOffset.UtcNow < end);
            throw new TimeoutException("Simulation evidence timeout");
        }
    }

    QuestContext Context(Scripted s, QuestJob? job = null) => new(s, new FileQuestJournal(dir), new(), job ?? new QuestJob { Character = s.Snapshot.Name!, Quest = Id });
    static AltarOffering[] Made(int count, DateTimeOffset last) => Enumerable.Range(0, count).Select(i => new AltarOffering(last.AddHours(3 * (i - count + 1)), 1, ShieldPrice, "25 experience!", 0)).ToArray();

    static async Task UntilScheduled(QuestContext c)
    {
        for (int i = 0; i < 200 && c.Job.Status != "Scheduled"; i++)
            await Task.Delay(5);
    }

    [Fact]
    public async Task First_offering_buys_a_shield_from_Torrance_drops_it_on_the_altar_and_waits_three_hours()
    {
        var q = new Scripted("Elara", "Priest");
        var c = Context(q);
        using var stop = new CancellationTokenSource();
        var run = new AltarWorshipActions().ExecuteAsync(c, new("run", "altar-run", "complete"), stop.Token);
        await UntilScheduled(c);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        var offering = Assert.Single(c.Job.AltarOfferings);
        Assert.Equal(ShieldPrice, offering.Paid);
        Assert.Null(c.Job.Pending);
        Assert.Equal(5000u - ShieldPrice, q.Snapshot.Gold);
        Assert.Equal(["inspect-self", "visit", "open", "choose", "buy", "close", "navigate", "drop"], q.Commands.Select(x => x.Kind));
        Assert.Equal(Shield, q.Commands.Single(x => x.Kind == "buy").Text);
        var drop = q.Commands.Single(x => x.Kind == "drop");
        Assert.Equal((Altar, (byte?)14, (uint?)1), (drop.Target, drop.Slot, drop.Amount));
        Assert.Equal(new GameCommand("navigate", Map, Stand, Tolerance: 1), q.Commands.Single(x => x.Kind == "navigate"));
        Assert.Equal(offering.At + Interval, NextOffering(c.Job));
        Assert.StartsWith("Offering 1 of 10 · next offering", Progress(c.Job));
    }

    [Fact]
    public async Task Scheduler_yields_after_verified_offering_without_claiming_quest_completion()
    {
        var session = new Scripted("Elara", "Priest");
        var journal = new FileQuestJournal(dir);
        var recipe = new QuestDefinition(Id, 1, "Altar Worship", "synthetic", [], [new("preflight", "altar-preflight", "run"), new("run", "altar-run", null)], 0) { Repeatable = false };
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync(singleAttempt: true, yieldScheduledWaits: true);
        Assert.Equal("Scheduled", runner.Job.Status);
        Assert.Equal("run", runner.Job.Step);
        Assert.Null(runner.Job.CompletedAt);
        Assert.Null(runner.Job.Pending);
        var offering = Assert.Single(runner.Job.AltarOfferings);
        Assert.Equal(offering.At + Interval, runner.Job.NextRun);
        Assert.Equal(1, session.Drops);
        Assert.Null(QueueAttemptOutcome.Evaluate(runner.Job));
    }

    [Fact]
    public async Task A_shield_already_in_the_bag_is_offered_without_buying()
    {
        var q = new Scripted("Elara", "Priest");
        q.Set(q.Snapshot with
        {
            Inventory = q.Snapshot.Inventory.SetItem(9, new(9, Shield, 1))
        });
        var c = Context(q);
        using var stop = new CancellationTokenSource();
        var run = new AltarWorshipActions().ExecuteAsync(c, new("run", "altar-run", "complete"), stop.Token);
        await UntilScheduled(c);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.DoesNotContain(q.Commands, x => x.Kind is "visit" or "buy");
        Assert.Equal(0u, Assert.Single(c.Job.AltarOfferings).Paid);
        Assert.Equal(5000u, q.Snapshot.Gold);
    }

    [Fact]
    public async Task An_offering_is_not_made_before_three_hours_have_passed()
    {
        var q = new Scripted("Elara", "Priest");
        var c = Context(q, new QuestJob { Character = "Elara", Quest = Id, AltarOfferings = Made(1, DateTimeOffset.UtcNow.AddHours(-2)) });
        using var stop = new CancellationTokenSource();
        var run = new AltarWorshipActions().ExecuteAsync(c, new("run", "altar-run", "complete"), stop.Token);
        await UntilScheduled(c);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.DoesNotContain(q.Commands, x => x.Kind == "drop");
        Assert.Single(c.Job.AltarOfferings);
    }

    [Fact]
    public async Task Tenth_offering_completes_a_priest_with_the_legend_mark()
    {
        var q = new Scripted("Elara", "Priest") { Reward = "Mileth Altar Worshipper (1) - Deoch 223" };
        var c = Context(q, new QuestJob { Character = "Elara", Quest = Id, AltarOfferings = Made(9, DateTimeOffset.UtcNow.AddHours(-3)) });
        q.Drops = 9;
        await new AltarWorshipActions().ExecuteAsync(c, new("run", "altar-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.Equal(10, c.Job.AltarOfferings.Length);
        Assert.Equal("Mileth Altar Worshipper (1) - Deoch 223", c.Job.CompletionLegend);
        Assert.Equal(225, c.Job.Rewards["experience"]);
    }

    [Fact]
    public async Task Tenth_offering_completes_another_class_with_a_new_red_jade_ring()
    {
        var q = new Scripted("Ferda", "Warrior") { Reward = "ring" };
        q.Drops = 9;
        var c = Context(q, new QuestJob { Character = "Ferda", Quest = Id, AltarNoMarkAccepted = true, AltarOfferings = Made(9, DateTimeOffset.UtcNow.AddHours(-3)) });
        await new AltarWorshipActions().ExecuteAsync(c, new("run", "altar-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.Null(c.Job.CompletionLegend);
        Assert.Contains("Red Jade Ring", c.Job.CompletionText);
    }

    [Fact]
    public async Task Tenth_offering_without_a_reward_stops_instead_of_offering_again()
    {
        var q = new Scripted("Elara", "Priest");
        var c = Context(q, new QuestJob { Character = "Elara", Quest = Id, AltarOfferings = Made(9, DateTimeOffset.UtcNow.AddHours(-3)) });
        var e = await Assert.ThrowsAsync<QuestBlockedException>(() => new AltarWorshipActions().ExecuteAsync(c, new("run", "altar-run", "complete"), default));
        Assert.StartsWith("10 offerings made, no reward seen.", e.Message);
        Assert.Equal(1, q.Drops);
        Assert.Null(c.Job.CompletedAt);
        await Assert.ThrowsAsync<QuestBlockedException>(() => new AltarWorshipActions().ExecuteAsync(c, new("run", "altar-run", "complete"), default));
        Assert.Equal(1, q.Drops);
    }

    [Fact]
    public async Task Other_classes_must_accept_that_there_is_no_legend_mark()
    {
        var q = new Scripted("Ferda", "Warrior");
        var c = Context(q);
        var e = await Assert.ThrowsAsync<QuestBlockedException>(() => new AltarWorshipActions().ExecuteAsync(c, new("preflight", "altar-preflight", "run"), default));
        Assert.Contains("Ferda is a Warrior and won't get the legend mark", e.Message);
        c = Context(q, new QuestJob { Character = "Ferda", Quest = Id, AltarNoMarkAccepted = true });
        await new AltarWorshipActions().ExecuteAsync(c, new("preflight", "altar-preflight", "run"), default);
        Assert.Null(NoMark("Elara", "Priest"));
        Assert.Null(NoMark("Elara", null));
    }

    [Fact]
    public async Task A_shield_the_altar_never_took_is_not_counted()
    {
        var q = new Scripted("Elara", "Priest") { AltarTakes = false };
        q.Set(q.Snapshot with
        {
            Inventory = q.Snapshot.Inventory.SetItem(9, new(9, Shield, 1))
        });
        var c = Context(q);
        var e = await Assert.ThrowsAsync<QuestBlockedException>(() => new AltarWorshipActions().ExecuteAsync(c, new("run", "altar-run", "complete"), default));
        Assert.Contains("Nothing was offered", e.Message);
        Assert.Empty(c.Job.AltarOfferings);
        Assert.Null(c.Job.Pending);
    }

    [Fact]
    public async Task An_offering_cut_off_by_a_reconnect_is_settled_from_the_bag()
    {
        var q = new Scripted("Elara", "Priest");
        q.Set(q.Snapshot with
        {
            Generation = 2
        });
        var pending = new Transaction("altar-offer", 1, 5, 5000, 0, 0, DateTimeOffset.UtcNow) { InventoryCounts = new() { [Shield] = 1 }, InventoryBaselineKnown = true };
        var c = Context(q, new QuestJob { Character = "Elara", Quest = Id, Pending = pending });
        await new AltarWorshipActions().ExecuteAsync(c, new("preflight", "altar-preflight", "run"), default);
        Assert.Equal("Not observed (reconnected)", Assert.Single(c.Job.AltarOfferings).Experience);
        Assert.Null(c.Job.Pending);

        c = Context(q, new QuestJob { Character = "Elara", Quest = Id, Pending = pending });
        q.Set(q.Snapshot with
        {
            Inventory = q.Snapshot.Inventory.SetItem(9, new(9, Shield, 1))
        });
        await new AltarWorshipActions().ExecuteAsync(c, new("preflight", "altar-preflight", "run"), default);
        Assert.Empty(c.Job.AltarOfferings);
        Assert.Null(c.Job.Pending);
    }

    [Fact]
    public void Eligibility_needs_gold_for_a_shield_unless_one_is_carried()
    {
        var s = FoundationTests.Ready(5, 700) with
        {
            Hp = 100
        };
        var job = new QuestJob { Quest = Id };
        Assert.Equal(EligibilityKind.Blocked, Eligibility(s, job).Kind);
        Assert.Equal(EligibilityKind.Eligible, Eligibility(s with
        {
            Inventory = s.Inventory.SetItem(3, new(3, Shield, 1))
        }, job).Kind);
        Assert.Equal(EligibilityKind.Eligible, Eligibility(s with
        {
            Gold = 750
        }, job).Kind);
    }

    [Fact]
    public void Torrance_shop_list_exposes_the_leather_shield_and_its_price()
    {
        var t = new StateTracker(1);
        t.Identity("Ferda", 7);
        t.Observe(0x2F, Convert.FromBase64String(ShopPacket));
        var d = t.Snapshot.Dialog!;
        Assert.Equal((false, (byte)4, (ushort)74, Torrance), (d.IsPursuit, d.Type, d.Pursuit, d.Name));
        Assert.Equal(ShieldPrice, d.Items.Single(i => i.Name == Shield).Price);
        Assert.Contains(d.Items, i => i.Name == "Amethyst Ring");
    }

    [Fact]
    public void Altar_reply_is_quest_evidence_only_in_Mileth()
    {
        var notice = new ServerNotice(1, 1, 3, Touch, DateTimeOffset.UtcNow);
        Assert.True(IsNotice(notice, Map));
        Assert.True(IsNotice(notice with
        {
            Text = "25 experience!"
        }, Map));
        Assert.False(IsNotice(notice, ShopMap));
        Assert.False(IsNotice(notice with
        {
            Type = 0
        }, Map));
    }

    [Fact]
    public void Client_drops_are_recorded_with_slot_tile_and_quantity()
    {
        var recorder = new ReplayRecorder();
        var tracker = new StateTracker(1);
        tracker.Identity("Ferda", 7);
        var b = new NetworkPacketBuilder(ClientCommand.Drop);
        ClientPacket packet;
        try
        {
            new Arbiter.Net.Client.Messages.ClientDropMessage { Slot = 14, X = 31, Y = 52, Quantity = 1 }.Serialize(ref b);
            packet = (ClientPacket)b.ToPacket();
        }
        finally { b.Dispose(); }
        recorder.RecordDropPacket(packet, tracker.Snapshot);
        Assert.Equal(1, recorder.ActionCount);
    }

    [Fact]
    public void Shipped_recipe_matches_the_catalog_and_the_runner()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content"));
        var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root, "recipes", "altar-worship.v1.json")))!;
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "catalog.json")));
        var row = catalog.RootElement.GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetString() == CatalogId);
        Assert.Equal(row.GetProperty("name").GetString(), recipe.Name);
        Assert.False(recipe.Repeatable);
        var runner = new QuestRunner(new Scripted("Elara", "Priest"), new FileQuestJournal(dir), recipe, new());
        Assert.All(recipe.Steps, s => Assert.IsType<AltarWorshipActions>(runner.Actions[s.Action]));
        Assert.Null(QuestRunPolicy.NextAttempt(recipe, new QuestJob { Quest = Id, AltarOfferings = Made(1, DateTimeOffset.UtcNow) }));
    }
}
