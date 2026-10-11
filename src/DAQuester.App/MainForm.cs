using System.Net;
using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;
using DAQuester.DarkAges.Launcher;
namespace DAQuester.App;
public sealed partial class MainForm:ThemedForm {
 readonly AppSettings settings=AppSettings.Load();
 readonly ProxyHost host=new();
 readonly ReplayRecorder recorder=new();
 readonly Dictionary<string,GameSession> sessions=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,QuestRunner> runners=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Task> permanentTasks=new(StringComparer.OrdinalIgnoreCase);
 readonly FileQuestJournal journal=new(Path.Combine(AppSettings.DataPath,"journal"));
 readonly QuestDefinition recipe=JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(AppSettings.Content,"recipes","spare-a-stick.v1.json")),AppSettings.Json)!;
 readonly DataGridView grid=new DarkDataGridView(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false,BackgroundColor=Theme.Surface,BorderStyle=BorderStyle.None};
 readonly ListBox log=new DarkListBox(){Dock=DockStyle.Fill,IntegralHeight=false};
 readonly TextBox dialog=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
 readonly CheckBox repeats=new JournalCheckBox(){Name="repeatQuest",Text="Repeat every 8 hours",AutoSize=true,Margin=new(10,9,5,0)};
 readonly CheckBox fastMode=new JournalCheckBox(){Name="fastMode",Text="Fast mode · slows near players",AccessibleName="Fast mode, slows near other players",AutoSize=false};
 readonly CheckBox recording=new JournalCheckBox(){Text="Record evidence",Checked=true,AutoSize=true,Margin=new(10,9,5,0)};
 readonly Label status=new(){Dock=DockStyle.Bottom,Height=32,Padding=new(10,7,0,0)};
 bool ready,closing;
 Calibration calibration;
 string CalibrationPath=>Path.Combine(AppSettings.DataPath,"calibration",settings.ServerProfile+".json");
 public MainForm() {
  characterMemory=new(Path.Combine(AppSettings.DataPath,"characters"),settings.ServerProfile);
  Directory.CreateDirectory(Path.GetDirectoryName(CalibrationPath)!);
  if(!File.Exists(CalibrationPath))File.Copy(Path.Combine(AppSettings.Content,"calibration","datester-7.41.json"),CalibrationPath);
  calibration=JsonSerializer.Deserialize<Calibration>(File.ReadAllText(CalibrationPath),AppSettings.Json)!;
  questChoice.Items.Add(recipes[recipe.Id]);foreach(var q in recipes.Values.Where(q=>q.Id!=recipe.Id))questChoice.Items.Add(q);questChoice.SelectedIndex=0;
  BuildWorkspace();
  InitializeUpdates();
  questChoice.SelectedIndexChanged+=(_,_)=>{foreach(var row in grid.Rows.Cast<DataGridViewRow>().ToArray())RefreshRow(row.Cells[0].Value?.ToString()??"");UpdateStatus();};
  host.Diagnostic+=Log;
  host.Connected+=c=>{
   c.Tracker.Observed+=(opcode,payload)=>recorder.Record(opcode,payload,c.Tracker.Snapshot);
   c.DialogAction+=packet=>recorder.RecordDialogPacket(packet,c.Tracker.Snapshot);
   c.Authenticated+=connection=>Ui(()=>Bind(connection));
  };
  Shown+=async(_,_)=>{if(IsSmoke)return;await StartProxy();};
  FormClosing+=async(_,e)=>{
   if(closing)return;e.Cancel=true;closing=true;try{await StopAll();}catch(Exception failure){closing=false;Theme.Error(this,PlayerReason(failure.Message),"Save progress before closing");return;}
   bool prepared=updates.PrepareExit(QuestActionsRunning,ConnectedGame,LaunchedClientRunning);
   if(installExitRequested&&!prepared){closing=false;installExitRequested=false;Theme.Error(this,updates.Status+" Finish quests and close game clients, then try again.","Install update");return;}
   characterMemoryLifetime.Cancel();await host.DisposeAsync();await Task.WhenAll(characterMemoryWrites.ToArray());Close();
  };
  UpdateStatus();
  InitializeControl();
  Disposed+=(_,_)=>characterMemoryLifetime.Dispose();
 }
 void Ui(Action a) {if(IsDisposed||Disposing)return;if(InvokeRequired){try{BeginInvoke(a);}catch(InvalidOperationException){}}else a();}
 bool logFileFailed;
 public void Log(string message)=>Ui(()=>{
  string line=DateTime.Now.ToString("HH:mm:ss")+"  "+message;log.Items.Add(line);
  try{File.AppendAllText(Path.Combine(AppSettings.DataPath,"diagnostics.log"),line+Environment.NewLine);logFileFailed=false;}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException){if(!logFileFailed){logFileFailed=true;log.Items.Add(DateTime.Now.ToString("HH:mm:ss")+"  diagnostics.log not written: "+e.Message);}}
  while(log.Items.Count>2000)log.Items.RemoveAt(0);log.TopIndex=Math.Max(0,log.Items.Count-1);
 });
 string Selected()=>grid.SelectedRows.Count>0?grid.SelectedRows[0].Cells[0].Value?.ToString()??"":settings.Characters.FirstOrDefault()??"";
 DateTimeOffset? NextAttempt(QuestJob job)=>recipes.TryGetValue(job.Quest,out var quest)?QuestRunPolicy.NextAttempt(quest,job):job.NextRun;
 bool OrdinaryAttemptComplete(QuestJob job)=>job.Quest==recipe.Id&&QuestRunPolicy.AttemptCompleted(job)&&job.PermanentCompletion is null;
 void AddCharacter(string character) {
  if(grid.Rows.Cast<DataGridViewRow>().Any(x=>string.Equals(x.Cells[0].Value?.ToString(),character,StringComparison.OrdinalIgnoreCase)))return;
  var job=journal.Load(character,SelectedQuest.Id);
  grid.Rows.Add(character,"Disconnected","—","—","—","—",OrdinaryAttemptComplete(job)?"Completed for now":job.Status,job.Step,job.Reason,NextAttempt(job)?.ToLocalTime().ToString("g")??"",QuestName(job.Quest));
 }
 async void Bind(ProxyConnection connection) {
  string name=connection.Tracker.Snapshot.Name!;
  if(runners.Remove(name,out var old))await old.PauseAsync("Connection replaced");
  CancelControl(name);
  var maps=new MapRepository(Path.GetDirectoryName(settings.ClientPath)!,Path.Combine(AppSettings.Content,"routes.json"));
  var session=new GameSession(connection,maps,settings.MovementMilliseconds){FastMode=settings.FastModeEnabled(name)};sessions[name]=session;
  AddCharacter(name);session.Changed+=snapshot=>Ui(()=>{
   if(sessions.GetValueOrDefault(name)!=session)return;
   RememberCharacter(name,session,snapshot);
   RefreshRow(name);
   if(PermanentQuestCompletion.Matches(snapshot)&&journal.Load(name,recipe.Id).PermanentCompletion is null&&(!permanentTasks.TryGetValue(name,out var task)||task.IsCompleted))
    permanentTasks[name]=ObservePermanent(name,session,snapshot);
   else if(!controlActions.ContainsKey(name)&&(!calibrationTasks.TryGetValue(name,out var capture)||capture.IsCompleted)&&
    runners.TryGetValue(name,out var runner)&&runner.Job.Quest==recipe.Id)ObserveOrdinaryCompletion(name,session,runner,snapshot);
  });
  session.Interrupted+=Log;RememberCharacter(name,session,session.Snapshot);_=ReadOwnLegendAsync(name,session);RefreshRow(name);Log(name+" connected; server state tracking active.");
 }
 async void ObserveOrdinaryCompletion(string name,GameSession session,QuestRunner runner,CharacterSnapshot snapshot) {
  try {if(sessions.GetValueOrDefault(name)==session&&runners.GetValueOrDefault(name)==runner)await runner.ReconcileAsync(snapshot);}
  catch(Exception e){Log(name+": reward confirmation could not be saved: "+e.Message);}
 }
 async Task ObservePermanent(string name,GameSession session,CharacterSnapshot snapshot) {
  try {
   var job=await PermanentQuestCompletion.ObserveAsync(journal,snapshot,async()=>{
    CancelControl(name);
    if(calibrationTasks.TryGetValue(name,out var capture))await capture;
    if(runners.TryGetValue(name,out var runner)&&runner.Job.Quest==recipe.Id)await runner.PauseAsync("Callough permanent outcome observed");
   });
   if(job is null||sessions.GetValueOrDefault(name)!=session)return;
   if(runners.TryGetValue(name,out var current)&&current.Job.Quest==recipe.Id)runners.Remove(name);calibrationRuns.Remove(name);RefreshRow(name);
   Log(name+": permanently finished; repeats disabled. Callough: "+job.PermanentCompletion!.Dialog.Text);
  }catch(Exception e){Log(name+": permanent outcome could not be saved: "+e.Message);}
 }
 void RefreshRow(string name) {
  sessions.TryGetValue(name,out var session);
  var s=session?.Snapshot??new CharacterSnapshot{Name=name};var job=GetJob(name);
  bool birth=job.Quest=="mundane-birth",hair=job.Quest=="perfect-hairstyle",muisir=job.Quest=="entered-muisir-village",emerald=job.Quest=="emerald-dawn",faerie=job.Quest==ShinewoodFaerieEvidence.Id;
  bool ordinaryComplete=OrdinaryAttemptComplete(job)&&(!runners.TryGetValue(name,out var owner)||!owner.IsRunning);
  var eligibility=birth?MundaneBirthEvidence.Eligibility(s,job.BirthOptions):hair?PerfectHairstyleEvidence.Eligibility(s):muisir?MuisirEvidence.Eligibility(s):emerald?EmeraldDawnEvidence.Eligibility(s):faerie?ShinewoodFaerieEvidence.Eligibility(s,job):StickEligibility.Check(s,job.CompletedAt,DateTimeOffset.UtcNow);
  var row=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(x=>string.Equals(x.Cells[0].Value?.ToString(),name,StringComparison.OrdinalIgnoreCase));if(row is null)return;
  row.SetValues(name,s.Connected?"Online":"Offline",s.Level?.ToString()??"—",s.Gold?.ToString("N0")??"—",
    s.Map+" · "+s.Position?.X+","+s.Position?.Y,birth?job.BirthOptions is {} o?$"{o.Season} · {o.Age} · {o.Town}":"Choose birth details":hair?job.LegendReward==true?"Suomi legend recorded":"Ponytail · no items":muisir?s.InventoryKnown?s.Count(MuisirEvidence.Mushroom)+"/5 mushrooms":"Unknown":emerald?job.EmeraldOptions?.Label??"Choose insignia recipient":s.InventoryKnown?s.Count(calibration.BranchName)+"/6 branches":"Unknown",ordinaryComplete?"Completed for now":(birth||hair||muisir||emerald)&&job.CompletedAt is not null?"Completed":job.Status,job.Step,
    job.CompletionText??(job.PermanentCompletion is not null?job.Reason:job.Status=="Paused"&&job.Reason!="Ready"?job.Reason:!birth&&!hair&&!muisir&&!emerald&&!faerie&&!calibration.Ready?"Calibration required":eligibility.Reason),NextAttempt(job)?.ToLocalTime().ToString("g")??"",QuestName(job.Quest));
  if(faerie)row.Cells[5].Value=ShinewoodFaerieEvidence.HasPet(s)||job.FaerieReceipts.Any(r=>r.Goal==ShinewoodFaerieEvidence.Pet)?"Pet verified":job.FaeriePetWait is {} wait&&ShinewoodFaerieEvidence.PetWaitPage(wait)?"Pet wait · 1 deoch":"Pet + missing spells";
  if(Selected()==name)ShowDialogState();UpdateStatus();
 }
 void ShowDialogState() {
  UpdateWorkspace();
  if(!sessions.TryGetValue(Selected(),out var session)){dialog.Text="Launch DATester, then log in manually. Select a character to inspect server dialogs.";return;}
  var d=session.Snapshot.Dialog;
  dialog.Text=d is null?"No current NPC dialog.":$"{d.Name}  ·  entity {d.EntityId}\r\nPursuit {d.Pursuit} · step {d.Step} · revision {d.Revision}\r\n\r\n{d.Text}\r\n\r\n"+string.Join("\r\n",d.Choices.Select(x=>$"{x.Option}. {x.Text}  [pursuit {x.Pursuit}]"))+(d.Prompt is null?"":"\r\nPrompt: "+d.Prompt);
 }
 void UpdateStatus() {status.Text=$"{SelectedQuest.Name} · Progress saved automatically"+(developerMode.Checked?$" · {recorder.Count:N0} recorded events":"");UpdateWorkspace();}
 async Task StartProxy() {
  try {
   if(settings.LocalPorts.Length!=3||settings.RemotePorts.Length!=3)throw new InvalidDataException("Configure exactly three local and remote ports.");
   for(int i=0;i<3;i++)await host.StartAsync(settings.LocalPorts[i],new(IPAddress.Parse(settings.RemoteAddress),settings.RemotePorts[i]));
   ready=true;Log("Loopback proxies ready on "+string.Join(", ",settings.LocalPorts));UpdateWorkspace();
  }catch(Exception e){Log("Proxy startup blocked: "+e.Message);status.Text="Connection unavailable. Check the connection profile in Developer mode, then restart DAQuester.";connectionSummary.Text="Connection unavailable";}
 }
 async Task StartQuest(bool resume=false) {
  string name=Selected();
  if(QueueReserves(name))throw new QuestBlockedException("Stop the queue before starting a separate quest.");
  var selected=SelectedQuest;
  if(!await ConfigureQuest(name,selected,resume))return;
  var runner=await PrepareRunner(name,selected);
  await runner.RunAsync(selected.Repeatable&&repeats.Checked,startNewAttempt:!resume);
 }
 async Task<bool> ConfigureQuest(string name,QuestDefinition selected,bool resume) {
  if(!sessions.TryGetValue(name,out var session)||!session.Snapshot.Connected)throw new InvalidOperationException("Launch DATester and log in as "+name+" first.");
  if(controlActions.ContainsKey(name))throw new QuestBlockedException("A local control action owns this character. Stop it first.");
  MundaneBirthOptions? options=null;EmeraldDawnOptions? emeraldOptions=null;
  if(selected.Id=="mundane-birth") {
   var saved=runners.TryGetValue(name,out var existing)&&existing.Job.Quest==selected.Id?existing.Job:journal.Load(name,selected.Id);
   if(saved.Pending is null&&saved.CompletedAt is null&&(!resume||saved.BirthOptions is null)) {
    using var form=new MundaneBirthOptionsForm(name,saved.BirthOptions);if(form.ShowDialog(this)!=DialogResult.OK)return false;options=form.Result;
   }
  }
  if(selected.Id=="emerald-dawn") {
   var saved=runners.TryGetValue(name,out var existing)&&existing.Job.Quest==selected.Id?existing.Job:journal.Load(name,selected.Id);
   if(EmeraldDawnEvidence.FinalMark(session.Snapshot) is null&&!EmeraldChoicesLocked(saved)&&(!resume||saved.EmeraldOptions is null)) {
    using var form=new EmeraldDawnOptionsForm(name,saved.EmeraldOptions);if(form.ShowDialog(this)!=DialogResult.OK)return false;emeraldOptions=form.Result;
   }
  }
  bool allowUnsafeWalking=false;
  if(selected.Id==ShinewoodFaerieEvidence.Id){
   var saved=runners.TryGetValue(name,out var existing)&&existing.Job.Quest==selected.Id?existing.Job:journal.Load(name,selected.Id);
   if(saved.Pending?.Kind.StartsWith("faerie-return",StringComparison.Ordinal)!=true&&ShinewoodFaerieEvidence.Escape(session.Snapshot) is null&&
     (ShinewoodFaerieEvidence.Goals(session.Snapshot,saved).Length>0||ShinewoodFaerieEvidence.InForest(session.Snapshot.Map))){
    string? error=null;try{session.ValidateFaerieReturn();}catch(Exception e){error=e.Message;}
    using var warning=new ShinewoodFaerieWarningForm(name,error);if(warning.ShowDialog(this)!=DialogResult.OK)return false;allowUnsafeWalking=true;
   }
  }
  var job=runners.TryGetValue(name,out var owner)&&owner.Job.Quest==selected.Id?owner.Job:journal.Load(name,selected.Id);
  bool repeatChanged=repeatChoices.TryGetValue((name.ToUpperInvariant(),selected.Id),out bool repeatChoice)&&job.Repeat!=(selected.Repeatable&&repeatChoice);
  if(repeatChanged)job=job with{Repeat=selected.Repeatable&&repeatChoice};
  if(options is not null)job=job with{BirthOptions=options,Repeat=false,NextRun=null};
  if(emeraldOptions is not null)job=job with{EmeraldOptions=emeraldOptions,Repeat=false,NextRun=null};
  if(allowUnsafeWalking){session.ValidateFaerieReturn();job=job with{FaerieWalkAcknowledgedGeneration=session.Snapshot.Generation,FaerieWalkAcknowledgedAt=DateTimeOffset.UtcNow};}
  if(options is not null||emeraldOptions is not null||allowUnsafeWalking||repeatChanged){if(owner is not null&&owner.Job.Quest==selected.Id){await owner.PauseAsync("Quest choices saved");runners.Remove(name);}await journal.SaveAsync(job);}
  return true;
 }
 async Task Pause(string reason){string name=Selected();if(QueueReserves(name)){await PauseQueue(name,reason.StartsWith("Stopped"));return;}CancelControl(name);if(runners.TryGetValue(name,out var runner))await runner.PauseAsync(reason);}
 internal Task StopAll()=>StopOwners(null);
 async Task StopOwners(string? character){
  CancelLaunches(character);CancelControl(character);
  foreach(var prepare in queuePreparations.Where(p=>character is null||p.Key.Equals(character,StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value).ToArray())prepare.Cancel();
  bool Match(string name)=>character is null||name.Equals(character,StringComparison.OrdinalIgnoreCase);
  // Start every cancellation before any checkpoint failure can stop the caller.
  var queues=settings.Characters.Concat(queueCache.Keys).Concat(queueCoordinators.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Where(Match).Select(name=>PauseQueue(name,true)).ToArray();
  var quests=runners.Where(p=>Match(p.Key)).Select(p=>p.Value.PauseAsync("Stopped; progress saved")).ToArray();
  await Task.WhenAll(queues.Concat(quests).Append(AwaitLaunches(character)).Append(WaitForQueueSaves(character)).Concat(calibrationTasks.Where(p=>Match(p.Key)).Select(p=>p.Value)).Concat(permanentTasks.Where(p=>Match(p.Key)).Select(p=>p.Value)));
 }
 Task Export() {
  var folder=Path.Combine(AppSettings.DataPath,"replays");Directory.CreateDirectory(folder);
  string path=Path.Combine(folder,"capture-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");recorder.Export(path);Log("Sanitized replay: "+path);UpdateStatus();return Task.CompletedTask;
 }
 async Task Calibrate() {
  if(!sessions.TryGetValue(Selected(),out var session))throw new InvalidOperationException("Connect a character before calibration.");
  await StopAll();
  using var form=new CalibrationForm(session,recorder,calibration,CalibrationPath);
  if(form.ShowDialog(this)==DialogResult.OK) {
   calibration=form.Result;
   runners.Clear();foreach(var name in sessions.Keys)RefreshRow(name);
   Log(calibration.Ready?"Calibration saved; automatic runs enabled.":"Calibration draft saved.");
  }
 }
 async Task EditConnectionProfile() {
  await StopAll();using var form=new ThemedForm{Text="Connection profile · Save, then restart DAQuester",Size=new(700,650),StartPosition=FormStartPosition.CenterParent};
  var text=new TextBox{Multiline=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Font=new("Consolas",10),Text=JsonSerializer.Serialize(settings,AppSettings.Json)};
  var save=new Button{Text="Save profiles",Dock=DockStyle.Bottom,Height=40};
  save.Click+=(_,_)=>{try{var value=JsonSerializer.Deserialize<AppSettings>(text.Text,AppSettings.Json)??throw new InvalidDataException();value.Save();form.Close();Log("Profiles saved. Restart DAQuester to apply endpoint changes.");}catch(Exception e){Theme.Error(form,e.Message);}};
  form.Controls.Add(text);form.Controls.Add(save);Theme.Apply(form);form.ShowDialog(this);
 }
}
