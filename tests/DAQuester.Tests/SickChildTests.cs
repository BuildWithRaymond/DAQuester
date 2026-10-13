using System.Collections.Immutable;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;
using static DAQuester.Core.SickChildEvidence;
namespace DAQuester.Tests;

public class SickChildTests
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString());

    sealed class Scripted(string name, uint id) : IGameSession
    {
        public CharacterSnapshot Snapshot { get; private set; } = FoundationTests.Ready(99, 20000) with { Name = name, Id = id, Map = 3070, Position = new(4, 4) };
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string> { "navigate", "dialog", "inventory", "legend", "status-message", "trade" };
        public event Action<CharacterSnapshot>? Changed;
        public event Action<string>? Interrupted;
        public List<GameCommand> Commands { get; } = [];
        public Func<GameCommand, CharacterSnapshot, CharacterSnapshot?>? Script;
        public void Set(CharacterSnapshot s)
        {
            Snapshot = s with
            {
                Revision = Snapshot.Revision + 1
            };
            Changed?.Invoke(Snapshot);
        }
        public void Interrupt() => Interrupted?.Invoke("Automation interrupted");
        public Task ExecuteAsync(GameCommand c, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            Commands.Add(c);
            if (Script?.Invoke(c, Snapshot) is { } next)
                Set(next);
            return Task.CompletedTask;
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
    sealed class Hub(Scripted helper) : IQuestHelpers, IQuestHelper
    {
        public bool LoggedOut, Disposed;
        public int FailLogins, Acquired;
        public Func<string, Exception?>? LoginError;
        public CancellationTokenSource TakeOver { get; } = new();
        public List<string> Reports { get; } = [];
        public Task<IQuestHelper> AcquireAsync(string name, CancellationToken ct)
        {
            Acquired++;
            if (LoginError?.Invoke(name) is { } error)
                throw error;
            if (FailLogins-- > 0)
                throw new HelperLoginException(name + ": Login not confirmed");
            return Task.FromResult<IQuestHelper>(this);
        }
        public void Report(string name, string status) => Reports.Add(name + ": " + status);
        public string Name => helper.Snapshot.Name!;
        public IGameSession Session => helper;
        public bool LoggedInForQuest => true;
        public CancellationToken Released => TakeOver.Token;
        public Task LogOutAsync(CancellationToken ct)
        {
            LoggedOut = true;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
        public List<SickChildPick> Shared { get; } = [];
        public IReadOnlyList<SickChildPick> SharedPicks => Shared;
        public void RecordPick(SickChildPick pick) => Shared.Add(pick);
    }

    static Dialog Popup(CharacterSnapshot s, string text, bool menu) => new(s.Generation, s.Revision + 1, 6813, 4, "", text, true, 124, (ushort)(menu ? 20 : 0), (byte)(menu ? 2 : 0),
     menu ? ImmutableArray.Create(new DialogChoice(PickChoice, 124, 1), new DialogChoice("Leave it be", 124, 2)) : ImmutableArray<DialogChoice>.Empty, null, true);
    static Dialog Paulin(CharacterSnapshot s, string text, params string[] choices) => new(s.Generation, s.Revision + 1, 6804, 1, "Paulin", text, true, 117, 37, (byte)(choices.Length > 0 ? 2 : 0), choices.Select((c, i) => new DialogChoice(c, 117, (byte)(i + 1))).ToImmutableArray(), null, true);
    static CharacterSnapshot Add(CharacterSnapshot s, byte slot, string item) => s with { Inventory = s.Inventory.SetItem(slot, new(slot, item, 1)) };
    static CharacterSnapshot Without(CharacterSnapshot s, string item) => s with { Inventory = s.Inventory.RemoveRange(s.Inventory.Values.Where(i => i.Name == item).Select(i => i.Slot)) };

    // Live-observed popup pages: two Next pages, then the pick menu (2026-10-11).
    static CharacterSnapshot? Flower(GameCommand c, CharacterSnapshot s) => c.Kind switch
    {
        "navigate" when c.Map == FlowerMap && c.Target == FlowerTile => s with { Map = FlowerMap, Position = FlowerTile, Dialog = Popup(s, FlowerHere, false) },
        "navigate" when c.Map == FlowerMap => s with { Map = FlowerMap, Position = c.Target, Dialog = null },
        "step" when c.Target == FlowerTile => s with { Position = FlowerTile, Dialog = Popup(s, FlowerHere, false) },
        "step" => s with { Position = c.Target },
        "next" when s.Dialog?.Text == FlowerHere => s with { Dialog = Popup(s, WhiteFlower, false) },
        "next" when s.Dialog?.Text == WhiteFlower && s.Dialog.Choices.Length == 0 => s with { Dialog = Popup(s, WhiteFlower, true) },
        "choose" when c.Text == PickChoice => Add(s, 1, Blossom) with { Dialog = null },
        _ => null
    };
    static CharacterSnapshot? Turnin(GameCommand c, CharacterSnapshot s) => c.Kind switch
    {
        "navigate" when c.Map == PaulinMap => s with { Map = PaulinMap, Position = new(4, 4), Entities = s.Entities.SetItem(6804, new(6804, "Paulin", new(2, 2), true)), Dialog = null },
        "open" => s with { Dialog = Paulin(s, "Yes. May I help you?", AskChild, "When he was living, had you seen Cyril?") },
        "choose" when c.Text == AskChild => s with { Dialog = Paulin(s, "She's not well.", "Perhaps for 1000 coins you can say", "I see.", "Offer Nila Blossom", Offer, "Offer Tulsi Blossom") },
        "choose" when c.Text == Offer => s with { Dialog = Paulin(s, Collect + " for the chief physician who will concoct a potion for the sick child.") },
        "next" when s.Dialog?.Text.StartsWith(Collect) == true => s with { Dialog = Paulin(s, "Shall you give the blossom to him, or pay him to secretly give the blossom to the sick child.", "Give Sevti Blossom", GiveWithCoins) },
        "choose" when c.Text == GiveWithCoins => Without(s, Blossom) with { Gold = s.Gold - 5000, Notice = new(s.Generation, s.Revision + 1, 3, "150000 experience!", DateTimeOffset.UtcNow), Dialog = Paulin(s, GaveIt + " and bade her eat it.") },
        "next" when s.Dialog?.Text.StartsWith(GaveIt) == true => s with { Dialog = null },
        _ => null
    };
    static CharacterSnapshot Legends(CharacterSnapshot s, params string[] legends) => s with { LegendsKnown = true, LegendsRevision = s.Revision + 1, Legends = [.. legends] };

    [Fact]
    public void Lockout_runs_both_ways_and_only_on_the_legend_mark()
    {
        Assert.NotNull(Lockout(Id, ["Aided the Pact of Anaman - Deoch 223, Spring"]));
        Assert.NotNull(Lockout(AnamanCatalogId, ["Cured a sick child in Loures - Deoch 223, Spring"]));
        Assert.Null(Lockout(Id, ["Aided the Emerald Knights - Deoch 223, Spring"]));
        Assert.Null(Lockout(AnamanCatalogId, []));
        var memory = new CharacterMemory { Character = "Nuclear", Legends = ["Aided the Pact of Anaman - Deoch 223, Spring"], LegendsObservedAt = DateTimeOffset.UtcNow, Level = 99 };
        Assert.Equal(EligibilityKind.Blocked, memory.SelectionRestriction(Id)?.Kind);
        Assert.Null(memory.SelectionRestriction("emerald-dawn"));
        Assert.Equal(EligibilityKind.Blocked, new CharacterMemory { Character = "Nuclear", Level = 10 }.SelectionRestriction(Id)?.Kind);
    }
    [Fact]
    public void Only_quest_messages_become_evidence_on_their_maps()
    {
        Assert.True(IsNotice(new(1, 1, 3, "150000 experience!", default), PaulinMap));
        Assert.False(IsNotice(new(1, 1, 3, "150000 experience!", default), 500));
        Assert.False(IsNotice(new(1, 1, 0, "150000 experience!", default), PaulinMap));
        Assert.True(IsNotice(new(1, 1, 3, Picked, default), FlowerMap));
        Assert.True(IsNotice(new(1, 1, 3, Fume, default), 4003));
        Assert.Null(Experience("You received 20,000 gold coins."));
    }
    [Fact]
    public void Pick_estimate_is_24_hours_and_refusals_say_when_they_happened()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new QuestJob { SickChildPicks = [new("valadriel", now.AddHours(-23), false), new("jackharkness", now.AddHours(-1), true), new("swallow", now.AddHours(-25), false)] };
        Assert.False(CanPick(job, "VALADRIEL", now));
        Assert.StartsWith("Can pick again at", PickLine(job, "valadriel", now));
        Assert.False(CanPick(job, "jackharkness", now));
        Assert.StartsWith("Can't pick, refused at", PickLine(job, "jackharkness", now));
        Assert.True(CanPick(job, "swallow", now));
        Assert.Null(PickLine(job, "swallow", now));
        Assert.True(CanPick(job, "Ferda", now));
    }
    [Fact]
    public void Options_require_a_mode_a_helper_for_helper_mode_and_a_sane_count()
    {
        new SickChildOptions(SickChildOptions.Daily, [], 2).Validate();
        new SickChildOptions(SickChildOptions.WithHelpers, ["jackharkness"], 0).Validate();
        Assert.Throws<QuestBlockedException>(() => new SickChildOptions(SickChildOptions.WithHelpers, [], 0).Validate());
        Assert.Throws<QuestBlockedException>(() => new SickChildOptions(SickChildOptions.Daily, [], 5).Validate());
        Assert.Throws<QuestBlockedException>(() => new SickChildOptions("other", [], 0).Validate());
        Assert.Throws<QuestBlockedException>(() => new SickChildOptions(SickChildOptions.WithHelpers, ["jack", "JACK"], 0).Validate());
    }
    static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));
    [Fact]
    public void Live_trade_bytes_track_both_accepts_without_treating_the_first_as_done()
    {
        var tracker = new StateTracker(7);
        tracker.Identity("jackharkness", 1687001);
        tracker.Observe(0x42, Hex("00 00 19 BD 76 09 76 61 6C 61 64 72 69 65 6C"));
        tracker.Observe(0x42, Hex("02 00 01 80 17 00 0D 57 68 69 74 65 20 42 6C 6F 73 73 6F 6D"));
        var e = tracker.Snapshot.Exchange!;
        Assert.Equal(1686902u, e.PartnerId);
        Assert.Equal("valadriel", e.PartnerName);
        Assert.Equal(new ExchangeItem(0, Blossom), Assert.Single(e.Items));
        tracker.Observe(0x42, Hex("05 00 0E 59 6F 75 20 65 78 63 68 61 6E 67 65 64 2E"));
        Assert.True(tracker.Snapshot.Exchange!.OwnAccepted);
        Assert.False(tracker.Snapshot.Exchange.PartnerAccepted);
        Assert.Equal("You exchanged.", tracker.Snapshot.Exchange.Message);
        tracker.Observe(0x42, Hex("05 01 0E 59 6F 75 20 65 78 63 68 61 6E 67 65 64 2E"));
        Assert.True(tracker.Snapshot.Exchange!.PartnerAccepted);
        tracker.Disconnect();
        Assert.Null(tracker.Snapshot.Exchange);
    }
    [Fact]
    public void Trade_packets_match_the_client()
    {
        Assert.Equal(Hex("00 00 19 BD 76"), ExchangePackets.Start(1686902).Data);
        Assert.Equal(Hex("01 00 19 BD 76 01"), ExchangePackets.AddItem(1686902, 1).Data);
        Assert.Equal(Hex("05 00 19 BD 76"), ExchangePackets.Accept(1686902).Data);
        Assert.Equal(Hex("04 00 19 BD 76"), ExchangePackets.Cancel(1686902).Data);
    }
    [Fact]
    public async Task Daily_flower_is_picked_given_with_coins_and_completes_on_the_legend()
    {
        var q = new Scripted("valadriel", 1686902);
        q.Script = (c, s) => c.Kind == "inspect-self" ? Legends(s, s.Gold == 15000 ? new[] { Legend + " - Deoch 223, Spring" } : Array.Empty<string>()) : Flower(c, s) ?? Turnin(c, s);
        var job = new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.Daily, [], 4) };
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), job);
        await new SickChildActions().ExecuteAsync(c, new("preflight", "sick-preflight", "run"), default);
        await new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.NotNull(c.Job.CompletedAt);
        Assert.StartsWith(Legend, c.Job.CompletionLegend);
        var receipt = Assert.Single(c.Job.SickChildTurnIns);
        Assert.Equal(20000u, receipt.GoldBefore);
        Assert.Equal(15000u, receipt.GoldAfter);
        Assert.Equal("150000 experience!", receipt.Experience);
        Assert.Null(receipt.Helper);
        Assert.False(Assert.Single(c.Job.SickChildPicks).Refused);
        Assert.Contains(q.Commands, x => x.Kind == "choose" && x.Text == GiveWithCoins);
        Assert.DoesNotContain(q.Commands, x => x.Text is "Give Sevti Blossom" or "Perhaps for 1000 coins you can say");
    }

    [Fact]
    public async Task Scheduled_daily_wait_yields_with_pick_checkpoint_without_resubmitting()
    {
        var q = new Scripted("valadriel", 1686902);
        q.Script = (command, snapshot) => command.Kind == "inspect-self" ? Legends(snapshot) : null;
        var picked = DateTimeOffset.UtcNow.AddHours(-1);
        var job = new QuestJob { Character = "valadriel", Quest = Id, Step = "run", SickChildOptions = new(SickChildOptions.Daily, [], 0), SickChildPicks = [new("valadriel", picked, false)] };
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), job) { YieldScheduledWaits = true };
        var wait = await Assert.ThrowsAsync<QuestScheduledWaitException>(() => new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default));
        Assert.Equal(picked + PickInterval, wait.Next);
        Assert.Null(c.Job.Pending);
        Assert.Null(c.Job.CompletedAt);
        Assert.DoesNotContain(q.Commands, command => command.Kind is "step" or "give" or "choose" or "navigate");
    }
    [Fact]
    public async Task Refused_pick_is_recorded_and_never_turned_in()
    {
        var q = new Scripted("valadriel", 1686902);
        q.Script = (c, s) => c.Kind switch
        {
            "inspect-self" => Legends(s),
            "navigate" when c.Target == FlowerTile => s with { Map = FlowerMap, Position = FlowerTile, Dialog = Popup(s, FlowerHere, false) },
            "navigate" => s with { Map = FlowerMap, Position = c.Target },
            "step" when c.Target == FlowerTile => s with { Position = FlowerTile, Dialog = Popup(s, FlowerHere, false) },
            "step" => s with { Position = c.Target },
            "next" => s with { Dialog = null, Notice = new(s.Generation, s.Revision + 1, 3, Picked, DateTimeOffset.UtcNow) },
            _ => null
        };
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.Daily, [], 0) });
        using var stop = new CancellationTokenSource();
        // After the refusal the run waits a day for the next flower; cancel once it starts waiting.
        var run = new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), stop.Token);
        for (int i = 0; i < 200 && c.Job.Status != "Scheduled"; i++)
            await Task.Delay(5);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.True(Assert.Single(c.Job.SickChildPicks).Refused);
        Assert.Empty(c.Job.SickChildTurnIns);
        Assert.DoesNotContain(q.Commands, x => x.Kind == "open");
        Assert.Null(c.Job.NextRun);
        // A leftover planned time never turns into a library cooldown.
        var recipe = new QuestDefinition(Id, 1, "Cured the Sick Child", "", [], []) { Repeatable = false };
        Assert.Null(QuestRunPolicy.NextAttempt(recipe, c.Job with
        {
            NextRun = DateTimeOffset.UtcNow.AddHours(20)
        }));
        Assert.Equal(PaulinMap, q.Commands.Last(x => x.Kind == "navigate").Map);
    }
    [Fact]
    public async Task Helper_picks_trades_and_is_logged_out_before_the_quester_turns_in()
    {
        var q = new Scripted("valadriel", 1686902);
        var h = new Scripted("jackharkness", 1687001);
        h.Set(h.Snapshot with
        {
            Map = 500,
            Position = new(20, 20)
        });
        q.Script = (c, s) => c.Kind switch
        {
            "inspect-self" => Legends(s, s.Gold == 15000 ? new[] { Legend + " - Deoch 223, Spring" } : Array.Empty<string>()),
            "trade-accept" => Add(s, 1, Blossom) with { Exchange = s.Exchange! with { OwnAccepted = true } },
            _ => Turnin(c, s)
        };
        h.Script = (c, s) =>
        {
            switch (c.Kind)
            {
                case "navigate" when c.Map == PaulinMap:
                    return s with
                    {
                        Map = PaulinMap,
                        Position = new(5, 6)
                    };
                case "visit":
                    return s with
                    {
                        Position = new(3, 4)
                    };
                case "trade-start":
                    q.Set(q.Snapshot with
                    {
                        Exchange = new(q.Snapshot.Generation, q.Snapshot.Revision + 1, 1687001, "jackharkness")
                    });
                    return s with
                    {
                        Exchange = new(s.Generation, s.Revision + 1, 1686902, "valadriel")
                    };
                case "trade-item":
                    q.Set(q.Snapshot with
                    {
                        Exchange = q.Snapshot.Exchange! with
                        {
                            Items = [new(1, Blossom)]
                        }
                    });
                    return Without(s, Blossom) with
                    {
                        Exchange = s.Exchange! with
                        {
                            Items = [new(0, Blossom)]
                        }
                    };
                case "trade-accept":
                    q.Set(q.Snapshot with
                    {
                        Exchange = q.Snapshot.Exchange! with
                        {
                            PartnerAccepted = true
                        }
                    });
                    return s with
                    {
                        Exchange = s.Exchange! with
                        {
                            OwnAccepted = true
                        }
                    };
                default:
                    return Flower(c, s);
            }
        };
        var hub = new Hub(h);
        var job = new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4), SickChildPicks = [new("valadriel", DateTimeOffset.UtcNow.AddHours(-1), false)] };
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), job, null, hub);
        await new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.Equal("jackharkness", Assert.Single(c.Job.SickChildTurnIns).Helper);
        Assert.Contains(c.Job.SickChildPicks, p => p.Character == "jackharkness" && !p.Refused);
        Assert.Equal(PaulinMap, q.Commands.First(x => x.Kind == "navigate").Map);
        Assert.True(hub.LoggedOut);
        Assert.True(hub.Disposed);
        Assert.Equal(["trade-start", "trade-item", "trade-accept"], h.Commands.Where(x => x.Kind.StartsWith("trade")).Select(x => x.Kind));
        Assert.All(h.Commands.Where(x => x.Kind.StartsWith("trade")), x => Assert.Equal(1686902u, x.EntityId));
        Assert.Equal(1687001u, Assert.Single(q.Commands, x => x.Kind == "trade-accept").EntityId);
    }
    [Fact]
    public async Task Trade_offering_anything_else_is_cancelled_and_nothing_is_given()
    {
        var q = new Scripted("valadriel", 1686902);
        var h = new Scripted("jackharkness", 1687001);
        h.Set(Add(h.Snapshot, 1, Blossom));
        q.Script = (c, s) => c.Kind == "inspect-self" ? Legends(s) : Turnin(c, s);
        h.Script = (c, s) =>
        {
            switch (c.Kind)
            {
                case "trade-start":
                    q.Set(q.Snapshot with
                    {
                        Exchange = new(q.Snapshot.Generation, q.Snapshot.Revision + 1, 1687001, "jackharkness")
                    });
                    return s with
                    {
                        Exchange = new(s.Generation, s.Revision + 1, 1686902, "valadriel")
                    };
                case "trade-item":
                    q.Set(q.Snapshot with
                    {
                        Exchange = q.Snapshot.Exchange! with
                        {
                            Items = [new(1, Blossom), new(1, "Cursed Belt")]
                        }
                    });
                    return s with
                    {
                        Exchange = s.Exchange! with
                        {
                            Items = [new(0, Blossom)]
                        }
                    };
                default:
                    return s with
                    {
                        Map = PaulinMap,
                        Position = new(3, 4)
                    };
            }
        };
        var hub = new Hub(h);
        var job = new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4), SickChildPicks = [new("valadriel", DateTimeOffset.UtcNow.AddHours(-1), false)] };
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), job, null, hub);
        await Assert.ThrowsAsync<QuestBlockedException>(() => new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default));
        Assert.Contains(h.Commands, x => x.Kind == "trade-cancel");
        Assert.DoesNotContain(q.Commands, x => x.Kind == "trade-accept");
        Assert.Empty(c.Job.SickChildTurnIns);
        Assert.False(hub.LoggedOut);
        Assert.True(hub.Disposed);
    }

    static (Scripted Quester, Scripted Helper, Hub Hub) Pair()
    {
        var q = new Scripted("valadriel", 1686902);
        var h = new Scripted("jackharkness", 1687001);
        h.Set(h.Snapshot with
        {
            Map = 500,
            Position = new(20, 20)
        });
        q.Script = (c, s) => c.Kind switch
        {
            "inspect-self" => Legends(s, s.Gold == 15000 ? new[] { Legend + " - Deoch 223, Spring" } : Array.Empty<string>()),
            "trade-accept" => Add(s, 1, Blossom) with { Exchange = s.Exchange! with { OwnAccepted = true } },
            "navigate" when c.Map == FlowerMap && c.Target == FlowerTile => s with { Map = FlowerMap, Position = FlowerTile, Dialog = Popup(s, FlowerHere, false) },
            "navigate" when c.Map == FlowerMap => s with { Map = FlowerMap, Position = c.Target },
            "step" when c.Target == FlowerTile => s with { Position = FlowerTile, Dialog = Popup(s, FlowerHere, false) },
            "step" => s with { Position = c.Target },
            "next" when s.Dialog?.Text == FlowerHere => s with { Dialog = null, Notice = new(s.Generation, s.Revision + 1, 3, Picked, DateTimeOffset.UtcNow) },
            _ => Turnin(c, s)
        };
        h.Script = (c, s) =>
        {
            switch (c.Kind)
            {
                case "navigate" when c.Map == PaulinMap:
                    return s with
                    {
                        Map = PaulinMap,
                        Position = new(5, 6)
                    };
                case "visit":
                    return s with
                    {
                        Position = new(3, 4)
                    };
                case "trade-start":
                    q.Set(q.Snapshot with
                    {
                        Exchange = new(q.Snapshot.Generation, q.Snapshot.Revision + 1, 1687001, "jackharkness")
                    });
                    return s with
                    {
                        Exchange = new(s.Generation, s.Revision + 1, 1686902, "valadriel")
                    };
                case "trade-item":
                    q.Set(q.Snapshot with
                    {
                        Exchange = q.Snapshot.Exchange! with
                        {
                            Items = [new(1, Blossom)]
                        }
                    });
                    return Without(s, Blossom) with
                    {
                        Exchange = s.Exchange! with
                        {
                            Items = [new(0, Blossom)]
                        }
                    };
                case "trade-accept":
                    q.Set(q.Snapshot with
                    {
                        Exchange = q.Snapshot.Exchange! with
                        {
                            PartnerAccepted = true
                        }
                    });
                    return s with
                    {
                        Exchange = s.Exchange! with
                        {
                            OwnAccepted = true
                        }
                    };
                default:
                    return Flower(c, s);
            }
        };
        return (q, h, new Hub(h));
    }
    [Fact]
    public async Task Refused_quester_pick_starts_a_helper_in_the_same_round()
    {
        var (q, h, hub) = Pair();
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4) }, null, hub);
        await new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.Contains(c.Job.SickChildPicks, p => p.Character == "valadriel" && p.Refused);
        Assert.Equal("jackharkness", Assert.Single(c.Job.SickChildTurnIns).Helper);
        Assert.Equal(1, hub.Acquired);
        // After the refusal she leaves the flower for Paulin's room before any trade.
        var moves = q.Commands.Where(x => x.Kind is "navigate" or "step").ToList();
        Assert.Equal(PaulinMap, moves[moves.FindIndex(x => x.Kind == "step" && x.Target == FlowerApproach) + 1].Map);
    }
    [Fact]
    public async Task Occupied_flower_spot_is_waited_out_beside_it()
    {
        SickChildActions.SpotPoll = TimeSpan.FromMilliseconds(5);
        var (q, h, hub) = Pair();
        int arrivals = 0;
        var stranger = new Entity(999, "Stranger", FlowerTile, true);
        var inner = h.Script!;
        h.Script = (c, s) =>
        {
            if (c.Kind == "navigate" && c.Map == FlowerMap && c.Target == FlowerTile && ++arrivals < 3)
                return s with
                {
                    Map = FlowerMap,
                    Position = FlowerApproach,
                    Entities = s.Entities.SetItem(999, stranger)
                };
            if (c.Kind == "navigate" && c.Map == FlowerMap && c.Target == FlowerTile)
                return s with
                {
                    Map = FlowerMap,
                    Position = FlowerApproach,
                    Entities = s.Entities.Remove(999)
                };
            return inner(c, s);
        };
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4), SickChildPicks = [new("valadriel", DateTimeOffset.UtcNow.AddHours(-1), true)] }, null, hub);
        await new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.Equal(3, arrivals);
        Assert.Contains(hub.Reports, r => r.Contains("Waiting for the flower spot to clear"));
        Assert.Contains(h.Commands, x => x.Kind == "step" && x.Target == FlowerTile);
    }
    [Fact]
    public async Task Walking_hiccup_is_retried_instead_of_dropping_the_helper()
    {
        SickChildActions.RetryRest = TimeSpan.FromMilliseconds(5);
        var (q, h, hub) = Pair();
        bool failed = false;
        var inner = h.Script!;
        h.Script = (c, s) => { if (c.Kind == "navigate" && c.Map == FlowerMap && !failed) { failed = true; throw new QuestBlockedException("Server position refresh unanswered. Resume when connection recovers."); } return inner(c, s); };
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4), SickChildPicks = [new("valadriel", DateTimeOffset.UtcNow.AddHours(-1), true)] }, null, hub);
        await new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.Contains(hub.Reports, r => r.Contains("Retrying in 15 seconds"));
    }
    // Live 2026-10-12 on kullan: (11,3), Golden Blossom, "Give Tulsi Blossom and 5000 coins", a final page with Close only.
    static CharacterSnapshot? Tulsi(GameCommand c, CharacterSnapshot s) => c.Kind switch
    {
        "inspect-self" => Legends(s, s.Count("Golden Blossom") == 0 && s.Notice?.Text == "10000 experience!" ? new[] { AnamanLegend + " - Deoch 223, Spring" } : Array.Empty<string>()),
        "navigate" when c.Map == FlowerMap && c.Target == SickChildEvidence.Tulsi.Spot => s with { Map = FlowerMap, Position = c.Target, Dialog = Popup(s, FlowerHere, false) with { EntityId = 6814, Pursuit = 123 } },
        "step" => s with { Position = c.Target },
        "next" when s.Dialog?.Text == FlowerHere => s with { Dialog = Popup(s, TulsiDescription, false) with { EntityId = 6814, Pursuit = 123 } },
        "next" when s.Dialog?.Text == TulsiDescription && s.Dialog.Choices.Length == 0 => s with { Dialog = Popup(s, TulsiDescription, true) with { EntityId = 6814, Pursuit = 123 } },
        "choose" when c.Text == PickChoice => Add(s, 1, "Golden Blossom") with { Dialog = null },
        "navigate" when c.Map == PaulinMap => s with { Map = PaulinMap, Position = new(4, 4), Entities = s.Entities.SetItem(6804, new(6804, "Paulin", new(2, 2), true)), Dialog = null },
        "open" => s with { Dialog = Paulin(s, "Yes. May I help you?", AskChild) },
        "choose" when c.Text == AskChild => s with { Dialog = Paulin(s, "She's not well.", "I see.", Offer, "Offer Tulsi Blossom") },
        "choose" when c.Text == "Offer Tulsi Blossom" => s with { Dialog = Paulin(s, Collect + " for the chief physician who will concoct a potion for the sick child.") },
        "next" when s.Dialog?.Text.StartsWith(Collect) == true => s with { Dialog = Paulin(s, "Shall you give the blossom to him, or pay him to secretly give the blossom to the sick child.", "Give Tulsi Blossom", "Give Tulsi Blossom and 5000 coins") },
        // The plain give's last page hasn't been seen live; any page after the give must count as the last one.
        "choose" when c.Text == "Give Tulsi Blossom" => Without(s, "Golden Blossom") with { Notice = new(s.Generation, s.Revision + 1, 3, "10000 experience!", DateTimeOffset.UtcNow), Dialog = Paulin(s, "An unseen page from Paulin.") with { HasNext = false } },
        "close" => s with { Dialog = null },
        _ => null
    };
    [Fact]
    public async Task Tulsi_goes_plainly_to_the_physician_without_gold_and_completes_the_pact()
    {
        var q = new Scripted("kullan", 1884987);
        q.Script = Tulsi;
        q.Set(q.Snapshot with
        {
            Gold = 100
        });
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "kullan", Quest = AnamanId, SickChildOptions = new(SickChildOptions.Daily, [], 4) });
        await new SickChildActions(SickChildEvidence.Tulsi).ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.StartsWith(AnamanLegend, c.Job.CompletionLegend);
        var receipt = Assert.Single(c.Job.SickChildTurnIns);
        Assert.Equal("10000 experience!", receipt.Experience);
        Assert.Equal(receipt.GoldBefore, receipt.GoldAfter);
        Assert.Contains(q.Commands, x => x.Kind == "choose" && x.Text == "Give Tulsi Blossom");
        Assert.DoesNotContain(q.Commands, x => x.Text is "Give Tulsi Blossom and 5000 coins" or Offer or GiveWithCoins);
        Assert.Contains(q.Commands, x => x.Kind == "close");
    }
    [Fact]
    public async Task Login_on_the_flower_spot_closes_the_popup_before_stepping_off()
    {
        var q = new Scripted("kullan", 1884987);
        q.Script = Tulsi;
        var spot = SickChildEvidence.Tulsi.Spot;
        q.Set(q.Snapshot with
        {
            Map = FlowerMap,
            Position = spot,
            Dialog = Popup(q.Snapshot, FlowerHere, false) with
            {
                EntityId = 6814,
                Pursuit = 123
            }
        });
        var inner = q.Script;
        q.Script = (c, s) => c.Kind == "step" && s.Dialog is not null ? throw new QuestBlockedException("Movement blocked by an open popup.") : c.Kind == "step" && c.Target == spot ? s with { Position = spot, Dialog = Popup(s, FlowerHere, false) with { EntityId = 6814, Pursuit = 123 } } : inner(c, s);
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "kullan", Quest = AnamanId, SickChildOptions = new(SickChildOptions.Daily, [], 4) });
        await new SickChildActions(SickChildEvidence.Tulsi).ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        int closeAt = q.Commands.FindIndex(x => x.Kind == "close"), stepAt = q.Commands.FindIndex(x => x.Kind == "step");
        Assert.True(closeAt >= 0 && closeAt < stepAt);
    }
    [Fact]
    public async Task Tulsi_refusal_is_recorded_not_retried()
    {
        var q = new Scripted("kullan", 1884987);
        q.Script = (c, s) => c.Kind == "next" && s.Dialog?.Text == FlowerHere ? s with { Dialog = null, Notice = new(s.Generation, s.Revision + 1, 3, TulsiPicked, DateTimeOffset.UtcNow) } : Tulsi(c, s);
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "kullan", Quest = AnamanId, SickChildOptions = new(SickChildOptions.Daily, [], 1) });
        using var stop = new CancellationTokenSource();
        var run = new SickChildActions(SickChildEvidence.Tulsi).ExecuteAsync(c, new("run", "sick-run", "complete"), stop.Token);
        for (int i = 0; i < 200 && c.Job.Status != "Scheduled"; i++)
            await Task.Delay(5);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.True(Assert.Single(c.Job.SickChildPicks).Refused);
        Assert.Single(q.Commands, x => x.Kind == "navigate" && x.Target == SickChildEvidence.Tulsi.Spot);
    }
    [Fact]
    public async Task A_pick_for_either_flower_uses_up_the_day_for_both()
    {
        var (q, h, hub) = Pair();
        hub.Shared.Add(new("jackharkness", DateTimeOffset.UtcNow.AddHours(-2), false));
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4), SickChildPicks = [new("valadriel", DateTimeOffset.UtcNow.AddHours(-1), true)] }, null, hub);
        using var stop = new CancellationTokenSource();
        var run = new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), stop.Token);
        for (int i = 0; i < 200 && c.Job.Status != "Scheduled"; i++)
            await Task.Delay(5);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(0, hub.Acquired);
        Assert.Empty(c.Job.SickChildTurnIns);
    }
    [Fact]
    public void Pact_mark_with_its_leading_dash_completes_and_locks_out()
    {
        const string live = "- Aided the Pact of Anaman - Deoch 223, Spring";
        Assert.Equal(live, Mark([live], AnamanLegend));
        Assert.NotNull(Lockout(Id, [live]));
        Assert.Equal(live, new CharacterMemory { Character = "heartc", Legends = [live], LegendsObservedAt = DateTimeOffset.UtcNow, Level = 99 }.CompletedLegend(AnamanId));
        Assert.Null(Mark(["Aided the Emerald Knights - Deoch 223, Spring"], AnamanLegend));
    }
    [Fact]
    public void Refusals_blindness_and_the_pact_lockout_are_recognised()
    {
        Assert.True(IsPicked(Picked));
        Assert.True(IsPicked(TulsiPicked));
        Assert.False(IsPicked("You notice a flower"));
        Assert.True(IsNotice(new(1, 1, 3, TulsiPicked, default), FlowerMap));
        Assert.True(IsNoFlower("You don't have a Tulsi Blossom"));
        Assert.True(IsHazard(AllBlack));
        Assert.True(IsRecovery(SeeAgain));
        Assert.True(IsNotice(new(1, 1, 3, "You notice a small golden flower that has been picked", default), FlowerMap));
        Assert.NotNull(Lockout(AnamanId, [Legend + " - Deoch 223, Spring"]));
        Assert.Equal(EligibilityKind.Blocked, new CharacterMemory { Character = "kullan", Legends = [Legend + " - Deoch 223, Spring"], LegendsObservedAt = DateTimeOffset.UtcNow, Level = 99 }.SelectionRestriction(AnamanId)?.Kind);
        Assert.Same(SickChildEvidence.Tulsi, For(AnamanId));
        Assert.Equal(new Tile(12, 3), SickChildEvidence.Tulsi.Sides[0]);
    }
    [Fact]
    public void Helper_holds_a_flower_until_one_of_theirs_is_turned_in()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new QuestJob { SickChildPicks = [new("Ferda", now.AddHours(-1), false), new("swallow", now.AddHours(-1), true)] };
        Assert.True(HoldsFlower(job, "ferda"));
        Assert.False(HoldsFlower(job, "swallow"));
        Assert.False(HoldsFlower(job, "Disillusion"));
        Assert.False(HoldsFlower(job with
        {
            SickChildTurnIns = [new(now, 1, 20000, 15000, "150000 experience!", "Ferda")]
        }, "Ferda"));
        Assert.True(HoldsFlower(job with
        {
            SickChildTurnIns = [new(now, 1, 20000, 15000, "150000 experience!", null)]
        }, "Ferda"));
    }
    [Fact]
    public async Task Helper_holding_a_flower_inside_24_hours_is_used_without_picking()
    {
        var (q, h, hub) = Pair();
        h.Set(Add(h.Snapshot, 1, Blossom));
        var picked = DateTimeOffset.UtcNow.AddHours(-1);
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4), SickChildPicks = [new("valadriel", picked, true), new("jackharkness", picked, false)] }, null, hub);
        await new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal("complete", c.Job.Step);
        Assert.Equal("jackharkness", Assert.Single(c.Job.SickChildTurnIns).Helper);
        Assert.DoesNotContain(h.Commands, x => x.Kind == "step" || x.Kind == "navigate" && x.Map == FlowerMap);
        Assert.Equal(2, c.Job.SickChildPicks.Length);
    }
    [Fact]
    public async Task Failed_login_is_retried_once_before_waiting_for_tomorrow()
    {
        var (q, h, hub) = Pair();
        hub.FailLogins = 1;
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4), SickChildPicks = [new("valadriel", DateTimeOffset.UtcNow.AddHours(-1), false)] }, null, hub);
        await new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), default);
        Assert.Equal(2, hub.Acquired);
        Assert.Equal("complete", c.Job.Step);
        Assert.Contains(hub.Reports, r => r.Contains("Login failed"));
    }

    [Fact]
    public async Task Scheduler_drains_all_helper_leases_when_multiple_logins_are_denied()
    {
        var (q, h, hub) = Pair();
        h.Set(Add(h.Snapshot, 1, Blossom));
        hub.LoginError = name => name.StartsWith("Denied", StringComparison.Ordinal) ? new ScheduledLoginException(name, "Account already logged in") : null;
        var job = new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["DeniedOne", "DeniedTwo", "jackharkness"], 2), SickChildPicks = [new("valadriel", DateTimeOffset.UtcNow, false)] };
        var context = new QuestContext(q, new FileQuestJournal(dir), new(), job, null, hub) { YieldScheduledWaits = true };
        await Assert.ThrowsAsync<ScheduledLoginException>(() => new SickChildActions().ExecuteAsync(context, new("run", "sick-run", "complete"), default));
        Assert.Equal(3, hub.Acquired);
        Assert.True(hub.Disposed);
        Assert.True(hub.LoggedOut);
        Assert.Empty(context.Job.SickChildTurnIns);
    }
    [Fact]
    public async Task Taken_over_helper_is_never_logged_out()
    {
        var (q, h, hub) = Pair();
        var inner = h.Script!;
        h.Script = (c, s) => { if (c.Kind == "navigate" && c.Map == FlowerMap) hub.TakeOver.Cancel(); return inner(c, s); };
        var c = new QuestContext(q, new FileQuestJournal(dir), new(), new QuestJob { Character = "valadriel", Quest = Id, SickChildOptions = new(SickChildOptions.WithHelpers, ["jackharkness"], 4), SickChildPicks = [new("valadriel", DateTimeOffset.UtcNow.AddHours(-1), false)] }, null, hub);
        using var stop = new CancellationTokenSource();
        var run = new SickChildActions().ExecuteAsync(c, new("run", "sick-run", "complete"), stop.Token);
        for (int i = 0; i < 200 && c.Job.Status != "Scheduled"; i++)
            await Task.Delay(5);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.False(hub.LoggedOut);
        Assert.True(hub.Disposed);
        Assert.Contains(hub.Reports, r => r.Contains("you took over jackharkness"));
        Assert.Empty(c.Job.SickChildTurnIns);
    }
}
