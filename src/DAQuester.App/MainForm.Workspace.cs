using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App;

public sealed partial class MainForm {
 readonly CheckBox developerMode=new JournalCheckBox(){Name="developerMode",Text="Developer mode",AutoSize=false,Size=new(160,24)};
 readonly Panel developerTools=new(){Name="developerTools",Dock=DockStyle.Bottom,Height=248,Visible=false,BackColor=Theme.Surface,Padding=new(14)};
 readonly Label questTitle=Theme.Label("",25),questDescription=Theme.Label("",10,Theme.Muted),questFacts=Theme.Label("",10),questRegion=Theme.Label("",8.5f,Theme.Gold,true);
 readonly Label characterTitle=Theme.Label("",20,Theme.Text,true),characterState=Theme.Label("",10,Theme.Gold,true),playerMessage=Theme.Label("",10,Theme.Muted),location=Theme.Label("",10),rewardSummary=Theme.Label("",9,Theme.Mint);
 readonly Label rosterSummary=Theme.Label("",9,Theme.Muted),connectionSummary=Theme.Label("",9,Theme.Muted),conversationHeading=Theme.Label("Conversation",9,Theme.Muted,true);
 readonly TextBox conversation=new(){Name="playerConversation",Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.None,BorderStyle=BorderStyle.None};
 readonly StageTrack stages=new(){Dock=DockStyle.Fill};
 readonly JournalButton startButton=Theme.Button("Start quest",true),resumeButton=Theme.Button("Resume"),pauseButton=Theme.Button("Pause"),stopButton=Theme.Button("Stop");
 readonly Dictionary<int,string> mapNames=LoadMapNames();
 bool updatingWorkspace;
 readonly System.Windows.Forms.Timer cooldownTimer=new(){Interval=1000};
 DateTimeOffset? displayedCooldown;
 static Dictionary<int,string> LoadMapNames(){using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.Content,"routes.json")));return data.RootElement.EnumerateArray().ToDictionary(m=>m.GetProperty("id").GetInt32(),m=>m.GetProperty("name").GetString()??"Unknown area");}
 string MapName(int? map)=>map is {} id?mapNames.GetValueOrDefault(id,"Unknown area"):"Location appears after sign-in";
 bool IsSmoke=>Environment.GetCommandLineArgs().Any(a=>a is "--smoke" or "--screenshots");
 void SaveFastMode(){
  if(updatingWorkspace)return;string name=Selected();if(name.Length==0)return;
  var previous=settings.FastModeCharacters;
  settings.FastModeCharacters=previous.Where(n=>!n.Equals(name,StringComparison.OrdinalIgnoreCase)).Concat(fastMode.Checked?new[]{name}:Array.Empty<string>()).ToArray();
  try{settings.Save();if(sessions.TryGetValue(name,out var session))session.FastMode=fastMode.Checked;UpdateWorkspace();}
  catch(IOException e){settings.FastModeCharacters=previous;UpdateWorkspace();Theme.Error(this,e.Message,"Fast mode settings");}
 }
 void SavePresentationPreference(){try{File.WriteAllText(Path.Combine(AppSettings.DataPath,"appearance.json"),JsonSerializer.Serialize(new{developerMode=developerMode.Checked}));}catch(IOException e){Log("Appearance preference could not be saved: "+e.Message);}}
 JournalButton Navigation(string label,Func<Task> action){var b=Theme.Button(label);b.Quiet=true;b.Width=168;b.Height=44;b.Margin=new(0,0,0,4);Wire(b,action);return b;}
 void Wire(Button button,Func<Task> action){button.Click+=async(_,_)=>{try{await action();}catch(Exception e){Log(e.Message);Theme.Error(this,PlayerReason(e.Message));}finally{if(!IsDisposed)UpdateWorkspace();}};}
 Control BuildRoster(){
  grid.Name="characterRoster";
  grid.ScrollBars=ScrollBars.Vertical;
  foreach(string column in new[]{"Character","Connection","Level","Gold","Map / position","Progress","State","Step","Eligibility / blocker","Next run","Quest"})grid.Columns.Add(column,column);
  string[] headers=["Character","Connection","Level","Gold","Location","Collected","Status","Stage","Details","Next attempt","Quest"];
  for(int i=0;i<headers.Length;i++)grid.Columns[i].HeaderText=headers[i];
  foreach(int i in new[]{4,7,8,10})grid.Columns[i].Visible=false;
  grid.Columns[0].FillWeight=140;grid.Columns[1].FillWeight=110;grid.Columns[2].FillWeight=55;grid.Columns[3].FillWeight=85;grid.Columns[5].FillWeight=155;grid.Columns[6].FillWeight=105;grid.Columns[9].FillWeight=125;grid.Columns[2].MinimumWidth=56;
  grid.AllowUserToResizeColumns=true;grid.ShowCellToolTips=true;grid.ColumnHeadersHeight=36;
  foreach(string name in settings.Characters)AddCharacter(name);
  grid.SelectionChanged+=(_,_)=>ShowDialogState();
  var card=new JournalCard{Dock=DockStyle.Fill,Padding=new(16,8,16,5)};
  var heading=new Panel{Dock=DockStyle.Top,Height=34};var label=Theme.Label("Your characters",11,Theme.Text,true);label.Dock=DockStyle.Left;label.Width=200;rosterSummary.Dock=DockStyle.Right;rosterSummary.Width=300;rosterSummary.TextAlign=ContentAlignment.MiddleRight;heading.Controls.Add(label);heading.Controls.Add(rosterSummary);
  card.Controls.Add(grid);card.Controls.Add(heading);return card;
 }
 void BuildDeveloperTools(){
  var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(0,0,0,8)};
  void Add(string label,Func<Task> action){var b=Theme.Button(label);b.Width=150;Wire(b,action);toolbar.Controls.Add(b);}
  Add("Review calibration",Calibrate);Add("Auto-calibrate",async()=>{await StartAutomaticCalibration(Selected());});Add("Export replay",Export);Add("Connection profile",EditConnectionProfile);recording.Margin=new(12,12,0,0);toolbar.Controls.Add(recording);
  var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Vertical,Size=new(900,180),SplitterDistance=450,SplitterWidth=8};split.Panel1.Controls.Add(log);split.Panel2.Controls.Add(dialog);
  log.Font=new("Consolas",9);dialog.Font=new("Consolas",9);developerTools.Controls.Add(split);developerTools.Controls.Add(toolbar);
 }
 static string PlayerReason(string value)=>value.Replace("observed progress","progress",StringComparison.OrdinalIgnoreCase).Replace("server-observed","confirmed",StringComparison.OrdinalIgnoreCase).Replace("server identity","character details",StringComparison.OrdinalIgnoreCase).Replace("server level","level",StringComparison.OrdinalIgnoreCase).Replace("server inventory","inventory",StringComparison.OrdinalIgnoreCase).Replace("Local control","Developer tools",StringComparison.OrdinalIgnoreCase).Replace("local control","developer tools",StringComparison.OrdinalIgnoreCase).Replace("Connection replaced","Your character reconnected",StringComparison.OrdinalIgnoreCase).Replace("preflight","quest requirements",StringComparison.OrdinalIgnoreCase);
 internal void UpdateQuestActions(QuestDefinition quest,QuestJob job,bool connected,bool owned,bool controlled,bool repeat){
  bool otherQuest=job.Quest!=quest.Id,complete=!otherQuest&&QuestRunPolicy.Finished(quest,job);
  bool attempted=!otherQuest&&job.CompletedAt is not null&&job.Step=="complete",cooldown=QuestRunPolicy.NextAttempt(quest,job)>DateTimeOffset.UtcNow;
  startButton.Text=attempted?cooldown&&repeat?"Schedule":"Run again":"Start quest";
  bool reserved=QueueReserves(Selected()),busy=owned&&!otherQuest||controlled||reserved;
  startButton.Enabled=connected&&!busy&&!complete&&(!attempted||!cooldown||repeat)&&job.Pending is null;
  resumeButton.Enabled=connected&&!busy&&!complete&&(!attempted||repeat||job.Pending is not null)&&(job.Reason!="Ready"||job.Step!="preflight"||job.NextRun is not null||job.Pending is not null);
  pauseButton.Enabled=owned||controlled||reserved;stopButton.Enabled=pauseButton.Enabled;
 }
 void UpdateWorkspace(){
  if(updatingWorkspace||questChoice.SelectedItem is null)return;updatingWorkspace=true;
  try{
   string name=Selected();sessions.TryGetValue(name,out var session);var snapshot=session?.Snapshot;var queue=GetQueue(name);var quest=queue.Locked&&queue.ActiveQuest is {} activeQuest&&recipes.TryGetValue(activeQuest,out var activeRecipe)?activeRecipe:SelectedQuest;var job=quest.Id==SelectedQuest.Id?GetJob(name):runners.TryGetValue(name,out var activeRunner)&&activeRunner.Job.Quest==quest.Id?activeRunner.Job:journal.Load(name,quest.Id);bool connected=snapshot?.Connected==true;
   fastMode.Enabled=name.Length>0;fastMode.Checked=settings.FastModeEnabled(name);
   fastMode.Text="Fast mode · "+(fastMode.Checked?session?.FastModeDescription??"ready when connected":"slows near players");
   string? completedLegend=RememberedCharacter(name).CompletedLegend(quest.Id,snapshot);
   bool otherQuest=job.Quest!=quest.Id;bool complete=!otherQuest&&(QuestRunPolicy.Finished(quest,job)||completedLegend is not null);
   bool owned=runners.TryGetValue(name,out var runner)&&runner.IsRunning;
   bool scheduled=owned&&job.Status=="Scheduled",running=owned&&!scheduled;
   bool attempted=!otherQuest&&QuestRunPolicy.AttemptCompleted(job);
   bool hair=quest.Id=="perfect-hairstyle";
   repeats.Text=hair?"Weekly until Suomi legend":"Repeat every 8 hours";repeats.Visible=quest.Repeatable;
   repeats.Checked=quest.Repeatable&&!complete&&(hair||repeatChoices.GetValueOrDefault((name.ToUpperInvariant(),quest.Id),otherQuest?journal.Load(name,quest.Id).Repeat:job.Repeat));
   repeats.Enabled=quest.Repeatable&&!hair&&!complete&&!owned&&!QueueReserves(name)&&!controlActions.ContainsKey(name);
   var nextAttempt=otherQuest?null:QuestRunPolicy.NextAttempt(quest,job);bool cooldown=nextAttempt>DateTimeOffset.UtcNow;
   displayedCooldown=cooldown?nextAttempt:null;
   (string region,string description,string facts,string[] labels)=quest.Id switch{
    "mundane-birth"=>("MILETH · RIONA","Write your birthplace and season into your legend. Choose the details before your journey begins.","A lasting legend entry\nChoose a season, age 12–40, and hometown",new[]{"Choose details","Meet Riona","Record birth"}),
    "perfect-hairstyle"=>("SUOMI · EEVA","Visit Eeva for a ponytail and keep trying each week until your Suomi legend is awarded.","Goal: Loved by Suomi Mundanes\nNo items needed · reserve 2 slots and 2 weight",new[]{"Check legend","Visit Eeva","Check reward"}),
    "entered-muisir-village"=>("UNDINE · KIRIL","Gather mushrooms for Kiril and earn your Muisir legend along with four Shrinking Potions.","Reward: Muisir legend + 4 Shrinking Potions\nLevel 1+ · registered character · 5 mushrooms",new[]{"Check legend","Gather","Meet Kiril","Check reward"}),
    "emerald-dawn"=>("SUOMI · SHINEWOOD","Recover the Emerald Knight Insignia, then choose who receives it. Visit Gans first if you want his story.","Level 11+ · choose Knights, Necromancers, or gold\nRecovery legend + experience · one lasting outcome",new[]{"Ready","Mioas","Berg","Search","Recover","Gans","Deliver","Verify"}),
    "shinewood-faerie"=>("SHINEWOOD · FAERIE","Adopt a Pet Faerie and learn eligible spells you do not already know. Bring town songs/scrolls or ready dachaidh; this forest can be dangerous at lower levels.","Every class · reserve 1 slot and 5 weight for your pet\nReturn: town song → dachaidh → walk back",new[]{"Prepare","Pet & spells","Return home"}),
    _=>("MILETH · CALLOUGH","Lend Callough a hand. Gather six branches and bring them back to earn your rewards.","Reward: experience + items from Callough\nLevels 1–11 · 6 branches · excess gold is banked",new[]{"Get ready","Bank gold","Meet Callough","Gather","Turn in","Check reward"})
   };
   questRegion.Text=region;questTitle.Text=quest.Name;questDescription.Text=description;questFacts.Text=facts;
   var index=Array.FindIndex(quest.Steps,s=>s.Id==job.Step);stages.Stages=labels;stages.Current=otherQuest?0:Math.Clamp(index,0,labels.Length-1);stages.Complete=complete||attempted;stages.Invalidate();
   characterTitle.Text=name.Length==0?"Choose a character":name;
   characterState.Text=!connected?attempted&&!complete?"Completed for now · Offline":"Offline":otherQuest?"Another quest is active":complete?"Quest complete":job.Pending is not null?"Awaiting confirmation":scheduled?"Next attempt scheduled":running?"Quest in progress":attempted?cooldown?"Completed for now":"Ready to run again":job.Reason=="Ready"?"Ready to begin":"Quest paused";
   characterState.ForeColor=complete||running?Theme.Mint:!connected?Theme.Muted:Theme.Gold;
   playerMessage.Text=name.Length==0?"Add your character names in Characters, then choose Launch. Your quest progress will stay with each character.":!connected?attempted&&!complete&&nextAttempt is {} offlineNext?$"Last attempt completed. Next attempt: {offlineNext.ToLocalTime():MMM d, h:mm tt}. Sign in as {name} when you're ready.":$"Open Dark Ages and sign in as {name}. Your saved progress will be here when you connect.":otherQuest?$"{QuestName(job.Quest)} is active for this character. Starting this quest will pause it and save its progress.":complete?job.CompletionText??"Your quest result has been saved to this character's journal.":job.Pending is not null?"Your last quest action is awaiting confirmation. Resume will check its result before continuing.":scheduled&&job.NextRun is {} next?$"Your next attempt is {next.ToLocalTime():MMM d, h:mm tt}. Pause to hold the schedule.":attempted?cooldown?$"This attempt is complete. Next attempt: {nextAttempt!.Value.ToLocalTime():MMM d, h:mm tt}. Choose Repeat to schedule it.":"Your last rewards are saved. Run again for another attempt, or choose Repeat to continue every eight hours.":job.Reason=="Ready"?"Choose your quest, then start when you're ready. Progress is saved as you go.":PlayerReason(job.Reason);
   if(completedLegend is not null&&!otherQuest&&!owned){
    characterState.Text=job.Pending is not null?"Awaiting confirmation":connected?"Completed":"Completed · Offline";
    characterState.ForeColor=Theme.Mint;
    playerMessage.Text=job.Pending is not null?"Your legend goal is recorded. Resume checks the saved submission before confirming its rewards.":"Legend already recorded: "+completedLegend;
    stages.Complete=job.Pending is null;stages.Invalidate();
   }
   location.Text=!connected?"Sign in to see your character's location.":$"{MapName(snapshot!.Map)}\n"+(quest.Id=="spare-a-stick"?snapshot.InventoryKnown?$"Branches collected: {snapshot.Count(calibration.BranchName)} / 6":"Waiting for inventory…":quest.Id=="entered-muisir-village"?snapshot.InventoryKnown?$"Mushrooms collected: {snapshot.Count(MuisirEvidence.Mushroom)} / 5":"Waiting for inventory…":quest.Id=="mundane-birth"&&job.BirthOptions is {} birth?$"{birth.Season} · age {birth.Age} · {birth.Town}":"Your choices and rewards are saved automatically.");
   if(quest.Id=="emerald-dawn"&&connected)location.Text=$"{MapName(snapshot!.Map)}\n"+(job.EmeraldOptions?.Label??"Choose insignia recipient")+(job.EmeraldRecovery is not null?" · recovered":snapshot.InventoryKnown?$" · {snapshot.Count(EmeraldDawnEvidence.Insignia)}/1 insignia":"");
   if(quest.Id==ShinewoodFaerieEvidence.Id){
    if(connected)location.Text=$"{MapName(snapshot!.Map)}\nReturn: "+(ShinewoodFaerieEvidence.Escape(snapshot)?.Name??"walk back · prepare an escape");
    if(attempted){
     bool petWait=job.FaeriePetWait is {} wait&&ShinewoodFaerieEvidence.PetWaitPage(wait)&&!ShinewoodFaerieEvidence.HasPet(snapshot??new())&&!job.FaerieReceipts.Any(r=>r.Goal==ShinewoodFaerieEvidence.Pet);
     if(petWait)characterState.Text="Pet wait · one deoch"+(connected?"":" · Offline");
     playerMessage.Text=job.CompletionText+(petWait?". Check newly eligible spells on a later visit.":". Run again to check for newly eligible missing spells.");
    }
   }
   var d=snapshot?.Dialog;conversationHeading.Text=d is null?"Conversation":$"Conversation with {d.Name}";
   string conversationText=!connected?"Your character's conversations will appear here once you connect.":d is null?"No conversation right now. Quest conversations appear here as you meet each character.":d.Text+(d.Choices.Length==0?"":"\r\n\r\n"+string.Join("\r\n",d.Choices.Select(c=>c.Text)));
   if(conversation.Text!=conversationText)conversation.Text=conversationText;
   rewardSummary.Text=job.Rewards.Count>0?"Last rewards: "+string.Join(" · ",job.Rewards.Select(p=>$"{p.Value:N0} {p.Key}")):job.LastAttemptAt is {} attemptedAt?$"Last attempt: {attemptedAt.ToLocalTime():MMM d, h:mm tt}":"Progress is saved for each character and quest.";
   UpdateQuestActions(quest,job,connected,owned,controlActions.ContainsKey(name),repeats.Checked);
   if(RememberedCharacter(name).SelectionRestriction(quest.Id,snapshot) is {} restriction){
    startButton.Enabled=false;
    if(job.Pending is null){resumeButton.Enabled=false;if(!owned&&!complete){characterState.Text="Blocked";playerMessage.Text=restriction.Reason;}}
   }
   if(queue.HomePending){questTitle.Text="Return home";characterState.Text=queue.State=="Paused"?"Paused · progress saved":"Returning home";playerMessage.Text=queue.State=="Paused"?"Resume the queue to verify arrival at your saved home.":"Travelling to "+(queue.Home?.Label??"your saved home")+". Arrival must be confirmed before this journey finishes.";stages.Stages=[];stages.Invalidate();location.Text=connected?MapName(snapshot!.Map):"Sign in to resume the return journey.";rewardSummary.Text="Home: "+(queue.Home?.Label??"not set");}
   int online=sessions.Count(p=>p.Value.Snapshot.Connected);rosterSummary.Text=$"{grid.Rows.Count} characters · {online} online";connectionSummary.Text=ready?"Connection ready":"Preparing connection…";
  }finally{updatingWorkspace=false;RefreshPlanner();}
 }
}
