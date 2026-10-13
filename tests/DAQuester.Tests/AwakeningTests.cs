using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;
using static DAQuester.Core.AwakeningEvidence;

namespace DAQuester.Tests;

public sealed class AwakeningTests
{
    sealed class Journal : IQuestJournal
    {
        public List<QuestJob> Saved { get; } = [];
        public QuestJob Load(string character, string quest) => Saved.LastOrDefault() ?? new() { Character = character, Quest = quest };
        public Task SaveAsync(QuestJob job, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            Saved.Add(job);
            return Task.CompletedTask;
        }
    }

    sealed class Scripted(int map = TutorialMap) : IGameSession
    {
        public CharacterSnapshot Snapshot { get; private set; } = FoundationTests.Ready(3, 50) with
        {
            Name = "Sample", Map = map, Position = new(26, 47),
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, Stick, 1)),
            Spells = ImmutableDictionary<byte, string>.Empty, Skills = ImmutableDictionary<byte, string>.Empty
        };
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string> { "navigate", "dialog", "inventory", "spell" };
        public event Action<CharacterSnapshot>? Changed;
        public event Action<string>? Interrupted { add { } remove { } }
        public List<GameCommand> Commands { get; } = [];
        readonly HashSet<string> discovered = [];
        public bool EntryUnanswered { get; set; }
        public bool ArrivedBeforeTimeout { get; set; }
        public bool RefuseEntry { get; set; }
        public bool PopupDuringDiscovery { get; set; }
        public bool ReconnectedDuringDiscovery { get; set; }
        bool popupSeen;

        public void Set(CharacterSnapshot value)
        {
            Snapshot = value with { Revision = Snapshot.Revision + 1 };
            Changed?.Invoke(Snapshot);
        }

        Dialog Dialogue(string name, string text, bool pursuit = false, DialogChoice[]? choices = null, bool next = false) =>
            new(Snapshot.Generation, Snapshot.Revision + 1, name == "Torrance" ? 100u : name == "Bartley" ? 200u : 300u,
                name.Length == 0 ? (byte)4 : (byte)1, name, text, pursuit, pursuit ? (ushort)8 : (ushort)0, 1,
                choices?.Length > 0 ? (byte)2 : (byte)0, [.. choices ?? []], HasNext: next);

        public Task ExecuteAsync(GameCommand command, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Commands.Add(command);
            var s = Snapshot;
            switch (command.Kind)
            {
                case "discover-npc":
                    if (ReconnectedDuringDiscovery)
                    {
                        Set(s with { Generation = s.Generation + 1 });
                        break;
                    }
                    if (PopupDuringDiscovery && !popupSeen)
                    {
                        popupSeen = true;
                        Set(s with { Dialog = Dialogue("", "Welcome to Dark Ages : Online Roleplaying.", true, next: true) });
                        throw new QuestPopupException(Snapshot.Dialog!, Snapshot.Map);
                    }
                    discovered.Add(command.Text!);
                    Set(s with { Entities = s.Entities.SetItem(200, new(200, command.Text!, new(7, 7), true)) });
                    break;
                case "visit":
                    if (command.Text is "Bartley" or "Raghnall" && !discovered.Contains(command.Text))
                        throw new TimeoutException("Server evidence timeout.");
                    Set(s with { Map = command.Map, Position = new(4, 6) });
                    break;
                case "open" when command.Text == "Torrance":
                    Set(s with { Dialog = Dialogue("Torrance", "Hello. What can I do for you?", choices: [new("(( Tutorial ))", 1032, 6)]) });
                    break;
                case "open" when command.Text == "Bartley":
                    Set(s with { Dialog = Dialogue("Bartley", "Choose a tutorial.", choices: [new(Interface, 10, 1), new(GeneralInfo, 11, 2)]) });
                    break;
                case "open" when command.Text == "Raghnall":
                    Set(s with { Dialog = Dialogue("Raghnall", "Combat advice", true, next: true) });
                    break;
                case "choose" when command.Text == "(( Tutorial ))":
                    Set(s with { Dialog = Dialogue("Torrance", "Want to master Dark Ages?", true, next: true) });
                    break;
                case "next" when s.Dialog?.Name == "Torrance":
                    Set(s with { Dialog = Dialogue("Torrance", "Are you up for the challenge?", true,
                        [new("No, I don't want to go there yet.", 8, 1), new("I am up to the challenge. Take me there.", 8, 2)], true) });
                    break;
                case "choose" when command.Text == "I am up to the challenge. Take me there.":
                    if (RefuseEntry)
                        Set(s with { Dialog = Dialogue("Torrance", "You are too old for this tutorial.", true) });
                    else if (!EntryUnanswered || ArrivedBeforeTimeout)
                        Set(s with { Map = TutorialMap, Position = new(26, 47), Entities = ImmutableDictionary<uint, Entity>.Empty, Dialog = null });
                    if (EntryUnanswered)
                        throw new TimeoutException("Server evidence timeout.");
                    break;
                case "choose" when command.Text == Interface:
                    Set(s with { Dialog = Dialogue("Bartley", "Press a for inventory.", true, next: true) });
                    break;
                case "choose" when command.Text == GeneralInfo:
                    Set(s with { Dialog = Dialogue("Bartley", "Do you understand these guidelines?", true, [new("Yes", 8, 1)]) });
                    break;
                case "choose" when command.Text == "Yes":
                    Set(s with { Dialog = null });
                    break;
                case "next" when s.Dialog?.Name == "Bartley":
                    Set(s with { Dialog = null, Spells = s.Spells.SetItem(1, Nis), Skills = s.Skills.SetItem(1, "Look") });
                    break;
                case "next" when s.Dialog?.Name == "Raghnall":
                    Set(s with { Dialog = null, Inventory = s.Inventory.SetItem(1, new(1, Stick, 1)) });
                    break;
                case "navigate" when command.Map == RoomMap:
                    Set(s with { Position = command.Target, Dialog = Dialogue("", "Wake up.", true, next: true) });
                    break;
                case "navigate":
                    Set(s with { Map = command.Map, Position = command.Target });
                    break;
                case "step":
                    Set(s with { Position = Exit, Dialog = Dialogue("", "I will take you back to your home now", true, [new("Ok", 8, 1)]) });
                    break;
                case "choose" when command.Text == "Ok":
                    Set(s with { Map = RoomMap, Position = new(2, 3), Dialog = null });
                    break;
                case "next" when s.Map == RoomMap:
                    var items = s.Inventory;
                    byte slot = 10;
                    foreach (string item in WakeRewards)
                        items = items.SetItem(slot, new(slot++, item, 1));
                    Set(s with { Level = 4, Gold = 2050, Inventory = items, Dialog = null });
                    break;
                case "next":
                case "close":
                    Set(s with { Dialog = null });
                    break;
                default:
                    throw new InvalidOperationException("Unexpected synthetic command: " + command.Kind);
            }
            return Task.CompletedTask;
        }

        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return Snapshot.Revision > after && predicate(Snapshot) ? Task.FromResult(Snapshot)
                : Task.FromException<CharacterSnapshot>(new TimeoutException("Server evidence timeout."));
        }
    }

    static QuestContext Context(Scripted session, Journal journal, QuestJob? job = null) =>
        new(session, journal, new(), job ?? new() { Character = "Sample", Quest = Id, Step = "run" });
    static Task Run(QuestContext context) => new AwakeningActions().ExecuteAsync(context, new("run", "awakening-run", "complete"), default);

    [Fact]
    public void Level_three_starter_can_plan_entry_from_Mileth_without_claiming_tutorial_completion()
    {
        var s = new Scripted(500).Snapshot;
        Assert.Equal(EligibilityKind.Eligible, Eligibility(s).Kind);
        Assert.Equal(EligibilityKind.Unknown, Eligibility(s with { Level = null }).Kind);
    }

    [Fact]
    public async Task Outside_start_walks_to_Torrance_and_verifies_transfer_before_tutorial_steps()
    {
        var session = new Scripted(500);
        var journal = new Journal();
        var context = Context(session, journal);
        await Run(context);
        Assert.Equal("complete", context.Job.Step);
        Assert.NotNull(context.Job.CompletedAt);
        Assert.Null(context.Job.Pending);
        Assert.Contains(session.Commands, c => c.Kind == "visit" && c.Map == 133 && c.Text == "Torrance");
        Assert.Single(session.Commands, c => c.Kind == "choose" && c.Text == "I am up to the challenge. Take me there.");
        Assert.Contains(journal.Saved, j => j.Pending?.Kind == "awakening-entry");
    }

    [Fact]
    public async Task Starter_entry_and_discovery_accept_saved_character_name_casing()
    {
        var session = new Scripted(500);
        var context = Context(session, new Journal(), new() { Character = "sample", Quest = Id, Step = "run" });
        await Run(context);
        Assert.NotNull(context.Job.CompletedAt);
    }

    [Fact]
    public async Task Resume_with_existing_Stick_discovers_Bartley_before_attempting_to_visit()
    {
        var session = new Scripted();
        var context = Context(session, new Journal());
        await Run(context);
        Assert.NotNull(context.Job.CompletedAt);
        Assert.Contains(session.Commands, c => c.Kind == "discover-npc" && c.Text == Bartley);
        Assert.DoesNotContain(session.Commands, c => c.Text == "Torrance" || c.Text == Raghnall);
        Assert.True(KnowsNis(session.Snapshot));
    }

    [Fact]
    public async Task Discovery_reactor_popup_is_read_before_continuing_toward_NPC()
    {
        var session = new Scripted { PopupDuringDiscovery = true };
        var context = Context(session, new Journal());
        await Run(context);
        Assert.NotNull(context.Job.CompletedAt);
        Assert.Contains(session.Commands, c => c.Kind == "next" && c.Dialog?.EntityType == 4);
    }

    [Fact]
    public async Task Unconfirmed_entry_stays_pending_and_does_not_resubmit_on_resume()
    {
        var session = new Scripted(500) { EntryUnanswered = true };
        var context = Context(session, new Journal());
        await Assert.ThrowsAnyAsync<Exception>(() => Run(context));
        Assert.Equal("awakening-entry", context.Job.Pending?.Kind);
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(context));
        Assert.Single(session.Commands, c => c.Kind == "choose" && c.Text == "I am up to the challenge. Take me there.");
    }

    [Fact]
    public async Task Reconnection_during_discovery_stops_before_opening_NPC_or_claiming_completion()
    {
        var session = new Scripted { ReconnectedDuringDiscovery = true };
        var context = Context(session, new Journal());
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(context));
        Assert.Null(context.Job.CompletedAt);
        Assert.DoesNotContain(session.Commands, c => c.Kind is "visit" or "open");
    }

    [Fact]
    public async Task Refused_entry_stops_and_keeps_submission_checkpoint_without_starting_tutorial()
    {
        var session = new Scripted(500) { RefuseEntry = true };
        var context = Context(session, new Journal());
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(context));
        Assert.NotNull(context.Job.Pending);
        Assert.Null(context.Job.CompletedAt);
        Assert.DoesNotContain(session.Commands, c => c.Kind == "discover-npc");
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(context));
        Assert.Single(session.Commands, c => c.Text == EntryAnswer);
    }

    [Fact]
    public async Task Arrived_entry_reconciles_pending_transfer_without_entering_again()
    {
        var session = new Scripted(500) { EntryUnanswered = true, ArrivedBeforeTimeout = true };
        var context = Context(session, new Journal());
        await Assert.ThrowsAnyAsync<Exception>(() => Run(context));
        Assert.Equal(TutorialMap, session.Snapshot.Map);
        Assert.NotNull(context.Job.Pending);
        await Run(context);
        Assert.NotNull(context.Job.CompletedAt);
        Assert.Null(context.Job.Pending);
        Assert.Single(session.Commands, c => c.Kind == "choose" && c.Text == "I am up to the challenge. Take me there.");
    }
}
