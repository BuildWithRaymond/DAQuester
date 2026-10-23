using System.Collections.Immutable;
using System.Reflection;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Synthetic merchant replies exercise purchase safety; no live seller identity is invented.
public class ResearchPotionSupplyTests
{
    const string Potion = "Exkuranum";
    const string Seller = "Synthetic research merchant";
    const uint SellerId = 92;

    static Task Call(string name, QuestContext context)
    {
        var method = typeof(WizardyResearchActions).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (Task)method.Invoke(null, [context, CancellationToken.None])!;
    }

    static object? Purchase(QuestContext context) => typeof(WizardyResearchProgress)
        .GetProperty("PotionPurchase")?.GetValue(context.Job.Research);

    static (SimulatedSession Session, QuestContext Context) Fixture(int count = 9, Func<SimulatedSession, IGameSession>? wrap = null)
    {
        var session = WizardyResearchTests.Ready();
        session.Set(session.Snapshot with { Inventory = session.Snapshot.Inventory.Where(pair =>
                !pair.Value.Name.Equals(Potion, StringComparison.OrdinalIgnoreCase)).ToImmutableDictionary()
            .SetItem(2, new(2, Potion, (uint)count)), Gold = 10000, Weight = 10, MaxWeight = 100 });
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        var context = new QuestContext(wrap?.Invoke(session) ?? session, journal, new(), new()
        {
            Character = session.Snapshot.Name!, Quest = WizardyResearchEvidence.Id, Research = new()
        });
        session.OnCommand = command =>
        {
            switch (command.Kind)
            {
                case "navigate":
                    Assert.Equal(3214, command.Map);
                    Assert.Equal(new Tile(9, 7), command.Target);
                    Assert.Equal(2, command.Tolerance);
                    session.Set(session.Snapshot with { Map = 3214, Position = new(9, 7),
                        Dialog = null,
                        Entities = ImmutableDictionary<uint, Entity>.Empty.Add(SellerId, new(SellerId, Seller, new(7, 4), true) { Kind = 2 }) });
                    break;
                case "open":
                    Assert.Equal(Seller, command.Text);
                    session.Set(session.Snapshot with { Dialog = Merchant(session, 0) with { Choices = [new("Buy", 41, 1)] } });
                    break;
                case "choose":
                    Assert.Equal("Buy", command.Text);
                    session.Set(session.Snapshot with { Dialog = Merchant(session, 4) with { Items = [new(Potion, 25)] } });
                    break;
                case "buy":
                    Assert.NotNull(Purchase(context));
                    Assert.Null(context.Job.Pending);
                    Assert.Equal(Potion, command.Text);
                    Assert.Equal(session.Snapshot.Generation, command.ExpectedGeneration);
                    Assert.Equal(session.Snapshot.Dialog!.Revision, command.ExpectedDialogRevision);
                    Assert.Equal(session.Snapshot.Dialog.Text, command.ExpectedPageText);
                    int amount = command.Quantity!.Value;
                    session.Set(session.Snapshot with { Inventory = session.Snapshot.Inventory.SetItem(2, new(2, Potion, (uint)(count + amount))),
                        Gold = session.Snapshot.Gold!.Value - (uint)amount * 25 });
                    break;
                case "close":
                    session.Set(session.Snapshot with { Dialog = null });
                    break;
            }
        };
        return (session, context);
    }

    static Dialog Merchant(SimulatedSession session, byte type) => new(session.Snapshot.Generation, session.Snapshot.Revision + 1,
        SellerId, 1, Seller, "Synthetic merchant page", false, 41, 0, type, []);

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(29)]
    public async Task Blackstar_research_buys_only_the_shortfall_at_its_actual_quote(int count)
    {
        var (session, context) = Fixture(count);
        await Call("PreparePotions", context);
        Assert.Equal(30 - count, Assert.Single(session.Commands, c => c.Kind == "buy").Quantity);
        Assert.Equal(30, session.Snapshot.Count(Potion));
        Assert.Equal(10000u - (uint)(30 - count) * 25, session.Snapshot.Gold);
        Assert.Null(Purchase(context));
        Assert.Null(context.Job.Pending);
        Assert.Null(session.Snapshot.Dialog);
    }

    [Fact]
    public async Task A_verified_potion_purchase_accepts_local_shop_dismissal_without_inventing_server_close()
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind != "close") normal(command);
        };

        await Call("PreparePotions", context);

        Assert.Equal(30, session.Snapshot.Count(Potion));
        Assert.Single(session.Commands, command => command.Kind == "buy");
        Assert.Single(session.Commands, command => command.Kind == "close");
        Assert.NotNull(session.Snapshot.Dialog);
        Assert.Equal(session.Snapshot.Dialog, typeof(WizardyResearchProgress)
            .GetProperty("PotionShopDeparture")?.GetValue(context.Job.Research));
        Assert.True(context.Job.Research!.NeedsRecovery);
        Assert.Null(Purchase(context));
    }

    [Fact]
    public async Task Research_returns_from_a_locally_dismissed_potion_shop_without_buying_again()
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind == "close" && command.Dialog is { IsPursuit: false }) return;
            if (command.Kind == "navigate" && command.Map == WizardyResearchEvidence.Map)
            {
                session.Set(session.Snapshot with { Map = command.Map, Position = command.Target, Dialog = null,
                    Entities = ImmutableDictionary<uint, Entity>.Empty.Add(55, new(55, "Dar", new(5, 2), true) { Kind = 2 }) });
                return;
            }
            if (command.Kind == "research-trigger")
            {
                session.Set(session.Snapshot with { Dialog = new(session.Snapshot.Generation,
                    session.Snapshot.Revision + 1, 77, 4, "", WizardyResearchEvidence.Cooldown,
                    true, 32, 1, 0, []) { ObservedAt = DateTimeOffset.UtcNow } });
                return;
            }
            normal(command);
        };

        await Assert.ThrowsAsync<QuestScheduledWaitException>(() => new WizardyResearchActions()
            .ExecuteAsync(context, new("preflight", "research-run", null), CancellationToken.None));

        Assert.Equal(new int?[] { 3214, WizardyResearchEvidence.Map },
            session.Commands.Where(command => command.Kind == "navigate").Select(command => command.Map));
        Assert.Single(session.Commands, command => command.Kind == "buy");
        Assert.Equal(30, session.Snapshot.Count(Potion));
        Assert.Null(Purchase(context));
        Assert.Null(context.Job.Pending);
        Assert.NotNull(context.Job.NextRun);
    }

    [Fact]
    public async Task Dar_shop_local_dismissal_does_not_wait_for_a_pursuit_close_receipt()
    {
        var (session, context) = Fixture();
        session.Set(session.Snapshot with { Dialog = new(session.Snapshot.Generation, session.Snapshot.Revision + 1,
            55, 1, "Dar", "Synthetic Dar shop menu", false, 41, 0, 0, [new("Buy", 41, 1)]),
            Entities = session.Snapshot.Entities.Add(55, new(55, "Dar", new(5, 2), true) { Kind = 2 }) });
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind != "close" || command.Dialog?.Name != "Dar") normal(command);
        };

        await Call("PreparePotions", context);

        Assert.Equal("Dar", session.Commands[0].Dialog?.Name);
        Assert.Single(session.Commands, command => command.Kind == "buy");
        Assert.Null(context.Job.Pending);
    }

    [Fact]
    public async Task The_legacy_verified_purchase_resumes_after_reconnect_without_a_second_payment()
    {
        var (session, context) = Fixture();
        var stock = Merchant(session, 4) with { Items = [new(Potion, 25)] };
        await context.SaveAsync(context.Job with { Research = new()
        {
            PotionPurchase = new(session.Snapshot.Generation, session.Snapshot.Revision, 9, 10000,
                21, 525, stock) { ReceiptVerified = true, Cleanup = stock, CloseSubmitted = true }
        } });
        session.Set(session.Snapshot with { Generation = session.Snapshot.Generation + 1, Map = 3214,
            Position = new(9, 8), Dialog = null, Inventory = session.Snapshot.Inventory.SetItem(2, new(2, Potion, 30)), Gold = 9475 });
        session.OnCommand = command =>
        {
            if (command.Kind == "navigate")
            {
                Assert.Equal(WizardyResearchEvidence.Map, command.Map);
                session.Set(session.Snapshot with { Map = command.Map, Position = command.Target, Dialog = null });
            }
            if (command.Kind == "research-trigger") session.Set(session.Snapshot with { Dialog = new(session.Snapshot.Generation,
                session.Snapshot.Revision + 1, 77, 4, "", WizardyResearchEvidence.Cooldown,
                true, 32, 1, 0, []) { ObservedAt = DateTimeOffset.UtcNow } });
            if (command.Kind == "close") session.Set(session.Snapshot with { Dialog = null });
        };

        await Assert.ThrowsAsync<QuestScheduledWaitException>(() => new WizardyResearchActions()
            .ExecuteAsync(context, new("preflight", "research-run", null), CancellationToken.None));

        Assert.DoesNotContain(session.Commands, command => command.Kind == "buy");
        Assert.Single(session.Commands, command => command.Kind == "navigate");
        Assert.Equal(30, session.Snapshot.Count(Potion));
        Assert.Null(Purchase(context));
        Assert.False(context.Job.Research!.NeedsRecovery);
    }

    [Theory]
    [InlineData("pursuit")]
    [InlineData("merchant")]
    [InlineData("revision")]
    [InlineData("connection")]
    public async Task A_dismissed_supply_shop_never_authorizes_departure_through_a_replacement_popup(string replacement)
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind != "close") normal(command);
        };
        await Call("PreparePotions", context);
        var old = session.Snapshot.Dialog!;
        var next = old with { Revision = session.Snapshot.Revision + 1, Text = "Synthetic replacement popup" };
        session.Set(replacement switch
        {
            "pursuit" => session.Snapshot with { Dialog = next with { IsPursuit = true } },
            "merchant" => session.Snapshot with { Dialog = next with { EntityId = 93, Name = "Another merchant" } },
            "revision" => session.Snapshot with { Dialog = next },
            _ => session.Snapshot with { Generation = session.Snapshot.Generation + 1,
                Dialog = next with { Generation = session.Snapshot.Generation + 1 } }
        });
        var popup = session.Snapshot.Dialog;
        int commandsBeforeResume = session.Commands.Count;

        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => new WizardyResearchActions()
            .ExecuteAsync(context, new("preflight", "research-run", null), CancellationToken.None));

        Assert.Equal(popup, session.Snapshot.Dialog);
        Assert.Empty(session.Commands.Skip(commandsBeforeResume));
        Assert.True(context.Job.Research!.NeedsRecovery);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    public async Task A_full_stack_skips_shop_travel(int count)
    {
        var (session, context) = Fixture(count);
        await Call("PreparePotions", context);
        Assert.Empty(session.Commands);
    }

    [Theory]
    [InlineData("unknown-inventory")]
    [InlineData("unknown-capacity")]
    [InlineData("full-weight")]
    [InlineData("no-gold")]
    public async Task Unknown_or_insufficient_resources_never_purchase(string problem)
    {
        var (session, context) = Fixture();
        session.Set(problem switch
        {
            "unknown-inventory" => session.Snapshot with { InventoryKnown = false },
            "unknown-capacity" => session.Snapshot with { MaxWeight = null },
            "full-weight" => session.Snapshot with { Weight = 100 },
            _ => session.Snapshot with { Gold = 1 }
        });
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => Call("PreparePotions", context));
        Assert.DoesNotContain(session.Commands, c => c.Kind == "buy");
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("unnamed")]
    [InlineData("nonmerchant")]
    [InlineData("connection")]
    public async Task An_uncertain_shop_arrival_does_not_open_a_guessed_seller(string problem)
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            normal(command);
            if (command.Kind != "navigate") return;
            session.Set(problem switch
            {
                "ambiguous" => session.Snapshot with { Entities = session.Snapshot.Entities.Add(93, new(93, "Another merchant", new(8, 4), true) { Kind = 2 }) },
                "unnamed" => session.Snapshot with { Entities = ImmutableDictionary<uint, Entity>.Empty.Add(SellerId, new(SellerId, "", new(7, 4), true) { Kind = 2 }) },
                "nonmerchant" => session.Snapshot with { Entities = ImmutableDictionary<uint, Entity>.Empty.Add(SellerId, new(SellerId, Seller, new(7, 4), true) { Kind = 0 }) },
                _ => session.Snapshot with { Generation = session.Snapshot.Generation + 1 }
            });
        };
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => Call("PreparePotions", context));
        Assert.DoesNotContain(session.Commands, c => c.Kind is "open" or "buy");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("different-entity")]
    [InlineData("character-change")]
    public async Task Only_the_bound_fresh_unique_Exkuranum_offer_authorizes_buying(string problem)
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            normal(command);
            if (command.Kind != "choose") return;
            if (problem == "character-change")
            {
                session.Set(session.Snapshot with { Name = "AnotherSyntheticWizard" });
                return;
            }
            var stock = session.Snapshot.Dialog!;
            session.Set(session.Snapshot with { Dialog = problem switch
            {
                "missing" => stock with { Items = [new("Other potion", 25)] },
                "duplicate" => stock with { Items = [new(Potion, 25), new(Potion, 26)] },
                _ => stock with { EntityId = 99 }
            } });
        };
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => Call("PreparePotions", context));
        Assert.DoesNotContain(session.Commands, c => c.Kind == "buy");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_uncertain_purchase_is_never_bought_again_and_reconnect_cannot_prove_it(bool reconnect)
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind == "buy") throw new TimeoutException("Synthetic uncertain purchase");
            normal(command);
        };
        await Assert.ThrowsAsync<TimeoutException>(() => Call("PreparePotions", context));
        Assert.NotNull(Purchase(context));
        session.Set(session.Snapshot with { Generation = session.Snapshot.Generation + (reconnect ? 1 : 0),
            Inventory = session.Snapshot.Inventory.SetItem(2, new(2, Potion, 30)), Gold = 9475 });
        if (reconnect) await Assert.ThrowsAnyAsync<QuestBlockedException>(() => Call("PreparePotions", context));
        else await Call("PreparePotions", context);
        Assert.Single(session.Commands, c => c.Kind == "buy");
        Assert.Equal(reconnect, Purchase(context) is not null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Gold_or_items_alone_cannot_settle_a_purchase(bool itemsOnly)
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind == "buy")
            {
                session.Set(itemsOnly ? session.Snapshot with { Inventory = session.Snapshot.Inventory.SetItem(2, new(2, Potion, 30)) }
                    : session.Snapshot with { Gold = 9475 });
                return;
            }
            normal(command);
        };
        await Assert.ThrowsAsync<TimeoutException>(() => Call("PreparePotions", context));
        await Assert.ThrowsAsync<TimeoutException>(() => Call("PreparePotions", context));
        Assert.Single(session.Commands, c => c.Kind == "buy");
        Assert.NotNull(Purchase(context));
    }

    [Fact]
    public async Task A_saved_shop_close_is_reconciled_without_another_close_or_payment()
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind == "close") throw new TimeoutException("Synthetic uncertain close");
            normal(command);
        };
        await Assert.ThrowsAsync<TimeoutException>(() => Call("PreparePotions", context));
        await Assert.ThrowsAsync<TimeoutException>(() => Call("PreparePotions", context));
        Assert.Single(session.Commands, c => c.Kind == "buy");
        Assert.Single(session.Commands, c => c.Kind == "close");
        session.Set(session.Snapshot with { Dialog = null });
        await Call("ReconcilePotionPurchase", context);
        Assert.Null(Purchase(context));
    }

    [Fact]
    public async Task A_known_research_opening_is_closed_and_its_trigger_left_before_supply_travel()
    {
        var (session, context) = Fixture();
        session.Set(session.Snapshot with { Position = new(6, 9), Dialog = new(session.Snapshot.Generation,
            session.Snapshot.Revision + 1, 77, 4, "", WizardyResearchEvidence.Opening, true, 32, 1, 2,
            [new(WizardyResearchEvidence.Begin, 0, 1)]) });
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind == "research-depart")
                session.Set(session.Snapshot with { Position = new(7, 8) });
            normal(command);
        };
        await Call("PreparePotions", context);
        Assert.Equal(new[] { "close", "research-depart", "navigate" }, session.Commands.Take(3).Select(c => c.Kind));
        Assert.Null(context.Job.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Supply_travel_preserves_unknown_popups_and_pending_research(bool submitted)
    {
        var (session, context) = Fixture();
        if (submitted)
            await context.BeginAsync("research-submit");
        else
            session.Set(session.Snapshot with { Dialog = new(session.Snapshot.Generation, session.Snapshot.Revision + 1,
                77, 4, "", "Synthetic unknown permanent choice", true, 32, 1, 2, [new("Yes", 0, 1)]) });
        var pending = context.Job.Pending;
        var popup = session.Snapshot.Dialog;
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => Call("PreparePotions", context));
        Assert.Empty(session.Commands);
        Assert.Equal(pending, context.Job.Pending);
        Assert.Equal(popup, session.Snapshot.Dialog);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_stock_is_awaited_even_after_the_old_menu_closes(bool closeBeforeStock)
    {
        DelayedStock? delayed = null;
        var (session, context) = Fixture(wrap: inner => delayed = new(inner));
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            if (command.Kind != "choose") { normal(command); return; }
            if (closeBeforeStock) session.Set(session.Snapshot with { Dialog = null });
            delayed!.WaitingForStock = true;
        };
        await Call("PreparePotions", context);
        Assert.True(delayed!.Delivered);
        Assert.Single(session.Commands, c => c.Kind == "buy");
        Assert.Equal(30, session.Snapshot.Count(Potion));
    }

    sealed class DelayedStock(SimulatedSession inner) : IGameSession
    {
        public bool WaitingForStock { get; set; }
        public bool Delivered { get; private set; }
        public CharacterSnapshot Snapshot => inner.Snapshot;
        public IReadOnlySet<string> Capabilities => inner.Capabilities;
        public event Action<CharacterSnapshot>? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
        public event Action<string>? Interrupted { add => inner.Interrupted += value; remove => inner.Interrupted -= value; }
        public Task ExecuteAsync(GameCommand command, CancellationToken ct) => inner.ExecuteAsync(command, ct);
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken ct)
        {
            if (WaitingForStock)
            {
                WaitingForStock = false;
                Delivered = true;
                inner.Set(Snapshot with { Dialog = Merchant(inner, 4) with { Items = [new(Potion, 25)] } });
            }
            return inner.WaitAsync(predicate, after, timeout, ct);
        }
    }

    [Fact]
    public async Task Dar_shop_menu_is_closed_before_the_potion_supply_trip()
    {
        var (session, context) = Fixture();
        session.Set(session.Snapshot with { Dialog = new(session.Snapshot.Generation, session.Snapshot.Revision + 1,
            55, 1, "Dar", "Synthetic Dar shop menu", false, 41, 0, 0, [new("Buy", 41, 1)]),
            Entities = session.Snapshot.Entities.Add(55, new(55, "Dar", new(5, 2), true) { Kind = 2 }) });
        await Call("PreparePotions", context);
        Assert.Equal("close", session.Commands[0].Kind);
        Assert.Equal("Dar", session.Commands[0].Dialog!.Name);
        Assert.Single(session.Commands, c => c.Kind == "buy");
        Assert.Null(context.Job.Pending);
        Assert.Null(Purchase(context));
    }

    [Fact]
    public async Task Stock_filled_before_the_quote_needs_no_payment_and_closes_the_shop()
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            normal(command);
            if (command.Kind == "choose") session.Set(session.Snapshot with
            {
                Inventory = session.Snapshot.Inventory.SetItem(2, new(2, Potion, 30))
            });
        };
        await Call("PreparePotions", context);
        Assert.DoesNotContain(session.Commands, c => c.Kind == "buy");
        Assert.Single(session.Commands, c => c.Kind == "close");
        Assert.Null(session.Snapshot.Dialog);
        Assert.Null(Purchase(context));
    }

    [Fact]
    public async Task Unknown_inventory_after_stock_arrival_cannot_confirm_a_full_stack()
    {
        var (session, context) = Fixture();
        var normal = session.OnCommand!;
        session.OnCommand = command =>
        {
            normal(command);
            if (command.Kind == "choose") session.Set(session.Snapshot with
            {
                InventoryKnown = false,
                Inventory = session.Snapshot.Inventory.SetItem(2, new(2, Potion, 30))
            });
        };
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => Call("PreparePotions", context));
        Assert.DoesNotContain(session.Commands, c => c.Kind == "buy");
    }
}
