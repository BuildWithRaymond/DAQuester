using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Control;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Diagnostics;
namespace DAQuester.App;
public sealed partial class MainForm {
 ControlServer? controlServer;
 readonly Dictionary<string,CancellationTokenSource> controlActions=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,AutomaticCalibration> calibrationRuns=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Task> calibrationTasks=new(StringComparer.OrdinalIgnoreCase);
 TreeRegion[] CandidateRegions=>TreePatrol.Load(Path.Combine(AppSettings.Content,"calibration","mileth-tree-candidates.json"));
 QuestJob GetJob(string name)=>calibrationRuns.TryGetValue(name,out var run)?run.Job:runners.TryGetValue(name,out var runner)?runner.Job:journal.Load(name,SelectedQuest.Id);
 void InitializeControl() {
  Shown+=(_,_)=>{if(Environment.GetCommandLineArgs().Contains("--smoke"))return;controlServer=new(r=>OnUiAsync(()=>ControlAsync(r)));controlServer.Start();Log("Local control bridge ready; current Windows user only.");};
  FormClosed+=async(_,_)=>{CancelControl();if(controlServer is not null)await controlServer.DisposeAsync();};
 }
 Task<object?> OnUiAsync(Func<Task<object?>> action) {
  var done=new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
  if(IsDisposed||Disposing){done.SetException(new IOException("DAQuester is closing."));return done.Task;}
  BeginInvoke(async()=>{try{done.SetResult(await action());}catch(Exception e){done.SetException(e);}});return done.Task;
 }
 void CancelControl(string? name=null) {foreach(var pair in controlActions.ToArray())if(name is null||name.Equals(pair.Key,StringComparison.OrdinalIgnoreCase))pair.Value.Cancel();}
 async Task<object?> ControlAsync(ControlRequest r) {
  bool Flag(string key)=>r.Arguments is {} a&&a.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
  int Number(string key,int fallback)=>r.Arguments is {} a&&a.TryGetProperty(key,out var v)?v.GetInt32():fallback;
  string? TextArgument(string key)=>r.Arguments is {} a&&a.TryGetProperty(key,out var v)?v.GetString():null;
  string name=r.Character??"";
  if(r.Method=="state")return new{characters=sessions.Where(p=>name.Length==0||p.Key.Equals(name,StringComparison.OrdinalIgnoreCase)).Select(p=>new{character=p.Key,snapshot=p.Value.Snapshot,job=GetJob(p.Key),visibleSteps=p.Value.VisibleMoves}).ToArray(),calibration,quests=recipes.Values.Select(q=>new{q.Id,q.Name,q.Repeatable}).ToArray(),selectedQuest=SelectedQuest.Id,recordedEvents=recorder.Count,controlActions=controlActions.Keys.ToArray()};
  if(r.Method=="stop"){CancelControl(r.Character);foreach(var pair in runners.ToArray())if(r.Character is null||pair.Key.Equals(name,StringComparison.OrdinalIgnoreCase))await pair.Value.PauseAsync("Stopped through local control; progress saved");return new{stopped=r.Character??"all"};}
  if(r.Method=="export") {var folder=Path.Combine(AppSettings.DataPath,"replays");Directory.CreateDirectory(folder);string path=Path.Combine(folder,"capture-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json");recorder.Export(path);return new{path,events=recorder.Count};}
  if(r.Method=="analyze")return new{evidence=recorder.Analyze(),candidateRegions=CandidateRegions};
  if(r.Method=="launch") {if(!ready)throw new QuestBlockedException("Proxy not ready.");int pid=await Task.Run(()=>DAQuester.DarkAges.Launcher.ClientLauncher.Launch(settings.ClientPath,settings.LocalPorts[0]));Log("New DATester client launched (PID "+pid+"). Log in manually.");return new{pid};}
  if(r.Method=="map") {
   int map=Number("map",500);var repository=new MapRepository(Path.GetDirectoryName(settings.ClientPath)!,Path.Combine(AppSettings.Content,"routes.json"));var record=repository.Record(map);var g=repository.Load(map);
   var rows=Enumerable.Range(0,record.Height).Select(y=>new string(Enumerable.Range(0,record.Width).Select(x=>g.Blocked(new(x,y))?'#':'.').ToArray())).ToArray();return new{record,collisionRows=rows,candidateRegions=map==500?CandidateRegions:[],candidatePatrol=map==500?TreePatrol.Build(g,new(13,69),CandidateRegions).Where(t=>!record.Portals.Any(p=>p.From==t)).ToArray():[]};
  }
  if(!sessions.TryGetValue(name,out var session)||!session.Snapshot.Connected)throw new QuestBlockedException("Connected character required: "+name);
  if(r.Method=="calibrate")return await StartAutomaticCalibration(name);
  if(r.Method=="start") {
   string id=TextArgument("quest")??recipe.Id;if(!recipes.TryGetValue(id,out var selected))throw new QuestBlockedException("Unsupported quest: "+id);
   MundaneBirthOptions? options=null;
   if(id=="mundane-birth"&&(TextArgument("season") is not null||TextArgument("town") is not null||r.Arguments is {} args&&args.TryGetProperty("age",out _)))
    options=new(TextArgument("season")??"",Number("age",0),TextArgument("town")??"");
   var runner=await PrepareRunner(name,selected,options);
   _=runner.RunAsync(selected.Repeatable&&Flag("repeat"));return new{started=name,quest=id,birthChoices=runner.Job.BirthOptions};
  }
  if(r.Method!="command")throw new InvalidOperationException("Unsupported control method: "+r.Method);
  var command=JsonSerializer.Deserialize<GameCommand>(r.Arguments?.GetRawText()??"{}",AppSettings.Json)??throw new InvalidDataException("Game command missing.");
  if(command.Kind is not ("navigate" or "visit" or "step" or "open" or "choose" or "next" or "close" or "input" or "say" or "collect-ground" or "pickup" or "inspect-self"))throw new QuestBlockedException("Command is unavailable through control bridge.");
  if(controlActions.ContainsKey(name))throw new QuestBlockedException("A control action already owns this character. Stop it first.");
  if(runners.TryGetValue(name,out var owner))await owner.PauseAsync("Local control owns character");
  using var cancellation=new CancellationTokenSource();controlActions[name]=cancellation;
  void Interrupt(string reason){cancellation.Cancel();Log(name+": control paused: "+reason);}
  session.Interrupted+=Interrupt;
  try {
   long before=session.Snapshot.Revision;
   await session.ExecuteAsync(command with{Dialog=command.Kind is "choose" or "next" or "close" or "input"?session.Snapshot.Dialog:null},cancellation.Token);
   if(command.Kind=="choose")await session.WaitAsync(s=>s.Dialog is {} d&&d.Revision>before,before,TimeSpan.FromSeconds(5),cancellation.Token);
   return new{snapshot=session.Snapshot};
  }finally{session.Interrupted-=Interrupt;controlActions.Remove(name);RefreshRow(name);}
 }
 async Task<object?> StartAutomaticCalibration(string name) {
  if(!sessions.TryGetValue(name,out var session)||!session.Snapshot.Connected)throw new QuestBlockedException("Connect "+name+" first.");
  if(controlActions.ContainsKey(name))throw new QuestBlockedException("An action already owns this character. Stop it first.");
  if(!recorder.Enabled)throw new QuestBlockedException("Enable Record evidence before automatic calibration.");
  if(runners.TryGetValue(name,out var owner))await owner.PauseAsync("Automatic calibration owns character");
  var repository=new MapRepository(Path.GetDirectoryName(settings.ClientPath)!,Path.Combine(AppSettings.Content,"routes.json"));var map=repository.Record(500);
  // Use the weapon-shop return landing, not the wall-flagged entrance door.
  var patrol=TreePatrol.Build(repository.Load(500),new(13,69),CandidateRegions).Where(t=>!map.Portals.Any(p=>p.From==t)).ToArray();
  var cancellation=new CancellationTokenSource();controlActions[name]=cancellation;
  var run=new AutomaticCalibration(session,journal,recorder,calibration,patrol);calibrationRuns[name]=run;
  run.Changed+=_=>Ui(()=>RefreshRow(name));
  void Interrupt(string reason){cancellation.Cancel();Log(name+": calibration paused: "+reason);}
  session.Interrupted+=Interrupt;
  string folder=Path.Combine(AppSettings.DataPath,"replays");Directory.CreateDirectory(folder);string path=Path.Combine(folder,"calibration-"+name+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json");
  async Task Run() {
   try {
    var result=await run.RunAsync(path,cancellation.Token);
    if(run.Job.PermanentCompletion is not null){Log(name+": permanently finished; repeats disabled. Callough: "+run.Job.PermanentCompletion.Dialog.Text);return;}
    calibration=calibration with{Validated=calibration.Validated||result.Validated,Patrol=result.Patrol,PatrolBasis=result.PatrolBasis,ObservedDropPositions=result.ObservedDropPositions,Accepted=calibration.Accepted.Concat(result.Accepted).Distinct().ToArray(),Completed=calibration.Completed.Concat(result.Completed).Distinct().ToArray(),Refused=calibration.Refused.Concat(result.Refused).Distinct().ToArray(),Cooldown=calibration.Cooldown.Concat(result.Cooldown).Distinct().ToArray(),EvidenceFiles=calibration.EvidenceFiles.Concat(result.EvidenceFiles).Distinct().ToArray(),ValidatedCharacters=calibration.ValidatedCharacters.Concat(result.ValidatedCharacters).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()};
    File.WriteAllText(CalibrationPath,JsonSerializer.Serialize(calibration,AppSettings.Json));runners.Remove(name);
    Log(name+": "+(calibration.Ready?"Server evidence validated patrol and replies; automatic recipe enabled.":"Completion proven; another collection capture is needed to validate acceptance/patrol."));
   }catch(Exception e){recorder.Export(path);Log(name+": calibration paused: "+e.Message+". Replay: "+path);}
   finally{session.Interrupted-=Interrupt;controlActions.Remove(name);cancellation.Dispose();RefreshRow(name);UpdateStatus();}
  }
  calibrationTasks[name]=Run();Log(name+": automatic calibration started; manual input or Stop pauses it.");return new{started=name,candidateTiles=patrol.Length,evidencePath=path};
 }
}
