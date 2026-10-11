using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;
namespace DAQuester.Tests;

public class StickCompletionTests {
 static readonly Calibration Cal=new(){Validated=true,Patrol=[new(22,22)],Accepted=["Accepted"],Completed=["Done"],EvidenceFiles=["synthetic"]};
 static FileQuestJournal Journal()=>new(Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString()));
 static Transaction Submission()=>new("turn-in",1,0,0,6,1000,DateTimeOffset.UtcNow.AddSeconds(-10));
 static CharacterSnapshot Reward(CharacterSnapshot snapshot,DateTimeOffset observed)=>snapshot with {
  Experience=4500,Inventory=ImmutableDictionary<byte,Item>.Empty.Add(2,new(2,"Stick",1)).Add(3,new(3,"Wooden Shield",1)),
  Dialog=new(1,1,99,1,"Callough","Done",true,177,92,0,[]){ObservedAt=observed}
 };
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Queue_closes_verified_Callough_reward_before_starting_next_quest(bool hasNext) {
  var session=new SimulatedSession();var journal=Journal();await journal.SaveAsync(new(){Character="Nuclear",Step="confirm",Pending=Submission()});
  session.OnCommand=command=>{if(command.Kind is "close" or "next")session.Set(session.Snapshot with{Dialog=null});};
  var store=new FileQuestQueueStore(Path.Combine(Path.GetTempPath(),"DAQuester-queue-"+Guid.NewGuid()));
  var queue=new QuestQueueCoordinator(store,new(){Character="Nuclear",Quests=["spare-a-stick","next"]},()=>session.Snapshot,async(id,_,ct)=>{
   if(id=="next"){Assert.Null(session.Snapshot.Dialog);return new(){Character="Nuclear",Quest=id,Step="complete",Status="Completed",CompletedAt=DateTimeOffset.UtcNow};}
   var reward=Reward(session.Snapshot,DateTimeOffset.UtcNow);session.Set(reward with{Dialog=reward.Dialog! with{HasNext=hasNext}});
   var runner=new QuestRunner(session,journal,new("spare-a-stick",1,"Stick","",[],[new("confirm","confirm","complete")]),Cal);
   await runner.RunAsync(cancellation:ct,singleAttempt:true);return runner.Job;
  },(_,_)=>Task.CompletedTask,id=>id=="spare-a-stick");
  await queue.RunAsync();Assert.Equal("Idle",queue.State.State);Assert.Empty(queue.State.Quests);Assert.Equal(2,queue.State.History.Length);
  Assert.Single(session.Commands,c=>c.Kind==(hasNext?"next":"close"));Assert.Null(session.Snapshot.Dialog);
  Assert.NotNull(journal.Load("Nuclear","spare-a-stick").NextRun);
 }
 [Theory][InlineData(false)][InlineData(true)]
 public void Completion_checks_the_calibrated_branch_name(bool consumed){
  var session=new SimulatedSession();var reply=Reward(session.Snapshot,DateTimeOffset.UtcNow) with{Revision=2};
  if(!consumed)reply=reply with{Inventory=reply.Inventory.Add(1,new(1,"Twig",6))};
  var history=new EvidenceHistory();history.Record(reply);var job=new QuestJob{Character="Nuclear",Step="confirm",Pending=Submission()};
  var observed=StickCompletionEvidence.Observe(job,Cal with{BranchName="Twig"},history);
  Assert.Equal(consumed,observed.Pending is null);Assert.Equal(consumed?"Completed":"Paused",observed.Status);
 }
 [Fact] public async Task Failed_popup_close_keeps_receipt_and_queue_resumes_without_another_turn_in(){
  var s=new SimulatedSession();var journal=Journal();await journal.SaveAsync(new(){Character="Nuclear",Step="confirm",Pending=Submission()});
  int closes=0;s.OnCommand=c=>{Assert.Equal("close",c.Kind);if(++closes==1)throw new TimeoutException("Synthetic missing close acknowledgment");s.Set(s.Snapshot with{Dialog=null});};
  var store=new FileQuestQueueStore(Path.Combine(Path.GetTempPath(),"DAQuester-queue-"+Guid.NewGuid()));
  var queue=new QuestQueueCoordinator(store,new(){Character="Nuclear",Quests=["spare-a-stick"]},()=>s.Snapshot,async(_,_,ct)=>{
   if(closes==0)s.Set(Reward(s.Snapshot,DateTimeOffset.UtcNow));
   var runner=new QuestRunner(s,journal,new("spare-a-stick",1,"Stick","",[],[new("confirm","confirm","complete")]),Cal);await runner.RunAsync(cancellation:ct,singleAttempt:true);return runner.Job;
  },(_,_)=>Task.CompletedTask,_=>true);
  await queue.RunAsync();Assert.Equal("Paused",queue.State.State);Assert.Single(queue.State.Quests);Assert.Empty(queue.State.History);
  var saved=journal.Load("Nuclear","spare-a-stick");Assert.NotNull(saved.CompletedAt);Assert.Null(saved.Pending);
  await queue.RunAsync();Assert.Equal("Idle",queue.State.State);Assert.Empty(queue.State.Quests);Assert.Single(queue.State.History);Assert.Equal(saved.CompletedAt,queue.State.History[0].At);Assert.Equal(2,closes);
 }
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Ordinary_reward_saves_next_attempt_for_both_single_and_repeat_runs(bool repeat) {
  var session=new SimulatedSession();var observed=DateTimeOffset.UtcNow.AddSeconds(-2);session.Set(Reward(session.Snapshot,observed));
  var context=new QuestContext(session,Journal(),Cal,new(){Character="Nuclear",Step="confirm",Repeat=repeat,Pending=Submission()});
  await new StickActions().ExecuteAsync(context,new("confirm","confirm",null),default);
  Assert.Equal(observed,context.Job.CompletedAt);Assert.Equal(observed.AddHours(8),context.Job.NextRun);
  Assert.Null(context.Job.Pending);Assert.Null(context.Job.PermanentCompletion);Assert.False(context.Job.Accepted);Assert.Null(context.Job.AcceptedGeneration);
 }
 sealed class InterruptedReward(SimulatedSession session,DateTimeOffset observed):IQuestAction {
  public Task ExecuteAsync(QuestContext context,QuestStep step,CancellationToken ct) {
   session.Set(Reward(session.Snapshot,observed));session.Manual();
   session.Set(session.Snapshot with{Dialog=null});ct.ThrowIfCancellationRequested();return Task.CompletedTask;
  }
 }
 sealed class LateReward(SimulatedSession session):IQuestAction {
  public Task ExecuteAsync(QuestContext context,QuestStep step,CancellationToken ct){session.Set(Reward(session.Snapshot,DateTimeOffset.UtcNow));throw new TimeoutException("Synthetic terminal evidence arrived at timeout");}
 }
 [Fact] public async Task Queue_attempt_closes_terminal_reward_found_during_final_reconciliation(){
  var s=new SimulatedSession();var journal=Journal();await journal.SaveAsync(new(){Character="Nuclear",Step="confirm",Pending=Submission()});
  s.OnCommand=c=>{Assert.Equal("close",c.Kind);s.Set(s.Snapshot with{Dialog=null});};
  var runner=new QuestRunner(s,journal,new("spare-a-stick",1,"Stick","",[],[new("confirm","confirm","complete")]),Cal);runner.Actions["confirm"]=new LateReward(s);
  await runner.RunAsync(singleAttempt:true);Assert.Equal("Completed",runner.Job.Status);Assert.Null(runner.Job.Pending);Assert.Null(s.Snapshot.Dialog);Assert.Single(s.Commands);
 }
 sealed class ManualPause(SimulatedSession session):IQuestAction {
  public Task ExecuteAsync(QuestContext context,QuestStep step,CancellationToken ct){session.Manual();ct.ThrowIfCancellationRequested();return Task.CompletedTask;}
 }
 sealed class OwnedConfirmation(TaskCompletionSource entered):IQuestAction {
  public async Task ExecuteAsync(QuestContext context,QuestStep step,CancellationToken ct){entered.SetResult();await Task.Delay(Timeout.InfiniteTimeSpan,ct);}
 }
 [Fact]
 public async Task Passive_observation_cannot_overwrite_a_live_writer_and_reconciles_when_it_releases() {
  var journal=Journal();await journal.SaveAsync(new(){Character="Nuclear",Step="confirm",Pending=Submission()});
  var session=new SimulatedSession();var runner=new QuestRunner(session,journal,new("spare-a-stick",1,"","",[],[new("confirm","confirm",null)]),Cal);
  var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);runner.Actions["confirm"]=new OwnedConfirmation(entered);
  using var stop=new CancellationTokenSource();var running=runner.RunAsync(cancellation:stop.Token);await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
  var observed=DateTimeOffset.UtcNow;var reward=Reward(session.Snapshot,observed) with{Revision=2};await runner.ReconcileAsync(reward);
  Assert.True(runner.IsRunning);Assert.Null(journal.Load("Nuclear","spare-a-stick").CompletedAt);Assert.NotNull(runner.Job.Pending);
  stop.Cancel();await running.WaitAsync(TimeSpan.FromSeconds(2));
  Assert.Equal("Completed",runner.Job.Status);Assert.Equal(observed,runner.Job.CompletedAt);Assert.Null(runner.Job.Pending);Assert.False(runner.IsRunning);Assert.Empty(session.Commands);
 }
 [Fact]
 public async Task Later_reply_and_rewards_complete_a_paused_attempt_without_commands_or_resuming() {
  var session=new SimulatedSession();session.Set(session.Snapshot with{Experience=1000,Inventory=ImmutableDictionary<byte,Item>.Empty.Add(1,new(1,"Branch",6))});
  var journal=Journal();await journal.SaveAsync(new(){Character="Nuclear",Step="confirm",Pending=Submission()});
  var recipe=new QuestDefinition("spare-a-stick",1,"Spare a Stick","",[],[new("confirm","confirm",null)]);
  var runner=new QuestRunner(session,journal,recipe,Cal);runner.Actions["confirm"]=new ManualPause(session);await runner.RunAsync();
  Assert.Equal("Paused",runner.Job.Status);Assert.NotNull(runner.Job.Pending);
  var observed=DateTimeOffset.UtcNow.AddSeconds(-1);
  session.Set(session.Snapshot with{Dialog=new(1,session.Snapshot.Revision+1,99,1,"Callough","Done",true,177,92,0,[]){ObservedAt=observed}});
  await runner.ReconcileAsync(session.Snapshot);
  Assert.True(runner.Job.Pending!.TerminalSeen);Assert.Null(runner.Job.CompletedAt);
  Assert.Equal(observed,journal.Load("Nuclear","spare-a-stick").CompletionDialog!.ObservedAt);
  session.Set(Reward(session.Snapshot,observed) with{Dialog=null});await runner.ReconcileAsync(session.Snapshot);
  Assert.Equal("Completed",runner.Job.Status);Assert.Equal(observed,runner.Job.CompletedAt);Assert.Equal(observed.AddHours(8),runner.Job.NextRun);
  Assert.Null(runner.Job.Pending);Assert.False(runner.IsRunning);Assert.Empty(session.Commands);
  var saved=runner.Job;session.Set(session.Snapshot with{Experience=5000});await runner.ReconcileAsync(session.Snapshot);
  Assert.Same(saved,runner.Job);Assert.Equal(3500,runner.Job.Rewards["experience"]);
 }
 [Fact]
 public async Task Saved_terminal_reply_reconciles_later_same_generation_rewards_after_restart() {
  var journal=Journal();var observed=DateTimeOffset.UtcNow.AddMinutes(-3);
  var pending=Submission() with{TerminalSeen=true,TerminalRevision=1};
  var completion=new Dialog(1,1,99,1,"Callough","Done",true,177,92,0,[]){ObservedAt=observed};
  await journal.SaveAsync(new(){Character="Nuclear",Step="confirm",Pending=pending,CompletionDialog=completion});
  var session=new SimulatedSession();session.Set(Reward(session.Snapshot,observed) with{Dialog=null});
  var runner=new QuestRunner(session,journal,new("spare-a-stick",1,"","",[],[new("confirm","confirm",null)]),Cal);
  await runner.RunAsync();Assert.Equal(observed,runner.Job.CompletedAt);Assert.Equal(observed.AddHours(8),runner.Job.NextRun);Assert.Null(runner.Job.Pending);Assert.Empty(session.Commands);
 }
 [Theory]
 [InlineData("generation")][InlineData("revision")][InlineData("npc")][InlineData("entity")][InlineData("inventory")]
 [InlineData("branches")][InlineData("experience")][InlineData("character")][InlineData("dialog-revision")]
 public void Partial_unrelated_or_stale_evidence_preserves_pending_transaction(string invalid) {
  var observed=DateTimeOffset.UtcNow;var transaction=Submission() with{EntityId=99};
  var snapshot=Reward(FoundationTests.Ready(),observed) with{Revision=2};
  snapshot=invalid switch {
   "generation"=>snapshot with{Generation=2,Dialog=snapshot.Dialog! with{Generation=2}},
   "revision"=>snapshot with{Revision=0,Dialog=snapshot.Dialog! with{Revision=0}},
   "npc"=>snapshot with{Dialog=snapshot.Dialog! with{Name="Cassidy"}},
   "entity"=>snapshot with{Dialog=snapshot.Dialog! with{EntityId=101}},
   "inventory"=>snapshot with{InventoryKnown=false},
   "branches"=>snapshot with{Inventory=snapshot.Inventory.Add(1,new(1,"Branch",6))},
   "experience"=>snapshot with{Experience=1000},
   "character"=>snapshot with{Name="Clock"},
   "dialog-revision"=>snapshot with{Dialog=snapshot.Dialog! with{Revision=99}},
   _=>snapshot
  };
  var history=new EvidenceHistory();history.Record(snapshot);
  var job=new QuestJob{Character="Nuclear",Step="confirm",Pending=transaction};
  var recorded=StickCompletionEvidence.Observe(job,Cal,history);
  Assert.NotNull(recorded.Pending);Assert.Null(recorded.CompletedAt);Assert.Null(recorded.NextRun);Assert.Null(recorded.PermanentCompletion);
 }
 [Fact]
 public async Task Saved_reply_cannot_confirm_reward_from_a_replacement_generation() {
  var journal=Journal();var observed=DateTimeOffset.UtcNow.AddMinutes(-1);
  await journal.SaveAsync(new(){Character="Nuclear",Step="confirm",Pending=Submission() with{TerminalSeen=true,TerminalRevision=1},CompletionDialog=new(1,1,99,1,"Callough","Done",true,177,92,0,[]){ObservedAt=observed}});
  var session=new SimulatedSession();session.Set(Reward(session.Snapshot,observed) with{Generation=2,Dialog=null});
  var runner=new QuestRunner(session,journal,new("spare-a-stick",1,"","",[],[new("confirm","confirm",null)]),Cal);
  await runner.ReconcileAsync(session.Snapshot);Assert.NotNull(runner.Job.Pending);Assert.Null(runner.Job.CompletedAt);Assert.Empty(session.Commands);
 }
 [Fact]
 public void Passive_confirmation_cannot_bypass_unvalidated_calibration() {
  var history=new EvidenceHistory();history.Record(Reward(FoundationTests.Ready(),DateTimeOffset.UtcNow) with{Revision=2});
  var job=new QuestJob{Character="Nuclear",Step="confirm",Pending=Submission()};
  Assert.Same(job,StickCompletionEvidence.Observe(job,Cal with{Validated=false},history));
 }
 [Fact]
 public async Task Resume_of_completed_single_attempt_preserves_cooldown_and_pause_does_not_erase_completion() {
  var journal=Journal();var completed=DateTimeOffset.UtcNow.AddMinutes(-1);
  await journal.SaveAsync(new(){Character="Nuclear",Step="complete",CompletedAt=completed});
  var session=new SimulatedSession();var runner=new QuestRunner(session,journal,new("spare-a-stick",1,"","",[],[]),Cal);
  await runner.RunAsync();await runner.PauseAsync();Assert.Equal("Completed",runner.Job.Status);Assert.Equal(completed.AddHours(8),runner.Job.NextRun);Assert.Empty(session.Commands);
 }
 [Fact]
 public async Task Manual_pause_keeps_already_observed_reward_and_replaces_previous_attempt_date() {
  var session=new SimulatedSession();var journal=Journal();var old=DateTimeOffset.UtcNow.AddHours(-10);var observed=DateTimeOffset.UtcNow.AddSeconds(-2);
  await journal.SaveAsync(new(){Character="Nuclear",Step="confirm",Repeat=true,CompletedAt=old,NextRun=old.AddHours(8),Pending=Submission()});
  var recipe=new QuestDefinition("spare-a-stick",1,"Spare a Stick","",[],[new("confirm","confirm",null)]);
  var runner=new QuestRunner(session,journal,recipe,Cal);runner.Actions["confirm"]=new InterruptedReward(session,observed);
  await runner.RunAsync(true);
  Assert.Equal("Completed",runner.Job.Status);Assert.Equal("complete",runner.Job.Step);Assert.Null(runner.Job.Pending);
  Assert.Equal(observed,runner.Job.CompletedAt);Assert.Equal(observed.AddHours(8),runner.Job.NextRun);Assert.False(runner.IsRunning);
  Assert.Equal(3500,runner.Job.Rewards["experience"]);Assert.Equal(6,runner.Job.Rewards["branchesConsumed"]);Assert.Empty(session.Commands);
  Assert.Equal(observed.AddHours(8),journal.Load("Nuclear","spare-a-stick").NextRun);
 }
}
