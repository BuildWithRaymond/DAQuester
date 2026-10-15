using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Only the opening labels are live evidence. Later replies, generations and
// revisions are synthetic execution of the retained Ase stage.
public class DanaanAseChoiceTests
{
    const string Original = "Have you seen anything unusual lately?";
    const string Shorter = "Seen anything unusual lately?";

    static ObservedQuestTask AseTask()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string file = Path.Combine(directory.FullName, "content/recipes/cap-band-of-danaan.v1.json");
            if (File.Exists(file))
                return JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(file))!
                    .JourneyVariants["loures-orders"].Journey.Single(task => task.Id == "ase-history");
        }
        throw new InvalidOperationException("Danaan recipe not found.");
    }

    static (SimulatedSession Session, QuestDefinition Recipe) Setup(string label, string text = "Hello.",
        string speaker = "Ase", long generation = 1, bool bothLabels = false)
    {
        var task = AseTask();
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Map = 195,
            Position = new(7, 2),
            Level = 98,
            Gold = 967001,
            Experience = 193264281,
            LegendsKnown = true,
            LegendsRevision = 1
        });
        var choices = new List<DialogChoice>
        {
            new("Nice weather is it not?", 1100, 1),
            new("Many warriors fighting lately?", 1100, 2),
            new(label, 1100, 3)
        };
        if (bothLabels)
            choices.Add(new(Original, 1100, 4));
        var opening = new Dialog(generation, session.Snapshot.Revision + 1, 55, 1, speaker,
            text, true, 1100, 48, 2, [.. choices]) { HasNext = true };
        session.Set(session.Snapshot with { Dialog = opening });
        int index = 0;
        session.OnCommand = command =>
        {
            if (command.Kind == "interact")
            {
                session.Set(session.Snapshot with
                {
                    Dialog = opening with { Revision = session.Snapshot.Revision + 1 }
                });
                return;
            }
            if (command.Kind == "close")
            {
                session.Set(session.Snapshot with { Dialog = null });
                return;
            }
            if (command.Kind is "choose" or "next")
            {
                var page = task.Pages[++index];
                session.Set(session.Snapshot with
                {
                    Dialog = new(1, session.Snapshot.Revision + 1, 55, 1, page.Speaker,
                        page.Text, true, 1100, 48, page.Kind == "choose" ? (byte)2 : (byte)0,
                        page.Choice is null ? [] : [new(page.Choice, 1100, 1)])
                    { HasNext = page.Kind == "next" }
                });
            }
        };
        return (session, FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with { Journey = [task] });
    }

    static async Task<QuestRunner> RunAsync(SimulatedSession session, QuestDefinition recipe)
    {
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new()
        {
            Character = session.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true)
        });
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync();
        return runner;
    }

    [Theory]
    [InlineData(Original)]
    [InlineData(Shorter)]
    public async Task Captured_opening_label_is_sent_once_from_the_exact_current_page(string label)
    {
        var (session, recipe) = Setup(label);
        var opening = session.Snapshot.Dialog!;
        var runner = await RunAsync(session, recipe);

        var selected = Assert.Single(session.Commands, command => command.Kind == "choose" && command.Text == label);
        Assert.Equal(opening.Generation, selected.ExpectedGeneration);
        Assert.Equal(opening.Revision, selected.ExpectedDialogRevision);
        Assert.Equal(opening.Text, selected.ExpectedPageText);
        Assert.DoesNotContain(session.Commands, command => command.Kind == "interact");
        Assert.Equal(1, runner.Job.Route?.Task);
        Assert.Null(runner.Job.Pending);
        Assert.Null(session.Snapshot.Dialog);
        Assert.Null(runner.Job.CompletedAt);
    }

    [Theory]
    [InlineData(Original)]
    [InlineData(Shorter)]
    public async Task Submitted_opening_reconciles_a_later_reply_without_resending_its_label(string label)
    {
        var (session, recipe) = Setup(label);
        var advance = session.OnCommand!;
        session.OnCommand = command =>
        {
            advance(command);
            if (command.Kind == "choose" && command.Text == label)
                session.Set(session.Snapshot with
                {
                    Dialog = session.Snapshot.Dialog! with { Text = "Unverified later reply" }
                });
        };
        var runner = await RunAsync(session, recipe);
        Assert.NotNull(runner.Job.Pending);
        Assert.Equal(0, runner.Job.Route?.Page);
        Assert.Single(session.Commands, command => command.Kind == "choose" && command.Text == label);

        session.Set(session.Snapshot with
        {
            Dialog = session.Snapshot.Dialog! with
            {
                Revision = session.Snapshot.Revision + 1,
                Text = recipe.Journey[0].Pages[1].Text
            }
        });
        session.OnCommand = advance;
        await runner.RunAsync();

        Assert.Single(session.Commands, command => command.Kind == "choose" && command.Text == label);
        Assert.DoesNotContain(session.Commands, command => command.Kind == "interact");
        Assert.Equal(1, runner.Job.Route?.Task);
        Assert.Null(runner.Job.Pending);
    }

    [Theory]
    [InlineData("Seen something unusual lately?", "Hello.", "Ase", 1, false)]
    [InlineData(Shorter, "A different greeting.", "Ase", 1, false)]
    [InlineData(Shorter, "Hello.", "Asea", 1, false)]
    [InlineData(Shorter, "Hello.", "Ase", 2, false)]
    [InlineData(Shorter, "Hello.", "Ase", 1, true)]
    public async Task Unknown_or_ambiguous_opening_preserves_the_stage_without_a_selection(
        string label, string text, string speaker, long generation, bool bothLabels)
    {
        var (session, recipe) = Setup(label, text, speaker, generation, bothLabels);
        var runner = await RunAsync(session, recipe);

        Assert.DoesNotContain(session.Commands, command => command.Kind is "choose" or "next" or "close");
        Assert.Equal(0, runner.Job.Route?.Task);
        Assert.Null(runner.Job.Pending);
        Assert.NotNull(session.Snapshot.Dialog);
    }
}
