using System.Text.Json;
namespace DAQuester.Core;

public sealed class EmeraldDawnActions:IQuestAction {
 static readonly TimeSpan timeout=TimeSpan.FromSeconds(5);
 record Conversation(int Map,string Npc,Tile Approach,string Speech,string? Choice=null);
 static readonly Conversation mioas=new(413,"Mioas",new(3,13),"Adventures"),berg=new(414,"Berg",new(5,7),"Emerald Knights"),
  recovery=berg with{Choice="Yes, I found it"},druid=new(701,"Gans",new(5,13),"Hello");
 static Conversation Delivery(EmeraldDawnOptions o)=>o.Outcome switch {
  "emerald-knights"=>new(2052,"Dislaidir",new(4,13),"Hello","Give him the Insignia"),
  "necromancer-order"=>new(660,"Sennaan",new(21,38),"Hello","Give him the Emerald Knight Insignia"),
  "rogue-order"=>new(425,"Skan",new(3,7),"Hello","Sure, I'll sell it"),
  _=>throw new QuestBlockedException("Emerald Dawn choice required.")
 };
 static DateTimeOffset ObservedTime(QuestContext c,Transaction t)=>c.Job.CompletionDialog is {} d&&d.Generation==t.Generation&&d.EntityId==t.EntityId&&d.Revision>t.Revision&&d.ObservedAt is {} observed&&observed>=t.Started?observed:DateTimeOffset.UtcNow;
 public async Task ExecuteAsync(QuestContext c,QuestStep step,CancellationToken ct) {
  if(c.Session.Snapshot.Name is not {} name||!name.Equals(c.Job.Character,StringComparison.OrdinalIgnoreCase))throw new QuestBlockedException("Emerald Dawn character identity changed; resume on the saved character.");
  if(c.Job.Pending is not null)await Reconcile(c,ct);
  if(c.Job.Step=="complete")return;
  if(!c.Session.Snapshot.LegendsKnown)await c.Session.ExecuteAsync(new("inspect-self"),ct);
  if(EmeraldDawnEvidence.FinalMark(c.Session.Snapshot) is {} existing) {
   await c.SaveAsync(c.Job with{Step="complete",CompletedAt=DateTimeOffset.UtcNow,CompletionLegend=existing,CompletionText=existing,Repeat=false,NextRun=null,Reason="Own final legend verified"});return;
  }
  var options=c.Job.EmeraldOptions??throw new QuestBlockedException("Emerald Dawn choice required before starting.");options.Validate();
  var gate=EmeraldDawnEvidence.Eligibility(c.Session.Snapshot);if(gate.Kind!=EligibilityKind.Eligible)throw new QuestBlockedException(gate.Reason);
  bool recovered=EmeraldDawnEvidence.HasRecovery(c.Session.Snapshot);
  switch(step.Action) {
   case "emerald-preflight":return;
   case "emerald-mioas":if(!recovered&&!c.Job.EmeraldMioasSeen)await Speak(c,mioas,"emerald-mioas",ct);return;
   case "emerald-briefing":if(!recovered&&!c.Job.EmeraldBergBriefed)await Speak(c,berg,"emerald-briefing",ct);return;
   case "emerald-gather":if(!recovered)await Gather(c,step,ct);return;
   case "emerald-recover":if(!recovered){if(c.Session.Snapshot.Count(EmeraldDawnEvidence.Insignia)<1)throw new QuestBlockedException("Carry one Emerald Knight Insignia before returning to Berg.");await Speak(c,recovery,"emerald-recovery-dialog",ct);}return;
   case "emerald-druid":if(options.VisitDruid&&!c.Job.EmeraldDruidSeen)await Speak(c,druid,"emerald-druid",ct);return;
   case "emerald-deliver":
    if(!recovered)throw new QuestBlockedException("Verify Berg's recovery legend before giving away the insignia.");
    if(options.Outcome=="rogue-order"&&(c.Session.Snapshot.Gold is null||c.Session.Snapshot.Gold>uint.MaxValue-1000000))throw new QuestBlockedException("Known carried gold and room for 1,000,000 gold required before sale.");
    await Speak(c,Delivery(options),"emerald-delivery-dialog",ct);return;
   case "emerald-confirm":throw new QuestBlockedException("Emerald Dawn disposition needs reconciliation; no saved submission proves the outcome.");
   default:throw new QuestBlockedException("Unsupported Emerald Dawn action: "+step.Action);
  }
 }
 static async Task Gather(QuestContext c,QuestStep step,CancellationToken ct) {
  if(c.Session.Snapshot.Dialog is not null&&c.Session.Snapshot.Map==542)await Discovery(c,ct);
  if(c.Session.Snapshot.Count(EmeraldDawnEvidence.Insignia)>=1)return;
  var patrol=JsonSerializer.Deserialize<Tile[]>(step.Arguments?.GetValueOrDefault("patrol")??"[]",new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
  if(patrol.Length==0)throw new QuestBlockedException("Shinewood Entrance patrol missing.");
  while(c.Session.Snapshot.Count(EmeraldDawnEvidence.Insignia)<1)foreach(var tile in patrol) {
   ct.ThrowIfCancellationRequested();var gate=EmeraldDawnEvidence.Eligibility(c.Session.Snapshot);if(gate.Kind!=EligibilityKind.Eligible)throw new QuestBlockedException(gate.Reason);
   await c.SaveAsync(c.Job with{Reason="Searching Shinewood Entrance for the Emerald Knight Insignia"});
   long before=c.Session.Snapshot.Revision;bool discoveryRead=false;
   try{await c.Session.ExecuteAsync(new("collect-ground",542,tile,Amount:EmeraldDawnEvidence.Sprite,StopWhen:new(EmeraldDawnEvidence.Insignia,1)),ct);}
   catch(QuestPopupException){await Discovery(c,ct);discoveryRead=true;}
   if(c.Session.Snapshot.Dialog is not null){await Discovery(c,ct);discoveryRead=true;}
   if(c.Session.Snapshot.Count(EmeraldDawnEvidence.Insignia)>=1){
    // Observed acquisition sends inventory before the discovery page. Give that
    // page time to arrive before navigation can dismiss a newly opened popup.
    if(!discoveryRead)try{await c.Session.WaitAsync(s=>s.Map==542&&s.Dialog is not null,before,timeout,ct);}catch(TimeoutException){}
    if(c.Session.Snapshot.Dialog is not null)await Discovery(c,ct);
    return;
   }
  }
 }
 static async Task Discovery(QuestContext c,CancellationToken ct) {
  if(!EmeraldDawnEvidence.IsDiscoveryPopup(c.Session.Snapshot))throw new QuestBlockedException("Search paused for an unfamiliar popup. It is preserved; review it before resuming.");
  var popup=c.Session.Snapshot.Dialog!;
  await c.BeginAsync("emerald-discovery",popup.EntityId,"Reading the insignia discovery popup",popup);
  await ReconcileDiscovery(c,ct);
 }
 static async Task ReconcileDiscovery(QuestContext c,CancellationToken ct) {
  var t=c.Job.Pending!;var s=c.Session.Snapshot;
  bool Found(CharacterSnapshot value)=>value.Connected&&value.InventoryKnown&&value.Count(EmeraldDawnEvidence.Insignia)>=1;
  if(s.Generation!=t.Generation||s.Map!=542){
   if(!Found(s)||s.Dialog is not null)throw new QuestBlockedException("Discovery popup changed connection or map. Reconcile the saved popup; it will not be resent.");
  }else{
   if(t.TerminalSeen&&s.Dialog is {} terminal){
    if(terminal.Generation!=t.Generation||terminal.EntityId!=t.EntityId||terminal.Revision!=t.TerminalRevision||terminal.HasNext||terminal.Type!=0||terminal.Choices.Length>0||terminal.Prompt is not null)throw new QuestBlockedException("Unexpected popup after discovery; preserved for review.");
    await c.Session.ExecuteAsync(new("close",Dialog:terminal),ct);
   }
   for(int page=0;!c.Job.Pending!.TerminalSeen&&page<32;page++){
    ct.ThrowIfCancellationRequested();t=c.Job.Pending!;s=c.Session.Snapshot;
    if(s.Dialog is not {} d){if(!Found(s))throw new QuestBlockedException("Discovery reply missing; saved popup needs reconciliation without resending.");break;}
    var basis=c.Job.CompletionDialog??throw new QuestBlockedException("Saved discovery page missing; reconcile before continuing.");
    if(s.Generation!=t.Generation||s.Map!=542||d.Generation!=t.Generation||d.Revision<=t.Revision||d.Revision>s.Revision||d.EntityId!=t.EntityId||d.EntityType!=basis.EntityType||d.Pursuit!=basis.Pursuit||!d.IsPursuit||d.Type!=0||d.Prompt is not null||d.Choices.Length>0)throw new QuestBlockedException("Unexpected or unconfirmed discovery page; popup preserved without resending.");
    if(d.HasNext){
     await c.SaveAsync(c.Job with{CompletionDialog=d,Pending=t with{Revision=d.Revision}});
     await c.Session.ExecuteAsync(new("next",Dialog:d),ct);
     if(c.Session.Snapshot.Dialog is null)await c.SaveAsync(c.Job with{Pending=c.Job.Pending! with{TerminalSeen=true,TerminalRevision=d.Revision}});
    }else{
     await c.SaveAsync(c.Job with{CompletionDialog=d,Pending=t with{TerminalSeen=true,TerminalRevision=d.Revision}});
     await c.Session.ExecuteAsync(new("close",Dialog:d),ct);
    }
   }
   if(!c.Job.Pending!.TerminalSeen&&!Found(c.Session.Snapshot))throw new QuestBlockedException("Discovery popup exceeded its page limit; preserved for reconciliation.");
   if(c.Session.Snapshot.Dialog is not null)throw new QuestBlockedException("Discovery popup remains open; review it before resuming.");
  }
  if(!Found(c.Session.Snapshot))try{await c.Session.WaitAsync(Found,c.Session.Snapshot.Revision,timeout,ct);}catch(TimeoutException){throw new QuestBlockedException("Discovery popup finished, but insignia inventory is unconfirmed. Saved progress needs reconciliation; popup will not be resent.");}
  await c.SaveAsync(c.Job with{Pending=null,Reason="Emerald Knight Insignia verified in inventory"});
 }
 static async Task Speak(QuestContext c,Conversation flow,string kind,CancellationToken ct) {
  if(c.Session.Snapshot.Map==542&&c.Session.Snapshot.Dialog is not null)await Discovery(c,ct);
  if(c.Session.Snapshot.Dialog is {} open)await c.Session.ExecuteAsync(new("close",Dialog:open),ct);
  // The approach tile only needs to bring the NPC into view; Visit picks the exact free tile beside them.
  await c.Session.ExecuteAsync(new("navigate",flow.Map,flow.Approach,Tolerance:4),ct);
  await c.Session.ExecuteAsync(new("visit",flow.Map,Text:flow.Npc),ct);
  var s=c.Session.Snapshot;
  if(s.Map!=flow.Map||!s.Connected)throw new QuestBlockedException("Fresh "+flow.Npc+" location required.");
  var npc=s.Entities.Values.SingleOrDefault(e=>!e.IsPlayer&&e.Name.Equals(flow.Npc,StringComparison.OrdinalIgnoreCase))??throw new QuestBlockedException(flow.Npc+" is not visible.");
  await c.BeginAsync(kind,npc.Id,"Speaking with "+flow.Npc);
  var t=c.Job.Pending!;
  await c.Session.ExecuteAsync(new("say",Text:flow.Speech),ct);
  await c.Session.WaitAsync(n=>n.Generation==t.Generation&&n.Map==flow.Map&&(n.Dialog is {} d&&d.EntityId==npc.Id&&d.Revision>t.Revision||n.Notice is {} notice&&notice.Generation==t.Generation&&notice.Revision>t.Revision&&
   (notice.Text.Contains(flow.Npc,StringComparison.OrdinalIgnoreCase)||notice.Text.Contains(flow.Speech,StringComparison.OrdinalIgnoreCase)||notice.Text.Contains("insignia",StringComparison.OrdinalIgnoreCase))),t.Revision,timeout,ct);
  if(c.Session.Snapshot.Dialog is null&&c.Session.Snapshot.Notice is {} refusal&&refusal.Revision>t.Revision)throw new QuestBlockedException(flow.Npc+": "+refusal.Text+" Conversation preserved for reconciliation.");
  await Reconcile(c,ct);
 }
 static Conversation Flow(QuestContext c,Transaction t)=>t.Kind switch {
  "emerald-mioas"=>mioas,"emerald-briefing"=>recovery,"emerald-druid"=>druid,
  "emerald-recovery-dialog" or "emerald-recover"=>recovery,
  "emerald-delivery-dialog" or "emerald-deliver"=>Delivery(c.Job.EmeraldOptions??throw new QuestBlockedException("Saved Emerald Dawn choice missing.")),
  _=>throw new QuestBlockedException("Unexpected Emerald Dawn transaction requires reconciliation.")
 };
 static async Task Reconcile(QuestContext c,CancellationToken ct) {
  if(c.Job.Pending?.Kind=="emerald-discovery"){await ReconcileDiscovery(c,ct);return;}
  var t=c.Job.Pending??throw new QuestBlockedException("No pending Emerald Dawn transaction.");var flow=Flow(c,t);
  if(c.Job.EmeraldOptions is {} options)options.Validate();
  // An already observed terminal is saved before Close; repeat only that harmless Close.
  if(t.TerminalSeen&&c.Session.Snapshot.Generation==t.Generation&&c.Session.Snapshot.Map==flow.Map&&c.Session.Snapshot.Dialog is {} terminal&&terminal.EntityId==t.EntityId&&terminal.Revision==t.TerminalRevision)
   await c.Session.ExecuteAsync(new("close",Dialog:terminal),ct);
  else if(!t.TerminalSeen&&c.Session.Snapshot.Generation==t.Generation&&c.Session.Snapshot.Map==flow.Map&&c.Session.Snapshot.Dialog is {} reply&&reply.Revision>t.Revision)
   await Advance(c,flow,ct);
  t=c.Job.Pending!;
  if(t.Kind is "emerald-mioas" or "emerald-briefing" or "emerald-druid") {
   if(!t.TerminalSeen)throw new QuestBlockedException(flow.Npc+" conversation needs reconciliation; speech will not be resent.");
   if(t.Kind=="emerald-druid"&&(!c.Session.Snapshot.InventoryKnown||c.Session.Snapshot.Count(EmeraldDawnEvidence.Insignia)!=EmeraldDawnEvidence.Count(t)))throw new QuestBlockedException("Gans consultation changed insignia inventory; reconcile before delivery.");
   await c.SaveAsync(c.Job with{Pending=null,EmeraldMioasSeen=c.Job.EmeraldMioasSeen||t.Kind=="emerald-mioas",EmeraldBergBriefed=c.Job.EmeraldBergBriefed||t.Kind=="emerald-briefing",EmeraldDruidSeen=c.Job.EmeraldDruidSeen||t.Kind=="emerald-druid"});return;
  }
  bool recovering=t.Kind is "emerald-recovery-dialog" or "emerald-recover";
  bool Proven(CharacterSnapshot s)=>recovering?EmeraldDawnEvidence.ProvesRecovery(s,t):c.Job.EmeraldOptions is {} o&&EmeraldDawnEvidence.ProvesDelivery(s,t,o);
  if(!Proven(c.Session.Snapshot)) {
   await c.Session.ExecuteAsync(new("inspect-self"),ct);
   if(!Proven(c.Session.Snapshot))try{await c.Session.WaitAsync(Proven,c.Session.Snapshot.Revision,timeout,ct);}catch(TimeoutException){throw new QuestBlockedException(recovering?"Berg recovery needs reconciliation: fresh recovery legend and retained insignia required. Submission will not be resent.":"Emerald Dawn disposition needs reconciliation: one consumed insignia and selected legend or exactly 1,000,000 new gold required. Submission will not be resent.");}
  }
  var after=c.Session.Snapshot;
  if(recovering) {
   var rewards=new Dictionary<string,int>();if(after.Experience is {} xp&&xp>t.Experience)rewards["experience"]=checked((int)(xp-t.Experience));
   var mark=EmeraldDawnEvidence.RecoveryMark(after)!;
   await c.SaveAsync(c.Job with{Pending=null,EmeraldRecovery=new(ObservedTime(c,t),mark,rewards){ExperienceBaseline=t.Experience},Rewards=rewards,Reason="Recovery legend verified; insignia retained"});return;
  }
  var finalRewards=new Dictionary<string,int>(c.Job.EmeraldRecovery?.Rewards??c.Job.Rewards);
  if(c.Job.EmeraldOptions!.Outcome=="rogue-order")finalRewards["gold"]=1000000;
  if(after.Experience is {} finalXp&&finalXp>t.Experience)finalRewards["experience"]=finalRewards.GetValueOrDefault("experience")+checked((int)(finalXp-t.Experience));
  // Recovery XP may arrive during travel, before the final submission baseline.
  if(c.Job.EmeraldRecovery?.ExperienceBaseline is {} initialXp&&after.Experience is {} totalXp&&totalXp>initialXp)finalRewards["experience"]=checked((int)(totalXp-initialXp));
  string finalMark=EmeraldDawnEvidence.OutcomeLegend(c.Job.EmeraldOptions.Outcome) is {} legend?EmeraldDawnEvidence.Mark(after,legend)!:EmeraldDawnEvidence.RecoveryMark(after)!;
  await c.SaveAsync(c.Job with{Step="complete",Pending=null,CompletedAt=ObservedTime(c,t),Repeat=false,NextRun=null,Rewards=finalRewards,CompletionLegend=finalMark,
   CompletionText=c.Job.EmeraldOptions.Outcome=="rogue-order"?"Sold Emerald Knight Insignia to Skan; 1,000,000 gold verified":finalMark,Reason="Selected Emerald Dawn outcome verified"});
 }
 static async Task Advance(QuestContext c,Conversation flow,CancellationToken ct) {
  long last=0;
  for(int page=0;page<32;page++) {
   ct.ThrowIfCancellationRequested();var t=c.Job.Pending!;var s=c.Session.Snapshot;
   if(s.Generation!=t.Generation||s.Map!=flow.Map)throw new QuestBlockedException("Emerald Dawn conversation changed connection or map; reconcile saved submission.");
   if(s.Dialog is not {} d) {
    if(last>0)await c.SaveAsync(c.Job with{Pending=t with{TerminalSeen=true,TerminalRevision=last}});
    return;
   }
   if(d.Generation!=t.Generation||d.Revision<=t.Revision||d.Revision<=last||d.Revision>s.Revision||d.EntityId!=t.EntityId||d.EntityType!=1||!d.IsPursuit||d.Type is not (0 or 2)||d.Prompt is not null||
    !(d.Name.Equals(flow.Npc,StringComparison.OrdinalIgnoreCase)||d.Name.Equals(c.Job.Character,StringComparison.OrdinalIgnoreCase)))throw new QuestBlockedException("Unexpected "+flow.Npc+" page; saved submission preserved: "+d.Text);
   last=d.Revision;
   if(d.Choices.Length>0) {
    bool earlyRecovery=t.Kind=="emerald-briefing"&&t.InventoryBaselineKnown&&EmeraldDawnEvidence.Count(t)>=1;
    bool ready=t.Kind is "emerald-recovery-dialog" or "emerald-delivery-dialog"||earlyRecovery;
    var choices=d.Choices.Where(x=>flow.Choice is {} expected&&x.Text.Trim().TrimEnd('.').Equals(expected,StringComparison.OrdinalIgnoreCase)).ToArray();
    if(!ready||choices.Length!=1)throw new QuestBlockedException("Unexpected "+flow.Npc+" choices; saved submission preserved: "+d.Text);
    await c.SaveAsync(c.Job with{Pending=t with{Kind=t.Kind=="emerald-recovery-dialog"||earlyRecovery?"emerald-recover":"emerald-deliver",Revision=s.Revision}});
    await c.Session.ExecuteAsync(new("choose",Text:choices[0].Text,Dialog:d),ct);
    await c.Session.WaitAsync(n=>n.Generation==t.Generation&&n.Map==flow.Map&&(n.Dialog is {} next&&next.EntityId==t.EntityId&&next.Revision>last||n.Dialog is null),s.Revision,timeout,ct);continue;
   }
   if(d.Type!=0)throw new QuestBlockedException("Unexpected input page from "+flow.Npc+"; submission preserved.");
   if(d.HasNext){await c.Session.ExecuteAsync(new("next",Dialog:d),ct);continue;}
   await c.SaveAsync(c.Job with{CompletionDialog=d,Pending=t with{TerminalSeen=true,TerminalRevision=d.Revision}});
   await c.Session.ExecuteAsync(new("close",Dialog:d),ct);return;
  }
  throw new QuestBlockedException(flow.Npc+" conversation exceeded 32 pages; submission preserved.");
 }
}
