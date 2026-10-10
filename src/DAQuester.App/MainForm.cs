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
  Directory.CreateDirectory(Path.GetDirectoryName(CalibrationPath)!);
  if(!File.Exists(CalibrationPath))File.Copy(Path.Combine(AppSettings.Content,"calibration","datester-7.41.json"),CalibrationPath);
  calibration=JsonSerializer.Deserialize<Calibration>(File.ReadAllText(CalibrationPath),AppSettings.Json)!;
  questChoice.Items.Add(recipes[recipe.Id]);foreach(var q in recipes.Values.Where(q=>q.Id!=recipe.Id))questChoice.Items.Add(q);questChoice.SelectedIndex=0;
  BuildWorkspace();
  InitializeUpdates();
  questChoice.SelectedIndexChanged+=(_,_)=>{bool hair=SelectedQuest.Id=="perfect-hairstyle";repeats.Text=hair?"Weekly until Suomi legend":"Repeat every 8 hours";repeats.Visible=SelectedQuest.Repeatable;repeats.Enabled=SelectedQuest.Repeatable&&!hair;repeats.Checked=hair;foreach(var row in grid.Rows.Cast<DataGridViewRow>().ToArray())RefreshRow(row.Cells[0].Value?.ToString()??"");UpdateStatus();};
  host.Diagnostic+=Log;
  host.Connected+=c=>{
   c.Tracker.Observed+=(opcode,payload)=>recorder.Record(opcode,payload,c.Tracker.Snapshot);
   c.DialogAction+=packet=>recorder.RecordDialogPacket(packet,c.Tracker.Snapshot);
   c.Authenticated+=connection=>Ui(()=>Bind(connection));
  };
  Shown+=async(_,_)=>{if(Environment.GetCommandLineArgs().Contains("--smoke"))return;await StartProxy();};
  FormClosing+=async(_,e)=>{
   if(closing)return;e.Cancel=true;closing=true;await StopAll();
   bool prepared=updates.PrepareExit(QuestActionsRunning,ConnectedGame,LaunchedClientRunning);
   if(installExitRequested&&!prepared){closing=false;installExitRequested=false;Theme.Error(this,updates.Status+" Finish quests and close game clients, then try again.","Install update");return;}
   await host.DisposeAsync();Close();
  };
  UpdateStatus();
  InitializeControl();
 }
 void Ui(Action a) {if(IsDisposed||Disposing)return;if(InvokeRequired){try{BeginInvoke(a);}catch(InvalidOperationException){}}else a();}
 public void Log(string message)=>Ui(()=>{string line=DateTime.Now.ToString("HH:mm:ss")+"  "+message;log.Items.Add(line);try{File.AppendAllText(Path.Combine(AppSettings.DataPath,"diagnostics.log"),line+Environment.NewLine);}catch(IOException){}if(log.Items.Count>2000)log.Items.RemoveAt(0);log.TopIndex=Math.Max(0,log.Items.Count-1);});
 string Selected()=>grid.SelectedRows.Count>0?grid.SelectedRows[0].Cells[0].Value?.ToString()??"":settings.Characters.FirstOrDefault()??"";
 void AddCharacter(string character) {
  if(grid.Rows.Cast<DataGridViewRow>().Any(x=>string.Equals(x.Cells[0].Value?.ToString(),character,StringComparison.OrdinalIgnoreCase)))return;
  var job=journal.Load(character,SelectedQuest.Id);
  grid.Rows.Add(character,"Disconnected","—","—","—","—",job.Status,job.Step,job.Reason,job.NextRun?.ToLocalTime().ToString("g")??"",QuestName(job.Quest));
 }
 async void Bind(ProxyConnection connection) {
  string name=connection.Tracker.Snapshot.Name!;
  if(runners.Remove(name,out var old))await old.PauseAsync("Connection replaced");
  CancelControl(name);
  var maps=new MapRepository(Path.GetDirectoryName(settings.ClientPath)!,Path.Combine(AppSettings.Content,"routes.json"));
  var session=new GameSession(connection,maps,settings.MovementMilliseconds){FastMode=settings.FastModeEnabled(name)};sessions[name]=session;
  AddCharacter(name);session.Changed+=snapshot=>Ui(()=>{
   if(sessions.GetValueOrDefault(name)!=session)return;
   RefreshRow(name);
   if(PermanentQuestCompletion.Matches(snapshot)&&journal.Load(name,recipe.Id).PermanentCompletion is null&&(!permanentTasks.TryGetValue(name,out var task)||task.IsCompleted))
    permanentTasks[name]=ObservePermanent(name,session,snapshot);
  });
  session.Interrupted+=Log;RefreshRow(name);Log(name+" connected; server state tracking active.");
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
  bool birth=job.Quest=="mundane-birth",hair=job.Quest=="perfect-hairstyle",muisir=job.Quest=="entered-muisir-village";
  var eligibility=birth?MundaneBirthEvidence.Eligibility(s,job.BirthOptions):hair?PerfectHairstyleEvidence.Eligibility(s):muisir?MuisirEvidence.Eligibility(s):StickEligibility.Check(s,job.CompletedAt,DateTimeOffset.UtcNow);
  var row=grid.Rows.Cast<DataGridViewRow>().First(x=>string.Equals(x.Cells[0].Value?.ToString(),name,StringComparison.OrdinalIgnoreCase));
  row.SetValues(name,s.Connected?"Online":"Offline",s.Level?.ToString()??"—",s.Gold?.ToString("N0")??"—",
    s.Map+" · "+s.Position?.X+","+s.Position?.Y,birth?job.BirthOptions is {} o?$"{o.Season} · {o.Age} · {o.Town}":"Choose birth details":hair?job.LegendReward==true?"Suomi legend recorded":"Ponytail · no items":muisir?s.InventoryKnown?s.Count(MuisirEvidence.Mushroom)+"/5 mushrooms":"Unknown":s.InventoryKnown?s.Count(calibration.BranchName)+"/6 branches":"Unknown",(birth||hair||muisir)&&job.CompletedAt is not null?"Completed":job.Status,job.Step,
    job.CompletionText??(job.PermanentCompletion is not null?job.Reason:job.Status=="Paused"&&job.Reason!="Ready"?job.Reason:!birth&&!hair&&!muisir&&!calibration.Ready?"Calibration required":eligibility.Reason),job.NextRun?.ToLocalTime().ToString("g")??"",QuestName(job.Quest));
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
 async Task Launch() {
  if(!ready)throw new InvalidOperationException("The game connection isn't ready. Check the connection profile in Developer mode, then restart DAQuester.");
  int pid=await Task.Run(()=>ClientLauncher.Launch(settings.ClientPath,settings.LocalPorts[0]));
  try{launchedClients.Add(System.Diagnostics.Process.GetProcessById(pid));}catch(ArgumentException){}
  Log("New DATester client launched (PID "+pid+"). Log in as "+Selected()+".");
 }
 async Task StartQuest(bool resume=false) {
  string name=Selected();
  if(!sessions.TryGetValue(name,out var session)||!session.Snapshot.Connected)throw new InvalidOperationException("Launch DATester and log in as "+name+" first.");
  if(controlActions.ContainsKey(name))throw new QuestBlockedException("A local control action owns this character. Stop it first.");
  var selected=SelectedQuest;MundaneBirthOptions? options=null;
  if(selected.Id=="mundane-birth") {
   var saved=runners.TryGetValue(name,out var existing)&&existing.Job.Quest==selected.Id?existing.Job:journal.Load(name,selected.Id);
   if(saved.Pending is null&&saved.CompletedAt is null&&(!resume||saved.BirthOptions is null)) {
    using var form=new MundaneBirthOptionsForm(name,saved.BirthOptions);if(form.ShowDialog(this)!=DialogResult.OK)return;options=form.Result;
   }
  }
  var runner=await PrepareRunner(name,selected,options);
  await runner.RunAsync(selected.Repeatable&&repeats.Checked);
 }
 Task Pause(string reason){CancelControl(Selected());return runners.TryGetValue(Selected(),out var runner)?runner.PauseAsync(reason):Task.CompletedTask;}
 async Task StopAll(){CancelControl();foreach(var r in runners.Values.ToArray())await r.PauseAsync("Global stop; progress saved");await Task.WhenAll(calibrationTasks.Values.ToArray());await Task.WhenAll(permanentTasks.Values.ToArray());}
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
 Task Catalog() {
  using var form=new QuestLibraryForm(recipes.Values);if(form.ShowDialog(this)==DialogResult.OK&&form.SelectedQuestId is {} id)questChoice.SelectedItem=recipes[id];return Task.CompletedTask;
 }
 async Task EditSettings() {
  await StopAll();using var form=new PlayerSettingsForm(settings,updates,InstallUpdate);
  if(form.ShowDialog(this)==DialogResult.OK&&form.Result is {} value){settings.AutomaticUpdateChecks=value.AutomaticUpdateChecks;updates.AutomaticChecks=value.AutomaticUpdateChecks;Log("Settings saved. Restart DAQuester to apply character or client changes.");}
 }
 async Task EditConnectionProfile() {
  await StopAll();using var form=new ThemedForm{Text="Connection profile · Save, then restart DAQuester",Size=new(700,650),StartPosition=FormStartPosition.CenterParent};
  var text=new TextBox{Multiline=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Font=new("Consolas",10),Text=JsonSerializer.Serialize(settings,AppSettings.Json)};
  var save=new Button{Text="Save profiles",Dock=DockStyle.Bottom,Height=40};
  save.Click+=(_,_)=>{try{var value=JsonSerializer.Deserialize<AppSettings>(text.Text,AppSettings.Json)??throw new InvalidDataException();value.Save();form.Close();Log("Profiles saved. Restart DAQuester to apply endpoint changes.");}catch(Exception e){Theme.Error(form,e.Message);}};
  form.Controls.Add(text);form.Controls.Add(save);Theme.Apply(form);form.ShowDialog(this);
 }
 Task Login() {
  string name=Selected();if(!name.All(char.IsAsciiLetter)||name.Length==0)throw new InvalidOperationException("Select a valid character name.");
  using var form=new SavedLoginForm(name);form.ShowDialog(this);return Task.CompletedTask;
 }
}
