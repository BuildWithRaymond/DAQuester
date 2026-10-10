using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Immutable;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using Xunit;
namespace DAQuester.Tests;
public class MundaneBirthTests {
 sealed record Fixture(ReplayEvent[] Events,RecordedAction[] Actions);
 static string Content(string path)=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content",path));
 static Fixture Capture()=>JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Content("evidence/datester-7.41/mundane-birth.json")))!;
 static QuestDefinition Recipe()=>JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Content("recipes/mundane-birth.v1.json")))!;
 static FileQuestJournal Journal()=>new(Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString()));
 static QuestJob Job(string name="Nuclear",string season="Winter",int age=12,string town="Undine")=>JsonSerializer.Deserialize<QuestJob>(JsonSerializer.Serialize(new{Character=name,Quest="mundane-birth",BirthOptions=new{Season=season,Age=age,Town=town}}))!;
 static void Notice(SimulatedSession s,string text="She winks, \"It's on your legend now\"",byte type=3,long? generation=null,long? revision=null) {
  var json=JsonSerializer.SerializeToNode(s.Snapshot)!;
  json["Notice"]=JsonSerializer.SerializeToNode(new{Generation=generation??s.Snapshot.Generation,Revision=revision??s.Snapshot.Revision+1,Type=type,Text=text,ObservedAt=DateTimeOffset.UtcNow});
  json["Dialog"]=null;s.Set(json.Deserialize<CharacterSnapshot>()!);
 }
 static SimulatedSession Session(Action<SimulatedSession>? completion=null,bool already=false) {
  var f=Capture();var s=new SimulatedSession();((HashSet<string>)s.Capabilities).Add("status-message");
  void Reply(long revision) {
   var d=f.Events.Single(e=>e.Revision==revision).Snapshot.Dialog!;
   s.Set(s.Snapshot with{Map=136,Dialog=d with{Generation=s.Snapshot.Generation,Revision=s.Snapshot.Revision+1,EntityId=98765,Choices=d.Choices.Reverse().Select((x,i)=>x with{Option=(byte)(i+1)}).ToImmutableArray()}});
  }
  s.OnCommand=c=>{
   if(c.Kind=="open")Reply(701);
   if(c.Kind=="next")Reply(709);
   if(c.Kind=="input")Reply(749);
   if(c.Kind=="choose") {
    if(c.Text=="Mundane Birth")Reply(already?783:707);
    else if(c.Text=="I have investigated my past")Reply(713);
    else if(c.Text=="I am committed to this")Reply(716);
    else if(new[]{"Spring","Summer","Fall","Winter"}.Contains(c.Text))Reply(726);
    else if(new[]{"Abel","Mileth","Piet","Rucesion","Suomi","Undine"}.Contains(c.Text)) {if(completion is null)Notice(s);else completion(s);}
   }
  };return s;
 }
 public static IEnumerable<object[]> Choices(){foreach(var season in new[]{"Spring","Summer","Fall","Winter"})foreach(var town in new[]{"Abel","Mileth","Piet","Rucesion","Suomi","Undine"})foreach(int age in new[]{12,40})yield return [season,age,town];}
 public static IEnumerable<object[]> Ages()=>Enumerable.Range(12,29).Select(age=>new object[]{age});
 [Theory][MemberData(nameof(Ages))]
 public async Task Every_whole_number_between_12_and_40_is_sent_unchanged(int age) {
  var s=Session();var j=Journal();await j.SaveAsync(Job(age:age));var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Completed",r.Job.Status);Assert.Equal(age.ToString(),s.Commands.Single(c=>c.Kind=="input").Text);
 }
 [Theory][MemberData(nameof(Choices))]
 public async Task Captured_dialog_flow_uses_player_choices_and_verifies_status(string season,int age,string town) {
  var s=Session();var j=Journal();await j.SaveAsync(Job(season:season,age:age,town:town));var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Completed",r.Job.Status);Assert.NotNull(r.Job.CompletedAt);Assert.Null(r.Job.Pending);
  Assert.Equal(new[]{"Mundane Birth","I have investigated my past","I am committed to this",season,town},s.Commands.Where(c=>c.Kind=="choose").Select(c=>c.Text));
  Assert.Equal(age.ToString(),s.Commands.Single(c=>c.Kind=="input").Text);Assert.Equal(136,s.Commands.Single(c=>c.Kind=="visit").Map);
  Assert.Contains("legend now",r.Job.Reason);Assert.Contains(season,JsonSerializer.Serialize(j.Load("Nuclear","mundane-birth")));
 }
 [Theory][InlineData("Winter",11,"Undine")][InlineData("Winter",41,"Undine")][InlineData("Monsoon",12,"Undine")][InlineData("Winter",12,"Loures")]
 public async Task Invalid_player_choices_never_send_game_commands(string season,int age,string town) {
  var s=Session();var j=Journal();await j.SaveAsync(Job(season:season,age:age,town:town));var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.Empty(s.Commands);Assert.Contains("choice",r.Job.Reason,StringComparison.OrdinalIgnoreCase);
 }
 [Fact] public async Task Missing_choices_block_before_walking() {
  var s=Session();var r=new QuestRunner(s,Journal(),Recipe(),new());await r.RunAsync();Assert.Empty(s.Commands);Assert.Contains("choice",r.Job.Reason,StringComparison.OrdinalIgnoreCase);
 }
 [Fact] public async Task Choices_survive_restart_under_separate_quest_journal() {
  var j=Journal();await j.SaveAsync(Job(season:"Fall",age:40,town:"Piet"));await j.SaveAsync(new(){Character="Nuclear",CompletedAt=DateTimeOffset.UtcNow,Step="complete"});
  string json=JsonSerializer.Serialize(j.Load("Nuclear","mundane-birth"));Assert.Contains("Fall",json);Assert.Contains("Piet",json);Assert.Contains("40",json);Assert.Null(j.Load("Nuclear","mundane-birth").CompletedAt);Assert.NotNull(j.Load("Nuclear","spare-a-stick").CompletedAt);
 }
 [Fact] public async Task Existing_birth_stops_before_experience_costing_rewrite() {
  var s=Session(already:true);var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.Contains("already",r.Job.Reason,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(s.Commands,c=>c.Text=="I am changing my birth!");Assert.Null(r.Job.Pending);
 }
 [Fact] public async Task Birth_has_no_stick_level_gold_capacity_or_permanent_outcome_rules() {
  var s=Session();s.Set(s.Snapshot with{Level=99,Gold=12345,InventoryKnown=false,Weight=null,MaxWeight=null,Dialog=new(1,s.Snapshot.Revision+1,7012,1,"Callough","You remind me of my own little child.",true,177,103,0,[])});
  var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.DoesNotContain(s.Commands,c=>c.Kind=="deposit");Assert.Null(r.Job.PermanentCompletion);
 }
 [Theory][InlineData("unrelated",3,1,999)][InlineData("She winks, \"It's on your legend now\"",0,1,999)][InlineData("She winks, \"It's on your legend now\"",3,2,999)][InlineData("She winks, \"It's on your legend now\"",3,1,0)]
 public async Task Unrelated_chat_stale_or_wrong_generation_cannot_confirm_or_resend(string text,byte type,long generation,long revision) {
  var s=Session(x=>Notice(x,text,type,generation,revision));var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.NotNull(r.Job.Pending);Assert.Null(r.Job.CompletedAt);int count=s.Commands.Count;await r.RunAsync();Assert.Equal(count,s.Commands.Count);Assert.Null(r.Job.CompletedAt);
 }
 [Fact] public async Task Reconnect_with_pending_final_town_never_restarts_dialog() {
  var s=Session(x=>Notice(x,"unrelated"));var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.NotNull(r.Job.Pending);
  int count=s.Commands.Count;s.Set(s.Snapshot with{Generation=2,Dialog=null});await r.RunAsync();Assert.Equal(count,s.Commands.Count);Assert.Contains("reconcil",r.Job.Reason,StringComparison.OrdinalIgnoreCase);
 }
 [Fact] public async Task Post_submission_failure_reconciles_even_when_legend_is_already_written() {
  var s=Session(x=>{Notice(x);x.Set(x.Snapshot with{Legends=ImmutableArray.Create("Undine mundane birth - Winter, Deoch 25")});throw new IOException("Connection action failed after server completion");});
  var j=Journal();await j.SaveAsync(Job());var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();Assert.Equal("riona",r.Job.Step);Assert.NotNull(r.Job.Pending);
  int count=s.Commands.Count;await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Null(r.Job.Pending);Assert.Equal(count,s.Commands.Count);
 }
 [Fact] public async Task Manual_input_pauses_then_safe_dialog_restart_uses_saved_choices() {
  var s=Session();var normal=s.OnCommand;s.OnCommand=c=>{normal!(c);if(c.Text=="Fall")s.Manual();};var j=Journal();await j.SaveAsync(Job(season:"Fall",age:40,town:"Piet"));var r=new QuestRunner(s,j,Recipe(),new());await r.RunAsync();
  Assert.Equal("Paused",r.Job.Status);Assert.DoesNotContain(s.Commands,c=>c.Kind=="input");s.OnCommand=normal;await r.RunAsync();Assert.Equal("Completed",r.Job.Status);Assert.Equal(2,s.Commands.Count(c=>c.Kind=="open"));
 }
 [Fact] public async Task Completion_never_repeats_and_characters_do_not_share_choices() {
  var j=Journal();await j.SaveAsync(Job());await j.SaveAsync(Job("Clock","Summer",40,"Mileth"));var a=Session();var b=Session();b.Set(b.Snapshot with{Name="Clock"});
  var r1=new QuestRunner(a,j,Recipe(),new());var r2=new QuestRunner(b,j,Recipe(),new());await Task.WhenAll(r1.RunAsync(true),r2.RunAsync(true));await r1.RunAsync(true);
  Assert.Equal("Completed",r1.Job.Status);Assert.Equal("Completed",r2.Job.Status);Assert.False(r1.Job.Repeat);Assert.Null(r1.Job.NextRun);Assert.Single(a.Commands,c=>c.Text=="Undine");Assert.Single(b.Commands,c=>c.Text=="Mileth");
 }
}
