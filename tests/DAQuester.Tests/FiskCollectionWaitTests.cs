using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class FiskCollectionWaitTests
{
    [Fact]
    public async Task Post_hour_refusal_reschedules_a_check_without_repaying_or_collecting()
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Level = 99,
            Map = 413,
            Position = new(10, 8),
            Gold = 30000,
            Experience = 1000,
            LegendsKnown = true
        });
        Dialog Reply(string text, bool root = false) => new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Viveka", text, !root, 0, 1, 0,
            root ? ImmutableArray.Create(new DialogChoice("Flower Bouquet", 3008, 7)) : [])
        {
            ObservedAt = DateTimeOffset.UtcNow
        };
        s.Set(s.Snapshot with
        {
            Dialog = Reply("Hello.  What can I do for you?", true)
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose")
                s.Set(s.Snapshot with
                {
                    Dialog = Reply(FiskEvidence.BouquetNotReady)
                });
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
        };
        var at = DateTimeOffset.UtcNow.AddMinutes(-61);
        var acceptance = new Dialog(1, 10, 55, 1, "Viveka", FiskEvidence.BouquetAccepted, true, 0, 1, 0, []) { ObservedAt = at };
        var j = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        await j.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = FiskEvidence.Id,
            Step = "run",
            Accepted = true,
            FiskWait = new(at, acceptance, 50000, 30000, true)
        });
        var recipe = FiskDanaanAutomationTests.Recipe(FiskEvidence.Id) with
        {
            Journey = [new("bouquet-collection","dialog",413,"Viveka",new(10,8))
        {
            Outputs=[new(FiskEvidence.Bouquet,1)],Pages=[new("Viveka","Hello.  What can I do for you?","choose","Flower Bouquet") { Root=true },new("Viveka","Here is your flower bouquet","close")]
        }]
        };
        var r = new QuestRunner(s, j, recipe, new(), hasReadyQueuedWork: () => true);
        await r.RunAsync(deferTimedWaits: true);
        Assert.Equal("Deferred", r.Job.Status);
        Assert.Null(r.Job.Pending);
        Assert.True(r.Job.NextRun > DateTimeOffset.UtcNow);
        Assert.Equal(at, r.Job.FiskWait!.AcceptedAt);
        Assert.Equal(30000u, s.Snapshot.Gold);
        Assert.Equal(0, s.Snapshot.Count(FiskEvidence.Bouquet));
        Assert.Single(s.Commands, c => c.Kind == "choose" && c.Text == "Flower Bouquet");
        Assert.DoesNotContain(s.Commands, c => c.Kind == "buy");
        Assert.Null(r.Job.CompletedAt);
    }
}
