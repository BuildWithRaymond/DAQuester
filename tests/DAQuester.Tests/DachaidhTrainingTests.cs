using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using Arbiter.Net.Client.Messages;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class DachaidhTrainingTests
{
    const string Root = "Hello.  What can I do for you?";
    const string Secrets = "You have unravelled your mind.  It is waiting now; is it not?  There are some secrets which you may learn. ";
    const string Consent = "dachaidh Return to the starting position, you swear you will this secret for just good causes?";
    const string WizardPrice = "To learn dachaidh, Goblin's Skull (1) and 5000 coins have to be presented to me.  You want to do that?";
    const string PriestPrice = "To learn dachaidh, 5000 coins have to be presented to me.  You want to do that?";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Spell_list_and_confirmation_send_the_actual_bound_spell_argument(bool confirmation)
    {
        var tracker = new StateTracker(1);
        tracker.Identity("Synthetic",42);
        var menu = new ServerScreenMenuMessage { EntityType=EntityTypeFlags.Creature,EntityId=55,Name="Devlin",
            MenuType=confirmation?DialogMenuType.MenuWithArgs:DialogMenuType.SpellChoices,
            Content=confirmation?WizardPrice:Secrets,Prompt=confirmation?"dachaidh":null,PursuitId=17,
            SpellChoices=confirmation?[]:[new() { Name="dachaidh" },new() { Name="beag ioc" }],
            MenuChoices=confirmation?[new() { Text="Yes",PursuitId=38 },new() { Text="No",PursuitId=39 }]:[] };
        var builder=new NetworkPacketBuilder(ServerCommand.ScreenMenu);
        try { menu.Serialize(ref builder);tracker.Observe((byte)ServerCommand.ScreenMenu,builder.ToPacket().Data); }
        finally { builder.Dispose(); }
        var dialog=tracker.Snapshot.Dialog!;
        Assert.Contains(dialog.Choices,c=>c.Text==(confirmation?"Yes":"dachaidh"));
        var selected=new ClientMerchantMessage();
        selected.Deserialize(new NetworkPacketReader(DialogPackets.Choice(tracker.Snapshot,dialog,confirmation?"Yes":"dachaidh")));
        Assert.Equal(new[] { "dachaidh" },selected.Arguments);
        Assert.Equal(confirmation?38:17,selected.PursuitId);
    }

    sealed class CompletedAction : IQuestAction
    {
        public Task ExecuteAsync(QuestContext c,QuestStep step,CancellationToken ct) =>
            c.SaveAsync(c.Job with { Step="complete",CompletedAt=DateTimeOffset.UtcNow });
    }

    static async Task<(SimulatedSession Session,QuestRunner Runner)> Create(string shortage="none",bool priest=false,bool receipt=true, QuestJob? saved = null)
    {
        var s=new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Class=shortage=="class"?"Warrior":priest?"Priest":"Wizard",Level=99,Experience=1000,
            Stats=shortage=="stats"?new(3,3,3,3,3,200):priest?new(3,10,9,3,6,0):new(5,36,5,5,5,0),
            Gold=shortage=="gold"?4999u:10000u,SpellsKnown=shortage!="unknown spells",LegendsKnown=true,
            Spells=shortage=="known"?s.Snapshot.Spells.SetItem(2,"dachaidh (Lev:0/100)"):s.Snapshot.Spells,
            Inventory=shortage=="skull"||priest?s.Snapshot.Inventory:s.Snapshot.Inventory.SetItem(9,new(9,"Goblin's Skull",1))
        });
        Dialog Page(string text,byte type,ushort pursuit,params DialogChoice[] choices) =>
            new(s.Snapshot.Generation,s.Snapshot.Revision+1,55,1,"Devlin",text,false,pursuit,0,type,[..choices],type==1?"dachaidh":null);
        s.OnCommand=c=>
        {
            if(c.Kind=="interact")
                s.Set(s.Snapshot with { Map=302,Position=new(8,2),Dialog=Page(Root,0,0,new("Learn Secret",5,1),new("Forget Secret",7,2)) });
            if(c.Kind=="choose")
            {
                Assert.Equal(s.Snapshot.Generation,c.ExpectedGeneration);
                Assert.Equal(s.Snapshot.Dialog!.Revision,c.ExpectedDialogRevision);
                Assert.Equal(s.Snapshot.Dialog.Text,c.ExpectedPageText);
                if(c.Text=="Learn Secret")
                    s.Set(s.Snapshot with { Dialog=Page(Secrets,6,17,new DialogChoice("dachaidh",17,0)) });
                else if(c.Text=="dachaidh")
                    s.Set(s.Snapshot with { Dialog=Page(Consent,1,0,new("Yes",36,1),new("No",37,2)) });
                else if(c.Text=="Yes"&&s.Snapshot.Dialog.Text==Consent)
                    s.Set(s.Snapshot with { Dialog=Page(priest?PriestPrice:WizardPrice,1,0,new("Yes",38,1),new("No",39,2)) });
                else if(c.Text=="Yes"&&receipt)
                    s.Set(s.Snapshot with { Gold=s.Snapshot.Gold-5000,Inventory=priest?s.Snapshot.Inventory:s.Snapshot.Inventory.Remove(9),Spells=s.Snapshot.Spells.SetItem(2,"dachaidh (Lev:0/100)"),Dialog=null });
            }
        };
        var recipe=new QuestDefinition("synthetic-travel",1,"Synthetic travel","synthetic",[],[new("preflight","finish","complete")]) { Repeatable=false };
        var journal=new FileQuestJournal(Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString()));
        await journal.SaveAsync((saved ?? new()) with { Character=s.Snapshot.Name!,Quest=recipe.Id });
        var runner=new QuestRunner(s,journal,recipe,new());
        runner.Actions["finish"]=new CompletedAction();
        return(s,runner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ready_caster_learns_once_before_starting_the_journey_with_full_resource_receipts(bool priest)
    {
        var(s,runner)=await Create(priest:priest);
        await runner.RunAsync();
        Assert.True(ShinewoodFaerieEvidence.HasSpell(s.Snapshot,"dachaidh"));
        Assert.Equal(5000u,s.Snapshot.Gold);
        Assert.Equal(0,s.Snapshot.Count("Goblin's Skull"));
        Assert.Single(s.Commands,c=>c.Kind=="interact");
        Assert.Equal(2,s.Commands.Count(c=>c.Kind=="choose"&&c.Text=="Yes"));
        Assert.DoesNotContain(s.Commands,c=>c.Kind is "raise-stat" or "assail" or "collect-ground");
        Assert.Null(runner.Job.Pending);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("stats")]
    [InlineData("gold")]
    [InlineData("skull")]
    [InlineData("unknown spells")]
    [InlineData("known")]
    public async Task Missing_requirements_or_already_known_spell_skip_the_trainer_without_hunting_or_spending(string shortage)
    {
        var(s,runner)=await Create(shortage);
        await runner.RunAsync();
        Assert.Empty(s.Commands);
        Assert.Equal(shortage=="gold"?4999u:10000u,s.Snapshot.Gold);
        Assert.Equal("Completed",runner.Job.Status);
    }

    [Fact]
    public async Task Uncertain_learning_waits_for_late_spell_gold_and_skull_receipts_without_another_payment()
    {
        var(s,runner)=await Create(receipt:false);
        await runner.RunAsync();
        Assert.NotNull(runner.Job.Pending);
        Assert.Equal("Paused",runner.Job.Status);
        var sent=s.Commands.Count;
        s.Set(s.Snapshot with { Gold=5000,Inventory=s.Snapshot.Inventory.Remove(9),Spells=s.Snapshot.Spells.SetItem(2,"dachaidh"),Dialog=null });
        await runner.RunAsync();
        Assert.Equal(sent,s.Commands.Count);
        Assert.Null(runner.Job.Pending);
        Assert.Equal("Completed",runner.Job.Status);
        Assert.NotNull(runner.Job.DachaidhPayment?.Receipt);
    }

    [Theory]
    [InlineData("spell-only")]
    [InlineData("missing-skull-debit")]
    [InlineData("replacement-generation")]
    public async Task Partial_or_replacement_receipts_preserve_the_original_payment_without_resending(string gap)
    {
        var (s, runner) = await Create(receipt: false);
        await runner.RunAsync();
        var pending = runner.Job.Pending;
        int sent = s.Commands.Count;
        s.Set(s.Snapshot with
        {
            Gold = gap == "spell-only" ? 10000u : 5000u,
            Inventory = gap == "replacement-generation" ? s.Snapshot.Inventory.Remove(9) : s.Snapshot.Inventory,
            Spells = s.Snapshot.Spells.SetItem(2, "dachaidh"),
            Generation = gap == "replacement-generation" ? s.Snapshot.Generation + 1 : s.Snapshot.Generation
        });
        await runner.RunAsync();
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Equal(pending, runner.Job.Pending);
        Assert.Equal(sent, s.Commands.Count);
        Assert.Null(runner.Job.DachaidhPayment?.Receipt);
    }

    [Fact]
    public async Task Unknown_actual_quote_stops_before_the_fee_selection()
    {
        var (s, runner) = await Create();
        var receive = s.OnCommand;
        s.OnCommand = command =>
        {
            receive!(command);
            if (s.Snapshot.Dialog is { Text: WizardPrice } quote)
                s.Set(s.Snapshot with { Dialog = quote with { Text = "Different requirements" } });
        };
        await runner.RunAsync();
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Single(s.Commands, c => c.Kind == "choose" && c.Text == "Yes");
        Assert.Equal(10000u, s.Snapshot.Gold);
        Assert.Null(runner.Job.Pending);
    }

    [Fact]
    public async Task Learning_preserves_the_parent_completion_dialog()
    {
        var terminal = new Dialog(1, 1, 77, 1, "Other", "Saved parent receipt", true, 0, 1, 0, []);
        var (_, runner) = await Create(saved: new() { CompletionDialog = terminal });
        await runner.RunAsync();
        Assert.Equal("Completed", runner.Job.Status);
        Assert.Equal(terminal, runner.Job.CompletionDialog);
    }
}
