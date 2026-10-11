using System.Collections.Immutable;
using System.Net.Sockets;
using System.Reflection;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.App;

// Documentation fixtures only. No network, game commands, credentials, or user Data.
internal static class ScreenshotCapture {
 public static void Run(MainForm form){
  const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var sessions=(Dictionary<string,GameSession>)typeof(MainForm).GetField("sessions",flags)!.GetValue(form)!;
  var memory=(FileCharacterMemory)typeof(MainForm).GetField("characterMemory",flags)!.GetValue(form)!;
  var queues=(Dictionary<string,QuestQueueState>)typeof(MainForm).GetField("queueCache",flags)!.GetValue(form)!;
  using var client=new TcpClient();using var remote=new TcpClient();
  var connection=new ProxyConnection(client,remote,91,_=>throw new InvalidOperationException("Screenshot fixtures cannot connect."));
  var snapshot=new CharacterSnapshot{Name="Agent",Id=42,Connected=true,Generation=91,Revision=12,Level=99,Hp=400,Mana=200,Gold=999,Experience=250000,
   Class="Wizard",IsMaster=true,IsPure=true,Weight=7,MaxWeight=100,InventoryKnown=true,SpellsKnown=true,LegendsKnown=true,LegendsRevision=12,
   Map=3082,MapName="Rucesion Threshold",Position=new(12,18),Inventory=ImmutableDictionary<byte,Item>.Empty.Add(1,new(1,"Rucesion Song",3)),
   Legends=ImmutableArray.Create("Entered Muisir Village - Deoch 223, Spring","Undine mundane birth - Winter, Deoch 25")};
  typeof(DarkAges.Protocol.StateTracker).GetProperty("Snapshot")!.SetValue(connection.Tracker,snapshot);
  var session=new GameSession(connection,new MapRepository(AppSettings.DataPath,Path.Combine(AppSettings.Content,"routes.json")));
  sessions.Add("Agent",session);
  void Wait(Task task){var limit=DateTime.UtcNow.AddSeconds(8);while(!task.IsCompleted){if(DateTime.UtcNow>limit)throw new InvalidOperationException("Screenshot fixture save timed out.");Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
  void Capture(string name){Application.DoEvents();form.Refresh();Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save("artifacts/screenshots/"+name+".png");}
  void Page(string id){((Button)form.Controls.Find("navigate"+id,true).Single()).PerformClick();Application.DoEvents();}
  try{
   Wait(memory.ObserveAsync("Agent",snapshot));
   Wait(memory.ObserveAsync("Elara",snapshot with{Name="Elara",Id=43,Level=11,Legends=[]}));
   Wait(memory.ObserveAsync("Wren",snapshot with{Name="Wren",Id=44,Level=35,Legends=[]}));
   var now=new DateTimeOffset(2026,10,11,14,30,0,TimeSpan.Zero);
   var queue=new QuestQueueState{Character="Agent",Quests=["shinewood-faerie","perfect-hairstyle","emerald-dawn"],Home=new(3082,new(12,18),"Rucesion Threshold"),ReturnHome=true,
    History=[new("entered-muisir-village","Completed",now,"Entered Muisir Village recorded. Five mushrooms consumed; four Shrinking Potions received."),
     new("mundane-birth","Completed",now.AddMinutes(-24),"Mundane birth recorded: Winter, age 25, Undine.")]};
   Wait(new FileQuestQueueStore(Path.Combine(AppSettings.DataPath,"queues")).SaveAsync(queue));queues["Agent"]=queue;
   var selector=(ComboBox)typeof(MainForm).GetField("questChoice",flags)!.GetValue(form)!;selector.SelectedItem=selector.Items.Cast<QuestDefinition>().Single(q=>q.Id=="shinewood-faerie");
   typeof(MainForm).GetMethod("RefreshRow",flags)!.Invoke(form,["Agent"]);
   Directory.CreateDirectory("artifacts/screenshots");form.Size=new(1450,930);Application.DoEvents();
   var rows=form.Controls.Find("plannerQuestList",true).Single().Controls.OfType<PlannerQuestRow>().ToArray();
   if(rows.Single(r=>r.Entry.RecipeId=="spare-a-stick").Availability.CanQueue||rows.Single(r=>r.Entry.RecipeId=="entered-muisir-village").Availability.State!="Completed")throw new InvalidOperationException("Screenshot must reflect actual level and own legend restrictions.");
   Capture("planner");Page("login");Capture("characters");Page("history");Capture("history");Page("settings");Capture("settings");
   File.WriteAllText("artifacts/screenshots.txt","Four native 1450 × 930 renders captured with isolated sample characters, actual quest availability, persisted queue/history, and no game connections or commands.");
  }finally{sessions.Remove("Agent");connection.DisposeAsync().AsTask().GetAwaiter().GetResult();}
 }
}
