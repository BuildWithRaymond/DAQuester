using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using System.Text.Json;
using Xunit;
namespace DAQuester.Tests;
public class LiveReplayTests {
 sealed record Fixture(ReplayEvent[] Events,RecordedAction[] Actions);
 static Fixture Load()=>JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/evidence/datester-7.41/spare-a-stick.json"))))!;
 [Theory][InlineData("Nuclear",82,"Stick",false)][InlineData("Clock",103,"Oak Stick",true)]
 public void Captured_packets_prove_rewards_and_both_terminal_variants(string character,int step,string weapon,bool permanent) {
  var fixture=Load();var terminal=fixture.Events.First(e=>e.Character==character&&e.Snapshot.Dialog is {IsPursuit:true} d&&d.Step==step&&d.Revision==e.Revision);
  var action=fixture.Actions.Last(a=>a.Character==character&&a.Generation==terminal.Generation&&a.Selection=="I have those branches, here"&&a.Revision<terminal.Revision);
  var baseline=fixture.Events.Last(e=>e.Character==character&&e.Generation==action.Generation&&e.Revision==action.Revision).Snapshot;
  var transaction=new Transaction("turn-in",action.Generation,action.Revision,baseline.Gold!.Value,action.Branches,action.Experience!.Value,action.Time){InventoryBaselineKnown=true,InventoryCounts=baseline.Inventory.Values.GroupBy(i=>i.Name).ToDictionary(g=>g.Key,g=>(int)g.Sum(i=>(long)i.Quantity))};
  var tracker=new StateTracker(terminal.Generation);tracker.Identity(character,terminal.Snapshot.Id!.Value);
  foreach(var e in fixture.Events.Where(e=>e.Character==character&&e.Generation==terminal.Generation&&e.Revision<=terminal.Revision))tracker.Observe(e.Opcode,Convert.FromBase64String(e.Payload));
  Assert.Equal(terminal.Snapshot.Experience,tracker.Snapshot.Experience);Assert.Equal(terminal.Snapshot.Count("Branch"),tracker.Snapshot.Count("Branch"));
  Assert.Equal(terminal.Snapshot.Dialog!.Text,tracker.Snapshot.Dialog!.Text);Assert.Equal(177,tracker.Snapshot.Dialog.Pursuit);Assert.Equal(step,tracker.Snapshot.Dialog.Step);
  var rewards=RewardEvidence.Record(transaction,terminal.Snapshot);
  Assert.Equal(3500,rewards["experience"]);Assert.Equal(6,rewards["branchesConsumed"]);Assert.Equal(1,rewards["item:"+weapon]);Assert.Equal(1,rewards["item:Wooden Shield"]);
  Assert.Equal(permanent,PermanentQuestCompletion.Matches(terminal.Snapshot));
  var report=QuestEvidenceAnalyzer.Analyze(fixture.Events,fixture.Actions);
  Assert.Contains(report.Labels,l=>l.Character==character&&l.Generation==terminal.Generation&&l.Revision==terminal.Revision&&l.Kind=="completion");
 }
 [Fact] public void Frozen_rotation_visits_three_areas_and_keeps_unproved_area() {
  string profile=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/calibration/datester-7.41.json"));
  var cal=JsonSerializer.Deserialize<Calibration>(File.ReadAllText(profile),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
  Assert.True(cal.Ready);Assert.Contains("Nuclear",cal.ValidatedCharacters);Assert.Contains("Clock",cal.ValidatedCharacters);
  foreach(var center in new[]{new Tile(18,56),new Tile(54,43),new Tile(64,44)})Assert.Contains(cal.Patrol,t=>Math.Abs(center.X-t.X)+Math.Abs(center.Y-t.Y)<=3);
  Assert.Equal(new Tile(18,56),TreePatrol.Candidates[0].Center);
 }
}
