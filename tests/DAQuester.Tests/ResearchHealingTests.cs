using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class ResearchHealingTests
{
    const string Begin = WizardyResearchEvidence.Begin;
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    static QuestDefinition Recipe() => new(WizardyResearchEvidence.Id, 1, "Research", "Synthetic", ["navigate", "dialog", "inventory"], [new("preflight", "research-run", null)], 12);

    static SimulatedSession Ready()
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with { Class = "Wizard", Hp = 1000, MaxHp = 1000, Gold = 5000, Map = 401,
            Position = new(7, 9), Inventory = ImmutableDictionary<byte, Item>.Empty
                .Add(1, new(1, "Parchment Scroll", 1)).Add(10, new(10, "Exkuranum", 30)) });
        s.Set(s.Snapshot with { Dialog = Page(s, "Synthetic opening", Begin) });
        return s;
    }

    static Dialog Page(SimulatedSession s, string text, params string[] choices) => new(s.Snapshot.Generation,
        s.Snapshot.Revision + 1, 77, 4, "", text, true, 32, 1, choices.Length == 0 ? (byte)0 : (byte)2,
        choices.Select((c, i) => new DialogChoice(c, 0, (byte)(i + 1))).ToImmutableArray()) { ObservedAt = DateTimeOffset.UtcNow };

    static void ContinueMenu(SimulatedSession s, GameCommand command)
    {
        s.Set(s.Snapshot with { Dialog = Page(s, "Synthetic continuation",
            command.Text switch { Begin => "Specific", "Specific" => "sorcery", _ => "Alone" }) });
    }

    [Fact]
    public async Task Failed_research_heals_to_actual_maximum_before_any_further_research()
    {
        var s = Ready();
        int attempts = 0;
        s.OnCommand = command =>
        {
            if (command.Kind == "use-item")
            {
                Assert.Equal("Exkuranum", command.ExpectedResource);
                Assert.Equal(s.Snapshot.Generation, command.ExpectedGeneration);
                s.Set(s.Snapshot with { Hp = 1000, Inventory = s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 29)),
                    ActiveStatus = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 3, "Synthetic potion used", DateTimeOffset.UtcNow) });
            }
            if (command.Kind == "choose")
            {
                if (command.Text != "Alone") ContinueMenu(s, command);
                else
                {
                    attempts++;
                    s.Set(s.Snapshot with { Hp = 300, Dialog = null, ActiveStatus = new(s.Snapshot.Generation,
                        s.Snapshot.Revision + 1, 3, WizardyResearchEvidence.FailedExperiment, DateTimeOffset.UtcNow) });
                }
            }
            if (command.Kind == "navigate")
            {
                Assert.Equal(1000u, s.Snapshot.Hp);
                throw new OperationCanceledException("Stop after the first healed research attempt.");
            }
        };
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.Equal(1, attempts);
        Assert.Equal(1000u, s.Snapshot.Hp);
        Assert.Equal(29, s.Snapshot.Count("Exkuranum"));
        Assert.Single(runner.Job.Research!.Attempts);
        Assert.Null(runner.Job.Pending);
        Assert.Empty(runner.Job.Rewards);
    }

    [Theory]
    [InlineData("no-receipt")]
    [InlineData("health-only")]
    [InlineData("item-only")]
    [InlineData("reconnect")]
    public async Task Interrupted_dose_never_resends_or_overwrites_pending_research(string variant)
    {
        var s = Ready();
        s.OnCommand = command =>
        {
            if (command.Kind == "choose")
            {
                if (command.Text != "Alone") ContinueMenu(s, command);
                else s.Set(s.Snapshot with { Hp = 300, Dialog = Page(s, "Synthetic unknown result") });
            }
            if (command.Kind == "use-item") throw new TimeoutException("Synthetic interrupted dose");
        };
        var journal = Journal();
        var runner = new QuestRunner(s, journal, Recipe(), new());
        await runner.RunAsync();
        Assert.Equal("research-submit", runner.Job.Pending?.Kind);
        Assert.NotNull(runner.Job.Research!.Healing);
        var pending = runner.Job.Pending;
        s.Set(s.Snapshot with
        {
            Hp = variant == "health-only" ? 1000u : s.Snapshot.Hp,
            Inventory = variant == "item-only" ? s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 29)) : s.Snapshot.Inventory,
            Generation = variant == "reconnect" ? s.Snapshot.Generation + 1 : s.Snapshot.Generation
        });
        var resumed = new QuestRunner(s, journal, Recipe(), new());
        await resumed.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.Equal(pending!.Kind, resumed.Job.Pending?.Kind);
        Assert.Equal(pending.Generation, resumed.Job.Pending?.Generation);
        Assert.Equal(pending.Started, resumed.Job.Pending?.Started);
        Assert.NotNull(resumed.Job.Research!.Healing);
        Assert.Empty(resumed.Job.Research.Attempts);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "buy" or "next" or "close");
    }

    [Fact]
    public async Task Late_dose_receipt_reconciles_without_drinking_again_while_unknown_result_stays_pending()
    {
        var s = Ready();
        s.OnCommand = command =>
        {
            if (command.Kind == "choose")
            {
                if (command.Text != "Alone") ContinueMenu(s, command);
                else s.Set(s.Snapshot with { Hp = 300, Dialog = Page(s, "Synthetic unknown result") });
            }
            if (command.Kind == "use-item") throw new TimeoutException("Synthetic dose sent before receipt");
        };
        var journal = Journal();
        var runner = new QuestRunner(s, journal, Recipe(), new());
        await runner.RunAsync();
        s.Set(s.Snapshot with { Hp = 1000, Inventory = s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 29)) });
        var resumed = new QuestRunner(s, journal, Recipe(), new());
        await resumed.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.Null(resumed.Job.Research!.Healing);
        Assert.Equal("research-submit", resumed.Job.Pending?.Kind);
        Assert.Empty(resumed.Job.Research.Attempts);
    }

    sealed class DelayedDamage(SimulatedSession inner) : IGameSession
    {
        public bool Armed { get; set; }
        public bool Hurt { get; private set; }
        public CharacterSnapshot Snapshot => inner.Snapshot;
        public IReadOnlySet<string> Capabilities => inner.Capabilities;
        public event Action<CharacterSnapshot>? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
        public event Action<string>? Interrupted { add => inner.Interrupted += value; remove => inner.Interrupted -= value; }
        public Task ExecuteAsync(GameCommand command, CancellationToken ct) => inner.ExecuteAsync(command, ct);
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken ct)
        {
            if (Armed && !Hurt)
            {
                Hurt = true;
                inner.Set(Snapshot with { Hp = 300, ActiveStatus = new(Snapshot.Generation, Snapshot.Revision + 1,
                    3, WizardyResearchEvidence.FailedExperiment, DateTimeOffset.UtcNow) });
                if (!predicate(Snapshot)) throw new TimeoutException("Damage must wake the research result wait immediately.");
            }
            return inner.WaitAsync(predicate, after, timeout, ct);
        }
    }

    [Fact]
    public async Task Damage_arriving_during_result_wait_wakes_healing_before_result_closure()
    {
        var s = Ready();
        var session = new DelayedDamage(s);
        s.OnCommand = command =>
        {
            if (command.Kind == "choose")
            {
                if (command.Text != "Alone") ContinueMenu(s, command);
                else session.Armed = true;
            }
            if (command.Kind == "use-item")
            {
                Assert.NotNull(s.Snapshot.Dialog);
                Assert.Equal(300u, s.Snapshot.Hp);
                s.Set(s.Snapshot with { Hp = 1000, Inventory = s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 29)),
                    Dialog = null, ActiveStatus = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 3,
                        "Synthetic potion reply", DateTimeOffset.UtcNow) });
            }
            if (command.Kind == "navigate") throw new OperationCanceledException("Stop after one healed attempt");
        };
        var runner = new QuestRunner(session, Journal(), Recipe(), new());
        await runner.RunAsync();
        Assert.True(session.Hurt);
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.Single(runner.Job.Research!.Attempts);
        Assert.Null(runner.Job.Pending);
        Assert.Equal(1000u, s.Snapshot.Hp);
    }

    [Fact]
    public async Task Damage_is_healed_before_reading_offered_result_Next()
    {
        var s = Ready();
        s.OnCommand = command =>
        {
            if (command.Kind == "choose")
            {
                if (command.Text != "Alone") ContinueMenu(s, command);
                else s.Set(s.Snapshot with { Hp = 300, Dialog = Page(s, "Synthetic result Next") with { HasNext = true } });
            }
            if (command.Kind == "use-item")
                s.Set(s.Snapshot with { Hp = 1000, Inventory = s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 29)) });
            if (command.Kind == "next")
            {
                Assert.Equal(1000u, s.Snapshot.Hp);
                s.Set(s.Snapshot with { Dialog = Page(s, "Synthetic final result"),
                    Inventory = s.Snapshot.Inventory.Remove(1).Add(2, new(2, "Abel Song", 3)) });
            }
            if (command.Kind == "close") throw new OperationCanceledException("Stop at cleanup");
        };
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.True(s.Commands.FindIndex(c => c.Kind == "use-item") < s.Commands.FindIndex(c => c.Kind == "next"));
        Assert.Single(runner.Job.Research!.Attempts);
    }

    [Fact]
    public async Task Research_status_arriving_during_potion_use_survives_its_later_potion_message()
    {
        var s = Ready();
        s.OnCommand = command =>
        {
            if (command.Kind == "choose")
            {
                if (command.Text != "Alone") ContinueMenu(s, command);
                else s.Set(s.Snapshot with { Hp = 300 });
            }
            if (command.Kind == "use-item")
            {
                s.Set(s.Snapshot with { ActiveStatus = new(s.Snapshot.Generation, s.Snapshot.Revision + 1,
                    3, WizardyResearchEvidence.FailedExperiment, DateTimeOffset.UtcNow) });
                s.Set(s.Snapshot with { Hp = 1000, Dialog = null,
                    Inventory = s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 29)),
                    ActiveStatus = new(s.Snapshot.Generation, s.Snapshot.Revision + 1,
                        3, "Synthetic potion message replaces the failure status", DateTimeOffset.UtcNow) });
            }
            if (command.Kind == "navigate") throw new OperationCanceledException("Stop after first attempt");
        };
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.Single(runner.Job.Research!.Attempts);
        Assert.Equal(WizardyResearchEvidence.FailedExperiment, runner.Job.Research.Attempts[0].Status?.Text);
        Assert.Null(runner.Job.Pending);
    }

    [Fact]
    public async Task Missing_split_stack_supplies_are_bought_then_character_returns_to_research()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 2))
            .Add(11, new(11, "Exkuranum", 5)) });
        s.OnCommand = command =>
        {
            if (command.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
            if (command.Kind == "navigate")
                s.Set(s.Snapshot with { Map = command.Map, Position = command.Target,
                    Entities = command.Map == 3214 ? ImmutableDictionary<uint, Entity>.Empty
                        .Add(92, new(92, "Synthetic potion merchant", new(8, 6), true) { Kind = 2 }) : [] });
            if (command.Kind == "open") s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation,
                s.Snapshot.Revision + 1, 92, 1, "Synthetic potion merchant", "Synthetic merchant", false,
                41, 0, 0, [new("Buy", 41, 1)]) });
            if (command.Kind == "choose")
            {
                if (command.Text == "Buy") s.Set(s.Snapshot with { Dialog = s.Snapshot.Dialog! with
                    { Revision = s.Snapshot.Revision + 1, Type = 4, Choices = [], Items = [new("Exkuranum", 20)] } });
                else if (command.Text == "Alone")
                {
                    Assert.Equal(401, s.Snapshot.Map);
                    Assert.Equal(30, s.Snapshot.Count("Exkuranum"));
                    s.Set(s.Snapshot with { Dialog = Page(s, WizardyResearchEvidence.Cooldown) });
                }
                else ContinueMenu(s, command);
            }
            if (command.Kind == "buy")
            {
                Assert.Equal(23, command.Quantity);
                s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(11).SetItem(10, new(10, "Exkuranum", 30)),
                    Gold = s.Snapshot.Gold - 460 });
            }
            if (command.Kind == "research-trigger") s.Set(s.Snapshot with { Dialog = Page(s, "Synthetic opening", Begin) });
        };
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync(singleAttempt: true, yieldScheduledWaits: true);
        Assert.Equal("Scheduled", runner.Job.Status);
        Assert.Equal(new int?[] { 3214, 401 }, s.Commands.Where(c => c.Kind == "navigate").Select(c => c.Map));
        Assert.Single(s.Commands, c => c.Kind == "buy");
        Assert.Single(s.Commands, c => c.Text == "Alone");
        Assert.Null(runner.Job.Research!.PotionPurchase);
        Assert.False(runner.Job.Research.HealthCheckRequired);
    }

    [Fact]
    public async Task Resuming_an_existing_research_menu_uses_remaining_potions_without_abandoning_the_sequence()
    {
        var s = Ready();
        var opening = s.Snapshot.Dialog!;
        s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 29)),
            Dialog = Page(s, "Synthetic resumed Specific choice", "Specific") });
        var journal = Journal();
        await journal.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = WizardyResearchEvidence.Id,
            Research = new() { Stage = 1, Source = opening } });
        s.OnCommand = command =>
        {
            if (command.Kind == "choose")
            {
                if (command.Text == "Alone") s.Set(s.Snapshot with { Dialog = Page(s, WizardyResearchEvidence.Cooldown) });
                else ContinueMenu(s, command);
            }
            if (command.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
        var runner = new QuestRunner(s, journal, Recipe(), new());
        await runner.RunAsync(singleAttempt: true, yieldScheduledWaits: true);
        Assert.Equal("Scheduled", runner.Job.Status);
        Assert.Equal(new[] { "Specific", "sorcery", "Alone" }, s.Commands.Where(c => c.Kind == "choose").Select(c => c.Text));
        Assert.DoesNotContain(s.Commands, c => c.Kind is "buy" or "navigate");
        Assert.Equal(29, s.Snapshot.Count("Exkuranum"));
    }

    [Fact]
    public async Task Final_result_Next_can_close_dialog_after_healing_without_leaving_a_saved_Next()
    {
        var s = Ready();
        s.OnCommand = command =>
        {
            if (command.Kind == "choose")
            {
                if (command.Text != "Alone") ContinueMenu(s, command);
                else s.Set(s.Snapshot with { Hp = 300, Dialog = Page(s, "Synthetic final research Next") with { HasNext = true } });
            }
            if (command.Kind == "use-item") s.Set(s.Snapshot with { Hp = 1000,
                Inventory = s.Snapshot.Inventory.SetItem(10, new(10, "Exkuranum", 29)) });
            if (command.Kind == "next") s.Set(s.Snapshot with { Dialog = null,
                Inventory = s.Snapshot.Inventory.Remove(1).Add(2, new(2, "Abel Song", 3)),
                ActiveStatus = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 3,
                    WizardyResearchEvidence.SorceryResult, DateTimeOffset.UtcNow) });
            if (command.Kind == "navigate") throw new OperationCanceledException("Stop after the final closed result");
        };
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.Single(runner.Job.Research!.Attempts);
        Assert.Null(runner.Job.Research.SubmittedNext);
        Assert.Null(runner.Job.Pending);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    public async Task Missing_maximum_health_blocks_before_research_submission(uint? maximum)
    {
        var s = Ready();
        s.Set(s.Snapshot with { MaxHp = maximum });
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync();
        Assert.Empty(s.Commands);
        Assert.Contains("maximum HP", runner.Job.Reason);
    }
}
