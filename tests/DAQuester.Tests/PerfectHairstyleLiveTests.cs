using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using Xunit;
namespace DAQuester.Tests;
public class PerfectHairstyleLiveTests {
 public sealed record Fixture(ReplayEvent[] Events,RecordedAction[] Actions);
 public static Fixture Load()=>JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/evidence/datester-7.41/perfect-hairstyle.json"))))!;
 static QuestDefinition Recipe()=>JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/recipes/perfect-hairstyle.v1.json"))),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
 static FileQuestJournal Journal()=>new(Path.Combine(Path.GetTempPath(),"DAQuester-hair-"+Guid.NewGuid()));
 public sealed class ReplaySession : IGameSession {
  readonly Fixture fixture=Load(); readonly EvidenceHistory history=new(); int index;
  public CharacterSnapshot Snapshot {get;private set;}
  public IReadOnlySet<string> Capabilities {get;}=new HashSet<string>{"navigate","dialog","inventory","legend"};
  public event Action<CharacterSnapshot>? Changed;
  public event Action<string>? Interrupted {add{} remove{}}
  public List<GameCommand> Commands {get;}=[];
  public ReplaySession(bool completed=false) {
   Snapshot=fixture.Events.First(e=>e.Revision==830).Snapshot with{Dialog=null};history.Record(Snapshot);
   if(completed)foreach(var e in fixture.Events.Where(e=>e.Revision>=830)){Snapshot=e.Snapshot;history.Record(Snapshot);}
  }
  public Task ExecuteAsync(GameCommand c,CancellationToken ct) {
   ct.ThrowIfCancellationRequested();Commands.Add(c);
   if(c.Kind=="visit") {Assert.Equal(950,c.Map);return Task.CompletedTask;}
   if(c.Kind=="inspect-self") {Snapshot=Snapshot with{Revision=Snapshot.Revision+1,LegendsKnown=true,LegendsRevision=Snapshot.Revision+1};history.Record(Snapshot);return Task.CompletedTask;}
   if(c.Kind=="choose"||c.Kind=="next") {
    var manual=fixture.Actions.Single(a=>a.Revision==Snapshot.Dialog!.Revision);
    Assert.Equal(manual.Kind,c.Kind);Assert.Equal(manual.Selection,c.Text);
    if(c.Kind=="choose")DialogPackets.Choice(Snapshot,c.Dialog!,c.Text!);else DialogPackets.Continue(Snapshot,c.Dialog!);
   }else Assert.Equal("open",c.Kind);
   var responses=fixture.Events.Where(e=>e.Revision>=830).ToArray();
   do {Assert.True(index<responses.Length,"Replay response exhausted");Snapshot=responses[index++].Snapshot;history.Record(Snapshot);Changed?.Invoke(Snapshot);}while(responses[index-1].Opcode is not (47 or 48));
   return Task.CompletedTask;
  }
  public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot,bool> predicate,long after,TimeSpan timeout,CancellationToken ct) {
   ct.ThrowIfCancellationRequested();return history.Find(Snapshot.Generation,after,predicate) is {} s?Task.FromResult(s):Task.FromException<CharacterSnapshot>(new TimeoutException("Missing replay evidence"));
  }
 }
 [Fact] public async Task Actual_next_pages_complete_ponytail_without_materials_or_manual_help() {
  var s=new ReplaySession();var runner=new QuestRunner(s,Journal(),Recipe(),new());await HairstyleScheduleTests.RunAttempt(runner);
  Assert.NotNull(runner.Job.LastAttemptAt);Assert.Null(runner.Job.CompletedAt);Assert.Null(runner.Job.Pending);Assert.Equal(15000,runner.Job.Rewards["experience"]);Assert.Equal(1,runner.Job.Rewards["item:Emerald Ring"]);
  Assert.False(runner.Job.LegendReward);Assert.Equal(5,s.Snapshot.Level);Assert.Null(s.Snapshot.Dialog);
  Assert.Equal(5,s.Commands.Count(c=>c.Kind=="next"));Assert.Single(s.Commands,c=>c.Text=="Yes I did");Assert.DoesNotContain(s.Commands,c=>c.Kind is "give" or "use-item");
 }
 [Fact] public async Task Paused_confirm_reconciles_captured_terminal_after_manual_next_without_resubmitting() {
  var f=Load();var baseline=f.Events.Single(e=>e.Revision==845).Snapshot;var j=Journal();
  await j.SaveAsync(new(){Character="Nuclear",Quest="perfect-hairstyle",Step="confirm",Pending=new("hairstyle-turn-in",baseline.Generation,baseline.Revision,0,0,baseline.Experience!.Value,DateTimeOffset.UtcNow){EntityId=baseline.Dialog!.EntityId,InventoryBaselineKnown=true,InventoryCounts=[],Legends=baseline.Legends.ToArray()}});
  var s=new ReplaySession(true);var r=new QuestRunner(s,j,Recipe(),new());await HairstyleScheduleTests.RunAttempt(r);Assert.NotNull(r.Job.LastAttemptAt);Assert.Null(r.Job.CompletedAt);Assert.Equal(15000,r.Job.Rewards["experience"]);Assert.DoesNotContain(s.Commands,c=>c.Kind is "choose" or "next" or "visit");
 }
 [Fact] public void Captured_wire_parses_true_next_terminal_and_actual_rewards() {
  var f=Load();var tracker=new StateTracker(f.Events[0].Generation);tracker.Identity("Nuclear",1291344);
  foreach(var e in f.Events.Where(e=>e.Revision>=830&&e.Revision<=855))tracker.Observe(e.Opcode,Convert.FromBase64String(e.Payload));
  Assert.Equal(18801u,tracker.Snapshot.Experience);Assert.Equal(5,tracker.Snapshot.Level);Assert.Equal(1,tracker.Snapshot.Count("Emerald Ring"));
  var terminal=tracker.Snapshot.Dialog!;Assert.True(terminal.HasNext);Assert.Equal(11107u,terminal.EntityId);Assert.Equal(0,terminal.Pursuit);Assert.Equal("Excellent! Thank you so much! I couldn't have done it without your help. Take this as my thanks!",terminal.Text);
 }
 [Fact] public void Automated_clock_fixture_proves_one_submission_and_saved_weekly_deadline_without_legend() {
  string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/evidence/datester-7.41/perfect-hairstyle-clock.json"));var f=JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path))!;
  var action=Assert.Single(f.Actions,a=>a.Selection=="Yes I did");var baseline=f.Events.Single(e=>e.Revision==action.Revision).Snapshot;
  var terminal=f.Events.Single(e=>e.Revision==844);var t=new Transaction("hairstyle-turn-in",action.Generation,action.Revision,baseline.Gold!.Value,baseline.Count("Branch"),baseline.Experience!.Value,action.Time){EntityId=action.Dialog!.EntityId,InventoryBaselineKnown=true,InventoryCounts=baseline.Inventory.Values.ToDictionary(i=>i.Name,i=>(int)i.Quantity)};
  Assert.True(PerfectHairstyleEvidence.FreshReply(terminal.Snapshot,t));Assert.True(PerfectHairstyleEvidence.Rewards(terminal.Snapshot,t));Assert.Equal(0,baseline.Count("Spider's Silk"));
  var rewards=RewardEvidence.Record(t,terminal.Snapshot);Assert.Equal(15000,rewards["experience"]);Assert.Equal(1,rewards["item:Emerald Ring"]);Assert.DoesNotContain("item:Wooden Shield",rewards.Keys);
  Assert.Equal(5,f.Actions.Count(a=>a.Kind=="next"&&a.Dialog?.Name=="Eeva"));Assert.Single(f.Actions,a=>a.Kind=="next"&&a.Dialog?.EntityType==4);
  using var json=JsonDocument.Parse(File.ReadAllText(path));var observation=JsonSerializer.Deserialize<CharacterSnapshot>(json.RootElement.GetProperty("FinalOwnLegendObservation"),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
  Assert.True(observation.LegendsKnown);Assert.False(PerfectHairstyleEvidence.HasLegend(observation));Assert.Equal(terminal.Snapshot.Dialog!.ObservedAt!.Value.AddDays(7),json.RootElement.GetProperty("NextRun").GetDateTimeOffset());
 }
}
