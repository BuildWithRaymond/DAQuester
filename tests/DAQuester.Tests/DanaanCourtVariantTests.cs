using System.Text.Json;
using System.Text.Json.Nodes;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanCourtVariantTests
{
    const string Shorter = "Seen anything unusual lately?";
    const string Reply = "If you call a handful of soldiers from Loures taking a stroll along the beach unusual, then I have.";
    const string Question = "Any idea what they came to do here?";

    static QuestDefinition Recipe(bool duplicate = false, bool corruptPrefix = false)
    {
        var opening = new ObservedQuestPage("Ase", "Hello.", "choose", "Have you seen anything unusual lately?")
        {
            ChoiceAlternatives = [Shorter]
        };
        var original = new ObservedQuestTask("ase-history", "dialog", 195, "Ase", new(7, 2))
        {
            Pages = [opening, new("Ase", "Synthetic original Ase reply", "close")]
        };
        var court = original with
        {
            Pages = [corruptPrefix ? opening with { Text = "Changed opening" } : opening,
                new("Ase", Reply, "choose", Question), new("Ase", "Synthetic court referral", "close")]
        };
        var root = JsonNode.Parse(JsonSerializer.Serialize(FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [original]
        }))!;
        var child = new
        {
            ParentVariant = "loures-orders",
            TaskId = "ase-history",
            Page = 1,
            Speaker = "Ase",
            Text = Reply,
            Choices = new[] { Question },
            Journey = new[] { court }
        };
        var variants = new Dictionary<string, object>
        {
            ["loures-orders"] = new
            {
                TaskId = "unused-intro", Page = 99, Speaker = "Blaise", Text = "Synthetic selector", Journey = new[] { original }
            },
            ["loures-court"] = child
        };
        if (duplicate)
            variants["duplicate-court"] = child;
        root["JourneyVariants"] = JsonSerializer.SerializeToNode(variants);
        // A separate JSON object creates separate alias arrays in both prefixes.
        return root.Deserialize<QuestDefinition>()!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_child_reply_preserves_the_submitted_opening_and_persists_the_new_path(bool alreadySubmitted)
    {
        var result = await RunAsync("loures-orders", Reply, alreadySubmitted: alreadySubmitted);
        Assert.Equal("loures-court", result.Runner.Job.JourneyVariant);
        Assert.Equal(1, result.Runner.Job.Route?.Task);
        Assert.Null(result.Runner.Job.Pending);
        Assert.Equal(alreadySubmitted ? 0 : 1, result.Session.Commands.Count(command => command.Text == Shorter));
        Assert.Single(result.Session.Commands, command => command.Text == Question);
        Assert.Equal("loures-court", result.Journal.Load(result.Session.Snapshot.Name!, DanaanEvidence.Id).JourneyVariant);
    }

    [Theory]
    [InlineData(null, Reply, false, false, false)]
    [InlineData("loures-orders", "Unobserved Ase reply", false, false, false)]
    [InlineData("loures-orders", Reply, true, false, false)]
    [InlineData("loures-orders", Reply, false, true, false)]
    [InlineData("loures-orders", Reply, false, false, true)]
    public async Task Unknown_ambiguous_or_incompatible_child_retains_the_pending_opening(
        string? parent, string reply, bool duplicate, bool corruptPrefix, bool extraChoice)
    {
        var result = await RunAsync(parent, reply, duplicate, corruptPrefix, extraChoice);
        Assert.Equal(parent, result.Runner.Job.JourneyVariant);
        Assert.NotNull(result.Runner.Job.Pending);
        Assert.Equal(0, result.Runner.Job.Route?.Page);
        Assert.DoesNotContain(result.Session.Commands, command => command.Text == Question || command.Kind == "close");
    }

    static async Task<(QuestRunner Runner, SimulatedSession Session, FileQuestJournal Journal)> RunAsync(
        string? parent, string reply, bool duplicate = false, bool corruptPrefix = false, bool extraChoice = false,
        bool alreadySubmitted = false)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Map = 195, Position = new(7, 2), Level = 98, Gold = 20000, Experience = 1000,
            LegendsKnown = true, LegendsRevision = 1
        });
        Dialog Page(string text, string? choice = null) => new(1, session.Snapshot.Revision + 1, 55, 1, "Ase", text,
            true, 1100, 48, choice is null ? (byte)0 : (byte)2,
            choice is null ? [] : extraChoice && choice == Question ? [new(choice, 1100, 1), new("Unobserved option", 1100, 2)] : [new(choice, 1100, 1)]);
        var opening = Page("Hello.", Shorter);
        session.Set(session.Snapshot with { Dialog = opening });
        if (alreadySubmitted)
            session.Set(session.Snapshot with { Dialog = Page(reply, Question) });
        session.OnCommand = command =>
        {
            if (command.Text == Shorter)
                session.Set(session.Snapshot with { Dialog = Page(reply, Question) });
            if (command.Text == Question)
                session.Set(session.Snapshot with { Dialog = Page("Synthetic court referral") });
            if (command.Kind == "close")
                session.Set(session.Snapshot with { Dialog = null });
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        var baseline = new Transaction("observed-stage", 1, opening.Revision, 20000, 0, 1000, DateTimeOffset.UtcNow)
        {
            InventoryBaselineKnown = true
        };
        await journal.SaveAsync(new()
        {
            Character = session.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true), JourneyVariant = parent,
            Step = alreadySubmitted ? "run" : "preflight",
            Pending = alreadySubmitted ? baseline with { Kind = "observed-page:ase-history", EntityId = 55, EntityType = 1 } : null,
            Route = alreadySubmitted ? new() { Task = 0, Page = 0, Baseline = baseline, BoundGeneration = 1, BoundEntity = 55, SubmittedPage = opening } : null
        });
        var runner = new QuestRunner(session, journal, Recipe(duplicate, corruptPrefix), new());
        await runner.RunAsync();
        return (runner, session, journal);
    }
}
