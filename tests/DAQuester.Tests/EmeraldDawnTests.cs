using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;
namespace DAQuester.Tests;

// Guide-derived conversations and rewards below are synthetic, not live captures.
public class EmeraldDawnTests {
 const string Id="emerald-dawn",Item="Emerald Knight Insignia",Recovery="Recovered the Emerald Knight Insignia";
 static readonly JsonSerializerOptions json=new(){PropertyNameCaseInsensitive=true};
 static QuestDefinition Recipe()=>new(Id,1,"Emerald Dawn","https://vorlof.com/quests/emeralddawn.html",["navigate","dialog","inventory","legend","speech","ground-item"],[
  new("preflight","emerald-preflight","mioas"),new("mioas","emerald-mioas","berg"),new("berg","emerald-briefing","gather"),
  new("gather","emerald-gather","recover",new(){["patrol"]="[{\"X\":14,\"Y\":23},{\"X\":10,\"Y\":12}]"}),
  new("recover","emerald-recover","druid"),new("druid","emerald-druid","deliver"),new("deliver","emerald-deliver","confirm"),new("confirm","emerald-confirm","complete")],0){Repeatable=false};
 static FileQuestJournal Journal()=>new(Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString()));
 static QuestJob Job(string outcome="emerald-knights",string step="preflight",bool druid=false)=>JsonSerializer.Deserialize<QuestJob>(JsonSerializer.Serialize(new{Character="Nuclear",Quest=Id,Step=step,EmeraldOptions=new{Outcome=outcome,VisitDruid=druid}}),json)!;
 static SimulatedSession Session(int insignias=0) {
  var s=new SimulatedSession();foreach(var cap in Recipe().Capabilities)((HashSet<string>)s.Capabilities).Add(cap);
  s.Set(s.Snapshot with{Level=11,Gold=200,Experience=1000,LegendsKnown=true,LegendsRevision=1,Inventory=insignias==0?[]:ImmutableDictionary<byte,Item>.Empty.Add(9,new(9,Item,(uint)insignias))});return s;
 }
 static string Final(string outcome)=>outcome=="emerald-knights"?"Aided the Emerald Knights":"Aided the Necromancer Order";
 static QuestJob Pending(SimulatedSession s,string outcome="emerald-knights",string kind="emerald-deliver",string step="confirm")=>Job(outcome,step) with{Pending=new(kind,s.Snapshot.Generation,s.Snapshot.Revision,s.Snapshot.Gold!.Value,0,s.Snapshot.Experience!.Value,DateTimeOffset.UtcNow){EntityId=991,InventoryBaselineKnown=true,InventoryCounts=new(){[Item]=1},Legends=[Recovery]}};
 static void Reward(SimulatedSession s,string outcome,bool consumed=true,bool mark=true,bool gold=true) {
  var legends=ImmutableArray.Create(Recovery);if(outcome!="rogue-order"&&mark)legends=legends.Add(Final(outcome)+" - Deoch 223, Fall");
  s.Set(s.Snapshot with{Inventory=consumed?[]:s.Snapshot.Inventory,Legends=legends,LegendsRevision=s.Snapshot.Revision+1,Gold=outcome=="rogue-order"&&gold?1000200:s.Snapshot.Gold,
   Map=outcome=="rogue-order"?425:s.Snapshot.Map,Notice=outcome=="rogue-order"&&gold?new(s.Snapshot.Generation,s.Snapshot.Revision+1,3,EmeraldDawnEvidence.RogueSaleNotice,DateTimeOffset.UtcNow):s.Snapshot.Notice});
 }
 static void Reply(SimulatedSession s,uint entity,string npc,string text="Synthetic guide-derived page",bool next=false,params DialogChoice[] choices)=>s.Set(s.Snapshot with{Dialog=new(s.Snapshot.Generation,s.Snapshot.Revision+1,entity,1,npc,text,true,731,7,0,ImmutableArray.Create(choices),HasNext:next)});
 sealed class BeforeWaitSession(SimulatedSession inner,Action<Func<CharacterSnapshot,bool>> beforeWait):IGameSession {
  public CharacterSnapshot Snapshot=>inner.Snapshot;
  public IReadOnlySet<string> Capabilities=>inner.Capabilities;
  public event Action<CharacterSnapshot>? Changed {add=>inner.Changed+=value;remove=>inner.Changed-=value;}
  public event Action<string>? Interrupted {add=>inner.Interrupted+=value;remove=>inner.Interrupted-=value;}
  public Task ExecuteAsync(GameCommand command,CancellationToken ct)=>inner.ExecuteAsync(command,ct);
  public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot,bool> predicate,long after,TimeSpan timeout,CancellationToken ct){beforeWait(predicate);return inner.WaitAsync(predicate,after,timeout,ct);}
 }

 [Theory][InlineData("emerald-knights",false)][InlineData("necromancer-order",false)][InlineData("rogue-order",false)][InlineData("emerald-knights",true)]
 public async Task Full_flow_saves_each_submission_and_verifies_selected_outcome(string outcome,bool druid) {
  var s=Session();var j=Journal();await j.SaveAsync(Job(outcome,druid:druid));string npc="";uint entity=0;int bergSpeeches=0;
  s.OnCommand=c=>{
   if(c.Kind=="navigate")s.Set(s.Snapshot with{Map=c.Map,Dialog=null});
   if(c.Kind=="visit"){npc=c.Text!;entity=npc switch{"Mioas"=>891,"Berg"=>892,"Gans"=>893,_=>991};s.Set(s.Snapshot with{Map=c.Map,Entities=ImmutableDictionary<uint,Entity>.Empty.Add(entity,new(entity,npc,new(5,5),true))});}
   if(c.Kind=="collect-ground")s.Set(s.Snapshot with{Inventory=s.Snapshot.Inventory.SetItem(9,new(9,Item,1))});
   if(c.Kind=="say") {
    Assert.NotNull(j.Load("Nuclear",Id).Pending);
    if(npc=="Berg"&&++bergSpeeches==2)Reply(s,entity,npc,choices:[new("Yes, I found it.",941,3)]);
    else if(npc is "Dislaidir" or "Sennaan" or "Skan")Reply(s,entity,npc,choices:[new(npc=="Dislaidir"?"Give him the Insignia":npc=="Sennaan"?"Give him the Emerald Knight Insignia":"Sure, I'll sell it",945,4)]);
    else Reply(s,entity,s.Snapshot.Name!,next:true);
   }
   if(c.Kind=="next")Reply(s,entity,npc);
   if(c.Kind=="choose") {
    Assert.Equal(npc=="Berg"?"emerald-recover":"emerald-deliver",j.Load("Nuclear",Id).Pending?.Kind);
    Assert.Equal(s.Snapshot.Dialog,c.Dialog);
    if(npc=="Berg")s.Set(s.Snapshot with{Legends=ImmutableArray.Create(Recovery),LegendsRevision=s.Snapshot.Revision+1,Experience=301000});
    else Reward(s,outcome);
    Reply(s,entity,npc);
   }
   if(c.Kind=="close")s.Set(s.Snapshot with{Dialog=null});
   if(c.Kind=="inspect-self")s.Set(s.Snapshot with{LegendsKnown=true,LegendsRevision=s.Snapshot.Revision+1});
  };
  var runner=new QuestRunner(s,j,Recipe(),new());await runner.RunAsync(true);
  Assert.Equal("Completed",runner.Job.Status);Assert.Null(runner.Job.Pending);Assert.NotNull(runner.Job.CompletedAt);Assert.False(runner.Job.Repeat);Assert.Null(runner.Job.NextRun);
  Assert.Equal(300000,runner.Job.Rewards["experience"]);Assert.Equal(2,s.Commands.Count(c=>c.Kind=="choose"));
  Assert.Equal(druid,s.Commands.Any(c=>c.Kind=="visit"&&c.Text=="Gans"));Assert.Equal(0,s.Snapshot.Count(Item));
  if(outcome=="rogue-order"){Assert.Equal(1000000,runner.Job.Rewards["gold"]);Assert.Equal(Recovery,runner.Job.CompletionLegend);}else Assert.StartsWith(Final(outcome),runner.Job.CompletionLegend);
 }
 [Theory][InlineData("emerald-knights")][InlineData("necromancer-order")]
 public async Task Existing_final_own_legend_finishes_without_commands(string outcome) {
  var s=Session();s.Set(s.Snapshot with{Legends=ImmutableArray.Create(Final(outcome)+" - Deoch 223, Fall")});
  var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync(true);
  Assert.Equal("Completed",r.Job.Status);Assert.Empty(s.Commands);Assert.Empty(r.Job.Rewards);Assert.False(r.Job.Repeat);
 }
 [Theory][InlineData(null)][InlineData(10)][InlineData(0)] public async Task Invalid_or_unknown_level_stops_before_walking(int? level) {
  var s=Session();s.Set(s.Snapshot with{Level=level});var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Paused",r.Job.Status);Assert.Empty(s.Commands);Assert.Contains("level",r.Job.Reason,StringComparison.OrdinalIgnoreCase);
 }
 [Fact] public async Task Missing_outcome_never_starts_or_picks_for_player() {
  var s=Session();var r=new QuestRunner(s,Journal(),Recipe(),new());await r.RunAsync();Assert.Contains("choice",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Unknown_inventory_stops_before_walking() {
  var s=Session();s.Set(s.Snapshot with{InventoryKnown=false});var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Contains("inventory",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Recovery_without_insignia_does_not_gather_another_or_claim_completion() {
  var s=Session();s.Set(s.Snapshot with{Legends=ImmutableArray.Create(Recovery)});var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Contains("reconcil",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.Null(r.Job.CompletedAt);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Recovery_mark_without_receipt_skips_intro_and_gather_but_hears_Berg_again() {
  var s=Session(1);s.Set(s.Snapshot with{Legends=ImmutableArray.Create(Recovery)});var j=Journal();await j.SaveAsync(Job());s.OnCommand=c=>{if(c.Kind=="visit")s.Manual();};var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Single(s.Commands,c=>c.Kind=="visit"&&c.Text=="Berg");Assert.DoesNotContain(s.Commands,c=>c.Kind=="collect-ground"||c.Kind=="visit"&&c.Text is "Mioas" or "Dislaidir");Assert.Null(r.Job.CompletedAt);
 }
 static EmeraldRecoveryReceipt Receipt()=>JsonSerializer.Deserialize<EmeraldRecoveryReceipt>("{\"ObservedAt\":\"2026-10-11T00:00:00Z\",\"Legend\":\"Recovered the Emerald Knight Insignia\",\"Rewards\":{}}",json)!;
 [Fact] public async Task Recovery_receipt_with_insignia_goes_straight_to_delivery() {
  var s=Session(1);s.Set(s.Snapshot with{Legends=ImmutableArray.Create(Recovery)});var j=Journal();await j.SaveAsync(Job() with{EmeraldRecovery=Receipt()});s.OnCommand=c=>{if(c.Kind=="visit")s.Manual();};var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Single(s.Commands,c=>c.Kind=="visit"&&c.Text=="Dislaidir");Assert.DoesNotContain(s.Commands,c=>c.Kind=="visit"&&c.Text is "Mioas" or "Berg");
 }
 [Fact] public async Task Pre_existing_recovery_mark_is_accepted_once_Berg_retells_the_story_to_its_last_page() {
  // Live 2026-10-11: Skan ignored a character whose mark came from an unfinished manual Berg visit; Berg re-offers the story and the recipients then respond.
  var s=Session(1);s.Set(s.Snapshot with{Legends=ImmutableArray.Create(Recovery)});var j=Journal();await j.SaveAsync(Job(step:"recover"));int pages=0;
  s.OnCommand=c=>{
   if(c.Kind=="navigate")s.Set(s.Snapshot with{Map=c.Map,Dialog=null});
   if(c.Kind=="visit")s.Set(s.Snapshot with{Map=c.Map,Entities=ImmutableDictionary<uint,Entity>.Empty.Add(892,new(892,"Berg",new(5,5),true))});
   if(c.Kind=="say")Reply(s,892,"Berg","Did you find the Emerald Knight Insignia?",choices:[new("Yes, I found it",941,1),new("No, not yet",941,2)]);
   if(c.Kind=="choose")Reply(s,892,"Berg","Astounding! You Aislings truly are a rare breed.",next:true);
   if(c.Kind=="next"){if(++pages<3)Reply(s,892,"Berg","Synthetic story page",next:true);else s.Set(s.Snapshot with{Dialog=null});}
   if(c.Kind=="close")s.Set(s.Snapshot with{Dialog=null});
  };
  var c=new QuestContext(s,j,new(),Job(step:"recover"));await new EmeraldDawnActions().ExecuteAsync(c,Recipe().Steps.Single(p=>p.Id=="recover"),default);
  Assert.NotNull(c.Job.EmeraldRecovery);Assert.Null(c.Job.Pending);Assert.Equal(Recovery,c.Job.EmeraldRecovery!.Legend);Assert.Single(s.Commands,x=>x.Kind=="choose");Assert.Equal(1,s.Snapshot.Count(Item));
 }
 [Fact] public async Task Delivery_without_a_recovery_receipt_is_blocked_in_player_terms() {
  var s=Session(1);s.Set(s.Snapshot with{Legends=ImmutableArray.Create(Recovery)});var j=Journal();await j.SaveAsync(Job(step:"deliver"));var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Contains("Berg",r.Job.Reason);Assert.Empty(s.Commands);Assert.Null(r.Job.CompletedAt);
 }
 [Fact] public async Task Sale_finished_by_hand_after_the_greeting_completes_on_resume_without_commands() {
  // Live 2026-10-11 (Swallow): runner greeted Skan, got no offer, paused; the player then sold by hand.
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s,"rogue-order",kind:"emerald-delivery-dialog",step:"deliver"));Reward(s,"rogue-order");
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Completed",r.Job.Status);Assert.Null(r.Job.Pending);Assert.Equal(1000000,r.Job.Rewards["gold"]);Assert.DoesNotContain(s.Commands,c=>c.Kind is "say" or "choose" or "visit");
 }
 [Fact] public void Choices_lock_only_once_the_insignia_is_handed_over() {
  Assert.False(EmeraldDawnEvidence.ChoicesLocked(Job()));
  Assert.False(EmeraldDawnEvidence.ChoicesLocked(Job(step:"deliver") with{EmeraldMioasSeen=true,EmeraldBergBriefed=true,EmeraldRecovery=Receipt()}));
  var s=Session(1);Assert.True(EmeraldDawnEvidence.ChoicesLocked(Pending(s)));Assert.True(EmeraldDawnEvidence.ChoicesLocked(Pending(s,kind:"emerald-delivery-dialog",step:"deliver")));
  Assert.False(EmeraldDawnEvidence.ChoicesLocked(Pending(s,kind:"emerald-recover",step:"recover")));
  Assert.True(EmeraldDawnEvidence.ChoicesLocked(Job() with{CompletedAt=DateTimeOffset.UtcNow}));
 }
 [Fact] public async Task Dry_search_keeps_collecting_until_inventory_proves_one() {
  var s=Session();var j=Journal();await j.SaveAsync(Job(step:"gather"));int attempts=0;s.OnCommand=c=>{if(c.Kind=="collect-ground"&&++attempts==7)s.Set(s.Snapshot with{Inventory=ImmutableDictionary<byte,Item>.Empty.Add(9,new(9,Item,1))});if(c.Kind=="visit")s.Manual();};
  var r=new QuestRunner(s,j,Recipe(),new());using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(3));await r.RunAsync(cancellation:limit.Token);
  Assert.Equal(7,attempts);Assert.All(s.Commands.Where(c=>c.Kind=="collect-ground"),c=>{Assert.Equal(542,c.Map);Assert.Equal(70u,c.Amount);Assert.Equal(new InventoryGoal(Item,1),c.StopWhen);});
 }
 [Fact] public async Task Discovery_popup_uses_next_before_close_and_requires_inventory_confirmation(){
  var s=Session();s.Set(s.Snapshot with{Map=542});Reply(s,88,"Forest","Synthetic: you found an Emerald Knight Insignia",next:true);s.Set(s.Snapshot with{Dialog=s.Snapshot.Dialog! with{EntityType=4}});
  var journal=Journal();var context=new QuestContext(s,journal,new(),Job(step:"gather"));
  s.OnCommand=command=>{
   if(command.Kind=="next"){
    Assert.Equal("emerald-discovery",journal.Load("Nuclear",Id).Pending!.Kind);
    s.Set(s.Snapshot with{Dialog=s.Snapshot.Dialog! with{Revision=s.Snapshot.Revision+1,HasNext=false,Text="Synthetic final discovery page"}});
   }else if(command.Kind=="close"){
    Assert.False(command.Dialog!.HasNext);Assert.True(journal.Load("Nuclear",Id).Pending!.TerminalSeen);
    s.Set(s.Snapshot with{Dialog=null,Inventory=ImmutableDictionary<byte,Item>.Empty.Add(9,new(9,Item,1))});
   }else throw new QuestBlockedException("Synthetic unexpected movement");
  };
  await new EmeraldDawnActions().ExecuteAsync(context,Recipe().Steps.Single(p=>p.Id=="gather"),default);
  Assert.Equal(new[]{"next","close"},s.Commands.Select(p=>p.Kind));Assert.Equal(1,s.Snapshot.Count(Item));Assert.Null(context.Job.Pending);
 }
 [Fact] public async Task Unknown_search_popup_is_preserved_and_pauses_without_any_command(){
  var s=Session();s.Set(s.Snapshot with{Map=542});Reply(s,88,"Forest","Synthetic unrelated popup",next:true);var popup=s.Snapshot.Dialog;
  s.OnCommand=command=>{if(command.Kind=="close")s.Set(s.Snapshot with{Dialog=null});else throw new QuestBlockedException("Synthetic attempted collection");};
  var context=new QuestContext(s,Journal(),new(),Job(step:"gather"));
  await Assert.ThrowsAnyAsync<QuestBlockedException>(()=>new EmeraldDawnActions().ExecuteAsync(context,Recipe().Steps.Single(p=>p.Id=="gather"),default));
  Assert.Same(popup,s.Snapshot.Dialog);Assert.Empty(s.Commands);Assert.Null(context.Job.Pending);
 }
 [Fact] public async Task Observed_item_before_discovery_popup_still_finishes_next_without_close(){
  string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/evidence/datester-7.41/emerald-dawn-discovery.json"));using var fixture=JsonDocument.Parse(File.ReadAllText(path));
  var tracker=new DAQuester.DarkAges.Protocol.StateTracker(1);tracker.Identity("Synthetic",42);
  tracker.Observe(21,[2,30,25,25,0,0,0,0,0,0]);tracker.Observe(0x58,[0]);
  var events=fixture.RootElement.GetProperty("Events").EnumerateArray().ToArray();
  tracker.Observe(15,Convert.FromBase64String(events[0].GetProperty("Payload").GetString()!));Assert.Equal(1,tracker.Snapshot.Count(Item));
  tracker.Observe(48,Convert.FromBase64String(events[1].GetProperty("Payload").GetString()!));var actual=tracker.Snapshot.Dialog!;
  Assert.Equal("",actual.Name);Assert.True(actual.HasNext);Assert.Equal((byte)4,actual.EntityType);Assert.Equal(0,actual.Pursuit);Assert.Equal(1,actual.Step);
  var s=Session(1);s.Set(s.Snapshot with{Map=542,Dialog=actual with{Generation=s.Snapshot.Generation,Revision=s.Snapshot.Revision+1}});
  Assert.True(EmeraldDawnEvidence.IsDiscoveryPopup(s.Snapshot));var c=new QuestContext(s,Journal(),new(),Job(step:"gather"));
  s.OnCommand=command=>{Assert.Equal("next",command.Kind);var packet=DAQuester.DarkAges.Protocol.DialogPackets.Continue(s.Snapshot,command.Dialog!);var next=new Arbiter.Net.Client.Messages.ClientPursuitMessage();next.Deserialize(new Arbiter.Net.Serialization.NetworkPacketReader(packet));Assert.Equal(2,next.StepId);Assert.Equal(actual.EntityId,next.EntityId);
   tracker.Observe(48,Convert.FromBase64String(events[2].GetProperty("Payload").GetString()!));s.Set(s.Snapshot with{Dialog=tracker.Snapshot.Dialog});};
  await new EmeraldDawnActions().ExecuteAsync(c,Recipe().Steps.Single(p=>p.Id=="gather"),default);Assert.Single(s.Commands);Assert.Null(c.Job.Pending);Assert.Null(s.Snapshot.Dialog);Assert.Equal(1,s.Snapshot.Count(Item));
 }
 [Theory][InlineData(false)][InlineData(true)] public async Task Unanswered_discovery_next_is_saved_and_never_resent_after_resume(bool reconnect){
  var s=Session();s.Set(s.Snapshot with{Map=542});Reply(s,88,"Forest","Synthetic Emerald Knight Insignia discovery",next:true);
  var journal=Journal();var c=new QuestContext(s,journal,new(),Job(step:"gather"));s.OnCommand=_=>throw new TimeoutException("Synthetic missing Next reply");
  await Assert.ThrowsAsync<TimeoutException>(()=>new EmeraldDawnActions().ExecuteAsync(c,Recipe().Steps.Single(p=>p.Id=="gather"),default));Assert.Equal(s.Snapshot.Dialog!.Revision,c.Job.Pending!.Revision);
  if(reconnect)s.Set(s.Snapshot with{Generation=2,Dialog=null});s.OnCommand=_=>Assert.Fail("Discovery submission repeated");
  await Assert.ThrowsAnyAsync<QuestBlockedException>(()=>new EmeraldDawnActions().ExecuteAsync(c,Recipe().Steps.Single(p=>p.Id=="gather"),default));Assert.Single(s.Commands);Assert.NotNull(c.Job.Pending);
  if(reconnect){s.Set(s.Snapshot with{Inventory=ImmutableDictionary<byte,Item>.Empty.Add(9,new(9,Item,1))});await new EmeraldDawnActions().ExecuteAsync(c,Recipe().Steps.Single(p=>p.Id=="gather"),default);Assert.Null(c.Job.Pending);Assert.Single(s.Commands);}
 }
 [Fact] public async Task Discovery_finished_without_item_pauses_and_late_inventory_reconciles_without_resend(){
  var s=Session();s.Set(s.Snapshot with{Map=542});Reply(s,88,"Forest","Synthetic Emerald Knight Insignia discovery",next:true);var c=new QuestContext(s,Journal(),new(),Job(step:"gather"));s.OnCommand=_=>s.Set(s.Snapshot with{Dialog=null});
  await Assert.ThrowsAnyAsync<QuestBlockedException>(()=>new EmeraldDawnActions().ExecuteAsync(c,Recipe().Steps.Single(p=>p.Id=="gather"),default));Assert.True(c.Job.Pending!.TerminalSeen);Assert.Single(s.Commands);
  s.Set(s.Snapshot with{Inventory=ImmutableDictionary<byte,Item>.Empty.Add(9,new(9,Item,1))});await new EmeraldDawnActions().ExecuteAsync(c,Recipe().Steps.Single(p=>p.Id=="gather"),default);Assert.Null(c.Job.Pending);Assert.Single(s.Commands);
 }
 [Fact] public async Task Inventory_arriving_before_late_discovery_page_still_reads_next_before_leaving(){
  var s=Session();s.Set(s.Snapshot with{Map=542});bool delivered=false;
  var late=new BeforeWaitSession(s,_=>{if(!delivered&&s.Snapshot.Count(Item)==1){delivered=true;Reply(s,88,"","Synthetic Emerald Knight Insignia discovery",next:true);}});
  s.OnCommand=command=>{if(command.Kind=="collect-ground")s.Set(s.Snapshot with{Inventory=ImmutableDictionary<byte,Item>.Empty.Add(9,new(9,Item,1))});else if(command.Kind=="next")s.Set(s.Snapshot with{Dialog=null});else Assert.Fail("Unexpected discovery command");};
  var c=new QuestContext(late,Journal(),new(),Job(step:"gather"));await new EmeraldDawnActions().ExecuteAsync(c,Recipe().Steps.Single(p=>p.Id=="gather"),default);
  Assert.True(delivered);Assert.Equal(new[]{"collect-ground","next"},s.Commands.Select(p=>p.Kind));Assert.Null(s.Snapshot.Dialog);Assert.Null(c.Job.Pending);
 }
 [Fact] public async Task Sale_after_reconnect_has_no_server_message_and_stays_pending() {
  // The sale message belongs to the old connection; a new one cannot prove it, so the player decides.
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s,"rogue-order"));s.Set(s.Snapshot with{Generation=2,Dialog=null});Reward(s,"rogue-order");
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Null(r.Job.CompletedAt);Assert.NotNull(r.Job.Pending);Assert.DoesNotContain(s.Commands,c=>c.Kind is "say" or "choose" or "visit");
 }
 [Fact] public async Task Gold_difference_without_the_sale_message_never_proves_the_sale() {
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s,"rogue-order",kind:"emerald-delivery-dialog",step:"deliver"));
  s.Set(s.Snapshot with{Inventory=[],Gold=1000200,Map=425});
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Null(r.Job.CompletedAt);Assert.NotNull(r.Job.Pending);
 }
 [Theory][InlineData("emerald-knights")][InlineData("necromancer-order")]
 public async Task Pending_delivery_reconciles_fresh_rewards_after_reconnect_without_resending(string outcome) {
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s,outcome));s.Set(s.Snapshot with{Generation=2,Dialog=null});Reward(s,outcome);
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Null(r.Job.Pending);Assert.DoesNotContain(s.Commands,c=>c.Kind is "say" or "choose" or "visit");
 }
 [Theory][InlineData("emerald-knights",true,false,true)][InlineData("emerald-knights",false,true,true)][InlineData("rogue-order",true,true,false)][InlineData("rogue-order",false,true,true)]
 public async Task Partial_reward_never_completes_or_replays(string outcome,bool consumed,bool mark,bool gold) {
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s,outcome));Reward(s,outcome,consumed,mark,gold);
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();r=new(s,j,Recipe(),new());await r.RunAsync();
  Assert.Null(r.Job.CompletedAt);Assert.NotNull(r.Job.Pending);Assert.DoesNotContain(s.Commands,c=>c.Kind is "say" or "choose" or "visit");
 }
 [Fact] public async Task Other_faction_mark_cannot_confirm_selected_delivery() {
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s));Reward(s,"necromancer-order");var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.Pending);Assert.Null(r.Job.CompletedAt);
 }
 [Theory][InlineData("emerald-delivery-dialog")][InlineData("emerald-deliver")]
 public async Task Interrupted_speech_or_submission_never_repeats_after_restart(string kind) {
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s,kind:kind,step:"deliver"));s.OnCommand=c=>{if(c.Kind=="inspect-self")s.Set(s.Snapshot with{LegendsKnown=true,LegendsRevision=s.Snapshot.Revision+1});};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();r=new(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.Pending);Assert.DoesNotContain(s.Commands,c=>c.Kind is "say" or "choose" or "visit");
 }
 [Fact] public async Task Stale_or_unrelated_dialog_cannot_authorize_delivery() {
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s,kind:"emerald-delivery-dialog",step:"deliver"));Reply(s,555,"Dislaidir",choices:[new("Give him the Insignia",945,4)]);
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.Pending);Assert.DoesNotContain(s.Commands,c=>c.Kind=="choose");
 }
 [Fact] public async Task Unexpected_choice_preserves_checkpoint() {
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s,kind:"emerald-delivery-dialog",step:"deliver"));s.Set(s.Snapshot with{Map=2052});Reply(s,991,"Dislaidir",choices:[new("Destroy the insignia",945,4)]);
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.Pending);Assert.Contains("Unexpected",r.Job.Reason);Assert.DoesNotContain(s.Commands,c=>c.Kind=="choose");
 }
 [Fact] public async Task Preexisting_mark_cannot_prove_pending_delivery() {
  var s=Session(1);var j=Journal();var pending=Pending(s);await j.SaveAsync(pending with{Pending=pending.Pending! with{Legends=[Recovery,Final("emerald-knights")]}});Reward(s,"emerald-knights");
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Null(r.Job.CompletedAt);Assert.NotNull(r.Job.Pending);
 }
 [Fact] public async Task Another_character_cannot_use_saved_reward() {
  var s=Session(1);var j=Journal();await j.SaveAsync(Pending(s));Reward(s,"emerald-knights");var other=Session(1);other.Set(other.Snapshot with{Name="Clock"});await j.SaveAsync(Pending(other) with{Character="Clock"});
  var a=new QuestRunner(s,j,Recipe(),new());var b=new QuestRunner(other,j,Recipe(),new());await Task.WhenAll(a.RunAsync(),b.RunAsync());Assert.Equal("Completed",a.Job.Status);Assert.Null(b.Job.CompletedAt);Assert.NotNull(b.Job.Pending);
 }
 [Fact] public void Shipped_recipe_and_catalog_expose_only_required_capabilities() {
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content"));
  Assert.True(File.Exists(Path.Combine(root,"recipes","emerald-dawn.v1.json")));
  var recipe=JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root,"recipes","emerald-dawn.v1.json")),json)!;
  Assert.False(recipe.Repeatable);Assert.DoesNotContain("combat-credit",recipe.Capabilities);Assert.DoesNotContain("party",recipe.Capabilities);
  using var catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"catalog.json")));var row=catalog.RootElement.GetProperty("entries").EnumerateArray().Single(e=>e.GetProperty("id").GetString()=="emeralddawn");Assert.Equal("implemented",row.GetProperty("status").GetString());Assert.False(row.GetProperty("capabilitiesInferred").GetBoolean());
 }
 [Fact] public async Task Guide_alternate_recovery_mark_skips_completed_stages() {
  var s=Session(1);s.Set(s.Snapshot with{Legends=ImmutableArray.Create("Found Emerald Knight Insignia - Deoch 223, Fall")});var j=Journal();await j.SaveAsync(Job() with{EmeraldRecovery=Receipt()});s.OnCommand=c=>{if(c.Kind=="visit")s.Manual();};var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Single(s.Commands,c=>c.Kind=="visit"&&c.Text=="Dislaidir");Assert.DoesNotContain(s.Commands,c=>c.Kind=="visit"&&c.Text is "Mioas" or "Berg");
 }
 [Fact] public async Task Unknown_legend_cannot_complete_from_stale_final_mark() {
  var s=Session();s.Set(s.Snapshot with{LegendsKnown=false,Legends=ImmutableArray.Create(Final("emerald-knights"))});var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Null(r.Job.CompletedAt);Assert.DoesNotContain(s.Commands,c=>c.Kind=="visit");
 }
 [Fact] public async Task Unrelated_notice_does_not_end_wait_for_mioas_reply() {
  var s=Session();var j=Journal();await j.SaveAsync(Job(step:"mioas"));s.OnCommand=c=>{
   if(c.Kind=="navigate")s.Set(s.Snapshot with{Map=413});
   if(c.Kind=="visit")s.Set(s.Snapshot with{Entities=ImmutableDictionary<uint,Entity>.Empty.Add(891,new(891,"Mioas",new(3,14),true))});
   if(c.Kind=="say"){
    s.Set(s.Snapshot with{Notice=new(s.Snapshot.Generation,s.Snapshot.Revision+1,3,"Unrelated world announcement",DateTimeOffset.UtcNow)});
   }
   if(c.Kind=="close")s.Set(s.Snapshot with{Dialog=null});
  };
  var session=new BeforeWaitSession(s,predicate=>{Assert.False(predicate(s.Snapshot));Reply(s,891,"Mioas");});
  var c=new QuestContext(session,j,new(),Job(step:"mioas"));await new EmeraldDawnActions().ExecuteAsync(c,new("mioas","emerald-mioas",null),default);Assert.True(c.Job.EmeraldMioasSeen);Assert.Null(c.Job.Pending);
 }
 [Theory][InlineData(false)][InlineData(true)] public async Task Full_capacity_blocks_search_but_allows_carried_insignia(bool carried) {
  var s=Session(carried?1:0);s.Set(s.Snapshot with{Weight=100,MaxWeight=100});var j=Journal();await j.SaveAsync(Job(step:"gather"));s.OnCommand=c=>{if(c.Kind=="visit")s.Manual();};var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.DoesNotContain(s.Commands,c=>c.Kind=="collect-ground");Assert.Equal(carried,s.Commands.Any(c=>c.Kind=="visit"&&c.Text=="Berg"));
 }
 [Theory][InlineData(null)][InlineData(uint.MaxValue)] public async Task Sale_requires_known_gold_and_no_overflow_before_speech(uint? gold) {
  var s=Session(1);s.Set(s.Snapshot with{Gold=gold,Legends=ImmutableArray.Create(Recovery)});var j=Journal();await j.SaveAsync(Job("rogue-order","deliver"));var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Contains("gold",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Recovery_receipt_reconciles_once_and_preserves_actual_xp() {
  var s=Session(1);var j=Journal();var pending=Pending(s,kind:"emerald-recover",step:"recover");await j.SaveAsync(pending with{Pending=pending.Pending! with{Legends=[]}});s.Set(s.Snapshot with{Experience=801000,Legends=ImmutableArray.Create(Recovery),LegendsRevision=s.Snapshot.Revision+1});
  s.OnCommand=c=>{if(c.Kind=="visit")s.Manual();};var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.EmeraldRecovery);Assert.Equal(800000,r.Job.EmeraldRecovery.Rewards["experience"]);Assert.Null(r.Job.CompletedAt);Assert.DoesNotContain(s.Commands,c=>c.Kind is "say" or "choose");
 }
 [Fact] public async Task Submission_choice_and_own_legend_are_durable_after_cancellation() {
  var s=Session(1);s.Set(s.Snapshot with{Map=2052,Legends=ImmutableArray.Create(Recovery)});var j=Journal();await j.SaveAsync(Pending(s,kind:"emerald-delivery-dialog",step:"deliver"));Reply(s,991,"Dislaidir",choices:[new("Give him the Insignia",945,4)]);
  s.OnCommand=c=>{if(c.Kind=="choose"){Assert.Equal("emerald-deliver",j.Load("Nuclear",Id).Pending?.Kind);Reward(s,"emerald-knights");s.Manual();}};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(j.Load("Nuclear",Id).Pending);r=new(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Single(s.Commands,c=>c.Kind=="choose");
 }
 [Fact] public async Task Berg_approach_avoids_collision_cross_around_guide_location() {
  // Sanitized lod414 collision footprint: x=5 at y=4/6; x=4/5/6 at y=5.
  var walls=new HashSet<Tile>{new(5,4),new(4,5),new(5,5),new(6,5),new(5,6)};
  var s=Session();var j=Journal();await j.SaveAsync(Job(step:"berg"));s.OnCommand=c=>{if(c.Kind=="navigate"){Assert.DoesNotContain(c.Target!,walls);s.Manual();}};
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Single(s.Commands,c=>c.Kind=="navigate");Assert.Equal("Manual input",r.Job.Reason);
 }
 [Fact] public async Task Delivery_completion_time_cannot_reuse_an_earlier_conversation() {
  var s=Session(1);var j=Journal();var pending=Pending(s);var old=DateTimeOffset.UtcNow.AddDays(-2);
  await j.SaveAsync(pending with{CompletionDialog=new(1,0,891,1,"Mioas","Synthetic old page",true,1,1,0,[]){ObservedAt=old}});Reward(s,"emerald-knights");
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.True(r.Job.CompletedAt>=pending.Pending!.Started);
 }
 [Fact] public async Task Late_recovery_xp_before_delivery_baseline_remains_in_total_rewards() {
  var s=Session(1);s.Set(s.Snapshot with{Experience=501000});var j=Journal();
  var receipt=JsonSerializer.Deserialize<EmeraldRecoveryReceipt>("{\"ObservedAt\":\"2026-10-10T00:00:00Z\",\"Legend\":\"Recovered the Emerald Knight Insignia\",\"Rewards\":{\"experience\":300000},\"ExperienceBaseline\":1000}",json)!;
  await j.SaveAsync(Pending(s) with{EmeraldRecovery=receipt,Rewards=receipt.Rewards});Reward(s,"emerald-knights");
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Equal(500000,r.Job.Rewards["experience"]);
 }
 [Fact] public void Shipped_patrol_excludes_portals_and_all_quest_maps_are_reachable() {
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content"));var routes=Path.Combine(root,"routes.json");
  var repository=new DAQuester.DarkAges.Navigation.MapRepository("",routes);using var profile=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"calibration","emerald-dawn-patrol.json")));
  var patrol=profile.RootElement.GetProperty("patrol").Deserialize<Tile[]>(json)!;var record=repository.Record(542);Assert.NotEmpty(patrol);
  Assert.All(patrol,t=>{Assert.InRange(t.X,0,record.Width-1);Assert.InRange(t.Y,0,record.Height-1);Assert.DoesNotContain(record.Portals,p=>p.From==t);});
  foreach(int map in new[]{413,414,542,701,2052,660,425}){Assert.NotEmpty(repository.Route(500,map));Assert.NotEmpty(repository.Route(map,500));}
 }
 [Theory][InlineData(0)][InlineData(1)] public async Task Berg_can_offer_recovery_on_first_visit_only_when_insignia_is_carried(int count) {
  var s=Session(count);var j=Journal();await j.SaveAsync(Job(step:"berg"));s.OnCommand=c=>{
   if(c.Kind=="navigate")s.Set(s.Snapshot with{Map=c.Map});
   if(c.Kind=="visit"){if(c.Text=="Dislaidir")s.Manual();else s.Set(s.Snapshot with{Entities=ImmutableDictionary<uint,Entity>.Empty.Add(892,new(892,"Berg",new(5,5),true))});}
   if(c.Kind=="say")Reply(s,892,"Berg",choices:[new("Yes, I found it",731,3)]);
   if(c.Kind=="choose"){Assert.Equal("emerald-recover",j.Load("Nuclear",Id).Pending?.Kind);s.Set(s.Snapshot with{Legends=ImmutableArray.Create(Recovery),LegendsRevision=s.Snapshot.Revision+1,Experience=301000});Reply(s,892,"Berg");}
   if(c.Kind=="close")s.Set(s.Snapshot with{Dialog=null});
   if(c.Kind=="inspect-self")s.Set(s.Snapshot with{LegendsKnown=true,LegendsRevision=s.Snapshot.Revision+1});
  };
  var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal(count,s.Commands.Count(c=>c.Kind=="choose"));Assert.Null(r.Job.CompletedAt);
  if(count==1){Assert.NotNull(r.Job.EmeraldRecovery);Assert.Equal(300000,r.Job.Rewards["experience"]);Assert.Null(r.Job.Pending);Assert.DoesNotContain(s.Commands,c=>c.Kind=="collect-ground");}else Assert.NotNull(r.Job.Pending);
 }
}
