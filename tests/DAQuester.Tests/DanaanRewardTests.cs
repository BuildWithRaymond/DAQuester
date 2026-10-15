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
    static async Task<(SimulatedSession Session, QuestRunner Runner)> Create(bool closeTimeout = false, bool deliver = true, bool band = false, bool delayed = false, bool activeOnly = false)
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
        bool rewardPending = false;
        void DeliverReward()
        {
            var reward = new ServerNotice(s.Snapshot.Generation, s.Snapshot.Revision + 1, 3, "1000000 experience!", DateTimeOffset.UtcNow);
            s.Set(s.Snapshot with
            {
                Experience = 1001000,
                Notice = activeOnly ? null : reward,
                ActiveStatus = activeOnly ? reward : null,
                Dialog = Page(s, Congratulations, true)
            });
        }
        s.OnCommand = c =>
        {
            if (c.Kind == "choose")
            {
                if (delayed)
                    rewardPending = true;
                else
                    DeliverReward();
            }
            if (c.Kind == "next")
                s.Set(s.Snapshot with
                {
                    Dialog = Page(s, Terminal, false),
                    Inventory = deliver ? s.Snapshot.Inventory.SetItem(3, new(3, band ? "Band of Danaan" : "Cap of Danaan", 1)) : s.Snapshot.Inventory,
                    Notice = delayed || activeOnly ? null : s.Snapshot.Notice,
                    ActiveStatus = delayed || activeOnly ? new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 3, "Synthetic later active message", DateTimeOffset.UtcNow) : s.Snapshot.ActiveStatus
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
        IGameSession session = delayed ? new ReplySession(s, () =>
        {
            if (rewardPending)
            {
                rewardPending = false;
                DeliverReward();
            }
        }) : s;
        return (s, new(session, j, Recipe(), new()));
    }
    sealed class ReplySession(SimulatedSession inner, Action receive) : IGameSession
    {
        public CharacterSnapshot Snapshot => inner.Snapshot;
        public IReadOnlySet<string> Capabilities => inner.Capabilities;
        public event Action<CharacterSnapshot>? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
        public event Action<string>? Interrupted { add => inner.Interrupted += value; remove => inner.Interrupted -= value; }
        public Task ExecuteAsync(GameCommand command, CancellationToken cancellation) => inner.ExecuteAsync(command, cancellation);
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation)
        {
            receive();
            return inner.WaitAsync(predicate, after, timeout, cancellation);
        }
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Reward_received_during_reply_wait_or_only_in_active_status_is_saved_before_final_cleanup(bool delayed, bool activeOnly)
    {
        var (s, r) = await Create(delayed: delayed, activeOnly: activeOnly);
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.Contains("1000000 experience!", r.Job.Route!.RewardMessages);
        Assert.Equal(1000000, r.Job.Rewards["experience"]);
        Assert.Equal(Mark, r.Job.CompletionLegend);
        Assert.Null(s.Snapshot.Dialog);
        Assert.Single(s.Commands, c => c.Kind == "choose");
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.Single(s.Commands, c => c.Kind == "close");
    }
    [Theory]
    [InlineData("fresh", true)]
    [InlineData("preinspected", true)]
    [InlineData("generation", false)]
    [InlineData("revision", false)]
    [InlineData("type", false)]
    [InlineData("text", false)]
    [InlineData("map", false)]
    public async Task Paused_terminal_reward_uses_only_a_correlated_retained_active_message(string evidence, bool completes)
    {
        var s = new SimulatedSession();
        var baseline = new Transaction("observed-stage",1,1,20000,0,1000,DateTimeOffset.UtcNow)
        { InventoryBaselineKnown=true,InventoryCounts=new() };
        s.Set(s.Snapshot with { Map=evidence=="map"?346:345,Position=new(6,5),Level=99,Gold=20000,Experience=1001000,LegendsKnown=true,LegendsRevision=1 });
        s.Set(s.Snapshot with
        {
            Inventory=s.Snapshot.Inventory.SetItem(3,new(3,"Cap of Danaan",1)), Dialog=Page(s,Terminal,false),
            Legends=evidence=="preinspected"?[Mark]:[],
            ActiveStatus=new(evidence=="generation"?2:1,evidence=="revision"?1:2,evidence=="type"?(byte)0:(byte)3,evidence=="text"?"750000 experience!":"1000000 experience!",DateTimeOffset.UtcNow)
        });
        s.OnCommand=c=>
        {
            if(c.Kind=="inspect-self")
                s.Set(s.Snapshot with { Legends=[Mark],LegendsRevision=s.Snapshot.Revision+1 });
            if(c.Kind=="close")
                s.Set(s.Snapshot with { Dialog=null });
        };
        var journal=new FileQuestJournal(Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString()));
        await journal.SaveAsync(new() { Character=s.Snapshot.Name!,Quest=DanaanEvidence.Id,Step="run",DanaanOptions=new("cail",true),
            Route=new() { Task=0,Page=2,Baseline=baseline,BoundGeneration=1,BoundEntity=55,LastReply=s.Snapshot.Dialog } });
        var runner=new QuestRunner(s,journal,Recipe(),new());
        await runner.RunAsync();
        Assert.Equal(completes?"Completed":"Paused",runner.Job.Status);
        Assert.DoesNotContain(s.Commands,c=>c.Kind is "choose" or "next");
        Assert.Equal(completes?1:0,s.Commands.Count(c=>c.Kind=="close"));
        if(completes)
            Assert.Equal(1000000,runner.Job.Rewards["experience"]);
        else
            Assert.Null(runner.Job.CompletedAt);
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
