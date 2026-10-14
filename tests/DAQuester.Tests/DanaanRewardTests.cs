using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanRewardTests
{
    const string Root = "Hello.  What can I do for you?";
    const string Congratulations = "So I see. Very well done, Aisling. The journey has been long, but it is finally over. You have proven yourself worthy of this boon, and increased once again our hope in those who carry Deoch's Spark. This Cap goes to a very worthy Aisling. Congratulations!";
    const string Terminal = "To have this consecrated to one of the eight deities, ask their Apostles for it. 'Please consecrate my Cap of Danaan' sounds polite enough for them to answer.";
    const string Mark = "Worthy of the Cap of Danaan, Deoch 223, Summer";
    static QuestDefinition Recipe() => FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
    {
        Journey = [new("erin-reward","dialog",345,"Erin",new(6,5)) { Experience=1000000,Outputs=[new("Cap of Danaan",1)],WeightReserve=4,
            Pages=[new("Erin",Root,"choose","I completed the tasks") { Root=true },new("Erin",Congratulations,"next"),new("Erin",Terminal,"close")] }]
    };
    static Dialog Page(SimulatedSession s, string text, bool next, bool root = false) => new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Erin", text, !root, 1118, 27, 0,
        root ? ImmutableArray.Create(new DialogChoice("I completed the tasks", 2142, 3)) : [], HasNext: next)
    {
        ObservedAt = DateTimeOffset.UtcNow
    };
    static async Task<(SimulatedSession Session, QuestRunner Runner)> Create(bool closeTimeout = false, bool deliver = true, bool band = false)
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Map = 345,
            Position = new(6, 5),
            Level = 99,
            Gold = 20000,
            Experience = 1000,
            LegendsKnown = true,
            LegendsRevision = 1
        });
        s.Set(s.Snapshot with
        {
            Dialog = Page(s, Root, false, true)
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose")
                s.Set(s.Snapshot with
                {
                    Experience = 1001000,
                    Notice = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 3, "1000000 experience!", DateTimeOffset.UtcNow),
                    Dialog = Page(s, Congratulations, true)
                });
            if (c.Kind == "next")
                s.Set(s.Snapshot with
                {
                    Dialog = Page(s, Terminal, false),
                    Inventory = deliver ? s.Snapshot.Inventory.SetItem(3, new(3, band ? "Band of Danaan" : "Cap of Danaan", 1)) : s.Snapshot.Inventory
                });
            if (c.Kind == "inspect-self")
                s.Set(s.Snapshot with
                {
                    Legends = ImmutableArray.Create(band ? Mark.Replace("Cap", "Band") : Mark),
                    LegendsRevision = s.Snapshot.Revision + 1
                });
            if (c.Kind == "close")
            {
                if (closeTimeout)
                    throw new TimeoutException("Synthetic lost close receipt");
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
            }
        };
        var j = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        await j.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true)
        });
        return (s, new(s, j, Recipe(), new()));
    }
    [Fact]
    public async Task Final_next_item_own_legend_and_explicit_close_finish_the_run()
    {
        var (s, r) = await Create();
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.Null(s.Snapshot.Dialog);
        Assert.Equal(1, s.Snapshot.Count("Cap of Danaan"));
        Assert.Equal(Mark, r.Job.CompletionLegend);
        Assert.Equal(1000000, r.Job.Rewards["experience"]);
        Assert.True(s.Commands.FindIndex(c => c.Kind == "next") < s.Commands.FindIndex(c => c.Kind == "inspect-self"));
        Assert.True(s.Commands.FindIndex(c => c.Kind == "inspect-self") < s.Commands.FindIndex(c => c.Kind == "close"));
        Assert.DoesNotContain(s.Commands, c => c.Text?.Contains("consecrate", StringComparison.OrdinalIgnoreCase) == true);
    }
    [Fact]
    public async Task Xp_and_congratulations_alone_do_not_complete_or_close()
    {
        var (s, r) = await Create(deliver: false);
        await r.RunAsync();
        Assert.Null(r.Job.CompletedAt);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "close");
        Assert.Single(s.Commands, c => c.Kind == "choose");
    }
    [Fact]
    public async Task Same_offered_route_requires_actual_band_receipt_and_corresponding_own_mark()
    {
        // Synthetic Band outcome; this does not certify unobserved live wording.
        var (s, r) = await Create(band: true);
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.Equal(1, r.Job.Rewards["item:Band of Danaan"]);
        Assert.Equal(Mark.Replace("Cap", "Band"), r.Job.CompletionLegend);
        Assert.Equal(0, s.Snapshot.Count("Cap of Danaan"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_close_or_reconnect_cleans_up_without_reclaiming(bool reconnect)
    {
        var (s, r) = await Create(closeTimeout: true);
        await r.RunAsync();
        Assert.True(r.Job.Route?.CleanupPending);
        Assert.NotNull(r.Job.CompletedAt);
        s.Set(s.Snapshot with
        {
            Dialog = null,
            Generation = reconnect ? 2 : 1,
            LegendsRevision = s.Snapshot.Revision + 1
        });
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.Single(s.Commands, c => c.Kind == "choose");
        Assert.Single(s.Commands, c => c.Kind == "close");
    }
}
