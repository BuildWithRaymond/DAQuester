using Xunit;
using DAQuester.Core;
using System.Collections.Immutable;
namespace DAQuester.Tests;
public class SimulatedSession : IGameSession {
 public CharacterSnapshot Snapshot {get;private set;}=FoundationTests.Ready();
 public IReadOnlySet<string> Capabilities {get;}=new HashSet<string>{"navigate","dialog","inventory","bank","walk-trigger"};
 public event Action<CharacterSnapshot>? Changed;
 public event Action<string>? Interrupted;
 public List<GameCommand> Commands {get;}=[];
 public Action<GameCommand>? OnCommand;
 public void Set(CharacterSnapshot s) {Snapshot=s with{Revision=Snapshot.Revision+1};Changed?.Invoke(Snapshot);}
 public void Manual()=>Interrupted?.Invoke("Manual input");
 public Task ExecuteAsync(GameCommand c,CancellationToken t) {t.ThrowIfCancellationRequested();Commands.Add(c);OnCommand?.Invoke(c);return Task.CompletedTask;}
 public async Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot,bool> test,long after,TimeSpan timeout,CancellationToken token) {
  var end=DateTimeOffset.UtcNow.AddMilliseconds(100);
  while(DateTimeOffset.UtcNow<end){token.ThrowIfCancellationRequested();if(Snapshot.Revision>after&&test(Snapshot))return Snapshot;await Task.Delay(2,token);}
  throw new TimeoutException("Simulation evidence timeout");
 }
}
public class QuestTests {
 readonly string dir=Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString());
 static readonly Calibration calibrated=new(){Validated=true,Patrol=[new(22,22)],Accepted=["Accepted"],Completed=["Done"],Refused=["No"],Cooldown=["Later"],EvidenceFiles=["simulated"]};
 static CharacterSnapshot Branches(CharacterSnapshot s,int count)=>s with{Inventory=s.Inventory.SetItem(1,new(1,"Branch",(uint)count))};
 [Fact] public async Task Gather_cannot_bypass_calibration_when_restored_mid_quest() {
  var s=new SimulatedSession();s.OnCommand=_=>s.Set(Branches(s.Snapshot,6));
  var c=new QuestContext(s,new FileQuestJournal(dir),new(){Patrol=[new(22,22)]},new(){Character="Nuclear",Accepted=true,Step="gather"});
  var blocked=await Assert.ThrowsAsync<QuestBlockedException>(()=>new StickActions().ExecuteAsync(c,new("gather","gather",null),default));
  Assert.Contains("calibration",blocked.Message,StringComparison.OrdinalIgnoreCase);Assert.Empty(s.Commands);
 }
 [Fact] public async Task Journal_survives_restart_and_restores_paused() {
  var j=new FileQuestJournal(dir);await j.SaveAsync(new(){Character="Nuclear",Status="Running",Step="gather",Accepted=true,CompletedAt=DateTimeOffset.UtcNow,Pending=new("bank",1,9,1001,0,0,DateTimeOffset.UtcNow)});
  var r=new FileQuestJournal(dir).Load("Nuclear","spare-a-stick");Assert.Equal("Paused",r.Status);Assert.Equal("gather",r.Step);Assert.Equal("bank",r.Pending?.Kind);Assert.NotNull(r.CompletedAt);
 }
 [Fact] public async Task Ambiguous_bank_never_sends_twice() {
  var s=new SimulatedSession();s.Set(s.Snapshot with{Gold=1001});
  s.OnCommand=x=>{if(x.Kind=="prepare-deposit")s.Set(s.Snapshot with{Dialog=new(1,s.Snapshot.Revision+1,7015,1,"Cassidy","Deposit amount?",false,82,0,2,[])});};
  var c=new QuestContext(s,new FileQuestJournal(dir),calibrated,new(){Character="Nuclear"});
  await Assert.ThrowsAnyAsync<Exception>(()=>new StickActions().ExecuteAsync(c,new("bank","bank",null),default));
  int sent=s.Commands.Count(x=>x.Kind=="deposit");
  await Assert.ThrowsAnyAsync<Exception>(()=>new StickActions().ExecuteAsync(c,new("bank","bank",null),default));
  Assert.Equal(1,sent);Assert.Equal(sent,s.Commands.Count(x=>x.Kind=="deposit"));
 }
 [Theory][InlineData(0)][InlineData(3)][InlineData(6)]
 public async Task Gather_only_missing_branches(int start) {
  var s=new SimulatedSession();s.Set(Branches(s.Snapshot,start));s.OnCommand=c=>{if(c.Kind=="navigate")s.Set(Branches(s.Snapshot,6));};
  var c=new QuestContext(s,new FileQuestJournal(dir),calibrated,new(){Character="Nuclear",Accepted=true});
  await new StickActions().ExecuteAsync(c,new("gather","gather",null),default);
  Assert.Equal(start==6?0:1,s.Commands.Count);Assert.True(s.Snapshot.Count("Branch")>=6);
 }
 [Fact] public async Task Existing_rewards_cannot_prove_completion() {
  var s=new SimulatedSession();s.Set(Branches(s.Snapshot,6) with{Experience=3500,Inventory=Branches(s.Snapshot,6).Inventory.Add(2,new(2,"Stick",1)).Add(3,new(3,"Wooden Shield",1))});
  var c=new QuestContext(s,new FileQuestJournal(dir),calibrated,new(){Character="Nuclear",Pending=new("turn-in",1,s.Snapshot.Revision,999,6,3500,DateTimeOffset.UtcNow)});
  await Assert.ThrowsAnyAsync<Exception>(()=>new StickActions().ExecuteAsync(c,new("confirm","confirm",null),default));
  Assert.Null(c.Job.CompletedAt);
 }
 [Fact] public async Task Completion_survives_level_gain_and_optional_reward_absence() {
  var s=new SimulatedSession();s.Set(Branches(s.Snapshot,0) with{Level=12,Experience=7000,Dialog=new(1,2,91,1,"Callough","Done",true,1,2,0,[])});
  var c=new QuestContext(s,new FileQuestJournal(dir),calibrated,new(){Character="Nuclear",Pending=new("turn-in",1,0,999,6,3500,DateTimeOffset.UtcNow)});
  await new StickActions().ExecuteAsync(c,new("confirm","confirm",null),default);
  Assert.NotNull(c.Job.CompletedAt);Assert.Null(c.Job.Pending);
 }
}
