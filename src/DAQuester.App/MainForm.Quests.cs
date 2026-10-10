using System.Text.Json;
using DAQuester.Core;
namespace DAQuester.App;
public sealed partial class MainForm {
 readonly Dictionary<string,QuestDefinition> recipes=Directory.GetFiles(Path.Combine(AppSettings.Content,"recipes"),"*.json").Select(p=>JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(p),AppSettings.Json)!).ToDictionary(q=>q.Id,StringComparer.OrdinalIgnoreCase);
 readonly ComboBox questChoice=new JournalComboBox(){Name="questSelector",DisplayMember="Name",Width=180,Margin=new(3,7,8,3)};
 QuestDefinition SelectedQuest=>questChoice.SelectedItem as QuestDefinition??recipe;
 string QuestName(string id)=>recipes.TryGetValue(id,out var q)?q.Name:id;
 async Task<QuestRunner> PrepareRunner(string name,QuestDefinition selected,MundaneBirthOptions? options=null) {
  if(controlActions.ContainsKey(name))throw new QuestBlockedException("A local action owns this character. Stop it first.");
  if(!sessions.TryGetValue(name,out var session)||!session.Snapshot.Connected)throw new QuestBlockedException("Launch DATester and log in as "+name+" first.");
  runners.TryGetValue(name,out var old);
  if(old?.Job.Quest==selected.Id&&old.IsRunning)throw new QuestBlockedException("This quest already runs for "+name+". Pause before changing choices.");
  var saved=old?.Job.Quest==selected.Id?old.Job:journal.Load(name,selected.Id);
  if(selected.Id=="mundane-birth") {
   if(options is not null) {
    options.Validate();
    if((saved.Pending is not null||saved.CompletedAt is not null)&&saved.BirthOptions!=options)throw new QuestBlockedException("Saved birth submission cannot change choices. Inspect the legend before reconciliation.");
   }
   options??=saved.BirthOptions;
   if(options is null)throw new QuestBlockedException("Birth choices required: season, age 12-40, and town.");
   options.Validate();
  }
  if(old is not null&&(old.Job.Quest!=selected.Id||options is not null&&old.Job.BirthOptions!=options)) {
   await old.PauseAsync("Another quest or birth choices selected; progress saved");runners.Remove(name);old=null;
  }
  calibrationRuns.Remove(name);
  if(old is not null)return old;
  if(selected.Id=="mundane-birth"&&options is not null)await journal.SaveAsync(saved with{BirthOptions=options,Repeat=false,NextRun=null});
  var runner=new QuestRunner(session,journal,selected,calibration);runners[name]=runner;
  runner.Changed+=job=>Ui(()=>{RefreshRow(name);Log(name+": "+QuestName(job.Quest)+" · "+job.Step+" · "+job.Status+" "+job.Reason);});
  RefreshRow(name);return runner;
 }
}
