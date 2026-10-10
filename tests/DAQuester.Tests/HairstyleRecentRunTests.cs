using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using Xunit;
namespace DAQuester.Tests;
public class HairstyleRecentRunTests {
 sealed record Fixture(ReplayEvent[] Events,RecordedAction[] Actions,QuestJob OriginalJob);
 static Fixture Load()=>JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/evidence/datester-7.41/perfect-hairstyle-vows.json"))))!;
 sealed class RecordingJournal:IQuestJournal {
  readonly FileQuestJournal inner=new(Path.Combine(Path.GetTempPath(),"DAQuester-atomic-"+Guid.NewGuid()));
  public List<QuestJob> Saved=[];
  public QuestJob Load(string character,string quest)=>inner.Load(character,quest);
  public async Task SaveAsync(QuestJob job,CancellationToken ct=default){await inner.SaveAsync(job,ct);Saved.Add(job);}
 }
 [Fact] public async Task First_durable_hairstyle_pending_contains_entity_and_own_legend() {
  var s=new PerfectHairstyleLiveTests.ReplaySession();var baseline=s.Snapshot;var journal=new RecordingJournal();var recipe=new QuestDefinition("perfect-hairstyle",1,"Hair","fixture",[],[new("preflight","hair-preflight","eeva"),new("eeva","hair-dialog","confirm"),new("confirm","hair-confirm","complete")],168);
  await HairstyleScheduleTests.RunAttempt(new QuestRunner(s,journal,recipe,new()));
  Assert.All(journal.Saved.Where(j=>j.Pending is not null),j=>{Assert.Equal(11107u,j.Pending!.EntityId);Assert.Equal(baseline.Legends,j.Pending.Legends);});
 }
 [Fact] public void Actual_two_ring_terminal_matches_only_fresh_correlated_Eeva_claim() {
  var f=Load();var action=f.Actions.Single(a=>a.Selection=="Yes I did");var baseline=f.Events.Single(e=>e.Revision==action.Revision).Snapshot;var terminal=f.Events.Single(e=>e.Revision==862).Snapshot;
  var t=f.OriginalJob.Pending! with{EntityId=action.Dialog!.EntityId,Legends=baseline.Legends.ToArray()};
  Assert.True(PerfectHairstyleEvidence.FreshReply(terminal,t));Assert.True(PerfectHairstyleEvidence.Rewards(terminal,t));
  var reward=RewardEvidence.Record(t,terminal);Assert.Equal(25000,reward["experience"]);Assert.Equal(1,reward["item:Emerald Ring"]);Assert.Equal(1,reward["item:Jade Ring"]);
  Assert.False(PerfectHairstyleEvidence.FreshReply(terminal,t with{EntityId=77}));Assert.False(PerfectHairstyleEvidence.FreshReply(terminal,t with{Revision=terminal.Revision}));Assert.False(PerfectHairstyleEvidence.Rewards(baseline,t));
 }
 [Fact] public async Task Captured_dialogs_and_two_ring_reward_finish_automatic_attempt_and_preserve_weekly_time() {
  var f=Load();var frames=f.Events.Where(e=>e.Revision<=847||e.Revision>=855).ToArray();int index=0;var s=MuisirTests.Session();s.Set(frames[0].Snapshot with{Dialog=null});
  s.OnCommand=c=>{
   if(c.Kind is "open" or "choose" or "next") {
    do {Assert.True(index<frames.Length,"Replay exhausted");var frame=frames[index++].Snapshot;s.Set(frame with{LegendsRevision=0,Dialog=frame.Dialog is {} d?d with{Revision=s.Snapshot.Revision+1}:null});}while(frames[index-1].Opcode is not (47 or 48));
   }
   if(c.Kind=="inspect-self")s.Set(s.Snapshot with{LegendsKnown=true,LegendsRevision=s.Snapshot.Revision+1});
  };
  // Map recorded revisions into this simulator; retain actual IDs, text and observation times.
  var journal=new RecordingJournal();var recipe=new QuestDefinition("perfect-hairstyle",1,"Hair","fixture",[],[new("preflight","hair-preflight","eeva"),new("eeva","hair-dialog","confirm"),new("confirm","hair-confirm","complete")],168);
  var runner=new QuestRunner(s,journal,recipe,new());await HairstyleScheduleTests.RunAttempt(runner);
  Assert.True(runner.Job.LastAttemptAt is not null,runner.Job.Reason);Assert.Null(runner.Job.Pending);Assert.Null(runner.Job.CompletedAt);Assert.Equal(25000,runner.Job.Rewards["experience"]);Assert.Equal(1,runner.Job.Rewards["item:Jade Ring"]);Assert.Equal(f.Events.Single(e=>e.Revision==862).Snapshot.Dialog!.ObservedAt!.Value.AddDays(7),runner.Job.NextRun);Assert.Single(s.Commands,c=>c.Text=="Yes I did");Assert.Null(s.Snapshot.Dialog);
 }
 [Theory][InlineData(true)][InlineData(false)] public async Task Reconnect_reconciles_only_a_durable_validated_terminal_and_fresh_reward_state(bool savedReply) {
  var f=Load();var a=f.Actions.Single(a=>a.Selection=="Yes I did");var baseline=f.Events.Single(e=>e.Revision==a.Revision).Snapshot;var terminal=f.Events.Single(e=>e.Revision==862).Snapshot;var s=MuisirTests.Session();
  s.Set(terminal with{Generation=terminal.Generation+1,Dialog=null,LegendsKnown=false,LegendsRevision=0});
  s.OnCommand=c=>{if(c.Kind=="inspect-self")s.Set(s.Snapshot with{LegendsKnown=true,LegendsRevision=s.Snapshot.Revision+1});};
  var t=f.OriginalJob.Pending! with{Revision=a.Revision,Started=a.Time,EntityId=a.Dialog!.EntityId,Legends=baseline.Legends.ToArray(),TerminalSeen=savedReply,TerminalRevision=savedReply?terminal.Revision:null};
  var job=f.OriginalJob with{Pending=t,HairstyleCompletion=savedReply?terminal.Dialog:null};var j=new RecordingJournal();await j.SaveAsync(job);
  var recipe=new QuestDefinition("perfect-hairstyle",1,"Hair","fixture",[],[new("confirm","hair-confirm","complete")],168);var runner=new QuestRunner(s,j,recipe,new());await HairstyleScheduleTests.RunAttempt(runner);
  Assert.DoesNotContain(s.Commands,c=>c.Kind is "visit" or "open" or "choose" or "next");
  if(savedReply){Assert.Null(runner.Job.Pending);Assert.Equal(25000,runner.Job.Rewards["experience"]);Assert.Equal(terminal.Dialog!.ObservedAt!.Value.AddDays(7),runner.Job.NextRun);Assert.Single(s.Commands,c=>c.Kind=="inspect-self");}
  else {Assert.NotNull(runner.Job.Pending);Assert.Null(runner.Job.LastAttemptAt);Assert.Empty(s.Commands);}
 }
}
