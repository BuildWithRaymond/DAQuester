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
