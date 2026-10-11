using DAQuester.Core;

namespace DAQuester.App;

public sealed partial class MainForm {
 readonly FileQuestQueueStore queueStore=new(Path.Combine(AppSettings.DataPath,"queues"));
 readonly Dictionary<string,QuestQueueState> queueCache=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,QuestQueueCoordinator> queueCoordinators=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> queueSaving=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,CancellationTokenSource> queuePreparations=new(StringComparer.OrdinalIgnoreCase);
 DataGridView? historyTable;
 QuestQueueState GetQueue(string name){if(name.Length==0)return new();if(!queueCache.TryGetValue(name,out var state))queueCache[name]=state=queueStore.Load(name);return queueCoordinators.TryGetValue(name,out var owner)?owner.State:state;}
 bool QueueOwns(string name)=>queueCoordinators.TryGetValue(name,out var q)&&q.IsRunning;
 bool QueueReserves(string name)=>queueSaving.Contains(name)||queuePreparations.ContainsKey(name)||QueueOwns(name)||GetQueue(name).Locked;
 static QueueOutcome? SavedQueueOutcome(QuestJob job)=>QueueAttemptOutcome.Evaluate(job)??(job.Pending is null&&(job.Step=="complete"||QueueAttemptOutcome.HasTerminalReceipt(job))?QueueAttemptOutcome.Evaluate(job with{Status=job.PermanentCompletion is null?"Completed":"PermanentlyCompleted"}):null);
 bool HasUnfinishedCheckpoint(string name,string id){var job=journal.Load(name,id);return job.Pending is not null||job.Step is not ("preflight" or "complete");}
 bool CanSettleQueueCheckpoint(string name,string id){var outcome=SavedQueueOutcome(journal.Load(name,id));if(outcome is null)return false;var state=GetQueue(name);return state.ActiveQuest==id&&(state.ActiveResume||state.ActiveStartedAt is null||outcome.At>=state.ActiveStartedAt);}
 async Task EditQueue(Func<QuestQueueState,QuestQueueState> change){
  string name=Selected();if(name.Length==0||queueSaving.Contains(name))return;
  var saved=GetQueue(name);if(saved.Locked||queuePreparations.ContainsKey(name))throw new QuestBlockedException("Stop this queue before editing it.");
  queueSaving.Add(name);RefreshPlanner();
  try{var next=change(saved);if(!next.Quests.SequenceEqual(saved.Quests)||!next.ReturnHome)next=next with{HomePending=false};if(next.ActiveQuest!=next.Quests.FirstOrDefault())next=next with{ActiveQuest=null,ActiveStartedAt=null,ActiveResume=false};if(next.Quests.Any(q=>!recipes.ContainsKey(q)))throw new QuestBlockedException("Only quests with an automatic journey can be queued.");foreach(string id in next.Quests.Except(saved.Quests)){var entry=plannerEntries.Single(e=>e.RecipeId==id);var available=Availability(entry,name);if(!available.CanQueue)throw new QuestBlockedException(QuestName(id)+": "+available.Reason);}await queueStore.SaveAsync(next);queueCoordinators.Remove(name);queueCache[name]=next;}
  catch(Exception e){Log("Queue could not be saved: "+e.Message);Theme.Error(this,PlayerReason(e.Message),"Save queue");}
  finally{queueSaving.Remove(name);queueSignature="";RefreshPlanner();}
 }
 void RefreshQueueControls(string name,QuestQueueState queue,bool locked){
  queueHeading.Text=name.Length==0?"Your queue":name+"’s queue   "+queue.Quests.Length;
  queueHint.Text=locked?"Stop this run to edit its order.":"Drag to set the order of your journey.";
  string signature=name+"|"+locked+"|"+queue.ActiveQuest+"|"+queue.State+"|"+string.Join('|',queue.Quests);
  if(signature!=queueSignature){queueSignature=signature;queueRows.SuspendLayout();foreach(var old in queueRows.Controls.Cast<Control>().ToArray())old.Dispose();
   for(int i=0;i<queue.Quests.Length;i++){
    string id=queue.Quests[i];int index=i;string title=QuestName(id);var job=journal.Load(name,id);string detail=queue.ActiveQuest==id&&locked?queue.State=="Paused"?"Paused · checkpoint saved":"In progress":job.Pending is not null||job.Step!="preflight"&&job.CompletedAt is null?"Resume saved progress":"One verified attempt";
    var row=new PlannerQueueRow(name,id,title,detail,i,queue.Quests.Length,locked,queue.ActiveQuest==id&&locked);row.MoveRequested+=target=>_=MoveQueueQuest(name,id,target);row.Remove+=()=>_=EditQueue(s=>s with{Quests=s.Quests.Where(q=>q!=id).ToArray(),ActiveQuest=s.ActiveQuest==id?null:s.ActiveQuest});row.Dropped+=(moved,insertion)=>{int from=Array.IndexOf(queue.Quests,moved);int target=insertion-(from<insertion?1:0);_=MoveQueueQuest(name,moved,target);};queueRows.Controls.Add(row);
   }
   if(queue.Quests.Length==0){var empty=Theme.Label(queue.HomePending?"Returning to your saved home…":"Your next journey starts here.\nSelect quests from the library.",9,Theme.Muted);empty.Height=82;empty.TextAlign=ContentAlignment.MiddleCenter;empty.Margin=Padding.Empty;queueRows.Controls.Add(empty);}
   queueRows.Height=queueRows.Controls.Cast<Control>().Sum(c=>c.Height+c.Margin.Vertical);queueRows.ResumeLayout(true);queueRows.PerformLayout();
   queueCard!.Height=queueRows.Height+217;queueCard.Parent?.PerformLayout();
  }
  string reason=QueueStartReason(name,queue);bool running=QueueOwns(name);
  queueStart.Text=running?"Pause run":queue.State=="Paused"||queue.HomePending?"Resume run":$"Start queue · {queue.Quests.Length}";
  queueStart.Enabled=!queueSaving.Contains(name)&&!queuePreparations.ContainsKey(name)&&(running||reason.Length==0);
  queueStop.Enabled=running||queue.Locked||queuePreparations.ContainsKey(name);queueClear.Enabled=!locked&&queue.Quests.Length>0;
  queueReadiness.Text=running?"Running for "+name:queue.State=="Paused"?PlayerReason(queue.Reason):reason.Length>0?reason:"All selected quests are ready";
  queueReadiness.ForeColor=reason.Length==0||running?Theme.Mint:Theme.Muted;
  returnHome.Enabled=!locked&&queue.Home is not null;returnHome.Checked=queue.ReturnHome;returnHome.Text=queue.Home is null?"Set a home location in Characters":"Return home when finished";
 }
 string QueueStartReason(string name,QuestQueueState queue){
  if(name.Length==0)return "Add a character to plan your first journey";
  if(!sessions.TryGetValue(name,out var session)||!session.Snapshot.Connected)return "Connect "+name+" to start";
  if(controlActions.ContainsKey(name)||calibrationTasks.TryGetValue(name,out var task)&&!task.IsCompleted)return "Another action owns this character";
  if(runners.TryGetValue(name,out var runner)&&runner.IsRunning&&!QueueOwns(name))return "Pause the current quest before starting a queue";
  if(queue.HomePending)return "";
  if(queue.Quests.Length==0)return "Select a quest to get started";
  foreach(string id in queue.Quests){var entry=plannerEntries.FirstOrDefault(e=>e.RecipeId==id);if(entry is null)return QuestName(id)+": automatic journey unavailable";var availability=Availability(entry,name);if(queue.ActiveQuest==id&&(CanSettleQueueCheckpoint(name,id)||journal.Load(name,id).Pending is not null))continue;if(!availability.Ready)return QuestName(id)+": "+availability.Reason;}
  return "";
 }
 async Task ToggleQueueRun(){
  string name=Selected();if(QueueOwns(name)){await PauseQueue(name);return;}
  if(queueSaving.Contains(name)||queuePreparations.ContainsKey(name))throw new QuestBlockedException("Wait for this character’s queue to finish preparing.");
  using var preparing=new CancellationTokenSource();queuePreparations.Add(name,preparing);RefreshPlanner();
  QuestQueueCoordinator coordinator;
  try{
  var state=GetQueue(name);string blocker=QueueStartReason(name,state);if(blocker.Length>0)throw new QuestBlockedException(blocker);
  if(!state.HomePending)foreach(string id in state.Quests){var choice=await ConfigureQuest(name,recipes[id],state.ActiveQuest==id);if(!choice) return;}
  preparing.Token.ThrowIfCancellationRequested();
  // Choice dialogs may have been open while a connection or owner changed.
  blocker=QueueStartReason(name,state);if(blocker.Length>0)throw new QuestBlockedException(blocker);
  if(!queueCoordinators.TryGetValue(name,out coordinator!)){
   coordinator=new(queueStore,state,()=>sessions.TryGetValue(name,out var session)?session.Snapshot:new(){Name=name},async(id,resume,ct)=>{
    var entry=plannerEntries.First(e=>e.RecipeId==id);var availability=Availability(entry,name);if(!availability.Ready&&!(resume&&CanSettleQueueCheckpoint(name,id)))throw new QuestBlockedException(availability.Reason);
    // A crash between queue activation and runner preparation must start a fresh attempt,
    // unless this entry deliberately captured a preexisting unfinished journal.
    var active=GetQueue(name);var oldOutcome=SavedQueueOutcome(journal.Load(name,id));
    if(resume&&!active.ActiveResume&&active.ActiveStartedAt is {} started&&oldOutcome is not null&&oldOutcome.At<started)resume=false;
    var runner=await PrepareRunner(name,recipes[id]);await runner.RunAsync(repeatChoices.GetValueOrDefault((name.ToUpperInvariant(),id)),ct,startNewAttempt:!resume,singleAttempt:true);return runner.Job;
   },async(home,ct)=>{
    if(!sessions.TryGetValue(name,out var session))throw new QuestBlockedException("Connect this character to return home.");
    if(controlActions.ContainsKey(name))throw new QuestBlockedException("Another action owns this character.");
    using var owner=CancellationTokenSource.CreateLinkedTokenSource(ct);controlActions[name]=owner;void Interrupt(string reason)=>owner.Cancel();session.Interrupted+=Interrupt;
    try{await session.ExecuteAsync(new("navigate",home.Map,home.Position),owner.Token);}finally{session.Interrupted-=Interrupt;controlActions.Remove(name);}
   },id=>HasUnfinishedCheckpoint(name,id));
   queueCoordinators[name]=coordinator;coordinator.Changed+=value=>Ui(()=>{queueCache[name]=value;UpdateWorkspace();});
  }
  }catch(OperationCanceledException)when(preparing.IsCancellationRequested){return;}
  finally{queuePreparations.Remove(name);RefreshPlanner();}
  await RunQueueCoordinatorAsync(coordinator,sessions[name]);
 }
 internal async Task RunQueueCoordinatorAsync(QuestQueueCoordinator coordinator,IGameSession session){
  using var cancellation=new CancellationTokenSource();Task? interrupted=null;bool interruptionRequested=false;var gate=new object();long generation=session.Snapshot.Generation;string character=coordinator.State.Character;
  void Interrupt(string reason){lock(gate){if(interruptionRequested)return;interruptionRequested=true;cancellation.Cancel();interrupted=coordinator.PauseAsync(PlayerReason(reason));}}
  void Changed(CharacterSnapshot snapshot){if(!snapshot.Connected||snapshot.Generation!=generation||!string.Equals(snapshot.Name,character,StringComparison.OrdinalIgnoreCase))Interrupt("Character connection changed. Resume after fresh state.");}
  session.Interrupted+=Interrupt;session.Changed+=Changed;
  try{await coordinator.RunAsync(cancellation.Token);}catch(OperationCanceledException)when(cancellation.IsCancellationRequested){}
  finally{session.Interrupted-=Interrupt;session.Changed-=Changed;Task? settle;lock(gate)settle=interrupted;if(settle is not null)await settle;UpdateWorkspace();}
 }
 async Task MoveQueueQuest(string name,string id,int target){await EditQueue(s=>s.Reorder(id,target));if(name.Equals(Selected(),StringComparison.OrdinalIgnoreCase))queueRows.Controls.OfType<PlannerQueueRow>().FirstOrDefault(r=>r.Quest==id)?.Focus();}
 async Task PauseQueue(string name,bool stop=false){if(queuePreparations.TryGetValue(name,out var preparing))preparing.Cancel();await WaitForQueueSaves(name);if(queueCoordinators.TryGetValue(name,out var coordinator))await coordinator.PauseAsync(stop?"Stopped; progress saved":"Paused by player",stop);else{var state=GetQueue(name);if(state.Locked){var next=state with{State=stop?"Idle":"Paused",Reason=stop?"Stopped; progress saved":"Paused by player"};await queueStore.SaveAsync(next);queueCache[name]=next;}}UpdateWorkspace();}
 async Task SetHome(string name){
  if(QueueReserves(name))throw new QuestBlockedException("Stop this queue before changing its home.");
  if(!sessions.TryGetValue(name,out var session)||session.Snapshot is not {Connected:true,Map:{} map,Position:{} tile})throw new QuestBlockedException("Connect this character to save its current location as home.");
  var state=GetQueue(name);queueSaving.Add(name);RefreshPlanner();
  try{var next=state with{Home=new(map,tile,MapName(map)),Reason="Home saved"};await queueStore.SaveAsync(next);queueCoordinators.Remove(name);queueCache[name]=next;Log(name+": home saved at "+MapName(map));}
  finally{queueSaving.Remove(name);RefreshPlanner();}
 }
 async Task WaitForQueueSaves(string? character){while(queueSaving.Any(name=>character is null||name.Equals(character,StringComparison.OrdinalIgnoreCase)))await Task.Delay(20);}
 Control CreateHistoryPage(){
  var page=new Panel{Name="historyPage",Dock=DockStyle.Fill,Padding=new(26,20,26,16),BackColor=Theme.Ink};var header=new Panel{Dock=DockStyle.Top,Height=98};var title=Theme.Label("Run history",25);title.Font=Theme.Heading(25);title.Dock=DockStyle.Top;title.Height=49;var intro=Theme.Label("Verified outcomes and saved attempts, across your characters.",10,Theme.Muted);intro.Dock=DockStyle.Fill;header.Controls.Add(intro);header.Controls.Add(title);
  historyTable=new DarkDataGridView{Name="runHistory",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};foreach(string column in new[]{"Character","Quest","Outcome","When","Details"})historyTable.Columns.Add(column,column);historyTable.Columns[4].FillWeight=220;historyTable.DefaultCellStyle.WrapMode=DataGridViewTriState.True;historyTable.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
  int[] minimums=[100,150,145,150,180];for(int i=0;i<historyTable.Columns.Count;i++){historyTable.Columns[i].MinimumWidth=minimums[i];historyTable.Columns[i].DefaultCellStyle.WrapMode=DataGridViewTriState.True;}
  var empty=Theme.Label("History grows when a quest attempt is verified. Your existing journals stay with each character.",9,Theme.Muted);empty.Dock=DockStyle.Bottom;empty.Height=46;page.Controls.Add(historyTable);page.Controls.Add(header);page.Controls.Add(empty);Theme.ApplyControls(page);historyTable.DefaultCellStyle.WrapMode=DataGridViewTriState.True;RefreshHistoryPage();return page;
 }
 void RefreshHistoryPage(){
  if(historyTable?.Visible!=true)return;var rows=new List<(string Character,string Quest,string Outcome,DateTimeOffset At,string Summary)>();
  foreach(string name in settings.Characters.Concat(sessions.Keys).Distinct(StringComparer.OrdinalIgnoreCase)){
   var queue=GetQueue(name);rows.AddRange(queue.History.Select(h=>(name,QuestName(h.Quest),h.Outcome switch{"Completed"=>"Journey completed","AttemptCompleted"=>"Attempt recorded","PermanentlyCompleted"=>"Goal completed",_=>h.Outcome},h.At,h.Summary)));
   foreach(var recipe in recipes.Values){var job=journal.Load(name,recipe.Id);foreach(var attempt in job.HairstyleAttempts)if(!queue.History.Any(h=>h.Quest==recipe.Id&&h.At==attempt.CompletedAt))rows.Add((name,recipe.Name,attempt.LegendReward?"Goal completed":"Attempt recorded",attempt.CompletedAt,string.Join(" · ",attempt.Rewards.Select(r=>$"{r.Value:N0} {r.Key}"))));if(job.CompletedAt is {} at&&!queue.History.Any(h=>h.Quest==recipe.Id&&h.At==at))rows.Add((name,recipe.Name,QuestRunPolicy.Finished(recipe,job)?"Goal completed":"Attempt recorded",at,job.CompletionText??"Verified outcome saved"));}
  }
  string signature=string.Join('|',rows.Select(r=>r.Character+r.Quest+r.At.ToString("O")));if(historyTable.Tag as string==signature)return;historyTable.Tag=signature;historyTable.Rows.Clear();foreach(var r in rows.OrderByDescending(r=>r.At))historyTable.Rows.Add(r.Character,r.Quest,r.Outcome,r.At.ToLocalTime().ToString("MMM d, h:mm tt"),r.Summary);
 }
}
