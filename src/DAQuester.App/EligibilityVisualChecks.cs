using System.Collections.Immutable;
using System.Net.Sockets;
using System.Reflection;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.App;

internal static class EligibilityVisualChecks {
 public static void Run(MainForm form){
  const string name="EligibilityFixture";
  const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var sessions=(Dictionary<string,GameSession>)typeof(MainForm).GetField("sessions",flags)!.GetValue(form)!;
  var entries=(List<PlannerEntry>)typeof(MainForm).GetField("plannerEntries",flags)!.GetValue(form)!;
  var availability=typeof(MainForm).GetMethod("Availability",flags)!;
  var failures=new List<string>();
  using var client=new TcpClient();using var remote=new TcpClient();
  var connection=new ProxyConnection(client,remote,51,_=>throw new InvalidOperationException("Synthetic checks never connect."));
  var session=new GameSession(connection,new MapRepository(AppSettings.DataPath,Path.Combine(AppSettings.Content,"routes.json")));
  sessions.Add(name,session);
  var journal=new FileQuestJournal(Path.Combine(AppSettings.DataPath,"journal"));
  var memory=(FileCharacterMemory)typeof(MainForm).GetField("characterMemory",flags)!.GetValue(form)!;
  void Wait(Task task){var deadline=DateTime.UtcNow.AddSeconds(5);while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Character memory check timed out.");Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();Application.DoEvents();}
  void Set(CharacterSnapshot snapshot)=>typeof(DarkAges.Protocol.StateTracker).GetProperty("Snapshot")!.SetValue(connection.Tracker,snapshot);
  PlannerAvailability State(string quest)=>(PlannerAvailability)availability.Invoke(form,[entries.Single(e=>e.RecipeId==quest),name])!;
  void Check(bool valid,string message){if(!valid)failures.Add(message);}
  try{
   Set(new(){Name=name,Id=42,Connected=false,Level=99,Generation=51,Revision=1});
   Check(!State("spare-a-stick").CanQueue,"Known offline level 99 must block Spare a Stick selection.");
   Set(session.Snapshot with{Connected=true});
   Check(!State("spare-a-stick").CanQueue,"Level 99 must block selection while other server stats are still unknown.");
   journal.SaveAsync(new(){Character=name,Quest="spare-a-stick",Step="confirm",Pending=new("turn-in",51,1,0,0,0,DateTimeOffset.UtcNow)}).GetAwaiter().GetResult();
   Check(!State("spare-a-stick").CanQueue,"Pending confirmation must not make level 99 selectable for a new queue.");
   Set(session.Snapshot with{Hp=100,LegendsKnown=true,LegendsRevision=2,Revision=2,Legends=ImmutableArray.Create("Entered Muisir Village - Deoch 223, Spring","Undine mundane birth - Winter, Deoch 25","Aided the Emerald Knights - Deoch 223, Spring")});
   var muisir=State("entered-muisir-village");
   Check(!muisir.CanQueue&&muisir.State=="Completed","Existing own Muisir legend must show Completed without a local completion journal.");
   foreach(string quest in new[]{"mundane-birth","emerald-dawn"})Check(State(quest).State=="Completed"&&!State(quest).CanQueue,"Known non-repeatable final marks must show Completed before offering choices.");
   var save=(Task)typeof(MainForm).GetMethod("SaveCharacterMemoryAsync",flags)!.Invoke(form,[name,session,session.Snapshot])!;Wait(save);
   Check(memory.Load(name).Level==99&&memory.Load(name).Legends.Contains("Entered Muisir Village - Deoch 223, Spring"),"The app must persist observed own level and marks.");
   sessions.Remove(name);
   Check(!State("spare-a-stick").CanQueue,"Saved level 99 must still block selection without a live session.");
   Check(!State("entered-muisir-village").CanQueue&&State("entered-muisir-village").State=="Completed","Saved Muisir mark must remain Completed offline.");
   var roster=(DataGridView)form.Controls.Find("characterRoster",true).Single();
   int rowIndex=roster.Rows.Add(name,"Disconnected","—","—","—","—","Idle","preflight","Ready","","Spare a Stick");
   try{
    roster.CurrentCell=roster.Rows[rowIndex].Cells[0];Application.DoEvents();
    form.ToggleJourneyDetails();Application.DoEvents();
    try{
     var selector=(ComboBox)form.Controls.Find("questSelector",true).Single();var originalQuest=selector.SelectedItem;
     try{
      selector.SelectedItem=selector.Items.Cast<QuestDefinition>().Single(q=>q.Id=="entered-muisir-village");Application.DoEvents();
      Check(form.Controls.Find("questProgressState",true).Single().Text.Contains("Completed")&&!((Button)form.Controls.Find("startQuest",true).Single()).Enabled,"Remembered goal completion must also appear in the journey and disable Start.");
     }finally{selector.SelectedItem=originalQuest;Application.DoEvents();}
    }finally{form.ToggleJourneyDetails();Application.DoEvents();}
    var list=form.Controls.Find("plannerQuestList",true).Single();
    foreach(string quest in new[]{"spare-a-stick","entered-muisir-village"}){
     var row=list.Controls.OfType<PlannerQuestRow>().Single(r=>r.Entry.RecipeId==quest);
     int before=new FileQuestQueueStore(Path.Combine(AppSettings.DataPath,"queues")).Load(name).Quests.Length;
     row.ToggleSelection();
     typeof(Control).GetMethod("OnKeyDown",flags)!.Invoke(row,[new KeyEventArgs(Keys.Space)]);Application.DoEvents();
     Check(!row.Availability.CanQueue&&new FileQuestQueueStore(Path.Combine(AppSettings.DataPath,"queues")).Load(name).Quests.Length==before,"Blocked rows must reject checkbox and keyboard queue selection.");
    }
    ((Button)form.Controls.Find("plannerFiltercompleted",true).Single()).PerformClick();Application.DoEvents();
    Check(list.Controls.OfType<PlannerQuestRow>().Any(r=>r.Entry.RecipeId=="entered-muisir-village"),"Saved Muisir completion belongs in Completed filter.");
    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save("artifacts/planner-saved-restrictions.png");}
    ((Button)form.Controls.Find("plannerFilterall",true).Single()).PerformClick();
   }finally{roster.Rows.RemoveAt(rowIndex);roster.CurrentCell=roster.Rows[0].Cells[0];Application.DoEvents();}
   sessions.Add(name,session);
   var queues=(Dictionary<string,QuestQueueState>)typeof(MainForm).GetField("queueCache",flags)!.GetValue(form)!;
   queues[name]=new(){Character=name,State="Paused",ActiveQuest="spare-a-stick",ActiveResume=true,Quests=["spare-a-stick"]};
   Check((string)typeof(MainForm).GetMethod("QueueStartReason",flags)!.Invoke(form,[name,queues[name]])! == "","Existing pending queue must still allow fresh reconciliation at level 99.");
   queues.Remove(name);
   Set(session.Snapshot with{Level=11,Gold=999,Experience=10,Weight=1,MaxWeight=100,InventoryKnown=true,Legends=[],LegendsRevision=3,Revision=3});
   int readyIndex=roster.Rows.Add(name,"Online","11","999","—","—","Idle","preflight","Ready","","Spare a Stick");
   try{
    roster.CurrentCell=roster.Rows[readyIndex].Cells[0];Application.DoEvents();
    var list=form.Controls.Find("plannerQuestList",true).Single();
    foreach(string quest in new[]{"mundane-birth","emerald-dawn","perfect-hairstyle","entered-muisir-village"})Check(list.Controls.OfType<PlannerQuestRow>().Single(r=>r.Entry.RecipeId==quest).Availability.Ready,"Fresh own requirements must restore ready/choice rows.");
    using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save("artifacts/planner-ready-states.png");
   }finally{roster.Rows.RemoveAt(readyIndex);roster.CurrentCell=roster.Rows[0].Cells[0];Application.DoEvents();}
   Check(memory.Load("Nuclear").Level is null&&State("shinewood-faerie").CanQueue,"Remembered restrictions must not leak to other characters or quests.");
  }finally{sessions.Remove(name);connection.DisposeAsync().AsTask().GetAwaiter().GetResult();}
  if(failures.Count>0)throw new InvalidOperationException(string.Join("\n",failures));
  File.WriteAllText("artifacts/character-memory-checks.txt","Known and saved level 99 gating, pending selection gating, own and saved Muisir completion, per-character memory persistence, offline checkbox/keyboard rejection, Completed filtering, and pending queue reconciliation passed. No game commands or connections executed.");
 }
}
