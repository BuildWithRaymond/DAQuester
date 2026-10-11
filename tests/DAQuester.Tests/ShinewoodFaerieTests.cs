using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;
namespace DAQuester.Tests;

// All Faerie menus and rewards in these tests are synthetic, not captured dialogs.
public class ShinewoodFaerieTests {
 const string Id="shinewood-faerie",Pet="Pet Faerie";
 static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
 static QuestDefinition Recipe()=>new(Id,1,"Shinewood Faerie","https://vorlof.com/general/shinewoodforest11.html",
  ["navigate","dialog","inventory","speech","spell","item"],[
   new("preflight","faerie-preflight","visit"),new("visit","faerie-visit","return"),new("return","faerie-return","complete")],0){Repeatable=false};
 static FileQuestJournal Journal()=>new(Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString()));
 static CharacterSnapshot State(string cls="Warrior",int level=1,bool master=false,bool? pure=null)=>
  JsonSerializer.Deserialize<CharacterSnapshot>(JsonSerializer.Serialize(new{
   Name="Nuclear",Id=42,Connected=true,Generation=1,Level=level,Hp=100,Mana=100,Gold=999,Weight=1,MaxWeight=100,
   InventoryKnown=true,SpellsKnown=true,Class=cls,IsMaster=master,IsPure=pure,Map=500,Position=new{X=20,Y=20},
   Inventory=new Dictionary<byte,Item>{{3,new(3,"Mileth Song",1)}}}),Json)!;
 static SimulatedSession Session(CharacterSnapshot state){var s=new SimulatedSession();foreach(var cap in Recipe().Capabilities)((HashSet<string>)s.Capabilities).Add(cap);s.Set(state);return s;}
 static void Menu(SimulatedSession s,string text,bool next=false,params string[] choices)=>s.Set(s.Snapshot with{
  Dialog=new(s.Snapshot.Generation,s.Snapshot.Revision+1,901,1,"Faerie",text,true,712,3,0,
    choices.Select((x,i)=>new DialogChoice(x,(ushort)(840+i),(byte)(i+1))).ToImmutableArray(),HasNext:next)});
 static void Flow(SimulatedSession s,FileQuestJournal j,string[] offered){
  s.OnCommand=c=>{
   if(c.Kind is "navigate" or "visit")s.Set(s.Snapshot with{Map=c.Map,Position=c.Target,Dialog=null,Entities=ImmutableDictionary<uint,Entity>.Empty.Add(901,new(901,"Faerie",new(71,37),true))});
   if(c.Kind=="say"){Assert.NotNull(j.Load("Nuclear",Id).Pending);Menu(s,"Synthetic Faerie greeting",choices:["Adopt a Pet Faerie","Learn spells"]);}
   if(c.Kind=="choose"){
    Assert.NotNull(j.Load("Nuclear",Id).Pending);Assert.Equal(s.Snapshot.Dialog,c.Dialog);
    if(c.Text=="Learn spells")Menu(s,"Synthetic spell choices",choices:offered);
    else if(c.Text=="Adopt a Pet Faerie"){s.Set(s.Snapshot with{Inventory=s.Snapshot.Inventory.SetItem(8,new(8,Pet,1))});Menu(s,"Synthetic Pet Faerie adoption",next:true);}
    else {s.Set(s.Snapshot with{Spells=s.Snapshot.Spells.SetItem((byte)(10+s.Snapshot.Spells.Count),c.Text!)});Menu(s,"Synthetic learned "+c.Text,next:true);}
   }
   if(c.Kind=="next"||c.Kind=="close")s.Set(s.Snapshot with{Dialog=null});
   if(c.Kind=="use-item"){Assert.Equal(3u,c.Amount);Assert.NotNull(j.Load("Nuclear",Id).Pending);s.Set(s.Snapshot with{Map=500,Position=new(11,11)});}
   if(c.Kind=="spell"){Assert.NotNull(j.Load("Nuclear",Id).Pending);s.Set(s.Snapshot with{Map=500,Position=new(12,12)});}
  };
 }
 [Theory][InlineData("Peasant")][InlineData("Warrior")][InlineData("Rogue")][InlineData("Monk")][InlineData("Wizard")][InlineData("Priest")]
 public async Task Every_class_gets_pet_and_returns_by_town_song(string cls){
  var s=Session(State(cls));var j=Journal();Flow(s,j,[]);var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync(true);
  Assert.Equal("Completed",r.Job.Status);Assert.NotNull(r.Job.CompletedAt);Assert.Null(r.Job.Pending);Assert.False(r.Job.Repeat);
  Assert.Equal(1,s.Snapshot.Count(Pet));Assert.Single(s.Commands,c=>c.Kind=="choose"&&c.Text=="Adopt a Pet Faerie");
  Assert.Single(s.Commands,c=>c.Kind=="use-item");Assert.Null(r.Job.NextRun);
 }
 [Theory][InlineData("Priest",10,false,false,"")][InlineData("Priest",11,false,false,"deo lamh")]
 [InlineData("Priest",20,false,false,"deo lamh|creag neart")]
 [InlineData("Priest",99,true,false,"deo lamh|creag neart|deo searg gar")]
 [InlineData("Priest",99,true,true,"deo lamh|creag neart|deo searg gar|deo searg")]
 [InlineData("Wizard",99,true,false,"fas spiorad")][InlineData("Wizard",99,true,true,"fas spiorad|Wings of Protection")]
 public async Task Learns_all_eligible_missing_spells_without_duplicate_pet(string cls,int level,bool master,bool pure,string expected){
  string[] wanted=expected.Split('|',StringSplitOptions.RemoveEmptyEntries);
  var state=State(cls,level,master,pure);var s=Session(state with{Inventory=state.Inventory.Add(8,new(8,Pet,1))});var j=Journal();
  Flow(s,j,["deo lamh","creag neart","deo searg gar","deo searg","fas spiorad","Wings of Protection"]);
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);
  Assert.Equal(wanted.Order(),s.Snapshot.Spells.Values.Order());Assert.DoesNotContain(s.Commands,c=>c.Text=="Adopt a Pet Faerie");
 }
 [Theory][InlineData("")][InlineData(" (Lev:100/100)")] public async Task Carried_spell_skips_learning_and_uses_dachaidh_when_no_song(string rank){
  var state=State("Priest",11);var s=Session(state with{Map=992,Inventory=ImmutableDictionary<byte,Item>.Empty.Add(8,new(8,Pet,1)),Spells=ImmutableDictionary<byte,string>.Empty.Add(7,"dachaidh"+rank).Add(9,"deo lamh"+rank)});
  var j=Journal();Flow(s,j,["deo lamh"]);var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Completed",r.Job.Status);Assert.DoesNotContain(s.Commands,c=>c.Kind=="say"||c.Kind=="choose");
  Assert.Single(s.Commands,c=>c.Kind=="spell"&&c.Slot==7);Assert.DoesNotContain(s.Commands,c=>c.Kind=="use-item");
 }
 [Fact] public async Task Ranked_baseline_spell_is_not_a_new_reward_after_a_saved_submission(){
  var state=State("Priest",11) with{Inventory=State().Inventory.Add(8,new(8,Pet,1)),Spells=ImmutableDictionary<byte,string>.Empty.Add(9,"deo lamh")};
  var j=Journal();await j.SaveAsync(new(){Character="Nuclear",Quest=Id,Step="visit",FaerieGoals=["deo lamh"],FaerieCurrentGoal="deo lamh",
   Pending=new("faerie-goal",1,1,99,0,0,DateTimeOffset.UtcNow){SpellsBaselineKnown=true,Spells=["deo lamh (Lev:100/100)"]}});
  var s=Session(state);s.Set(state);var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.NotNull(r.Job.Pending);Assert.Empty(r.Job.FaerieReceipts);Assert.Empty(s.Commands);
 }
 [Theory][InlineData("deo lamh extra")][InlineData("deo lamh (Lev:unknown/100)")][InlineData("deo lamh (Lev:100/100) extra")]
 public void Unrecognized_spell_suffix_does_not_prove_known_spell(string name)=>Assert.False(ShinewoodFaerieEvidence.HasSpell(State() with{Spells=ImmutableDictionary<byte,string>.Empty.Add(9,name)},"deo lamh"));
 [Fact] public async Task Newly_learned_ranked_spell_proves_its_saved_goal(){
  var state=State("Priest",11) with{Inventory=State().Inventory.Add(8,new(8,Pet,1))};var s=Session(state);var j=Journal();Flow(s,j,["deo lamh"]);var flow=s.OnCommand!;
  s.OnCommand=c=>{flow(c);if(c.Kind=="choose"&&c.Text=="deo lamh")s.Set(s.Snapshot with{Spells=s.Snapshot.Spells.ToImmutableDictionary(p=>p.Key,p=>p.Value+" (Lev:0/100)")});};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.True(r.Job.Status=="Completed",r.Job.Reason);
  Assert.Single(r.Job.FaerieReceipts,r=>r.Goal=="deo lamh");Assert.Single(s.Commands,c=>c.Text=="deo lamh");
 }
 [Fact] public async Task Unprepared_visit_requires_acknowledgment_before_movement(){
  var s=Session(State() with{Inventory=ImmutableDictionary<byte,Item>.Empty.Add(3,new(3,"Recall Scroll",1))});
  var r=new QuestRunner(s,Journal(),Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.Contains("escape",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Missing_pet_capacity_stops_before_departure(){
  var s=Session(State() with{Weight=98,MaxWeight=100});var r=new QuestRunner(s,Journal(),Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.Contains("weight",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Unanswered_adoption_is_never_repeated_on_resume(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{if(c.Kind=="choose"&&c.Text=="Adopt a Pet Faerie")throw new TimeoutException("Synthetic lost adoption reply");flow(c);};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.Pending);await r.RunAsync();
  Assert.Single(s.Commands,c=>c.Kind=="choose"&&c.Text=="Adopt a Pet Faerie");Assert.Null(r.Job.CompletedAt);
 }
 [Fact] public async Task Unanswered_return_preserves_pet_and_never_consumes_second_song(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{if(c.Kind=="use-item")throw new TimeoutException("Synthetic lost teleport reply");flow(c);};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.Pending);Assert.Equal(1,s.Snapshot.Count(Pet));await r.RunAsync();
  Assert.Single(s.Commands,c=>c.Kind=="use-item");Assert.Single(s.Commands,c=>c.Text=="Adopt a Pet Faerie");Assert.Null(r.Job.CompletedAt);
  s.Set(s.Snapshot with{Generation=2,Map=500,Position=new(10,10),Dialog=null});await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Null(r.Job.Pending);
 }
 [Fact] public async Task Unknown_popup_is_preserved(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{flow(c);if(c.Kind=="say")Menu(s,"Synthetic unrelated offer",choices:["Pay 1000000 coins"]);};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.NotNull(s.Snapshot.Dialog);Assert.DoesNotContain(s.Commands,c=>c.Kind=="choose"||c.Kind=="close");
 }
 [Fact] public async Task Song_has_priority_over_known_ready_dachaidh(){
  var s=Session(State() with{Spells=ImmutableDictionary<byte,string>.Empty.Add(7,"dachaidh")});var j=Journal();Flow(s,j,[]);
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Single(s.Commands,c=>c.Kind=="use-item");Assert.DoesNotContain(s.Commands,c=>c.Kind=="spell");
 }
 [Fact] public async Task Low_mana_dachaidh_does_not_hide_unprepared_warning(){
  var s=Session(State() with{Mana=29,Inventory=[],Spells=ImmutableDictionary<byte,string>.Empty.Add(7,"dachaidh")});var r=new QuestRunner(s,Journal(),Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.Contains("escape",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Unknown_pure_status_uses_actual_offer_and_skips_unoffered_pure_spell(){
  var state=State("Wizard",99,true);var s=Session(state with{Inventory=state.Inventory.Add(8,new(8,Pet,1))});var j=Journal();Flow(s,j,["fas spiorad"]);
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Contains("fas spiorad",s.Snapshot.Spells.Values);Assert.DoesNotContain("Wings of Protection",s.Snapshot.Spells.Values);
 }
 [Fact] public async Task Unknown_pure_status_learns_pure_spell_when_actually_offered(){
  var state=State("Wizard",99,true);var s=Session(state with{Inventory=state.Inventory.Add(8,new(8,Pet,1))});var j=Journal();Flow(s,j,["fas spiorad","Wings of Protection"]);
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Contains("Wings of Protection",s.Snapshot.Spells.Values);
 }
 [Fact] public async Task Equipped_pet_and_known_spell_finish_in_town_without_commands(){
  var s=Session(State("Priest",11,false,false) with{Equipment=ImmutableDictionary<byte,string>.Empty.Add(1,Pet),Spells=ImmutableDictionary<byte,string>.Empty.Add(9,"deo lamh")});
  var r=new QuestRunner(s,Journal(),Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Walking_acknowledgment_enables_documented_exit_chain_and_real_landings(){
  var s=Session(State() with{Inventory=[]});var j=Journal();await j.SaveAsync(new(){Character="Nuclear",Quest=Id,FaerieWalkAcknowledgedGeneration=1});Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{if(c.Kind=="walk-exit")s.Set(s.Snapshot with{Map=c.Map,Position=new(17,11)});else flow(c);};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Equal(8,s.Commands.Count(c=>c.Kind=="walk-exit"));Assert.Equal(500,s.Snapshot.Map);Assert.DoesNotContain(s.Commands,c=>c.Kind is "use-item" or "spell");
 }
 [Fact] public async Task Stale_walking_acknowledgment_is_invalid_after_reconnect(){
  var s=Session(State() with{Generation=2,Inventory=[]});var j=Journal();await j.SaveAsync(new(){Character="Nuclear",Quest=Id,FaerieWalkAcknowledgedGeneration=1});
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Contains("escape",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.Empty(s.Commands);
 }
 [Theory][InlineData("entity")][InlineData("generation")][InlineData("revision")][InlineData("map")]
 public async Task Wrong_or_stale_reply_never_selects_pet(string kind){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{flow(c);if(c.Kind=="say"){var d=s.Snapshot.Dialog!;s.Set(s.Snapshot with{Map=kind=="map"?500:s.Snapshot.Map,Dialog=d with{EntityId=kind=="entity"?999:d.EntityId,Generation=kind=="generation"?2:d.Generation,Revision=kind=="revision"?1:d.Revision}});}};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Paused",r.Job.Status);Assert.NotNull(r.Job.Pending);Assert.DoesNotContain(s.Commands,c=>c.Kind=="choose");
 }
 [Fact] public async Task Late_pet_reward_reconciles_without_adopting_again(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{if(c.Kind=="choose"&&c.Text=="Adopt a Pet Faerie")Menu(s,"Synthetic Pet Faerie delivery");else flow(c);};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.Pending);Assert.Null(r.Job.CompletedAt);
  s.Set(s.Snapshot with{Inventory=s.Snapshot.Inventory.SetItem(8,new(8,Pet,1))});await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Single(s.Commands,c=>c.Text=="Adopt a Pet Faerie");
 }
 [Fact] public async Task Lost_adoption_reconciles_fresh_pet_after_reconnect(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{if(c.Kind=="choose"&&c.Text=="Adopt a Pet Faerie")throw new TimeoutException("Synthetic lost reply");flow(c);};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();s.Set(s.Snapshot with{Generation=2,Dialog=null,Inventory=s.Snapshot.Inventory.SetItem(8,new(8,Pet,1))});await r.RunAsync();
  Assert.Equal("Completed",r.Job.Status);Assert.Single(s.Commands,c=>c.Text=="Adopt a Pet Faerie");
 }
 [Fact] public async Task Mid_visit_manual_input_preserves_submission(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{if(c.Kind=="choose"&&c.Text=="Adopt a Pet Faerie"){s.Manual();return;}flow(c);};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Paused",r.Job.Status);Assert.NotNull(r.Job.Pending);Assert.Single(s.Commands,c=>c.Text=="Adopt a Pet Faerie");
 }
 [Fact] public async Task Another_character_cannot_receive_saved_reward_or_return(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{flow(c);if(c.Kind=="choose"&&c.Text=="Adopt a Pet Faerie")s.Set(s.Snapshot with{Name="Other"});};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Paused",r.Job.Status);Assert.Null(r.Job.CompletedAt);Assert.DoesNotContain(s.Commands,c=>c.Kind=="use-item");
 }
 [Fact] public async Task Later_explicit_visit_picks_up_newly_unlocked_spell_without_another_pet(){
  var s=Session(State("Priest",10,false,false));var j=Journal();Flow(s,j,["deo lamh"]);var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  s.Set(s.Snapshot with{Level=11});await r.RunAsync(startNewAttempt:true);Assert.Equal("Completed",r.Job.Status);Assert.Contains("deo lamh",s.Snapshot.Spells.Values);Assert.Single(s.Commands,c=>c.Text=="Adopt a Pet Faerie");
 }
 [Fact] public async Task Merchant_close_is_presentation_only_and_does_not_block_later_goals_or_return(){
  var s=Session(State("Priest",20,false,false));var j=Journal();Flow(s,j,["deo lamh","creag neart"]);var flow=s.OnCommand!;
  s.OnCommand=c=>{if(c.Kind=="close")return;flow(c);if(s.Snapshot.Dialog is {} d)s.Set(s.Snapshot with{Dialog=d with{IsPursuit=false,HasNext=false}});};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Equal(1,s.Snapshot.Count(Pet));Assert.Equal(2,s.Snapshot.Spells.Count);Assert.Single(s.Commands,c=>c.Kind=="use-item");
 }
 [Theory][InlineData(false)][InlineData(true)] public async Task Delayed_selection_reply_does_not_reuse_or_resubmit_the_old_menu(bool spell){
  var state=State("Priest",11,false,false);if(spell)state=state with{Inventory=state.Inventory.Add(8,new(8,Pet,1))};
  else state=state with{Spells=ImmutableDictionary<byte,string>.Empty.Add(9,"deo lamh")};
  var s=Session(state);var j=Journal();Flow(s,j,["deo lamh"]);var flow=s.OnCommand!;var delayed=new DeferredReplySession(s);
  s.OnCommand=c=>{if(c.Kind=="choose")delayed.Reply=()=>flow(c);else flow(c);};
  var r=new QuestRunner(delayed,j,Recipe(),new());await r.RunAsync();
  Assert.True(r.Job.Status=="Completed",r.Job.Reason);Assert.Null(r.Job.Pending);
  Assert.Single(s.Commands,c=>c.Kind=="choose"&&c.Text==(spell?"deo lamh":"Adopt a Pet Faerie"));
  if(spell)Assert.Single(s.Commands,c=>c.Kind=="choose"&&c.Text=="Learn spells");
 }
 sealed class DeferredReplySession(SimulatedSession inner):IGameSession {
  public Action? Reply;
  public CharacterSnapshot Snapshot=>inner.Snapshot;
  public IReadOnlySet<string> Capabilities=>inner.Capabilities;
  public event Action<CharacterSnapshot>? Changed {add=>inner.Changed+=value;remove=>inner.Changed-=value;}
  public event Action<string>? Interrupted {add=>inner.Interrupted+=value;remove=>inner.Interrupted-=value;}
  public Task ExecuteAsync(GameCommand command,CancellationToken ct)=>inner.ExecuteAsync(command,ct);
  public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot,bool> test,long after,TimeSpan timeout,CancellationToken ct){
   if(Reply is {} reply){
    // No server reply exists yet: the previous menu cannot satisfy this wait.
    Assert.False(Snapshot.Revision>after&&test(Snapshot));Reply=null;reply();
   }
   return inner.WaitAsync(test,after,timeout,ct);
  }
 }
 [Theory][InlineData(false)][InlineData(true)] public async Task Reward_with_server_dialog_close_returns_without_an_extra_resume(bool spell){
  var state=State("Priest",11,false,false);if(spell)state=state with{Inventory=state.Inventory.Add(8,new(8,Pet,1))};
  else state=state with{Spells=ImmutableDictionary<byte,string>.Empty.Add(9,"deo lamh")};
  var s=Session(state);var j=Journal();Flow(s,j,["deo lamh"]);var flow=s.OnCommand!;
  s.OnCommand=c=>{flow(c);if(c.Kind=="choose"&&c.Text==(spell?"deo lamh":"Adopt a Pet Faerie"))s.Set(s.Snapshot with{Dialog=null});};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.True(r.Job.Status=="Completed",r.Job.Reason);Assert.Null(r.Job.Pending);Assert.Single(s.Commands,c=>c.Kind=="use-item");
 }
 [Fact] public async Task Empty_recognized_spell_menu_excludes_unknown_pure_eligibility(){
  var state=State("Wizard",99,true);var s=Session(state with{Inventory=state.Inventory.Add(8,new(8,Pet,1)),Spells=ImmutableDictionary<byte,string>.Empty.Add(9,"fas spiorad")});
  var j=Journal();Flow(s,j,[]);var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.DoesNotContain(s.Commands,c=>c.Text=="Wings of Protection");
 }
 [Theory][InlineData(false)][InlineData(true)] public async Task Unnamed_entity_at_documented_anchor_and_reactor_dialogs_use_live_ids(bool reactor){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{flow(c);if(c.Kind=="visit")s.Set(s.Snapshot with{Entities=ImmutableDictionary<uint,Entity>.Empty.Add(901,new(901,"",new(71,37),true))});
   if(reactor&&s.Snapshot.Dialog is {} d)s.Set(s.Snapshot with{Dialog=d with{EntityId=0,EntityType=4}});};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Single(s.Commands,c=>c.Text=="Adopt a Pet Faerie");
 }
 [Fact] public async Task Undrawn_faerie_reactor_is_greeted_at_verified_approach_and_binds_reply_id(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  const string greeting="Synthetic princess fae of Shinewood; be kind to its faeries.";
  s.OnCommand=c=>{
   flow(c);
   if(c.Kind=="visit")s.Set(s.Snapshot with{Position=ShinewoodFaerieEvidence.Approach,Entities=ImmutableDictionary<uint,Entity>.Empty});
   if(c.Kind=="say"){Assert.Null(j.Load("Nuclear",Id).Pending!.EntityId);Menu(s,greeting,next:true);}
   if(c.Kind=="next"&&c.Dialog?.Text==greeting)Menu(s,"Synthetic Faerie choices",choices:["Adopt a Pet Faerie"]);
   if(c.Kind=="choose")Assert.Equal(18311u,c.Dialog!.EntityId);
   if(s.Snapshot.Dialog is {} d)s.Set(s.Snapshot with{Dialog=d with{EntityId=18311,EntityType=4,Name=""}});
  };
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.True(r.Job.Status=="Completed",r.Job.Reason);Assert.Single(s.Commands,c=>c.Kind=="say"&&c.Text=="Hello");
  Assert.Single(s.Commands,c=>c.Text=="Adopt a Pet Faerie");Assert.Single(s.Commands,c=>c.Kind=="use-item");
 }
 [Fact] public async Task Missing_faerie_entity_away_from_verified_approach_never_sends_hello(){
  var s=Session(State());var j=Journal();Flow(s,j,[]);var flow=s.OnCommand!;
  s.OnCommand=c=>{flow(c);if(c.Kind=="visit")s.Set(s.Snapshot with{Position=new(68,45),Entities=ImmutableDictionary<uint,Entity>.Empty});};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.DoesNotContain(s.Commands,c=>c.Kind=="say");Assert.Null(r.Job.Pending);
 }
}
