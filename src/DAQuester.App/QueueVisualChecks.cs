using DAQuester.Core;
namespace DAQuester.App;

// Exercises real app ownership wiring with simulated sessions and isolated checkpoints.
internal static class QueueVisualChecks {
 public static void Run(MainForm form){
  var failures=new List<Exception>();
  foreach(var check in new Func<Task>[] {()=>Boundary(form,false),()=>Boundary(form,true),()=>FailedSaveStop(form),()=>PendingEditStop(form)}){
   try{var task=check();var deadline=DateTime.UtcNow.AddSeconds(12);while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Synthetic queue ownership check timed out.");Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}catch(Exception e){failures.Add(e);}
  }
  if(failures.Count>0)throw new AggregateException("Queue ownership checks failed",failures);
  File.WriteAllText("artifacts/queue-ownership-checks.txt","Manual input and disconnect between entries pause the whole queue; Stop cancels every owner despite a failed checkpoint save; pending edits preserve order/home when Stop overlaps. Synthetic sessions only; no game commands executed.");
 }
 static FileQuestQueueStore Store()=>new(Path.Combine(AppSettings.DataPath,"queue-checks",Guid.NewGuid().ToString("N")));
 static async Task PendingEditStop(MainForm form){
  const string name="QueuePendingEdit";
  object Field(string field)=>typeof(MainForm).GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
  var queues=(Dictionary<string,QuestQueueCoordinator>)Field("queueCoordinators");var saving=(HashSet<string>)Field("queueSaving");var cache=(Dictionary<string,QuestQueueState>)Field("queueCache");var store=(FileQuestQueueStore)Field("queueStore");
  var old=new QuestQueueState{Character=name,Quests=["mundane-birth","shinewood-faerie"]};
  queues.Add(name,new(store,old,()=>new(){Name=name,Connected=true,Generation=1},(_,_,_)=>throw new NotSupportedException(),(_,_)=>throw new NotSupportedException()));saving.Add(name);
  Task? stop=null;
  try{
   stop=form.StopAll();await Task.Yield();
   if(queues[name].State.Reason!=old.Reason)throw new InvalidOperationException("Stop must defer an idle coordinator checkpoint until the pending queue edit settles.");
   if(stop.IsCompleted)throw new InvalidOperationException("Stop must wait for a pending queue edit before saving an idle coordinator.");
   var next=old with{Quests=["shinewood-faerie","mundane-birth"],Home=new(1,new(2,3),"Synthetic home")};await store.SaveAsync(next);cache[name]=next;queues.Remove(name);saving.Remove(name);await stop;
   var saved=store.Load(name);if(!saved.Quests.SequenceEqual(next.Quests)||saved.Home!=next.Home)throw new InvalidOperationException("Stop must preserve the newly saved queue order and home.");
  }finally{saving.Remove(name);queues.Remove(name);if(stop is not null)await stop;cache.Remove(name);}
 }
 static async Task Boundary(MainForm form,bool disconnect){
  var session=new FixtureSession("QueueBoundary");int attempts=0;
  var queue=new QuestQueueCoordinator(Store(),new(){Character="QueueBoundary",Quests=["first","second"]},()=>session.Snapshot,(id,_,_)=>{attempts++;return Task.FromResult(new QuestJob{Character="QueueBoundary",Quest=id,Step="complete",Status="Completed",CompletedAt=DateTimeOffset.UtcNow});},(_,_)=>throw new InvalidOperationException("Synthetic check cannot travel"));
  queue.Changed+=state=>{if(state.Quests.Length==1&&state.ActiveQuest is null){if(disconnect)session.Disconnect();else session.Interrupt();}};
  await form.RunQueueCoordinatorAsync(queue,session);
  if(attempts!=1||!queue.State.Quests.SequenceEqual(new[]{"second"})||queue.State.State!="Paused")throw new InvalidOperationException((disconnect?"Disconnect":"Manual input")+" between entries must pause the whole queue before another attempt.");
 }
 static async Task FailedSaveStop(MainForm form){
  var queues=(Dictionary<string,QuestQueueCoordinator>)typeof(MainForm).GetField("queueCoordinators",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
  string first="QueueBlockedSave",second="QueueOtherOwner";
  var firstDir=Path.Combine(AppSettings.DataPath,"queue-checks",Guid.NewGuid().ToString("N"));var reachedA=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var reachedB=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  QuestQueueCoordinator Create(string name,FileQuestQueueStore store,TaskCompletionSource reached)=>new(store,new(){Character=name,Quests=["fixture"]},()=>new(){Name=name,Generation=1,Connected=true},async(_,_,ct)=>{reached.SetResult();await Task.Delay(Timeout.Infinite,ct);return new();},(_,_)=>throw new InvalidOperationException("Synthetic check cannot travel"));
  var a=Create(first,new(firstDir),reachedA);var b=Create(second,Store(),reachedB);queues.Add(first,a);queues.Add(second,b);
  var runA=a.RunAsync();var runB=b.RunAsync();await Task.WhenAll(reachedA.Task,reachedB.Task);
  using var locked=new FileStream(Directory.GetFiles(firstDir,"*.json").Single(),FileMode.Open,FileAccess.Read,FileShare.Read);
  try{
   Exception? failure=null;try{await form.StopAll();}catch(Exception e){failure=e;}
   if(failure is null)throw new InvalidOperationException("Failed checkpoint save must be reported.");
   if(a.IsRunning||b.IsRunning)throw new InvalidOperationException("Stop all must cancel every owner even when the first queue cannot save.");
  }finally{locked.Dispose();await Task.WhenAll(a.PauseAsync(stop:true),b.PauseAsync(stop:true));try{await Task.WhenAll(runA,runB);}catch(IOException){}queues.Remove(first);queues.Remove(second);}
 }
 sealed class FixtureSession(string name):IGameSession {
  public CharacterSnapshot Snapshot {get;private set;}=new(){Name=name,Generation=1,Connected=true};
  public IReadOnlySet<string> Capabilities {get;}=new HashSet<string>();
  public event Action<CharacterSnapshot>? Changed;
  public event Action<string>? Interrupted;
  public void Interrupt()=>Interrupted?.Invoke("Manual movement paused automation.");
  public void Disconnect(){Snapshot=Snapshot with{Connected=false};Changed?.Invoke(Snapshot);}
  public Task ExecuteAsync(GameCommand command,CancellationToken cancellation)=>throw new NotSupportedException("Synthetic checks never execute game commands.");
  public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot,bool> predicate,long after,TimeSpan timeout,CancellationToken cancellation)=>throw new NotSupportedException();
 }
}
