using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Synthetic delayed acknowledgements model the live Blaise pause. They never
// operate a real character or authorize an unknown dialogue branch.
public class ObservedDialogueTimingTests
{
    sealed class DelayedSession : SimulatedSession, IGameSession
    {
        public Action? Reply;
        async Task<CharacterSnapshot> IGameSession.WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken ct)
        {
            var reply = Reply;
            Reply = null;
            reply?.Invoke();
            return await base.WaitAsync(predicate, after, timeout, ct);
        }
    }
    const string Intro = "Ssshhh!! Quiet. You are here to help that boy in Abel, right?";
    static Dialog Page(SimulatedSession s, string text, bool root = false) => new(1, s.Snapshot.Revision + 1, 55, 1, "Blaise", text, !root, 1094, 48, 0,
        root ? [new("Gods and hats", 2118, 5)] : []);

    [Fact]
    public async Task Late_known_choice_reply_is_awaited_without_resending_the_choice()
    {
        var s = new DelayedSession();
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
        s.Set(s.Snapshot with
        {
            Dialog = Page(s, "Hello.  What can I do for you?", true)
        });
        s.OnCommand = command =>
        {
            if (command.Kind == "choose")
                s.Reply = () => s.Set(s.Snapshot with { Dialog = Page(s, Intro) });
            if (command.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
        };
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("blaise-records","dialog",3041,"Blaise",new(10,12))
            { Pages=[new("Blaise","Hello.  What can I do for you?","choose","Gods and hats") { Root=true },new("Blaise",Intro,"close")] }]
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        await journal.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true)
        });
        var runner = new QuestRunner(s, journal, recipe, new());
        await runner.RunAsync();
        Assert.True(runner.Job.Pending is null, runner.Job.Reason);
        Assert.Single(s.Commands, c => c.Kind == "choose");
        Assert.Single(s.Commands, c => c.Kind == "close");
        Assert.Equal(1, runner.Job.Route?.Task);
    }

    [Fact]
    public async Task Late_merchant_stock_is_awaited_before_one_quoted_purchase()
    {
        var s = new DelayedSession();
        s.Set(s.Snapshot with
        {
            Map = 413,
            Position = new(10, 8),
            Level = 99,
            Gold = 20000,
            Experience = 1000,
            LegendsKnown = true
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "interact")
                s.Set(s.Snapshot with
                {
                    Dialog = new(1, s.Snapshot.Revision + 1, 55, 1, "Viveka", "Hello", false, 0, 0, 0, [new("Buy", 64, 1)])
                });
            if (c.Kind == "choose")
                s.Reply = () => s.Set(s.Snapshot with { Dialog = new(1, s.Snapshot.Revision + 1, 55, 1, "Viveka", "Stock", false, 74, 0, 4, []) { Items = [new("Wine", 500)] } });
            if (c.Kind == "buy")
                s.Set(s.Snapshot with
                {
                    Gold = 19500,
                    Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Wine", 1))
                });
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
        };
        var recipe = FiskDanaanAutomationTests.Recipe(FiskEvidence.Id) with
        {
            Journey = [new("wine", "buy", 413, "Viveka", new(10, 8)) { Item = "Wine" }]
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        var runner = new QuestRunner(s, journal, recipe, new());
        await runner.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "buy");
        Assert.Equal(1, s.Snapshot.Count("Wine"));
        Assert.Null(runner.Job.Pending);
    }
}
