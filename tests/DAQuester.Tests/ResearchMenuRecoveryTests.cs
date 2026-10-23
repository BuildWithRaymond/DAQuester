using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class ResearchMenuRecoveryTests
{
    static Dialog Page(SimulatedSession s, string text, params string[] choices) => new(s.Snapshot.Generation,
        s.Snapshot.Revision + 1, 77, 4, "", text, true, 315, 19, 2,
        choices.Select((label, i) => new DialogChoice(label, 315, (byte)(i + 1))).ToImmutableArray());

    static async Task<(SimulatedSession Session, QuestContext Context)> Fixture(int stage, string kind, bool reconnect)
    {
        var s = WizardyResearchTests.Ready();
        string label = stage switch { 1 => WizardyResearchEvidence.ObservedBegin, 2 => "Specific", 3 => "Sorcery", _ => "Assistant" };
        var source = Page(s, "Synthetic setup menu", label);
        if (stage == 1)
        {
            string file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "../../../../../content/evidence/datester-7.41/wizardry-research-setup-closure.json"));
            using var evidence = JsonDocument.Parse(File.ReadAllText(file));
            source = evidence.RootElement.GetProperty("Source").Deserialize<Dialog>()! with
                { Generation = s.Snapshot.Generation, Revision = s.Snapshot.Revision + 1 };
        }
        if (kind == "research-next") source = source with { Choices = [], HasNext = true, Prompt = null };
        s.Set(s.Snapshot with { Dialog = source });
        var job = new QuestJob { Character = s.Snapshot.Name!, Quest = WizardyResearchEvidence.Id,
            ResearchOptions = stage == 4 ? new("Wren") : new(),
            Research = new() { Stage = stage, Source = source,
                Attempts = [new(DateTimeOffset.UtcNow.AddMinutes(-1), null, new())] },
            Rewards = new() { ["item:Mileth Song"] = 3 },
            Pending = new(kind, s.Snapshot.Generation, source.Revision - 1, 5000, 0, 0, DateTimeOffset.UtcNow)
            { EntityId = source.EntityId, EntityType = source.EntityType, InventoryBaselineKnown = true,
                InventoryCounts = new() { [WizardyResearchEvidence.Parchment] = 1 } } };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        await journal.SaveAsync(job);
        s.Set(s.Snapshot with { Generation = s.Snapshot.Generation + (reconnect ? 1 : 0), Dialog = null });
        return (s, new(s, journal, new(), job));
    }

    static Task Reconcile(QuestContext c) => (Task)typeof(WizardyResearchActions)
        .GetMethod("Reconcile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [c, CancellationToken.None])!;

    [Theory]
    [InlineData(1, "research-menu", false)]
    [InlineData(1, "research-menu", true)]
    [InlineData(2, "research-menu", true)]
    [InlineData(3, "research-menu", true)]
    [InlineData(4, "research-menu", true)]
    [InlineData(0, "research-next", true)]
    public async Task Abandoned_setup_can_restart_without_claiming_or_resending_an_experiment(int stage, string kind, bool reconnect)
    {
        var (s, c) = await Fixture(stage, kind, reconnect);
        await Reconcile(c);
        Assert.Null(c.Job.Pending);
        Assert.Equal(0, c.Job.Research!.Stage);
        Assert.Null(c.Job.Research.Source);
        Assert.Single(c.Job.Research.Attempts);
        Assert.Equal(3, c.Job.Rewards["item:Mileth Song"]);
        Assert.Null(c.Job.CompletedAt);
        Assert.Null(c.Job.NextRun);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_fresh_opening_or_explicit_rest_can_replace_the_abandoned_setup(bool reconnect, bool rest)
    {
        var (s, c) = await Fixture(1, "research-menu", reconnect);
        s.Set(s.Snapshot with { Dialog = rest ? Page(s, WizardyResearchEvidence.Cooldown)
            : Page(s, WizardyResearchEvidence.Opening, WizardyResearchEvidence.Begin) });
        var page = s.Snapshot.Dialog;
        await Reconcile(c);
        Assert.Null(c.Job.Pending);
        Assert.Equal(0, c.Job.Research!.Stage);
        Assert.Equal(page, s.Snapshot.Dialog);
        Assert.Empty(s.Commands);
        Assert.Null(c.Job.NextRun);
    }

    [Fact]
    public async Task An_unanswered_current_setup_page_is_not_selected_again()
    {
        var (s, c) = await Fixture(1, "research-menu", false);
        s.Set(s.Snapshot with { Dialog = c.Job.Research!.Source });
        var pending = c.Job.Pending;
        await Assert.ThrowsAsync<TimeoutException>(() => Reconcile(c));
        Assert.Equal(pending, c.Job.Pending);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData("research-submit")]
    [InlineData("research-buy")]
    [InlineData("research-deposit")]
    public async Task Actual_submissions_and_transactions_keep_their_original_connection_receipt_requirement(string kind)
    {
        var (s, c) = await Fixture(1, kind, true);
        var pending = c.Job.Pending;
        await Assert.ThrowsAsync<QuestBlockedException>(() => Reconcile(c));
        Assert.Equal(pending, c.Job.Pending);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData("unknown-source")]
    [InlineData("unrelated-popup")]
    [InlineData("result-next")]
    [InlineData("result-cleanup")]
    [InlineData("result-status")]
    public async Task Conflicting_or_unrecognized_state_is_not_reset_as_a_benign_menu(string variant)
    {
        var (s, c) = await Fixture(1, "research-menu", true);
        var p = c.Job.Research!;
        p = variant switch
        {
            "unknown-source" => p with { Source = p.Source! with { Choices = [new("Unknown permanent choice", 315, 1)] } },
            "result-next" => p with { SubmittedNext = p.Source },
            "result-cleanup" => p with { Cleanup = p.Source },
            "result-status" => p with { ResultStatus = new(p.Source!.Generation, p.Source.Revision + 1, 3,
                WizardyResearchEvidence.SorceryResult, DateTimeOffset.UtcNow) },
            _ => p
        };
        await c.SaveAsync(c.Job with { Research = p });
        if (variant == "unrelated-popup") s.Set(s.Snapshot with { Dialog = Page(s, "Unknown permanent choice", "Accept") });
        var pending = c.Job.Pending;
        await Assert.ThrowsAsync<QuestBlockedException>(() => Reconcile(c));
        Assert.Equal(pending, c.Job.Pending);
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task Server_closing_the_setup_reply_does_not_pause_the_research_runner()
    {
        var s = WizardyResearchTests.Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, WizardyResearchEvidence.Opening, WizardyResearchEvidence.Begin) });
        s.OnCommand = command =>
        {
            if (command.Kind == "choose") s.Set(s.Snapshot with { Dialog = null });
            if (command.Kind == "navigate") throw new OperationCanceledException("Stop after automatic setup recovery.");
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        var recipe = new QuestDefinition(WizardyResearchEvidence.Id, 1, "Research", "Synthetic", ["navigate", "dialog", "inventory"],
            [new("preflight", "research-run", null)], 12);
        var runner = new QuestRunner(s, journal, recipe, new());
        await runner.RunAsync();
        Assert.Null(runner.Job.Pending);
        Assert.Equal(0, runner.Job.Research!.Stage);
        Assert.Empty(runner.Job.Research.Attempts);
        Assert.Empty(runner.Job.Rewards);
        Assert.Equal(new[] { "choose", "navigate" }, s.Commands.Select(c => c.Kind));
        Assert.Null(runner.Job.NextRun);
    }

    sealed class ClosingReply(SimulatedSession inner, bool historicalReply) : IGameSession
    {
        public bool Armed { get; set; }
        public CharacterSnapshot Snapshot => inner.Snapshot;
        public IReadOnlySet<string> Capabilities => inner.Capabilities;
        public event Action<CharacterSnapshot>? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
        public event Action<string>? Interrupted { add => inner.Interrupted += value; remove => inner.Interrupted -= value; }
        public Task ExecuteAsync(GameCommand command, CancellationToken ct) => inner.ExecuteAsync(command, ct);
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken ct)
        {
            if (Armed)
            {
                Armed = false;
                inner.Set(Snapshot with { Dialog = Page(inner, "Synthetic Specific reply", "General", "Specific", "Help") });
                var reply = Snapshot;
                inner.Set(Snapshot with { Dialog = null });
                if (historicalReply && predicate(reply)) return Task.FromResult(reply);
            }
            return inner.WaitAsync(predicate, after, timeout, ct);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_setup_reply_that_closes_during_the_wait_restarts_without_manual_resume(bool historicalReply)
    {
        var s = WizardyResearchTests.Ready();
        var session = new ClosingReply(s, historicalReply);
        s.Set(s.Snapshot with { Dialog = Page(s, WizardyResearchEvidence.Opening, WizardyResearchEvidence.Begin) });
        s.OnCommand = command =>
        {
            if (command.Kind == "choose") session.Armed = true;
            if (command.Kind == "navigate") throw new OperationCanceledException("Stop after automatic setup recovery.");
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        var recipe = new QuestDefinition(WizardyResearchEvidence.Id, 1, "Research", "Synthetic", ["navigate", "dialog", "inventory"],
            [new("preflight", "research-run", null)], 12);
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync();
        Assert.Null(runner.Job.Pending);
        Assert.Equal(new[] { "choose", "navigate" }, s.Commands.Select(c => c.Kind));
        Assert.Empty(runner.Job.Research!.Attempts);
    }
}
