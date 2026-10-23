using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class WolfJourneyRegressionTests
{
    static SimulatedSession Ready(int map = 463)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = "Elara", Id = 1, Connected = true, Level = 99,
            Hp = 100, MaxHp = 100, Gold = 1000000, Experience = 1000, Weight = 1, MaxWeight = 100,
            Map = map, Position = new(47, 24), InventoryKnown = true, Inventory = ImmutableDictionary<byte, Item>.Empty,
            LegendsKnown = true, LegendsRevision = session.Snapshot.Revision + 1, Legends = [] });
        return session;
    }

    static QuestContext Context(IGameSession session, WolfQuestProgress progress) => new(session,
        new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N"))),
        new(), new() { Character = "Elara", Quest = WolfQuestEvidence.Id, Wolf = progress });

    static Dialog Warning(SimulatedSession session, string? text = null) => new(session.Snapshot.Generation,
        session.Snapshot.Revision + 1, 77, 4, "", text ?? WolfQuestEvidence.EntranceWarning, true, 995, 6, 2,
        [new("Yes", 995, 1), new("No", 995, 2)]) { HasNext = true };

    [Theory]
    [InlineData(47, 25)]
    [InlineData(48, 25)]
    [InlineData(48, 26)]
    public async Task Expected_entrance_warning_interrupts_approach_and_continues_without_manual_resume(int x, int y)
    {
        var session = Ready();
        var context = Context(new WolfMap(session), new() { Briefed = true });
        session.OnCommand = command =>
        {
            if (command.Kind == "navigate" && command.Map == 463)
            {
                session.Set(session.Snapshot with { Position = new(x, y), Dialog = Warning(session) });
                throw new QuestBlockedException("A popup opened during travel. It is preserved; review it before continuing.");
            }
            if (command.Kind == "choose")
            {
                Assert.Equal("Yes", command.Text);
                Assert.Equal(session.Snapshot.Dialog, command.Dialog);
                Assert.Equal(session.Snapshot.Dialog!.Revision, command.ExpectedDialogRevision);
                session.Set(session.Snapshot with { Map = 464, Position = new(0, 5), Dialog = null });
            }
            else if (command.Kind == "navigate" && command.Map == 464)
                session.Set(session.Snapshot with { Position = command.Target });
        };

        await new WolfQuestActions().ExecuteAsync(context, new("enter", "wolf-enter", "hunt"), default);

        Assert.Single(session.Commands, c => c.Kind == "choose");
        Assert.True(context.Job.Wolf!.EntrySettled);
        Assert.Null(context.Job.Pending);
        Assert.Equal(context.Job.Wolf.Interior, session.Snapshot.Position);
    }

    [Fact]
    public async Task An_unknown_entrance_choice_is_preserved_without_selecting_yes()
    {
        var session = Ready();
        var context = Context(new WolfMap(session), new() { Briefed = true });
        session.OnCommand = command =>
        {
            if (command.Kind == "navigate")
            {
                session.Set(session.Snapshot with { Position = new(47, 25), Dialog = Warning(session, "Unknown permanent offer") });
                throw new QuestBlockedException("A popup opened during travel.");
            }
        };

        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => new WolfQuestActions()
            .ExecuteAsync(context, new("enter", "wolf-enter", "hunt"), default));

        Assert.DoesNotContain(session.Commands, c => c.Kind == "choose");
        Assert.Equal("Unknown permanent offer", session.Snapshot.Dialog!.Text);
    }

    [Fact]
    public async Task Resume_handles_the_warning_already_observed_before_the_navigation_target()
    {
        var session = Ready();
        var context = Context(new WolfMap(session), new() { Briefed = true });
        await context.BeginAsync("wolf-approach");
        session.Set(session.Snapshot with { Position = new(47, 25), Dialog = Warning(session) });
        session.OnCommand = command =>
        {
            if (command.Kind == "choose")
                session.Set(session.Snapshot with { Map = 464, Position = new(0, 5), Dialog = null });
            else if (command.Kind == "navigate")
                session.Set(session.Snapshot with { Position = command.Target });
        };

        await new WolfQuestActions().ExecuteAsync(context, new("enter", "wolf-enter", "hunt"), default);

        Assert.Equal("Yes", Assert.Single(session.Commands, c => c.Kind == "choose").Text);
        Assert.True(context.Job.Wolf!.EntrySettled);
        Assert.DoesNotContain(session.Commands, c => c.Kind == "navigate" && c.Map == 463);
    }

    [Fact]
    public async Task An_uncertain_yes_is_never_submitted_again_on_resume()
    {
        var session = Ready();
        var context = Context(new WolfMap(session), new() { Briefed = true });
        await context.BeginAsync("wolf-approach");
        session.Set(session.Snapshot with { Position = new(47, 25), Dialog = Warning(session) });
        session.OnCommand = _ => throw new TimeoutException("Synthetic uncertain entry reply");
        var action = new WolfQuestActions();

        await Assert.ThrowsAsync<TimeoutException>(() => action.ExecuteAsync(context, new("enter", "wolf-enter", "hunt"), default));
        await Assert.ThrowsAsync<TimeoutException>(() => action.ExecuteAsync(context, new("enter", "wolf-enter", "hunt"), default));

        Assert.Equal("choose", Assert.Single(session.Commands).Kind);
        Assert.Equal("wolf-entry", context.Job.Pending!.Kind);
        Assert.Equal(463, session.Snapshot.Map);
    }

    [Theory]
    [InlineData("Vanilla Potion")]
    [InlineData("Fifleaf deum")]
    public async Task Confirm_records_the_actual_observed_potion_with_new_experience_and_legend(string potion)
    {
        var session = Ready(454);
        var before = session.Snapshot;
        var turnIn = new Transaction("wolf-open", before.Generation, before.Revision, before.Gold!.Value, 0,
            before.Experience!.Value, DateTimeOffset.UtcNow)
        { InventoryBaselineKnown = true, InventoryCounts = new(), Legends = [] };
        var context = Context(session, new() { TurnIn = turnIn, LegendBaselineRevision = before.Revision });
        session.Set(before with { Experience = before.Experience + 60000, Inventory = before.Inventory.SetItem(2, new(2, potion, 1)),
            Legends = [WolfQuestEvidence.Legend], LegendsRevision = before.Revision + 1 });

        await new WolfQuestActions().ExecuteAsync(context, new("confirm", "wolf-confirm", "complete"), default);

        Assert.NotNull(context.Job.CompletedAt);
        Assert.Equal(1, context.Job.Rewards["item:" + potion]);
        Assert.Equal(60000, context.Job.Rewards["experience"]);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public void An_unrelated_item_does_not_establish_the_wolf_reward()
    {
        var session = Ready(454);
        var s = session.Snapshot;
        var baseline = new Transaction("wolf-open", s.Generation, s.Revision - 1, 1000000, 0, 1000, DateTimeOffset.UtcNow)
        { InventoryBaselineKnown = true, InventoryCounts = new(), Legends = [] };
        var after = s with { Experience = 61000, Legends = [WolfQuestEvidence.Legend],
            Inventory = s.Inventory.SetItem(2, new(2, "Unknown Potion", 1)) };

        Assert.False(WolfQuestEvidence.Reward(after, new() { TurnIn = baseline, LegendBaselineRevision = s.Revision - 1 }));
    }

    [Theory]
    [InlineData("old-potion")]
    [InlineData("no-xp")]
    [InlineData("old-legend")]
    [InlineData("reconnect")]
    public void Fifleaf_alone_never_replaces_the_other_reward_receipts(string scenario)
    {
        var session = Ready(454);
        var before = session.Snapshot;
        var baseline = new Transaction("wolf-open", before.Generation, before.Revision, 1000000, 0, 1000, DateTimeOffset.UtcNow)
        { InventoryBaselineKnown = true, InventoryCounts = new() { ["Fifleaf deum"] = scenario == "old-potion" ? 1 : 0 },
            Legends = scenario == "old-legend" ? [WolfQuestEvidence.Legend] : [] };
        session.Set(before with { Generation = before.Generation + (scenario == "reconnect" ? 1 : 0),
            Experience = scenario == "no-xp" ? 1000u : 61000u, Legends = [WolfQuestEvidence.Legend],
            LegendsRevision = before.Revision + 1, Inventory = before.Inventory.SetItem(2, new(2, "Fifleaf deum", 1)) });

        Assert.False(WolfQuestEvidence.Reward(session.Snapshot, new() { TurnIn = baseline, LegendBaselineRevision = before.Revision }));
    }

    [Fact]
    public async Task A_manually_completed_wolf_goal_settles_only_an_unsubmitted_approach_without_claiming_rewards()
    {
        var session = Ready(454);
        var oldGeneration = session.Snapshot.Generation;
        session.Set(session.Snapshot with { Generation = oldGeneration + 1, Legends = [WolfQuestEvidence.Legend],
            LegendsRevision = session.Snapshot.Revision + 1 });
        var context = Context(session, new() { Briefed = true });
        await context.SaveAsync(context.Job with { Step = "enter", Pending = new("wolf-approach", oldGeneration, 1, 1000000, 0, 1000, DateTimeOffset.UtcNow) });

        await new WolfQuestActions().ExecuteAsync(context, new("enter", "wolf-enter", "hunt"), default);

        Assert.Equal("complete", context.Job.Step);
        Assert.NotNull(context.Job.CompletedAt);
        Assert.Null(context.Job.Pending);
        Assert.Equal(WolfQuestEvidence.Legend, context.Job.CompletionLegend);
        Assert.Empty(context.Job.Rewards);
        Assert.Empty(session.Commands);
    }

    [Theory]
    [InlineData("submitted-entry")]
    [InlineData("submitted-reward")]
    [InlineData("stale-legend")]
    public async Task A_current_mark_does_not_discard_an_uncertain_submission_or_stale_evidence(string scenario)
    {
        var session = Ready(454);
        long oldGeneration = session.Snapshot.Generation;
        session.Set(session.Snapshot with { Generation = oldGeneration + 1, Legends = [WolfQuestEvidence.Legend],
            LegendsRevision = scenario == "stale-legend" ? session.Snapshot.Revision + 100 : session.Snapshot.Revision + 1 });
        var pending = new Transaction(scenario == "submitted-entry" ? "wolf-entry" : "wolf-approach",
            oldGeneration, 1, 1000000, 0, 1000, DateTimeOffset.UtcNow);
        var context = Context(session, new() { Briefed = true,
            TurnIn = scenario == "submitted-reward" ? pending with { Kind = "wolf-open" } : null });
        await context.SaveAsync(context.Job with { Step = "enter", Pending = pending });

        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => new WolfQuestActions()
            .ExecuteAsync(context, new("enter", "wolf-enter", "hunt"), default));

        Assert.Null(context.Job.CompletedAt);
        Assert.Equal(pending, context.Job.Pending);
        Assert.Empty(session.Commands);
    }

    sealed class WolfMap(SimulatedSession inner) : IGameSession, ICombatMap
    {
        public CharacterSnapshot Snapshot => inner.Snapshot;
        public IReadOnlySet<string> Capabilities => inner.Capabilities;
        public event Action<CharacterSnapshot>? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
        public event Action<string>? Interrupted { add => inner.Interrupted += value; remove => inner.Interrupted -= value; }
        public Task ExecuteAsync(GameCommand command, CancellationToken ct) => inner.ExecuteAsync(command, ct);
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken ct) => inner.WaitAsync(predicate, after, timeout, ct);
        public (int Width, int Height)? Size => (10, 10);
        public bool IsWall(Tile t) => t.X < 0 || t.Y < 0 || t.X >= 10 || t.Y >= 10;
        public bool IsWalkable(Tile t) => !IsWall(t);
        public bool IsWarp(Tile t) => t == new Tile(0, 5);
        public int PathLength(Tile from, Tile to) => Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
    }
}
