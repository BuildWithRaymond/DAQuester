using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Dialogue comes from the captured recipe. These simulated resources exercise
// automation contracts and are not new server evidence or a live completion.
public class DanaanCourtFlowTests
{
    [Theory]
    [InlineData(true, true, 4)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 2)]
    public async Task Court_prefix_requires_separate_purchase_and_handoff_receipts(bool purchaseReceipt, bool handoffReceipt, int through)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var full = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root, "content/recipes/cap-band-of-danaan.v1.json")))!;
        // Travel consent and doorway receipts have their own regression suite.
        var tasks = full.JourneyVariants["loures-court"].Journey.Skip(5).Take(4)
            .Select(t => t with { ApproachStrategy = null }).ToArray();
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with { Journey = tasks };
        var s = new SimulatedSession();
        s.Set(s.Snapshot with { Level = 98, Gold = 1000, Experience = 100, LegendsKnown = true, LegendsRevision = 1 });
        int taskIndex = 0, pageIndex = 0;
        Dialog Page()
        {
            var page = tasks[taskIndex].Pages[pageIndex];
            return new(s.Snapshot.Generation, s.Snapshot.Revision + 1, (uint)(100 + taskIndex), 1, page.Speaker, page.Text,
                !page.Root, 1100, (ushort)pageIndex, page.Kind == "choose" ? (byte)2 : (byte)0,
                page.Choice is null ? [] : [new(page.Choice, 1100, 1)], HasNext: page.Kind == "next");
        }
        s.OnCommand = command =>
        {
            if (command.Kind == "navigate")
            {
                s.Set(s.Snapshot with { Map = command.Map, Position = command.Target });
                return;
            }
            if (command.Kind == "interact")
            {
                s.Set(s.Snapshot with { Map = command.Map, Position = command.Target, Dialog = Page() });
                return;
            }
            if (command.Kind is not ("choose" or "next" or "close"))
                return;
            var task = tasks[taskIndex];
            if (command.Text == "Oren Brandy - 75 gold" && purchaseReceipt)
                s.Set(s.Snapshot with { Gold = s.Snapshot.Gold - 75, Inventory = s.Snapshot.Inventory.SetItem(6, new(6, "Oren Brandy", 1)) });
            if (command.Text == "Here, have a drink." && handoffReceipt)
                s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(6) });
            pageIndex++;
            if (pageIndex == task.Pages.Length)
            {
                taskIndex++;
                pageIndex = 0;
                s.Set(s.Snapshot with { Dialog = null });
            }
            else
                s.Set(s.Snapshot with { Dialog = Page() });
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true) });
        var runner = new QuestRunner(s, journal, recipe, new());
        await runner.RunAsync();
        Assert.True(runner.Job.Route?.Task == through, runner.Job.Reason + " (task " + runner.Job.Route?.Task + ", page " + runner.Job.Route?.Page + ")");
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Null(runner.Job.CompletedAt);
        Assert.Single(s.Commands, c => c.Text == "Oren Brandy - 75 gold");
        Assert.Equal(purchaseReceipt ? 1 : 0, s.Commands.Count(c => c.Text == "Here, have a drink."));
        Assert.Equal(purchaseReceipt && handoffReceipt ? 1 : 0, s.Commands.Count(c => c.Text == "Luathas, the Cap"));
    }
}
