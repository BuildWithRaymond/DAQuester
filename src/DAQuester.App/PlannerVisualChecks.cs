namespace DAQuester.App;

internal static class PlannerVisualChecks {
 public static void Run(MainForm form){
  Control Require(string name)=>form.Controls.Find(name,true).SingleOrDefault()??throw new InvalidOperationException("Planner interaction missing: "+name);
  var search=Require("plannerSearch") as TextBox??throw new InvalidOperationException("Planner search must accept text.");
  var list=Require("plannerQuestList");
  void Capture(string name){using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save("artifacts/"+name+".png");}
  Capture("planner-initial");
  search.Text="Shinewood Faerie";Application.DoEvents();
  if(list.Controls.Count!=1||!list.Controls[0].AccessibleName!.Contains("Shinewood Faerie"))throw new InvalidOperationException("Planner search must filter real quest rows. Found "+list.Controls.Count+": "+string.Join(",",list.Controls.Cast<Control>().Select(c=>c.AccessibleName)));
  search.Text="";Application.DoEvents();
  if(list.Controls.Count<70)throw new InvalidOperationException("Planner must show the real catalog.");
  var queue=Require("plannerQueue");var switcher=(ComboBox)Require("characterSwitcher");
  void Wait(Func<bool> done){var deadline=DateTime.UtcNow.AddSeconds(4);while(!done()){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Planner edit did not settle.");Application.DoEvents();Thread.Sleep(10);}Application.DoEvents();}
  var faerie=list.Controls.OfType<PlannerQuestRow>().Single(r=>r.Entry.RecipeId=="shinewood-faerie");faerie.ToggleSelection();Wait(()=>queue.Controls.OfType<PlannerQueueRow>().Count()==1);
  list.Controls.OfType<PlannerQuestRow>().Single(r=>r.Entry.RecipeId=="mundane-birth").ToggleSelection();Wait(()=>queue.Controls.OfType<PlannerQueueRow>().Count()==2);
  queue.Controls.OfType<PlannerQueueRow>().Last().Controls.OfType<Button>().Single(b=>b.Name=="Move up").PerformClick();Wait(()=>queue.Controls.OfType<PlannerQueueRow>().First().Quest=="mundane-birth");
  var store=new Core.FileQuestQueueStore(Path.Combine(AppSettings.DataPath,"queues"));if(!store.Load("NUCLEAR").Quests.SequenceEqual(new[]{"mundane-birth","shinewood-faerie"}))throw new InvalidOperationException("Quest ordering must persist for the correct character.");
  void KeyMove(PlannerQueueRow row,Keys key){row.Focus();typeof(Control).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(row,new object[]{new KeyEventArgs(Keys.Control|key)});}
  KeyMove(queue.Controls.OfType<PlannerQueueRow>().First(),Keys.Down);Wait(()=>queue.Controls.OfType<PlannerQueueRow>().Last().Quest=="mundane-birth");
  if(!queue.Controls.OfType<PlannerQueueRow>().Last().Focused)throw new InvalidOperationException("Keyboard queue moves must retain focus on the moved quest.");
  KeyMove(queue.Controls.OfType<PlannerQueueRow>().Last(),Keys.Up);Wait(()=>queue.Controls.OfType<PlannerQueueRow>().First().Quest=="mundane-birth");
  void Drag(PlannerQueueRow target,string character,string quest){var data=new DataObject("DAQuester.Queue",character+"\n"+quest);var point=target.PointToScreen(new Point(20,10));var drag=new DragEventArgs(data,1,point.X,point.Y,DragDropEffects.Move,DragDropEffects.Move);foreach(string method in new[]{"OnDragOver","OnDragDrop"})typeof(Control).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(target,new object[]{drag});}
  Drag(queue.Controls.OfType<PlannerQueueRow>().First(),"Another","shinewood-faerie");Application.DoEvents();if(queue.Controls.OfType<PlannerQueueRow>().First().Quest!="mundane-birth")throw new InvalidOperationException("Cross-character drag must be rejected.");
  Drag(queue.Controls.OfType<PlannerQueueRow>().First(),"Nuclear","shinewood-faerie");Wait(()=>queue.Controls.OfType<PlannerQueueRow>().First().Quest=="shinewood-faerie");if(!store.Load("NUCLEAR").Quests.SequenceEqual(new[]{"shinewood-faerie","mundane-birth"}))throw new InvalidOperationException("Drag ordering must persist.");
  if(!Require("journeyNext").Text.Contains("Shinewood Faerie"))throw new InvalidOperationException("Current journey must show which queued quest starts next.");
  Capture("planner-queued");
  var standardSize=form.Size;var minimum=form.MinimumSize;form.MinimumSize=new(1024,768);form.Size=form.MinimumSize;Application.DoEvents();Capture("planner-queued-small-screen");form.MinimumSize=minimum;form.Size=standardSize;Application.DoEvents();
  var roster=(DataGridView)Require("characterRoster");roster.Rows.Add("PlannerOther","Disconnected","—","—","—","—","Idle","preflight","Ready","","Spare a Stick");roster.CurrentCell=roster.Rows[1].Cells[0];Application.DoEvents();
  if(queue.Controls.OfType<PlannerQueueRow>().Any()||switcher.SelectedItem?.ToString()!="PlannerOther")throw new InvalidOperationException("Character switching must isolate queue state.");
  switcher.SelectedItem="Nuclear";Application.DoEvents();if(queue.Controls.OfType<PlannerQueueRow>().Count()!=2)throw new InvalidOperationException("Switching back must restore saved order.");
  roster.Rows.RemoveAt(1);((Button)Require("clearQueue")).PerformClick();Wait(()=>!queue.Controls.OfType<PlannerQueueRow>().Any());
  if(((Button)Require("startQueue")).Enabled)throw new InvalidOperationException("Offline preview must not start gameplay.");
  var launch=(Button)Require("launchSelectedCharacter");if(launch.Enabled||string.IsNullOrEmpty(launch.AccessibleDescription))throw new InvalidOperationException("A blocked Launch button must say why.");
  var receipt=new Core.QueueOutcome("mundane-birth","Completed",DateTimeOffset.UtcNow,"Synthetic smoke fixture: verified birth receipt saved.");var saveHistory=store.SaveAsync(store.Load("Nuclear") with{History=[receipt]});Wait(()=>saveHistory.IsCompleted);saveHistory.GetAwaiter().GetResult();
  ((Dictionary<string,Core.QuestQueueState>)typeof(MainForm).GetField("queueCache",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!).Remove("Nuclear");
  ((Button)Require("navigatehistory")).PerformClick();Application.DoEvents();
  if(!Require("historyPage").Visible)throw new InvalidOperationException("Run history must open inside workspace.");
  var history=(DataGridView)Require("runHistory");if(!history.Rows.Cast<DataGridViewRow>().Any(r=>r.Cells[0].Value?.ToString()=="Nuclear"&&r.Cells[1].Value?.ToString()=="Mundane Birth"&&r.Cells[2].Value?.ToString()=="Journey completed"))throw new InvalidOperationException("History must display durable per-character queue receipts.");Capture("planner-history");
  form.MinimumSize=new(1024,768);form.Size=form.MinimumSize;Application.DoEvents();if(history.Rows[0].Cells[4].InheritedStyle.WrapMode!=DataGridViewTriState.True||history.Rows[0].Height<history.Rows[0].GetPreferredHeight(0,DataGridViewAutoSizeRowMode.AllCells,true))throw new InvalidOperationException("Long history receipts must wrap into readable rows at minimum width.");Capture("planner-history-small-screen");form.MinimumSize=minimum;form.Size=standardSize;Application.DoEvents();
  ((Button)Require("navigatelogin")).PerformClick();Application.DoEvents();var accounts=(DataGridView)Require("accountLoginsTable");if(!accounts.Columns.Contains("Launch")||!accounts.Columns.Contains("Home"))throw new InvalidOperationException("Every saved character must have launch and home actions.");
  ((Button)Require("navigatejournal")).PerformClick();Application.DoEvents();
  ((Button)Require("journeyRecentActivity")).PerformClick();Application.DoEvents();if(!Require("journeyActivity").Visible||!Require("journeyActivity").Text.Contains(receipt.Summary))throw new InvalidOperationException("Recent activity must show saved receipts for the selected character.");var right=Require("journeyNext").Parent!.Parent!.Parent!;if(right.Height<right.PreferredSize.Height)throw new InvalidOperationException("Expanding journey activity must resize scroll content so it remains reachable.");Capture("planner-activity");((Button)Require("journeyRecentActivity")).PerformClick();Application.DoEvents();
  File.WriteAllText("artifacts/planner-checks.txt","Real catalog search, membership, persisted button/keyboard/drag ordering, keyboard focus, cross-character drag rejection, character queue isolation, offline run gate, durable history, next entry, recent activity and per-character Launch/Home actions passed. Standard and 1024px renders saved.");
 }
}
