using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Synthetic delivery timing using verbatim retained NPC pages. No game session.
public class ObservedQuestFlowTests
{
    const string Donation = "However, the temple is in need a small charitable donation. Would you mind sparing 10,000 coins, please?";
    const string RoseThanks = "Thank you, kindly. Here you go.";
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    static SimulatedSession Ready(int map)
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Map = map,
            Position = new(4, 4),
            Level = 99,
            Gold = 20000,
            Experience = 1000,
            MaxHp = 100,
            LegendsKnown = true,
            LegendsRevision = 1
        });
        return s;
    }
    static Dialog Page(SimulatedSession s, string npc, string text, bool next, params string[] choices) =>
        new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, npc, text, true, 0, 1, choices.Length == 0 ? (byte)0 : (byte)2,
            choices.Select((x, i) => new DialogChoice(x, 0, (byte)(i + 1))).ToImmutableArray(), HasNext: next)
        {
            ObservedAt = DateTimeOffset.UtcNow
        };
    static QuestDefinition Rose() => FiskDanaanAutomationTests.Recipe(FiskEvidence.Id) with
    {
        Journey = [new("pink-rose", "dialog", 3017, "Vivianne", new(4,4))
        {
            Cost = 10000, Outputs = [new("Pink Rose",1)],
            Pages = [new("Vivianne",Donation,"choose","Certainly."),new("Vivianne",RoseThanks,"next")]
        }]
    };

    [Fact]
    public async Task Donation_reply_does_not_substitute_for_delayed_final_next_item()
    {
        var s = Ready(3017);
        s.Set(s.Snapshot with
        {
            Dialog = Page(s, "Vivianne", Donation, false, "Certainly.")
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose")
                s.Set(s.Snapshot with
                {
                    Gold = 10000,
                    Dialog = Page(s, "Vivianne", RoseThanks, true)
                });
            if (c.Kind == "next")
                s.Set(s.Snapshot with
                {
                    Dialog = null,
                    Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Pink Rose", 1))
                });
        };
        var r = new QuestRunner(s, Journal(), Rose(), new());
        await r.RunAsync();
        Assert.Equal(1, s.Snapshot.Count("Pink Rose"));
        Assert.Equal(10000u, s.Snapshot.Gold);
        Assert.Single(s.Commands, c => c.Kind == "choose");
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.Null(r.Job.Pending);
        Assert.Equal(1, r.Job.Route?.Task);
        Assert.Null(r.Job.CompletedAt); // This is a supplier stage, not whole-quest completion.
    }

    [Fact]
    public async Task Uncertain_committed_donation_is_not_submitted_again_on_resume()
    {
        var s = Ready(3017);
        s.Set(s.Snapshot with
        {
            Dialog = Page(s, "Vivianne", Donation, false, "Certainly.")
        });
        s.OnCommand = c => { if (c.Kind == "choose") { s.Set(s.Snapshot with { Gold = 10000 }); throw new TimeoutException("Synthetic missing reply"); } };
        var r = new QuestRunner(s, Journal(), Rose(), new());
        await r.RunAsync();
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "choose");
        Assert.NotNull(r.Job.Pending);
        Assert.Equal(10000u, s.Snapshot.Gold);
        Assert.Null(r.Job.CompletedAt);
    }

    [Fact]
    public async Task Unexpected_popup_is_preserved_without_a_quest_selection()
    {
        var s = Ready(3017);
        var popup = Page(s, "Other NPC", "Synthetic unrelated quest popup", false, "Yes");
        s.Set(s.Snapshot with
        {
            Dialog = popup
        });
        var r = new QuestRunner(s, Journal(), Rose(), new());
        await r.RunAsync();
        Assert.DoesNotContain(s.Commands, c => c.Kind is "choose" or "close" or "next");
        Assert.Equal(popup, s.Snapshot.Dialog);
        Assert.Null(r.Job.CompletedAt);
    }
}
