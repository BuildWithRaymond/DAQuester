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
 void BuildWorkspace() {
  playerMessage.Name="questProgressMessage";characterState.Name="questProgressState";
  Text="DAQuester · Quest journal";Size=new(1450,930);MinimumSize=new(1140,800);StartPosition=FormStartPosition.CenterScreen;Font=Theme.Body();BackColor=Theme.Ink;
  var rail=new Panel{Dock=DockStyle.Left,Width=208,BackColor=Theme.Surface,Padding=new(20,28,20,24)};
  var brand=Theme.Label("DAQuester",21,Theme.Text,true);brand.Dock=DockStyle.Top;brand.Height=44;
  var brandNote=Theme.Label("DARK AGES COMPANION",8,Theme.Gold,true);brandNote.Dock=DockStyle.Top;brandNote.Height=48;
  var navigation=new FlowLayoutPanel{Dock=DockStyle.Top,Height=246,FlowDirection=FlowDirection.TopDown,WrapContents=false};
  navigation.Controls.Add(PageNavigation("journal","Quest journal"));navigation.Controls.Add(PageNavigation("library","Quest library"));navigation.Controls.Add(PageNavigation("login","Account manager"));navigation.Controls.Add(PageNavigation("settings","Settings"));
  var railBottom=new Panel{Dock=DockStyle.Bottom,Height=148};
  developerMode.Location=new(8,7);railBottom.Controls.Add(developerMode);
  var stopAll=Navigation("Stop all quests",StopAll);stopAll.Danger=true;stopAll.Location=new(0,45);railBottom.Controls.Add(stopAll);
  var saved=Theme.Label("Your progress is saved\nautomatically.",9,Theme.Muted);saved.SetBounds(10,100,150,46);railBottom.Controls.Add(saved);
  rail.Controls.Add(navigation);rail.Controls.Add(brandNote);rail.Controls.Add(brand);rail.Controls.Add(railBottom);
  var main=new Panel{Name="journalPage",Dock=DockStyle.Fill,Padding=new(24,16,24,8),BackColor=Theme.Ink};
  var scroll=new DarkScrollPanel{Dock=DockStyle.Fill,BackColor=Theme.Ink};
  var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=5,Margin=Padding.Empty,BackColor=Theme.Ink};layout.ColumnStyles.Add(new(SizeType.Percent,100));
  foreach(int height in new[]{98,182,20,492,36})layout.RowStyles.Add(new(SizeType.Absolute,height));
  layout.Controls.Add(BuildHeader(),0,0);layout.Controls.Add(BuildRoster(),0,1);layout.Controls.Add(BuildQuestArea(),0,3);
  status.Dock=DockStyle.Fill;status.Font=Theme.Body(9);status.ForeColor=Theme.Muted;status.Padding=new(2,10,0,0);layout.Controls.Add(status,0,4);
  scroll.Controls.Add(layout);main.Controls.Add(scroll);BuildDeveloperTools();main.Controls.Add(developerTools);
  pages.Add("journal",main);pageHost.Controls.Add(main);Controls.Add(pageHost);Controls.Add(rail);Theme.Apply(this);
  questTitle.Font=Theme.Display(25);conversation.BackColor=Theme.Surface;conversation.BorderStyle=BorderStyle.None;
  repeats.Margin=new(0,3,0,0);repeats.Font=Theme.Body(9);repeats.CheckedChanged+=(_,_)=>SaveRepeatChoice();recording.CheckedChanged+=(_,_)=>recorder.Enabled=recording.Checked;
  fastMode.Font=Theme.Body(9);fastMode.CheckedChanged+=(_,_)=>SaveFastMode();
  developerMode.CheckedChanged+=(_,_)=>{developerTools.Visible=developerMode.Checked;UpdateWorkspace();if(!IsSmoke)SavePresentationPreference();};
  if(!IsSmoke){try{developerMode.Checked=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.DataPath,"appearance.json"))).RootElement.GetProperty("developerMode").GetBoolean();}catch(IOException){}catch(JsonException){}catch(KeyNotFoundException){}}
  cooldownTimer.Tick+=(_,_)=>{if(displayedCooldown is {} due&&due<=DateTimeOffset.UtcNow)UpdateWorkspace();};cooldownTimer.Start();Disposed+=(_,_)=>cooldownTimer.Dispose();
  UpdateWorkspace();
 }
 bool IsSmoke=>Environment.GetCommandLineArgs().Contains("--smoke");
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
 Control BuildHeader(){
  var panel=new Panel{Dock=DockStyle.Fill};
  var title=Theme.Label("Quest journal",24);title.Font=Theme.Heading();title.SetBounds(0,6,700,42);title.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;
  var subtitle=Theme.Label("Manage quests and saved progress for every character.",10,Theme.Muted);subtitle.SetBounds(2,53,700,24);subtitle.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;
  var launch=Theme.Button("Open Dark Ages",true);launch.Width=164;launch.Anchor=AnchorStyles.Top|AnchorStyles.Right;Wire(launch,Launch);
  panel.Controls.Add(title);panel.Controls.Add(subtitle);panel.Controls.Add(launch);panel.Controls.Add(connectionSummary);
  panel.Resize+=(_,_)=>{launch.SetBounds(panel.Width-164,16,164,Theme.ControlHeight);title.Width=subtitle.Width=Math.Max(200,panel.Width-208);connectionSummary.SetBounds(panel.Width-200,60,200,22);connectionSummary.TextAlign=ContentAlignment.MiddleRight;};return panel;
 }
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
 Control BuildQuestArea(){
  var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};columns.ColumnStyles.Add(new(SizeType.Percent,60));columns.ColumnStyles.Add(new(SizeType.Absolute,Theme.Space*4));columns.ColumnStyles.Add(new(SizeType.Percent,40));
  var quest=new JournalCard{Dock=DockStyle.Fill,Margin=Padding.Empty};var questLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=10,Margin=Padding.Empty};questLayout.ColumnStyles.Add(new(SizeType.Percent,100));
  foreach(int height in new[]{22,40,108,58,74,30,30,48,28})questLayout.RowStyles.Add(new(SizeType.Absolute,height));questLayout.RowStyles.Add(new(SizeType.Percent,100));
  questRegion.Dock=DockStyle.Fill;questLayout.Controls.Add(questRegion,0,0);
  questChoice.Dock=DockStyle.Fill;questChoice.Margin=new(0,0,0,6);questChoice.AccessibleName="Choose a quest";questLayout.Controls.Add(questChoice,0,1);
  var hero=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};hero.ColumnStyles.Add(new(SizeType.Percent,100));hero.ColumnStyles.Add(new(SizeType.Absolute,112));hero.RowStyles.Add(new(SizeType.Absolute,57));hero.RowStyles.Add(new(SizeType.Percent,100));
  questTitle.Dock=DockStyle.Fill;questDescription.Dock=DockStyle.Fill;hero.Controls.Add(questTitle,0,0);hero.Controls.Add(questDescription,0,1);var artwork=new JournalArtwork{Dock=DockStyle.Fill};hero.Controls.Add(artwork,1,0);hero.SetRowSpan(artwork,2);questLayout.Controls.Add(hero,0,2);
  questFacts.Dock=DockStyle.Fill;questFacts.Padding=new(0,8,0,0);questLayout.Controls.Add(questFacts,0,3);questLayout.Controls.Add(stages,0,4);
  repeats.Dock=DockStyle.Fill;repeats.AutoSize=false;questLayout.Controls.Add(repeats,0,5);
  fastMode.Dock=DockStyle.Fill;fastMode.Margin=new(0,0,0,3);questLayout.Controls.Add(fastMode,0,6);
  var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};for(int i=0;i<4;i++)actions.ColumnStyles.Add(new(SizeType.Percent,25));
  Wire(startButton,()=>StartQuest());Wire(resumeButton,()=>StartQuest(true));Wire(pauseButton,()=>Pause("Paused by player"));Wire(stopButton,()=>Pause("Stopped; observed progress saved"));
  Button[] buttons=[startButton,resumeButton,pauseButton,stopButton];string[] names=["startQuest","resumeQuest","pauseQuest","stopQuest"];for(int i=0;i<4;i++){buttons[i].Name=names[i];buttons[i].Dock=DockStyle.Fill;buttons[i].Margin=new(i==0?0:Theme.Space,0,i==3?0:Theme.Space,Theme.Space*2);actions.Controls.Add(buttons[i],i,0);}questLayout.Controls.Add(actions,0,7);
  void SizeActionColumns(){
   var widths=buttons.Select(button=>TextRenderer.MeasureText(button.Text,button.Font,Size.Empty,TextFormatFlags.SingleLine).Width+2*(int)Math.Round(12*button.DeviceDpi/96f)+button.Margin.Horizontal+Theme.Space).ToArray();
   int total=widths.Sum();for(int i=0;i<widths.Length;i++)actions.ColumnStyles[i].Width=100f*widths[i]/total;
  }
  actions.Resize+=(_,_)=>SizeActionColumns();foreach(var button in buttons){button.TextChanged+=(_,_)=>SizeActionColumns();button.FontChanged+=(_,_)=>SizeActionColumns();button.DpiChangedAfterParent+=(_,_)=>SizeActionColumns();}SizeActionColumns();
  var reassurance=Theme.Label("Manual movement pauses your quest. You stay in control.",8.5f,Theme.Muted);reassurance.Dock=DockStyle.Fill;reassurance.Padding=new(0,5,0,0);questLayout.Controls.Add(reassurance,0,8);quest.Controls.Add(questLayout);
  var journey=new JournalCard{Dock=DockStyle.Fill,Margin=Padding.Empty};var details=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=9,Margin=Padding.Empty};details.ColumnStyles.Add(new(SizeType.Percent,100));
  foreach(int h in new[]{22,40,28,65,50,26})details.RowStyles.Add(new(SizeType.Absolute,h));details.RowStyles.Add(new(SizeType.Percent,100));details.RowStyles.Add(new(SizeType.Absolute,48));
  var detailHeading=Theme.Label("CURRENT JOURNEY",8.5f,Theme.Gold,true);detailHeading.Dock=DockStyle.Fill;details.Controls.Add(detailHeading,0,0);
  Control[] texts=[characterTitle,characterState,playerMessage,location,conversationHeading,conversation,rewardSummary];for(int i=0;i<texts.Length;i++){texts[i].Dock=DockStyle.Fill;details.Controls.Add(texts[i],0,i+1);}rewardSummary.Padding=new(0,10,0,0);journey.Controls.Add(details);
  columns.Controls.Add(quest,0,0);columns.Controls.Add(journey,2,0);return columns;
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
  bool busy=owned&&!otherQuest||controlled;
  startButton.Enabled=connected&&!busy&&!complete&&(!attempted||!cooldown||repeat)&&job.Pending is null;
  resumeButton.Enabled=connected&&!busy&&!complete&&(!attempted||repeat||job.Pending is not null)&&(job.Reason!="Ready"||job.Step!="preflight"||job.NextRun is not null||job.Pending is not null);
  pauseButton.Enabled=owned||controlled;stopButton.Enabled=pauseButton.Enabled;
 }
 void UpdateWorkspace(){
  if(updatingWorkspace||questChoice.SelectedItem is null)return;updatingWorkspace=true;
  try{
   string name=Selected();sessions.TryGetValue(name,out var session);var snapshot=session?.Snapshot;var job=GetJob(name);bool connected=snapshot?.Connected==true;
   fastMode.Enabled=name.Length>0;fastMode.Checked=settings.FastModeEnabled(name);
   fastMode.Text="Fast mode · "+(fastMode.Checked?session?.FastModeDescription??"ready when connected":"slows near players");
   var quest=SelectedQuest;bool otherQuest=job.Quest!=quest.Id;bool complete=!otherQuest&&QuestRunPolicy.Finished(quest,job);
   bool owned=runners.TryGetValue(name,out var runner)&&runner.IsRunning;
   bool scheduled=owned&&job.Status=="Scheduled",running=owned&&!scheduled;
   bool attempted=!otherQuest&&QuestRunPolicy.AttemptCompleted(job);
   bool hair=quest.Id=="perfect-hairstyle";
   repeats.Text=hair?"Weekly until Suomi legend":"Repeat every 8 hours";repeats.Visible=quest.Repeatable;
   repeats.Checked=quest.Repeatable&&!complete&&(hair||repeatChoices.GetValueOrDefault((name.ToUpperInvariant(),quest.Id),otherQuest?journal.Load(name,quest.Id).Repeat:job.Repeat));
   repeats.Enabled=quest.Repeatable&&!hair&&!complete&&!owned&&!controlActions.ContainsKey(name);
   var nextAttempt=otherQuest?null:QuestRunPolicy.NextAttempt(quest,job);bool cooldown=nextAttempt>DateTimeOffset.UtcNow;
   displayedCooldown=cooldown?nextAttempt:null;
   (string region,string description,string facts,string[] labels)=quest.Id switch{
    "mundane-birth"=>("MILETH · RIONA","Write your birthplace and season into your legend. Choose the details before your journey begins.","A lasting legend entry\nChoose a season, age 12–40, and hometown",new[]{"Choose details","Meet Riona","Record birth"}),
    "perfect-hairstyle"=>("SUOMI · EEVA","Visit Eeva for a ponytail and keep trying each week until your Suomi legend is awarded.","Goal: Loved by Suomi Mundanes\nNo items needed · reserve 2 slots and 2 weight",new[]{"Check legend","Visit Eeva","Check reward"}),
    "entered-muisir-village"=>("UNDINE · KIRIL","Gather mushrooms for Kiril and earn your Muisir legend along with four Shrinking Potions.","Reward: Muisir legend + 4 Shrinking Potions\nLevel 1+ · registered character · 5 mushrooms",new[]{"Check legend","Gather","Meet Kiril","Check reward"}),
    _=>("MILETH · CALLOUGH","Lend Callough a hand. Gather six branches and bring them back to earn your rewards.","Reward: experience + items from Callough\nLevels 1–11 · 6 branches · excess gold is banked",new[]{"Get ready","Bank gold","Meet Callough","Gather","Turn in","Check reward"})
   };
   questRegion.Text=region;questTitle.Text=quest.Name;questDescription.Text=description;questFacts.Text=facts;
   var index=Array.FindIndex(quest.Steps,s=>s.Id==job.Step);stages.Stages=labels;stages.Current=otherQuest?0:Math.Clamp(index,0,labels.Length-1);stages.Complete=complete||attempted;stages.Invalidate();
   characterTitle.Text=name.Length==0?"Choose a character":name;
   characterState.Text=!connected?attempted&&!complete?"Completed for now · Offline":"Offline":otherQuest?"Another quest is active":complete?"Quest complete":job.Pending is not null?"Awaiting confirmation":scheduled?"Next attempt scheduled":running?"Quest in progress":attempted?cooldown?"Completed for now":"Ready to run again":job.Reason=="Ready"?"Ready to begin":"Quest paused";
   characterState.ForeColor=complete||running?Theme.Mint:!connected?Theme.Muted:Theme.Gold;
   playerMessage.Text=name.Length==0?"Add your character names in Account manager, then open Dark Ages and sign in. Your quest progress will stay with each character.":!connected?attempted&&!complete&&nextAttempt is {} offlineNext?$"Last attempt completed. Next attempt: {offlineNext.ToLocalTime():MMM d, h:mm tt}. Sign in as {name} when you're ready.":$"Open Dark Ages and sign in as {name}. Your saved progress will be here when you connect.":otherQuest?$"{QuestName(job.Quest)} is active for this character. Starting this quest will pause it and save its progress.":complete?job.CompletionText??"Your quest result has been saved to this character's journal.":job.Pending is not null?"Your last quest action is awaiting confirmation. Resume will check its result before continuing.":scheduled&&job.NextRun is {} next?$"Your next attempt is {next.ToLocalTime():MMM d, h:mm tt}. Pause to hold the schedule.":attempted?cooldown?$"This attempt is complete. Next attempt: {nextAttempt!.Value.ToLocalTime():MMM d, h:mm tt}. Choose Repeat to schedule it.":"Your last rewards are saved. Run again for another attempt, or choose Repeat to continue every eight hours.":job.Reason=="Ready"?"Choose your quest, then start when you're ready. Progress is saved as you go.":PlayerReason(job.Reason);
   location.Text=!connected?"Sign in to see your character's location.":$"{MapName(snapshot!.Map)}\n"+(quest.Id=="spare-a-stick"?snapshot.InventoryKnown?$"Branches collected: {snapshot.Count(calibration.BranchName)} / 6":"Waiting for inventory…":quest.Id=="entered-muisir-village"?snapshot.InventoryKnown?$"Mushrooms collected: {snapshot.Count(MuisirEvidence.Mushroom)} / 5":"Waiting for inventory…":quest.Id=="mundane-birth"&&job.BirthOptions is {} birth?$"{birth.Season} · age {birth.Age} · {birth.Town}":"Your choices and rewards are saved automatically.");
   var d=snapshot?.Dialog;conversationHeading.Text=d is null?"Conversation":$"Conversation with {d.Name}";
   string conversationText=!connected?"Your character's conversations will appear here once you connect.":d is null?"No conversation right now. Quest conversations appear here as you meet each character.":d.Text+(d.Choices.Length==0?"":"\r\n\r\n"+string.Join("\r\n",d.Choices.Select(c=>c.Text)));
   if(conversation.Text!=conversationText)conversation.Text=conversationText;
   rewardSummary.Text=job.Rewards.Count>0?"Last rewards: "+string.Join(" · ",job.Rewards.Select(p=>$"{p.Value:N0} {p.Key}")):job.LastAttemptAt is {} attemptedAt?$"Last attempt: {attemptedAt.ToLocalTime():MMM d, h:mm tt}":"Progress is saved for each character and quest.";
   UpdateQuestActions(quest,job,connected,owned,controlActions.ContainsKey(name),repeats.Checked);
   int online=sessions.Count(p=>p.Value.Snapshot.Connected);rosterSummary.Text=$"{grid.Rows.Count} characters · {online} online";connectionSummary.Text=ready?"Connection ready":"Preparing connection…";
  }finally{updatingWorkspace=false;}
 }
}
