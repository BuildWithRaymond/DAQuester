using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class ObservedQuestPurchaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Purchase_only_missing_wine_and_never_repeat_partial_receipt(bool partial)
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Level = 99,
            Map = 413,
            Position = new(10, 8),
            Gold = 20000,
            Experience = 1000,
            LegendsKnown = true,
            Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Wine", 3))
        });
        Dialog Stock() => new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Viveka", "Observed stock", false, 64, 0, 4, [])
        {
            Items = ImmutableArray.Create(new ShopItem("Wine", 500))
        };
        s.OnCommand = c =>
        {
            if (c.Kind == "interact")
                s.Set(s.Snapshot with
                {
                    Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Viveka", "Hello.  What can I do for you?", false, 0, 0, 0, ImmutableArray.Create(new DialogChoice("Buy", 64, 1)))
                });
            if (c.Kind == "choose")
                s.Set(s.Snapshot with
                {
                    Dialog = Stock()
                });
            if (c.Kind == "buy")
                s.Set(s.Snapshot with
                {
                    Gold = 19000,
                    Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Wine", partial ? 4u : 5u))
                });
        };
        var recipe = FiskDanaanAutomationTests.Recipe(FiskEvidence.Id) with
        {
            Journey = [new("wine", "buy", 413, "Viveka", new(10, 8)) { Item = "Wine", Quantity = 5, Cost = 500 }]
        };
        var r = new QuestRunner(s, new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString())), recipe, new());
        await r.RunAsync();
        await r.RunAsync();
        var purchase = Assert.Single(s.Commands, c => c.Kind == "buy");
        Assert.Equal(2, purchase.Quantity);
        Assert.Equal(19000u, s.Snapshot.Gold);
        if (partial)
            Assert.NotNull(r.Job.Pending);
        else
            Assert.Null(r.Job.Pending);
        Assert.Null(r.Job.CompletedAt);
    }
}
