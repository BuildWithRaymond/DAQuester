using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class EventNoticeTests
{
    internal const string RedMoon = "The full Red Moon shines brightly in the sky. You stare at it, hypnotized by its light. Your body starts to tremble. You breath heavily as you continue to stare at the moon. Your body begins to change...";
    internal static Dialog Popup() => new(1, 1, 1245044, 4, "", RedMoon, true, 0, 1, 0, []) { HasNext = true };

    [Fact]
    public async Task Uncertain_notice_close_preserves_a_previously_submitted_transaction()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Dialog = Popup() });
        session.OnCommand = _ => throw new TimeoutException("Synthetic unconfirmed event close");
        var pending = new Transaction("synthetic-handoff", 1, 1, 999, 0, 0, DateTimeOffset.UtcNow)
        {
            InventoryBaselineKnown = true,
            InventoryCounts = new() { ["Parchment Scroll"] = 1 },
            Legends = ["Synthetic own mark"],
            SpellsBaselineKnown = true,
            Spells = ["Synthetic known spell"]
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new() { Character = "Nuclear", Quest = "notice-fixture", Pending = pending });
        var recipe = new QuestDefinition("notice-fixture", 1, "Notice fixture", "Synthetic", [], [new("preflight", "probe", null)]);
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync();
        Assert.Equal("close", Assert.Single(session.Commands).Kind);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(pending), System.Text.Json.JsonSerializer.Serialize(runner.Job.Pending));
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Null(runner.Job.CompletedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Captured_notice_is_closed_before_a_quest_action(bool uncertain)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Dialog = Popup() });
        session.OnCommand = command =>
        {
            Assert.Equal("close", command.Kind);
            Assert.Equal(Popup(), command.Dialog);
            Assert.Equal(1, command.ExpectedGeneration);
            Assert.Equal(1, command.ExpectedDialogRevision);
            Assert.Equal(RedMoon, command.ExpectedPageText);
            if (uncertain) throw new TimeoutException("Synthetic unconfirmed event close");
            session.Set(session.Snapshot with { Dialog = null });
        };
        var runner = Runner(session);
        await runner.RunAsync();
        Assert.Single(session.Commands);
        Assert.Equal(uncertain ? "Paused" : "Completed", runner.Job.Status);
        if (uncertain) Assert.NotNull(session.Snapshot.Dialog);
    }

    [Theory]
    [InlineData("unknown-text")]
    [InlineData("choice")]
    [InlineData("prompt")]
    [InlineData("npc")]
    [InlineData("stale")]
    [InlineData("pursuit")]
    public async Task Unrecognized_or_unbound_popups_are_preserved(string variant)
    {
        var d = variant switch
        {
            "unknown-text" => Popup() with { Text = RedMoon + " Continue the event?" },
            "choice" => Popup() with { Choices = [new("Yes", 0, 1)] },
            "prompt" => Popup() with { Prompt = "Enter name" },
            "npc" => Popup() with { EntityType = 1 },
            "stale" => Popup() with { Generation = 2 },
            _ => Popup() with { Pursuit = 2 }
        };
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Dialog = d });
        var runner = Runner(session);
        await runner.RunAsync();
        Assert.Empty(session.Commands);
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Same(d, session.Snapshot.Dialog);
    }

    static QuestRunner Runner(SimulatedSession session)
    {
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        var recipe = new QuestDefinition("notice-fixture", 1, "Notice fixture", "Synthetic", [], [new("preflight", "probe", null)]);
        var runner = new QuestRunner(session, journal, recipe, new());
        runner.Actions["probe"] = new Probe();
        return runner;
    }

    sealed class Probe : IQuestAction
    {
        public Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
        {
            if (c.Session.Snapshot.Dialog is not null) throw new QuestBlockedException("Unrelated popup preserved.");
            return c.SaveAsync(c.Job with { Step = "complete", CompletedAt = DateTimeOffset.UtcNow });
        }
    }
}
