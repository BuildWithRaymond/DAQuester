using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class CharacterMemoryTests : IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString("N"));
 FileCharacterMemory Store(string profile="test")=>new(directory,profile);
 static CharacterSnapshot Own(long generation=1,long revision=10)=>new(){Name="Clock",Id=42,Connected=true,Generation=generation,Revision=revision,Level=99,LegendsKnown=true,LegendsRevision=revision,Legends=ImmutableArray.Create("Entered Muisir Village - Deoch 223, Spring","Loved by Suomi Mundanes")};

 [Fact] public async Task Observed_level_and_all_own_marks_survive_restart_and_name_casing(){
  await Store().ObserveAsync("Clock",Own());
  var saved=Store().Load("CLOCK");
  Assert.Equal(99,saved.Level);Assert.Equal(Own().Legends,saved.Legends);Assert.NotNull(saved.LegendsObservedAt);
  Assert.NotNull(saved.SelectionRestriction("spare-a-stick"));Assert.Contains("already",saved.SelectionRestriction("entered-muisir-village")!.Reason,StringComparison.OrdinalIgnoreCase);
 }
 [Fact] public async Task Memory_is_isolated_by_character_and_server_profile(){
  await Store().ObserveAsync("Clock",Own());
  Assert.Null(Store().Load("Other").Level);Assert.Empty(Store().Load("Other").Legends);
  Assert.Null(Store("another-server").Load("Clock").Level);Assert.Empty(Store("another-server").Load("Clock").Legends);
 }
 [Theory][InlineData(false,"Clock",42u)][InlineData(true,"Other",42u)][InlineData(true,"Clock",null)]
 public async Task Disconnected_foreign_or_unidentified_observations_are_not_saved(bool connected,string name,uint? id){
  var store=Store();await store.ObserveAsync("Clock",Own() with{Connected=connected,Name=name,Id=id});
  Assert.Null(store.Load("Clock").Level);Assert.Empty(store.Load("Clock").Legends);
 }
 [Theory][InlineData(false,10)][InlineData(true,0)][InlineData(true,11)]
 public async Task Unknown_or_invalid_own_legend_revision_cannot_replace_memory(bool known,long legendRevision){
  var store=Store();await store.ObserveAsync("Clock",Own());
  await store.ObserveAsync("Clock",Own(2) with{Level=null,LegendsKnown=known,LegendsRevision=legendRevision,Legends=[]});
  Assert.Equal(99,store.Load("Clock").Level);Assert.Equal(Own().Legends,store.Load("Clock").Legends);
 }
 [Fact] public async Task Older_snapshot_or_replaced_generation_cannot_overwrite_current_memory(){
  var store=Store();await store.ObserveAsync("Clock",Own(3,30));
  await store.ObserveAsync("Clock",Own(3,29) with{Level=1,Legends=[]});
  await store.ObserveAsync("Clock",Own(2,99) with{Level=1,Legends=[]});
  Assert.Equal(99,store.Load("Clock").Level);Assert.Equal(Own().Legends,store.Load("Clock").Legends);
 }
 [Fact] public async Task Fresh_complete_empty_legend_list_replaces_old_marks_after_reconnect(){
  var store=Store();await store.ObserveAsync("Clock",Own());
  await store.ObserveAsync("Clock",Own(2,2) with{Level=11,Legends=[]});
  var saved=Store().Load("Clock");Assert.Equal(11,saved.Level);Assert.Empty(saved.Legends);
  Assert.Null(saved.SelectionRestriction("entered-muisir-village"));Assert.Null(saved.SelectionRestriction("spare-a-stick"));
 }
 [Fact] public async Task Restart_accepts_new_proxy_generation_numbers_and_preserves_marks_until_fresh_self_look(){
  await Store().ObserveAsync("Clock",Own(99,100));
  var restarted=Store();await restarted.ObserveAsync("Clock",Own(1,1) with{Level=11,LegendsKnown=false,LegendsRevision=0,Legends=[]});
  Assert.Equal(11,restarted.Load("Clock").Level);Assert.Equal(Own().Legends,restarted.Load("Clock").Legends);
 }
 [Theory][InlineData(null,false)][InlineData(1,false)][InlineData(11,false)][InlineData(12,true)][InlineData(99,true)]
 public void Stick_selection_uses_known_level_boundaries_without_claiming_readiness(int? level,bool blocked){
  var memory=new CharacterMemory{Character="Clock",Level=level};
  Assert.Equal(blocked,memory.SelectionRestriction("spare-a-stick") is not null);
  Assert.Null(memory.SelectionRestriction("entered-muisir-village"));
 }
 [Theory][InlineData("Entered Muisir Village",true)][InlineData("entered muisir village - Deoch 223, Spring",true)][InlineData("Nearly Entered Muisir Village",false)][InlineData("Entered Muisir Village someday",false)]
 public void Muisir_requires_exact_own_mark_or_dated_mark(string legend,bool blocked){
  var memory=new CharacterMemory{Character="Clock",Legends=[legend],LegendsObservedAt=DateTimeOffset.UtcNow};
  Assert.Equal(blocked,memory.SelectionRestriction("entered-muisir-village") is not null);
 }
 [Fact] public void Fresh_own_observation_overrides_memory_but_foreign_or_unknown_state_does_not(){
  var memory=new CharacterMemory{Character="Clock",Level=99,Legends=Own().Legends.ToArray(),LegendsObservedAt=DateTimeOffset.UtcNow};
  var fresh=Own() with{Level=11,Legends=[]};
  Assert.Null(memory.SelectionRestriction("spare-a-stick",fresh));Assert.Null(memory.SelectionRestriction("entered-muisir-village",fresh));
  Assert.NotNull(memory.SelectionRestriction("entered-muisir-village",fresh with{LegendsKnown=false}));
  Assert.NotNull(memory.SelectionRestriction("entered-muisir-village",fresh with{Name="Other"}));
 }
 [Fact] public async Task Saved_memory_contains_no_runtime_identity_dialog_or_inventory_evidence(){
  await Store().ObserveAsync("Clock",Own());
  using var json=JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(directory,"*.json").Single()));
  foreach(string field in new[]{"Generation","Revision","Id","Connected","Inventory","Dialog","LegendsRevision"})Assert.False(json.RootElement.TryGetProperty(field,out _));
 }
 [Fact] public async Task Canceled_save_preserves_previous_memory(){
  var store=Store();await store.ObserveAsync("Clock",Own());
  using var cancellation=new CancellationTokenSource();cancellation.Cancel();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.ObserveAsync("Clock",Own(2) with{Legends=[]},cancellation.Token));
  Assert.Equal(Own().Legends,Store().Load("Clock").Legends);Assert.Empty(Directory.GetFiles(directory,"*.tmp"));
 }
 [Fact] public async Task Path_like_character_names_are_stored_only_inside_memory_directory(){
  const string name="../Clock";await Store().ObserveAsync(name,Own() with{Name=name});
  Assert.Equal(99,Store().Load(name).Level);Assert.Single(Directory.GetFiles(directory,"*.json"));Assert.Empty(Directory.GetDirectories(directory));
 }
 [Theory][InlineData("mundane-birth","Undine mundane birth - Winter, Deoch 25")][InlineData("emerald-dawn","Aided the Emerald Knights - Deoch 223, Spring")][InlineData("emerald-dawn","Aided the Necromancer Order")][InlineData("perfect-hairstyle","Loved by Suomi Mundanes - Deoch 223, Spring")]
 public void Known_finished_legend_goals_cannot_be_selected_for_another_run(string quest,string legend){
  var memory=new CharacterMemory{Character="Clock",Level=11,Legends=[legend],LegendsObservedAt=DateTimeOffset.UtcNow};
  Assert.NotNull(memory.SelectionRestriction(quest));
 }
 [Theory][InlineData("spare-a-stick","Loved by Mileth Mundanes")][InlineData("emerald-dawn","Recovered the Emerald Knight Insignia")]
 public void Repeatable_stick_mark_or_partial_emerald_recovery_does_not_claim_finished_goal(string quest,string legend){
  var memory=new CharacterMemory{Character="Clock",Level=11,Legends=[legend],LegendsObservedAt=DateTimeOffset.UtcNow};
  Assert.Null(memory.SelectionRestriction(quest));
 }
 public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
