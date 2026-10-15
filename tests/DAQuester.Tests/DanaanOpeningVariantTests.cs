using System.Text.Json;
using System.Text.Json.Nodes;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanOpeningVariantTests
{
    [Theory]
    [InlineData("observed-page:blaise-records", true)]
    [InlineData("observed-buy:wine", false)]
    public async Task Own_completed_mark_reconciles_only_the_old_read_only_intro(string kind, bool complete)
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Generation = 2,
            Map = 345,
            Position = new(6, 5),
            Level = 99,
            Gold = 20000,
            Experience = 1000,
            LegendsKnown = true,
            LegendsRevision = 1,
            Legends = ["Worthy of the Cap of Danaan, Deoch 223, Summer"]
        });
        var page = new Dialog(1, 2, 55, 1, "Blaise", Intro, true, 111, 1, 2, [new("Yes!", 111, 1)]);
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true),
            Pending = new(kind, 1, 2, 20000, 0, 1000, DateTimeOffset.UtcNow)
            {
                EntityId = 55,
                EntityType = 1
            },
            Route = new()
            {
                Task = 0,
                Page = 1,
                SubmittedPage = page,
                BoundGeneration = 1,
                BoundEntity = 55
            }
        });
        var runner = new QuestRunner(s, journal, Recipe(), new());
        await runner.RunAsync();
        Assert.Empty(s.Commands);
        Assert.Equal(complete, runner.Job.Step == "complete");
        Assert.Equal(complete, runner.Job.Pending is null);
    }

    const string Root = "Hello.  What can I do for you?";
    const string Intro = "Ssshhh!! Quiet. You are here to help that boy in Abel, right?";
    const string Orders = "Don't be so loud. We received orders not to ever touch the subject, and the guards make sure the orders are followed.";
    const string Default = "Synthetic original researched-records reply";
    static QuestDefinition Recipe()
    {
        ObservedQuestPage[] prefix = [new("Blaise", Root, "choose", "Gods and hats") { Root = true }, new("Blaise", Intro, "choose", "Yes!")];
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [
            new("blaise-records","dialog",3041,"Blaise",new(10,12)) { Pages=[..prefix,new("Blaise",Default,"close")] },
            new("erika","dialog",3015,"Erika",new(4,4)) { Pages=[new("Erika","Synthetic terminal","close")] }]
        };
        // JSON avoids inventing a test-only controller and exercises packaged recipe loading.
        var json = JsonNode.Parse(JsonSerializer.Serialize(recipe))!;
        json["JourneyVariants"] = JsonSerializer.SerializeToNode(new Dictionary<string, object>
        {
            ["loures-orders"] = new
            {
                TaskId = "blaise-records",
                Page = 2,
                Speaker = "Blaise",
                Text = Orders,
                Choices = new[] { "Why is the subject forbidden?" },
                Journey = new ObservedQuestTask[] {
                    new("blaise-records","dialog",3041,"Blaise",new(10,12)) { Pages=[..prefix,
                        new("Blaise",Orders,"choose","Why is the subject forbidden?"),new("Blaise","Synthetic Camille referral","close")] },
                    new("camille","dialog",3013,"Camille",new(4,4)) { Pages=[new("Camille","Synthetic terminal","close")] } }
            }
        });
        return json.Deserialize<QuestDefinition>()!;
    }

    [Theory]
    [InlineData("orders", "Camille")]
    [InlineData("original", "Erika")]
    [InlineData("unknown", null)]
    [InlineData("changed-prefix", null)]
    public async Task Exact_new_reply_selects_only_its_observed_branch(string response, string? destination)
    {
        bool orders = response is "orders" or "changed-prefix";
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Map = 3041,
            Position = new(10, 12),
            Level = 99,
            Gold = 20000,
            Experience = 1000,
            LegendsKnown = true,
            LegendsRevision = 1
        });
        Dialog Page(string npc, string text, string? choice = null, bool root = false) => new(1, s.Snapshot.Revision + 1, 55, 1, npc, text, !root, 111, 1, choice is null ? (byte)0 : (byte)2,
            choice is null ? [] : [new(choice, 111, 1)]);
        s.Set(s.Snapshot with
        {
            Dialog = Page("Blaise", Root, "Gods and hats", true)
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate")
                s.Set(s.Snapshot with
                {
                    Map = c.Map,
                    Position = c.Target
                });
            if (c.Kind == "choose" && c.Text == "Gods and hats")
                s.Set(s.Snapshot with
                {
                    Dialog = Page("Blaise", Intro, "Yes!")
                });
            if (c.Kind == "choose" && c.Text == "Yes!")
                s.Set(s.Snapshot with
                {
                    Dialog = Page("Blaise", orders ? Orders : response == "original" ? Default : "Unobserved branch", orders ? "Why is the subject forbidden?" : null)
                });
            if (c.Kind == "choose" && c.Text == "Why is the subject forbidden?")
                s.Set(s.Snapshot with
                {
                    Dialog = Page("Blaise", "Synthetic Camille referral")
                });
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
            if (c.Kind == "interact")
                s.Set(s.Snapshot with
                {
                    Dialog = Page(c.Text!, "Synthetic terminal")
                });
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true)
        });
        var recipe = Recipe();
        if (response == "changed-prefix")
        {
            var variant = recipe.JourneyVariants["loures-orders"];
            var prefix = variant.Journey[0];
            recipe.JourneyVariants["loures-orders"] = variant with
            {
                Journey = [prefix with { Pages = [prefix.Pages[0], prefix.Pages[1] with { TextAlternatives = ["Unobserved submitted body"] }, ..prefix.Pages[2..]] }, ..variant.Journey[1..]]
            };
        }
        var runner = new QuestRunner(s, journal, recipe, new());
        await runner.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "choose" && c.Text == "Yes!");
        if (destination is null)
        {
            Assert.NotNull(runner.Job.Pending);
            Assert.DoesNotContain(s.Commands, c => c.Kind == "interact");
            Assert.DoesNotContain(s.Commands, c => c.Kind == "close");
        }
        else
        {
            Assert.Null(runner.Job.Pending);
            Assert.Single(s.Commands, c => c.Kind == "interact" && c.Text == destination);
            Assert.Equal(2, runner.Job.Route?.Task);
            Assert.Contains(response == "orders" ? "loures-orders" : "\"JourneyVariant\":null", JsonSerializer.Serialize(journal.Load(s.Snapshot.Name!, DanaanEvidence.Id)));
        }
    }
}
