using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App;

public sealed partial class MainForm {
 readonly TextBox plannerSearch=new JournalTextBox{Name="plannerSearch",PlaceholderText="Search quests, rewards, or requirements",Dock=DockStyle.Fill};
 readonly JournalComboBox characterSwitcher=new(){Name="characterSwitcher",Dock=DockStyle.Top,Height=30};
 readonly CharacterSeal characterSeal=new();
 readonly PlannerStack questRows=new(){Name="plannerQuestList"};
 readonly PlannerStack queueRows=new(){Name="plannerQueue"};
 readonly Label libraryCount=Theme.Label("",8.5f,Theme.Muted),libraryCompletion=Theme.Label("",8.5f,Theme.Mint),queueHeading=Theme.Label("",12,Theme.Text,true),queueHint=Theme.Label("",8.5f,Theme.Muted),queueReadiness=Theme.Label("",9,Theme.Mint);
 readonly JournalButton queueStart=Theme.Button("Start queue",true),queueStop=Theme.Button("Stop"),queueClear=Theme.Button("Clear");
 readonly JournalCheckBox returnHome=new(){Name="queueReturnHome",Text="Return home when finished"};
 readonly Dictionary<string,JournalButton> plannerFilters=[];
 readonly List<PlannerEntry> plannerEntries=[];
 readonly Dictionary<string,int> filterCounts=[];
 readonly JournalButton journeyDetails=Theme.Button("Quest controls");
 readonly JournalButton journeyRecentActivity=Theme.Button("Recent activity");
 readonly Label journeyNext=Theme.Label("",8.5f,Theme.Muted),journeyActivity=Theme.Label("",8.5f,Theme.Muted);
 PlannerStack? journeyStack;
 JournalCard? journeyCard;
 PlannerStack? journeyControls;
 JournalCard? queueCard;
 string plannerFilter="all";
 bool updatingPlanner;
 string rowSignature="",queueSignature="";

 void BuildWorkspace(){
  Text="DAQuester · Quest planner";Size=new(1450,930);MinimumSize=new(1140,800);StartPosition=FormStartPosition.CenterScreen;Font=Theme.Body();BackColor=Theme.Ink;
  playerMessage.Name="questProgressMessage";characterState.Name="questProgressState";
  LoadPlannerEntries();
  var rail=new Panel{Dock=DockStyle.Left,Width=202,BackColor=Color.FromArgb(16,25,37),Padding=new(18,25,18,20)};
  var brand=new PlannerBrand{Dock=DockStyle.Top};
  var navigation=new FlowLayoutPanel{Dock=DockStyle.Top,Height=266,FlowDirection=FlowDirection.TopDown,WrapContents=false};
  navigation.Controls.Add(PageNavigation("journal","Quest planner"));navigation.Controls.Add(PageNavigation("history","Run history"));navigation.Controls.Add(PageNavigation("login","Characters"));navigation.Controls.Add(PageNavigation("settings","Settings"));
  var bottom=new Panel{Dock=DockStyle.Bottom,Height=162};
  developerMode.SetBounds(6,0,160,26);bottom.Controls.Add(developerMode);
  var saved=Theme.Label("Progress saved\nfor each character",9,Theme.Mint);saved.SetBounds(8,43,158,42);bottom.Controls.Add(saved);
  var stopAll=Navigation("Stop all quests",StopAll);stopAll.Name="stopAllQuests";stopAll.Danger=true;stopAll.SetBounds(0,99,166,40);bottom.Controls.Add(stopAll);
  var line=new Panel{Dock=DockStyle.Right,Width=1,BackColor=Theme.Line};rail.Controls.Add(line);rail.Controls.Add(navigation);rail.Controls.Add(brand);rail.Controls.Add(bottom);
  var main=new Panel{Name="journalPage",Dock=DockStyle.Fill,Padding=new(26,18,26,12),BackColor=Theme.Ink};
  var header=BuildPlannerHeader();header.Dock=DockStyle.Top;header.Height=90;
  var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty,BackColor=Theme.Ink};body.ColumnStyles.Add(new(SizeType.Percent,100));body.ColumnStyles.Add(new(SizeType.Absolute,18));body.ColumnStyles.Add(new(SizeType.Absolute,338));body.RowStyles.Add(new(SizeType.Percent,100));body.Controls.Add(BuildPlannerLibrary(),0,0);
  var right=new DarkScrollPanel{Dock=DockStyle.Fill,BackColor=Theme.Ink};var stack=new PlannerStack{BackColor=Theme.Ink};stack.Controls.Add(BuildPlannerQueue());stack.Controls.Add(BuildPlannerJourney());right.Controls.Add(stack);body.Controls.Add(right,2,0);
  body.Resize+=(_,_)=>body.ColumnStyles[2].Width=body.Width<1000?306:338;
  status.Dock=DockStyle.Bottom;status.Height=30;status.Font=Theme.Body(8.5f);status.ForeColor=Theme.Muted;status.Padding=new(0,10,0,0);
  var hiddenRoster=BuildRoster();hiddenRoster.Visible=false;hiddenRoster.Size=new(1,1);hiddenRoster.Name="legacyRoster";
  main.Controls.Add(body);main.Controls.Add(header);main.Controls.Add(status);main.Controls.Add(hiddenRoster);BuildDeveloperTools();main.Controls.Add(developerTools);
  pages.Add("journal",main);pageHost.Controls.Add(main);Controls.Add(pageHost);Controls.Add(rail);Theme.Apply(this);
  questTitle.Font=Theme.Heading(14);characterState.Font=Theme.Body(9);playerMessage.Font=Theme.Body(9);conversation.BackColor=Theme.Surface;
  repeats.Font=Theme.Body(8.5f);repeats.CheckedChanged+=(_,_)=>SaveRepeatChoice();recording.CheckedChanged+=(_,_)=>recorder.Enabled=recording.Checked;
  fastMode.Font=Theme.Body(8.5f);fastMode.CheckedChanged+=(_,_)=>SaveFastMode();
  developerMode.CheckedChanged+=(_,_)=>{developerTools.Visible=developerMode.Checked;UpdateWorkspace();if(!IsSmoke)SavePresentationPreference();};
  if(!IsSmoke){try{developerMode.Checked=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.DataPath,"appearance.json"))).RootElement.GetProperty("developerMode").GetBoolean();}catch(IOException){}catch(JsonException){}catch(KeyNotFoundException){}}
  cooldownTimer.Tick+=(_,_)=>{if(displayedCooldown is {} due&&due<=DateTimeOffset.UtcNow)UpdateWorkspace();};cooldownTimer.Start();Disposed+=(_,_)=>cooldownTimer.Dispose();
  UpdateWorkspace();
 }

 void LoadPlannerEntries(){
  var definitions=recipes.Values.ToDictionary(q=>q.Name,StringComparer.OrdinalIgnoreCase);
  using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.Content,"catalog.json")));
  foreach(var q in doc.RootElement.GetProperty("entries").EnumerateArray()){
   string Get(string key)=>q.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
   string name=Get("name");definitions.TryGetValue(name,out var definition);
   plannerEntries.Add(new(Get("id"),name,Get("rewards"),Get("requiredItems"),Get("level"),Get("status"),Get("source"),definition?.Id));
  }
  plannerEntries.Sort((a,b)=>{int ready=(b.RecipeId is not null).CompareTo(a.RecipeId is not null);return ready!=0?ready:a.Name.CompareTo(b.Name);});
 }

 JournalButton? launchButton;
 readonly JournalTip tip=new();
 Control BuildPlannerHeader(){
  var header=new Panel();var title=Theme.Label("Quest planner",25);title.Font=Theme.Heading(25);title.SetBounds(0,4,550,40);var sub=Theme.Label("Your quests. Your order. Your next adventure.",10,Theme.Muted);sub.SetBounds(1,48,550,26);
  var account=new JournalCard{Padding=new(10),Width=254,Height=70,Anchor=AnchorStyles.Top|AnchorStyles.Right};characterSeal.Location=new(10,14);account.Controls.Add(characterSeal);
  var info=new Panel{Location=new(59,7),Size=new(185,54)};characterSwitcher.Height=28;connectionSummary.Dock=DockStyle.Bottom;connectionSummary.Height=19;connectionSummary.Font=Theme.Body(8);info.Controls.Add(connectionSummary);info.Controls.Add(characterSwitcher);account.Controls.Add(info);
  var launch=Theme.Button("Launch");launch.Name="launchSelectedCharacter";launch.Width=88;launch.Height=40;Wire(launch,()=>LaunchCharacterAsync(Selected()));launchButton=launch;
  header.Controls.AddRange([title,sub,account,launch]);header.Resize+=(_,_)=>{account.Location=new(header.Width-254,4);launch.Location=new(header.Width-354,16);title.Width=sub.Width=Math.Max(150,header.Width-375);};
  // A disabled button gets no mouse messages; its parent shows the blocker instead.
  bool blockerShown=false;
  header.MouseMove+=(_,e)=>{bool over=!launch.Enabled&&launch.Bounds.Contains(e.Location);if(over==blockerShown)return;blockerShown=over;if(over)tip.ShowAt(launch.AccessibleDescription??"",header,new(launch.Left,launch.Bottom+4));else tip.Hide(header);};
  header.MouseLeave+=(_,_)=>{if(!blockerShown)return;blockerShown=false;tip.Hide(header);};
  Disposed+=(_,_)=>tip.Dispose();
  characterSwitcher.SelectedIndexChanged+=(_,_)=>{if(updatingPlanner||characterSwitcher.SelectedItem is not string name)return;SelectCharacter(name);};return header;
 }

 Control BuildPlannerLibrary(){
  var card=new JournalCard{Dock=DockStyle.Fill,Padding=new(1),Margin=Padding.Empty};var top=new Panel{Dock=DockStyle.Top,Height=151,Padding=new(16)};
  var heading=Theme.Label("Quest library",12,Theme.Text,true);heading.SetBounds(16,16,210,27);libraryCompletion.TextAlign=ContentAlignment.MiddleRight;libraryCompletion.SetBounds(230,16,250,27);libraryCompletion.Anchor=AnchorStyles.Top|AnchorStyles.Right;
  var search=new InputField(plannerSearch){Dock=DockStyle.None,Location=new(16,54),Height=37,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};top.Controls.Add(search);top.Controls.Add(heading);top.Controls.Add(libraryCompletion);
  var filters=new FlowLayoutPanel{Location=new(13,101),Height=41,WrapContents=false,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
  foreach(var (id,label) in new[]{("all","All quests"),("ready","Ready"),("completed","Completed"),("unavailable","Unavailable")}){var button=Theme.Button(label);button.Name="plannerFilter"+id;button.Quiet=true;button.Compact=true;button.Font=Theme.Body(8.5f);button.Width=id=="unavailable"?118:100;button.Height=36;button.Margin=new(0,0,4,0);button.Click+=(_,_)=>{plannerFilter=id;RefreshPlanner();};plannerFilters.Add(id,button);filters.Controls.Add(button);}top.Controls.Add(filters);
  top.Resize+=(_,_)=>{search.Width=Math.Max(1,top.Width-32);libraryCompletion.Left=Math.Max(165,top.Width-280);libraryCompletion.Width=Math.Max(1,top.Width-libraryCompletion.Left-16);heading.Width=Math.Min(210,libraryCompletion.Left-16);filters.Width=top.Width-26;foreach(Control filter in filters.Controls)filter.Width=Math.Max(70,(filters.Width-16)/4);};
  var scroll=new DarkScrollPanel{Dock=DockStyle.Fill,BackColor=Theme.Surface};scroll.Controls.Add(questRows);
  var foot=new Panel{Dock=DockStyle.Bottom,Height=39,Padding=new(16,0,16,0)};libraryCount.Dock=DockStyle.Fill;libraryCount.TextAlign=ContentAlignment.MiddleLeft;foot.Controls.Add(libraryCount);
  plannerSearch.TextChanged+=(_,_)=>RefreshPlanner();card.Controls.Add(scroll);card.Controls.Add(top);card.Controls.Add(foot);return card;
 }

 Control BuildPlannerQueue(){
  queueCard=new JournalCard{Height=292,Padding=new(14),Margin=new(0,0,0,14)};var stack=new PlannerStack();
  var heading=new Panel{Height=35,Margin=Padding.Empty};queueHeading.Dock=DockStyle.Fill;queueClear.Name="clearQueue";queueClear.Quiet=true;queueClear.Font=Theme.Body(8.5f);queueClear.Dock=DockStyle.Right;queueClear.Width=72;queueClear.Height=28;Wire(queueClear,()=>EditQueue(s=>s with{Quests=[],ActiveQuest=null}));heading.Controls.Add(queueHeading);heading.Controls.Add(queueClear);stack.Controls.Add(heading);
  queueHint.Height=31;queueHint.Margin=Padding.Empty;stack.Controls.Add(queueHint);queueRows.Height=112;queueRows.Margin=Padding.Empty;stack.Controls.Add(queueRows);
  queueReadiness.Height=38;queueReadiness.Margin=new(0,7,0,0);stack.Controls.Add(queueReadiness);
  var actions=new TableLayoutPanel{Height=42,ColumnCount=3,RowCount=1,Margin=Padding.Empty};actions.ColumnStyles.Add(new(SizeType.Percent,100));actions.ColumnStyles.Add(new(SizeType.Absolute,8));actions.ColumnStyles.Add(new(SizeType.Absolute,76));actions.RowStyles.Add(new(SizeType.Percent,100));queueStart.Name="startQueue";queueStop.Name="stopQueue";queueStart.Dock=queueStop.Dock=DockStyle.Fill;queueStart.Margin=queueStop.Margin=Padding.Empty;queueStart.Font=queueStop.Font=Theme.Body(9,FontStyle.Bold);Wire(queueStart,ToggleQueueRun);Wire(queueStop,()=>PauseQueue(Selected(),true));actions.Controls.Add(queueStart,0,0);actions.Controls.Add(queueStop,2,0);stack.Controls.Add(actions);
  returnHome.Height=28;returnHome.Margin=new(0,8,0,0);returnHome.Font=Theme.Body(8);returnHome.CheckedChanged+=async(_,_)=>{if(!updatingPlanner)await EditQueue(s=>s with{ReturnHome=returnHome.Checked});};stack.Controls.Add(returnHome);stack.Dock=DockStyle.Fill;queueCard.Controls.Add(stack);return queueCard;
 }

 Control BuildPlannerJourney(){
  var card=journeyCard=new JournalCard{Padding=new(16),Margin=Padding.Empty};var stack=journeyStack=new PlannerStack{Dock=DockStyle.Fill};
  void Add(Control c,int height,int space=0){c.Dock=DockStyle.None;c.Height=height;c.Margin=new(0,0,0,space);stack.Controls.Add(c);}
  Add(Theme.Label("CURRENT JOURNEY",8.5f,Theme.Gold,true),24);Add(questTitle,30);Add(characterState,22);Add(playerMessage,52,4);Add(stages,46,4);
  journeyNext.Name="journeyNext";journeyNext.AutoEllipsis=true;Add(journeyNext,28,4);
  var disclosures=new TableLayoutPanel{ColumnCount=3,RowCount=1,Margin=Padding.Empty};disclosures.ColumnStyles.Add(new(SizeType.Percent,50));disclosures.ColumnStyles.Add(new(SizeType.Absolute,4));disclosures.ColumnStyles.Add(new(SizeType.Percent,50));disclosures.RowStyles.Add(new(SizeType.Percent,100));
  journeyDetails.Name="journeyDetails";journeyRecentActivity.Name="journeyRecentActivity";foreach(var button in new[]{journeyDetails,journeyRecentActivity}){button.Quiet=true;button.Compact=true;button.Font=Theme.Body(8.5f);button.Dock=DockStyle.Fill;button.Margin=Padding.Empty;}disclosures.Controls.Add(journeyDetails,0,0);disclosures.Controls.Add(journeyRecentActivity,2,0);Add(disclosures,36);
  journeyActivity.Name="journeyActivity";journeyActivity.AutoEllipsis=true;journeyActivity.Height=80;journeyActivity.Margin=new(0,8,0,0);
  journeyControls=new PlannerStack{Margin=Padding.Empty};stack=journeyControls;
  Add(location,43,2);Add(rewardSummary,34,8);
  Add(Theme.Label("Inspect a quest",8.5f,Theme.Muted),23);questChoice.Name="questSelector";Add(questChoice,34,8);
  Add(repeats,26);Add(fastMode,26,10);
  var actions=new TableLayoutPanel{ColumnCount=3,RowCount=2,Margin=Padding.Empty};actions.ColumnStyles.Add(new(SizeType.Percent,50));actions.ColumnStyles.Add(new(SizeType.Absolute,8));actions.ColumnStyles.Add(new(SizeType.Percent,50));actions.RowStyles.Add(new(SizeType.Percent,50));actions.RowStyles.Add(new(SizeType.Percent,50));
  foreach(var (button,x,y,name,action) in new[]{(startButton,0,0,"startQuest",(Func<Task>)(()=>StartQuest())),(resumeButton,2,0,"resumeQuest",()=>StartQuest(true)),(pauseButton,0,1,"pauseQuest",()=>Pause("Paused by player")),(stopButton,2,1,"stopQuest",()=>Pause("Stopped; progress saved"))}){button.Name=name;button.Dock=DockStyle.Fill;button.Font=Theme.Body(9);button.Margin=new(0,0,0,6);Wire(button,action);actions.Controls.Add(button,x,y);}Add(actions,88,6);
  Add(conversationHeading,24);Add(conversation,70);journeyControls.Height=journeyControls.PreferredSize.Height;card.Controls.Add(journeyStack);journeyDetails.Click+=(_,_)=>ToggleJourneyDetails();journeyRecentActivity.Click+=(_,_)=>ToggleJourneyActivity();Disposed+=(_,_)=>{journeyControls.Dispose();journeyActivity.Dispose();};card.Height=journeyStack.PreferredSize.Height+card.Padding.Vertical;return card;
 }
 internal void ToggleJourneyDetails(){if(journeyStack is null||journeyControls is null)return;bool expanded=journeyStack.Controls.Contains(journeyControls);if(expanded)journeyStack.Controls.Remove(journeyControls);else journeyStack.Controls.Add(journeyControls);journeyDetails.Text=expanded?"Quest controls  ›":"Hide controls  ‹";ResizeJourneyCard();}
 void ToggleJourneyActivity(){if(journeyStack is null)return;bool expanded=journeyStack.Controls.Contains(journeyActivity);if(expanded)journeyStack.Controls.Remove(journeyActivity);else{journeyStack.Controls.Add(journeyActivity);if(journeyControls is not null&&journeyStack.Controls.Contains(journeyControls))journeyStack.Controls.SetChildIndex(journeyActivity,journeyStack.Controls.IndexOf(journeyControls));}journeyRecentActivity.Text=expanded?"Recent activity  ›":"Hide activity  ‹";ResizeJourneyCard();}
 void ResizeJourneyCard(){if(journeyStack is null||journeyCard is null)return;journeyCard.Height=journeyStack.PreferredSize.Height+journeyCard.Padding.Vertical;journeyStack.PerformLayout();journeyCard.Parent?.PerformLayout();}
 void RefreshJourneyActivity(string name,QuestQueueState queue){
  int active=queue.ActiveQuest is {} id&&queue.Locked?Array.IndexOf(queue.Quests,id):-1;string? next=queue.Quests.ElementAtOrDefault(active+1);
  journeyNext.Text=queue.HomePending?"Next · Confirm arrival at home":next is not null?(active<0?"Start with · ":"Next · ")+QuestName(next):queue.ReturnHome&&queue.Home is not null&&active>=0?"Next · Return home":active>=0?"Last quest in this run":queue.Reason is "Queue completed" or "Home arrival verified"?"Run complete · Your progress is saved":"Select quests to plan your next run";
  var recent=queue.History.Concat(name.Length==0?[]:recipes.Values.Select(r=>SavedQueueOutcome(journal.Load(name,r.Id))).OfType<QueueOutcome>()).DistinctBy(h=>(h.Quest,h.At)).OrderByDescending(h=>h.At).Take(3).ToArray();
  journeyActivity.Text=recent.Length==0?"Verified activity appears here after a quest attempt.":string.Join("\n\n",recent.Select(h=>QuestName(h.Quest)+" · "+h.At.ToLocalTime().ToString("MMM d, h:mm tt")+"\n"+PlayerReason(h.Summary)));
  int height=recent.Length==0?48:recent.Length*96;if(journeyActivity.Height!=height){journeyActivity.Height=height;if(journeyStack?.Controls.Contains(journeyActivity)==true)ResizeJourneyCard();}
 }

 void SelectCharacter(string name){var row=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>string.Equals(r.Cells[0].Value?.ToString(),name,StringComparison.OrdinalIgnoreCase));if(row is null)return;grid.ClearSelection();grid.CurrentCell=row.Cells[0];row.Selected=true;UpdateWorkspace();}

 PlannerAvailability Availability(PlannerEntry entry,string name){
  if(entry.RecipeId is null)return new("Unavailable","Automatic journey unavailable. Open the guide for manual play.",false);
  var quest=recipes[entry.RecipeId];var job=journal.Load(name,quest.Id);if(runners.TryGetValue(name,out var owner)&&owner.Job.Quest==quest.Id)job=owner.Job;
  var remembered=RememberedCharacter(name);var current=sessions.GetValueOrDefault(name)?.Snapshot;
  if(remembered.CompletedLegend(quest.Id,current) is {} legend)return new("Completed","Legend already recorded: "+legend,false);
  if(QuestRunPolicy.Finished(quest,job)&&job.Pending is null)return new("Completed",job.CompletionText??"Saved goal completed",false);
  if(remembered.SelectionRestriction(quest.Id,current) is {} restriction)return new("Blocked",restriction.Reason,false);
  if(job.Pending is not null)return new(sessions.TryGetValue(name,out var pendingSession)&&pendingSession.Snapshot.Connected?"Resume ready":"Not checked","Resume checks your last action before continuing",true);
  if(QuestRunPolicy.NextAttempt(quest,job) is {} due&&due>DateTimeOffset.UtcNow)return new("Cooldown",$"Next attempt {due.ToLocalTime():MMM d, h:mm tt}",false);
  if(!sessions.TryGetValue(name,out var session)||!session.Snapshot.Connected)return new("Not checked","Connect this character to check requirements",true);
  var snapshot=session.Snapshot;
  if(quest.Id=="mundane-birth"&&job.BirthOptions is null)return new("Choices","Choose season, age, and town before starting",true);
  if(quest.Id=="emerald-dawn"&&job.EmeraldOptions is null)return new("Choices","Choose who receives the insignia before starting",true);
  Eligibility eligibility=quest.Id switch{"mundane-birth"=>MundaneBirthEvidence.Eligibility(snapshot,job.BirthOptions),"perfect-hairstyle"=>PerfectHairstyleEvidence.Eligibility(snapshot),"entered-muisir-village"=>MuisirEvidence.Eligibility(snapshot),"emerald-dawn"=>EmeraldDawnEvidence.Eligibility(snapshot),"shinewood-faerie"=>ShinewoodFaerieEvidence.Eligibility(snapshot,job),_=>StickEligibility.Check(snapshot,job.CompletedAt,DateTimeOffset.UtcNow)};
  if(quest.Id==recipe.Id&&!calibration.Ready)return new("Blocked","Review quest calibration in Developer mode",false);
  return eligibility.Kind==EligibilityKind.Eligible?new(job.Step!="preflight"?"Resume ready":"Ready",eligibility.Reason,true):new(eligibility.Kind==EligibilityKind.Unknown?"Not checked":"Blocked",PlayerReason(eligibility.Reason),eligibility.Kind==EligibilityKind.Unknown);
 }

 void RefreshPlanner(){
  if(updatingPlanner||queueCard is null)return;updatingPlanner=true;
  try{
   string name=Selected();var names=grid.Rows.Cast<DataGridViewRow>().Select(r=>r.Cells[0].Value?.ToString()??"").Where(n=>n.Length>0).ToArray();
   if(!characterSwitcher.Items.Cast<string>().SequenceEqual(names)){characterSwitcher.Items.Clear();characterSwitcher.Items.AddRange(names);}
   characterSwitcher.SelectedItem=name;characterSeal.Character=name;characterSeal.Invalidate();characterSwitcher.Enabled=names.Length>0;
   sessions.TryGetValue(name,out var session);var remembered=RememberedCharacter(name);connectionSummary.Text=session?.Snapshot.Connected==true?$"Level {session.Snapshot.Level?.ToString()??"?"} · {MapName(session.Snapshot.Map)}":remembered.Level is {} level?$"Offline · level {level} saved":"Offline · progress saved";
   if(session?.Snapshot.Connected!=true&&launchStates.TryGetValue(name,out var launchState))connectionSummary.Text=launchState;
   if(launchButton is {} launch){string? blocker=LaunchBlocker(name);launch.Enabled=blocker is null;launch.AccessibleDescription=blocker??LaunchHint(name);tip.SetToolTip(launch,blocker is null?LaunchHint(name):null);}
   var queue=GetQueue(name);bool locked=queue.Locked||queueSaving.Contains(name)||queuePreparations.ContainsKey(name);var projected=plannerEntries.Select(e=>(Entry:e,Availability:Availability(e,name))).ToArray();
   filterCounts["all"]=projected.Length;filterCounts["ready"]=projected.Count(e=>e.Availability.Ready);filterCounts["completed"]=projected.Count(e=>e.Availability.State=="Completed");filterCounts["unavailable"]=projected.Count(e=>!e.Availability.Ready&&e.Availability.State!="Completed");
   foreach(var (id,button) in plannerFilters){string label=id switch{"all"=>"All quests","ready"=>"Ready","completed"=>"Completed",_=>"Unavailable"};button.Text=label+"  "+filterCounts[id];button.Active=id==plannerFilter;}
   string query=plannerSearch.Text.Trim();var visible=projected.Where(e=>(plannerFilter=="all"||plannerFilter=="ready"&&e.Availability.Ready||plannerFilter=="completed"&&e.Availability.State=="Completed"||plannerFilter=="unavailable"&&!e.Availability.Ready&&e.Availability.State!="Completed")&&(query.Length==0||$"{e.Entry.Name} {e.Entry.Reward} {e.Entry.Requirements}".Contains(query,StringComparison.OrdinalIgnoreCase))).ToArray();
   string signature=name+"|"+SelectedQuest.Id+"|"+locked+"|"+string.Join('|',visible.Select(e=>e.Entry.Id+e.Availability.State+e.Availability.Reason+e.Availability.CanQueue+":"+Array.IndexOf(queue.Quests,e.Entry.RecipeId)));
   if(signature!=rowSignature){rowSignature=signature;questRows.SuspendLayout();foreach(var old in questRows.Controls.Cast<Control>().ToArray())old.Dispose();foreach(var (entry,availability) in visible){var row=new PlannerQuestRow(entry,availability){QueuePosition=entry.RecipeId is null?0:Array.IndexOf(queue.Quests,entry.RecipeId)+1,Locked=locked,SelectedQuest=entry.RecipeId==SelectedQuest.Id};row.Toggle+=()=>_=EditQueue(s=>s with{Quests=s.Quests.Contains(entry.RecipeId!)?s.Quests.Where(q=>q!=entry.RecipeId).ToArray():s.Quests.Append(entry.RecipeId!).ToArray()});row.Inspect+=()=>InspectPlannerQuest(entry);questRows.Controls.Add(row);}questRows.ResumeLayout(true);questRows.PerformLayout();}
   libraryCompletion.Text=$"{filterCounts["completed"]} of {recipes.Count} journeys finished";libraryCount.Text=visible.Length==0?"No matching quests. Try another name or filter.":$"Showing {visible.Length} of {projected.Length} quests · Select a name for details";
   RefreshQueueControls(name,queue,locked);RefreshJourneyActivity(name,queue);RefreshHistoryPage();if(pages.TryGetValue("login",out var accounts)&&accounts.Visible)((AccountManagerPage)accounts).RefreshStatus();
  }catch(Exception e){Log("Planner could not refresh: "+e.Message);if(IsSmoke)throw;}finally{updatingPlanner=false;}
 }

 void InspectPlannerQuest(PlannerEntry entry){
  if(entry.RecipeId is {} id){questChoice.SelectedItem=recipes[id];UpdateWorkspace();}
  using var form=new ThemedForm{Text=entry.Name,ClientSize=new(630,520),StartPosition=FormStartPosition.CenterParent,Padding=new(24)};
  var title=Theme.Label(entry.Name,22,Theme.Text,true);title.Dock=DockStyle.Top;title.Height=58;
  var content=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,Text=$"{entry.Reward}\r\n\r\nRequirements\r\nLevel: {entry.Level}\r\n{entry.Requirements}\r\n\r\n{Availability(entry,Selected()).Reason}"};
  var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};var close=Theme.Button("Close");close.DialogResult=DialogResult.OK;bottom.Controls.Add(close);if(entry.RecipeId is {} quest){var add=Theme.Button("Add to queue",true);add.Width=145;add.Enabled=!GetQueue(Selected()).Locked&&!GetQueue(Selected()).Quests.Contains(quest)&&Availability(entry,Selected()).CanQueue;add.Click+=async(_,_)=>{await EditQueue(s=>s with{Quests=s.Quests.Append(quest).ToArray()});form.Close();};bottom.Controls.Add(add);}if(Uri.TryCreate(entry.Source,UriKind.Absolute,out var url)&&url.Scheme=="https"){var guide=Theme.Button("Open guide");guide.Width=140;guide.Click+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true});bottom.Controls.Add(guide);}
  form.Controls.Add(content);form.Controls.Add(title);form.Controls.Add(bottom);form.AcceptButton=close;form.CancelButton=close;Theme.Apply(form);form.ShowDialog(this);
 }
}
