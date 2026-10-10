using Xunit;
using DAQuester.Core;
namespace DAQuester.Tests;
public class RunnerTests {
 sealed class CallbackAction(Action<QuestContext> action):IQuestAction {public Task ExecuteAsync(QuestContext c,QuestStep step,CancellationToken ct){action(c);return Task.CompletedTask;}}
 [Fact] public async Task Connection_scoped_activation_restarts_before_resumed_gathering() {
  var session=new SimulatedSession();session.Set(session.Snapshot with{Generation=2});var journal=Journal();
  await journal.SaveAsync(new(){Character="Nuclear",Accepted=true,AcceptedGeneration=1,Step="gather"});
  var recipe=Recipe(new QuestStep("accept","accept","gather",new(){{"activationScope","connection"}}),new QuestStep("gather","gather",null));
  var runner=new QuestRunner(session,journal,recipe,new());var calls=new List<string>();
  runner.Actions["accept"]=new CallbackAction(_=>calls.Add("accept"));
  runner.Actions["gather"]=new CallbackAction(_=>{calls.Add("gather");throw new QuestBlockedException("Test ends after activation");});
  await runner.RunAsync();Assert.Equal(new[]{"accept","gather"},calls);
 }
 static QuestDefinition Recipe(params QuestStep[] steps)=>new("spare-a-stick",1,"Spare a Stick","",["inventory"],steps);
 static FileQuestJournal Journal()=>new(Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString()));
 static QuestJob CompletedAttempt()=>new(){Character="Nuclear",Step="complete",Status="Completed",CompletedAt=DateTimeOffset.UtcNow.AddHours(-9),AcceptedGeneration=1,Rewards=new(){{"experience",3500}}};
 [Fact] public async Task Explicit_start_reopens_a_completed_single_attempt_without_enabling_continuous_repeat() {
  var journal=Journal();await journal.SaveAsync(CompletedAttempt());
  var runner=new QuestRunner(new SimulatedSession(),journal,Recipe(new QuestStep("preflight","preflight",null)),new());QuestJob? checkpoint=null;
  runner.Actions["preflight"]=new CallbackAction(c=>{checkpoint=c.Job;throw new QuestBlockedException("Reached new attempt");});
  await runner.RunAsync(startNewAttempt:true);
  Assert.NotNull(checkpoint);Assert.False(checkpoint.Repeat);Assert.False(checkpoint.Accepted);Assert.Null(checkpoint.AcceptedGeneration);
  Assert.Equal("Reached new attempt",runner.Job.Reason);Assert.Equal(3500,runner.Job.Rewards["experience"]);
 }
 [Fact] public async Task Resume_does_not_silently_restart_a_completed_single_attempt() {
  var journal=Journal();await journal.SaveAsync(CompletedAttempt());
  var runner=new QuestRunner(new SimulatedSession(),journal,Recipe(new QuestStep("preflight","preflight",null)),new());bool called=false;
  runner.Actions["preflight"]=new CallbackAction(_=>called=true);await runner.RunAsync();
  Assert.False(called);Assert.Equal("Completed",runner.Job.Status);
 }
 [Fact] public async Task Repeat_after_single_attempt_restores_eight_hour_cooldown_even_without_saved_schedule() {
  var journal=Journal();var completed=CompletedAttempt() with{CompletedAt=DateTimeOffset.UtcNow.AddHours(-1)};await journal.SaveAsync(completed);
  var runner=new QuestRunner(new SimulatedSession(),journal,Recipe(new QuestStep("preflight","preflight",null)),new());bool called=false;QuestJob? scheduled=null;
  using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(3));
  runner.Changed+=job=>{if(job.Status=="Scheduled"){scheduled=job;stop.Cancel();}};
  runner.Actions["preflight"]=new CallbackAction(_=>{called=true;throw new QuestBlockedException("Cooldown bypassed");});
  await runner.RunAsync(true,stop.Token,startNewAttempt:true);
  Assert.False(called);Assert.NotNull(scheduled);Assert.Equal(completed.CompletedAt!.Value.AddHours(8),scheduled.NextRun);
 }
 [Fact] public async Task Single_new_attempt_before_cooldown_pauses_without_submitting() {
  var journal=Journal();await journal.SaveAsync(CompletedAttempt() with{CompletedAt=DateTimeOffset.UtcNow.AddHours(-1)});
  var runner=new QuestRunner(new SimulatedSession(),journal,Recipe(new QuestStep("preflight","preflight",null)),new());bool called=false;
  runner.Actions["preflight"]=new CallbackAction(_=>called=true);await runner.RunAsync(startNewAttempt:true);
  Assert.False(called);Assert.Equal("Paused",runner.Job.Status);Assert.Contains("cooldown",runner.Job.Reason,StringComparison.OrdinalIgnoreCase);
 }
 [Fact] public async Task New_attempt_cannot_discard_an_unreconciled_transaction() {
  var journal=Journal();var pending=new Transaction("turn-in",1,10,0,6,0,DateTimeOffset.UtcNow);
  await journal.SaveAsync(CompletedAttempt() with{Pending=pending});
  var runner=new QuestRunner(new SimulatedSession(),journal,Recipe(new QuestStep("preflight","preflight",null)),new());bool called=false;
  runner.Actions["preflight"]=new CallbackAction(_=>called=true);await runner.RunAsync(startNewAttempt:true);
  Assert.False(called);Assert.Equal(System.Text.Json.JsonSerializer.Serialize(pending),System.Text.Json.JsonSerializer.Serialize(runner.Job.Pending));Assert.Equal("complete",runner.Job.Step);Assert.Contains("reconcil",runner.Job.Reason,StringComparison.OrdinalIgnoreCase);
 }
 [Fact] public async Task Explicit_start_never_reopens_a_nonrepeatable_completed_quest() {
  var journal=Journal();await journal.SaveAsync(CompletedAttempt());
  var runner=new QuestRunner(new SimulatedSession(),journal,Recipe(new QuestStep("preflight","preflight",null)) with{Repeatable=false},new());bool called=false;
  runner.Actions["preflight"]=new CallbackAction(_=>called=true);await runner.RunAsync(startNewAttempt:true);
  Assert.False(called);Assert.Equal("Completed",runner.Job.Status);
 }
 [Fact] public async Task Final_notification_releases_live_ownership_even_when_saved_status_was_running() {
  var journal=Journal();await journal.SaveAsync(new(){Character="Nuclear",Status="Running"});
  var runner=new QuestRunner(new SimulatedSession(),journal,Recipe(new QuestStep("preflight","preflight",null)),new());Assert.False(runner.IsRunning);
  var ownership=new List<bool>();runner.Changed+=_=>ownership.Add(runner.IsRunning);
  runner.Actions["preflight"]=new CallbackAction(_=>throw new QuestBlockedException("Test pause"));await runner.RunAsync();
  Assert.False(runner.IsRunning);Assert.True(ownership.First());Assert.False(ownership.Last());
 }
 [Theory][InlineData(1)][InlineData(6)]
 public async Task Fresh_attempt_accepts_at_Callough_before_using_carried_branches(int branches) {
  var session=new SimulatedSession();session.Set(session.Snapshot with{Experience=0,Inventory=session.Snapshot.Inventory.SetItem(1,new(1,"Branch",(uint)branches))});
  Dialog Reply(string text,params DialogChoice[] choices)=>new(1,session.Snapshot.Revision+1,99,1,"Callough",text,true,177,1,0,System.Collections.Immutable.ImmutableArray.Create(choices));
  session.OnCommand=c=>{
   if(c.Kind=="open")session.Set(session.Snapshot with{Dialog=Reply("Welcome",new DialogChoice("Spare a Stick",177,1))});
   if(c.Kind=="choose"&&c.Text=="Spare a Stick")session.Set(session.Snapshot with{Dialog=Reply("Help?",new DialogChoice("I'll gather them now",177,1))});
   if(c.Kind=="choose"&&c.Text=="I'll gather them now")session.Set(session.Snapshot with{Dialog=Reply("Accepted")});
   if(c.Kind=="close")session.Set(session.Snapshot with{Dialog=null});
   if(c.Kind=="navigate")session.Set(session.Snapshot with{Inventory=session.Snapshot.Inventory.SetItem(1,new(1,"Branch",6))});
  };
  var cal=new Calibration{Validated=true,Patrol=[new(40,40)],Accepted=["Accepted"],Completed=["Done"],EvidenceFiles=["simulated"]};
  var runner=new QuestRunner(session,Journal(),Recipe(new("preflight","preflight","bank"),new("bank","bank","accept"),new("accept","accept","gather"),new("gather","gather","turn-in"),new("turn-in","turn-in",null)),cal);
  runner.Actions["turn-in"]=new CallbackAction(_=>throw new QuestBlockedException("Test reached turn-in"));await runner.RunAsync();
  Assert.Equal("Test reached turn-in",runner.Job.Reason);
  Assert.Contains(session.Commands,c=>c.Kind=="visit"&&c.Text=="Callough");
  int accepted=session.Commands.FindIndex(c=>c.Kind=="choose"&&c.Text=="I'll gather them now");Assert.True(accepted>=0);
  int gathered=session.Commands.FindIndex(c=>c.Kind=="navigate");Assert.True(gathered<0||accepted<gathered);
 }
 [Fact] public async Task Unsupported_capability_blocks_before_any_command() {
  var s=new SimulatedSession();var r=new QuestRunner(s,Journal(),Recipe(new QuestStep("preflight","preflight",null)) with{Capabilities=["party"]},new());
  await r.RunAsync();Assert.Equal("Paused",r.Job.Status);Assert.Contains("party",r.Job.Reason);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Unvalidated_recipe_never_sends_packet() {
  var s=new SimulatedSession();var r=new QuestRunner(s,Journal(),Recipe(new QuestStep("preflight","preflight",null)),new());
  await r.RunAsync();Assert.Contains("calibration",r.Job.Reason);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Concurrent_characters_do_not_share_journals() {
  var a=new SimulatedSession();var b=new SimulatedSession();b.Set(b.Snapshot with{Name="Clock"});
  var j=Journal();var recipe=Recipe(new QuestStep("gather","gather",null));
  var r1=new QuestRunner(a,j,recipe,new());var r2=new QuestRunner(b,j,recipe,new());
  await Task.WhenAll(r1.RunAsync(),r2.RunAsync());
  Assert.Equal("Nuclear",r1.Job.Character);Assert.Equal("Clock",r2.Job.Character);
  Assert.Equal("Nuclear",j.Load("Nuclear","spare-a-stick").Character);
 }
}
