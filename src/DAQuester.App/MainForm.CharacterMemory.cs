using DAQuester.Core;
using DAQuester.DarkAges;

namespace DAQuester.App;

public sealed partial class MainForm {
 readonly FileCharacterMemory characterMemory;
 readonly HashSet<Task> characterMemoryWrites=[];
 readonly CancellationTokenSource characterMemoryLifetime=new();

 CharacterMemory RememberedCharacter(string name){
  if(name.Length==0)return new();
  try{return characterMemory.Load(name);}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){
   Log("Saved character marks could not be read: "+e.Message);return new(){Character=name};
  }
 }
 void RememberCharacter(string name,GameSession session,CharacterSnapshot snapshot){
  if(closing)return;
  var write=SaveCharacterMemoryAsync(name,session,snapshot);characterMemoryWrites.Add(write);_=ForgetWrite(write);
 }
 async Task ForgetWrite(Task write){try{await write;}finally{characterMemoryWrites.Remove(write);}}
 async Task SaveCharacterMemoryAsync(string name,GameSession session,CharacterSnapshot snapshot){
  if(sessions.GetValueOrDefault(name)!=session||snapshot.Generation!=session.Snapshot.Generation)return;
  try{
   var before=RememberedCharacter(name);var saved=await characterMemory.ObserveAsync(name,snapshot);
   if(saved!=before&&sessions.GetValueOrDefault(name)==session&&!closing)UpdateWorkspace();
  }catch(Exception e){Log(name+": character marks could not be saved: "+e.Message);}
 }
 async Task ReadOwnLegendAsync(string name,GameSession session){
  if(IsSmoke)return;
  try{
   var initial=session.Snapshot;
   if(!initial.InventoryKnown&&!initial.LegendsKnown)
    await session.WaitAsync(s=>s.InventoryKnown||s.LegendsKnown,initial.Revision,TimeSpan.FromSeconds(20),characterMemoryLifetime.Token);
   if(closing||sessions.GetValueOrDefault(name)!=session||!session.Snapshot.Connected||session.Snapshot.LegendsKnown||
    controlActions.ContainsKey(name)||QueueReserves(name)||runners.TryGetValue(name,out var runner)&&runner.IsRunning||
    calibrationTasks.TryGetValue(name,out var calibrationTask)&&!calibrationTask.IsCompleted)return;
   await session.ExecuteAsync(new("inspect-self"),characterMemoryLifetime.Token);
  }catch(OperationCanceledException)when(characterMemoryLifetime.IsCancellationRequested){}
  catch(Exception e){Log(name+": own legend refresh deferred: "+e.Message);}
 }
}
