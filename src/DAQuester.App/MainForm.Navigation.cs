using DAQuester.Core;

namespace DAQuester.App;

public sealed partial class MainForm {
 readonly Panel pageHost=new(){Name="workspacePages",Dock=DockStyle.Fill,BackColor=Theme.Ink};
 readonly Dictionary<string,Control> pages=[];
 readonly Dictionary<string,JournalButton> navigationButtons=[];
 readonly Dictionary<(string Character,string Quest),bool> repeatChoices=[];

 JournalButton PageNavigation(string id,string label){
  var button=Navigation(label,()=>ShowPage(id));button.Name="navigate"+id;button.Active=id=="journal";navigationButtons.Add(id,button);return button;
 }
 Task ShowPage(string id){
  if(!pages.TryGetValue(id,out var page)){
   page=id switch{
    "settings"=>CreateSettingsPage(),
    "login"=>CreateAccountManagerPage(),
    "history"=>CreateHistoryPage(),
    _=>throw new InvalidOperationException("Unknown workspace page: "+id)
   };
   page.Visible=false;pages.Add(id,page);pageHost.Controls.Add(page);
  }
  pageHost.SuspendLayout();
  foreach(var pair in pages)pair.Value.Visible=pair.Key==id;
  page.BringToFront();pageHost.ResumeLayout(true);
  foreach(var pair in navigationButtons)pair.Value.Active=pair.Key==id;
  Text="DAQuester · "+navigationButtons[id].Text;
  if(id=="journal")UpdateWorkspace();
  if(id=="history")RefreshHistoryPage();
  return Task.CompletedTask;
 }
 PlayerSettingsPage CreateSettingsPage(){
  var page=new PlayerSettingsPage(settings,updates,InstallUpdate);
  page.Saved+=value=>{updates.AutomaticChecks=value.AutomaticUpdateChecks;Log("Settings saved.");};
  page.Cancelled+=()=>_=ShowPage("journal");return page;
 }
 AccountManagerPage CreateAccountManagerPage(){
  var page=new AccountManagerPage(settings,settings.Characters.Concat(sessions.Where(p=>p.Value.Snapshot.Connected).Select(p=>p.Key)),IsSmoke,name=>!QueueReserves(name)&&!pendingLaunches.ContainsKey(name)&&!controlActions.ContainsKey(name)&&!(runners.TryGetValue(name,out var runner)&&runner.IsRunning),name=>{string state=sessions.TryGetValue(name,out var session)&&session.Snapshot.Connected?$"Online · level {session.Snapshot.Level?.ToString()??"?"}":launchStates.GetValueOrDefault(name,"Offline");int count=GetQueue(name).Quests.Length;return state+(count>0?$" · {count} queued":"");},IsSmoke?null:LaunchBlocker,LaunchHint);
  async void Action(string name,bool home){try{if(IsSmoke)throw new InvalidOperationException("Preview does not launch game clients or save observed home locations.");if(home)await SetHome(name);else await LaunchCharacterAsync(name);}catch(Exception e){Theme.Error(this,PlayerReason(e.Message),home?"Set home":"Launch character");}}
  page.LaunchRequested+=name=>Action(name,false);page.HomeRequested+=name=>Action(name,true);
  page.Saved+=()=>{
   foreach(var row in grid.Rows.Cast<DataGridViewRow>().ToArray()){
    string name=row.Cells[0].Value?.ToString()??"";
    if(!settings.Characters.Contains(name,StringComparer.OrdinalIgnoreCase)&&!(sessions.TryGetValue(name,out var session)&&session.Snapshot.Connected)&&!(runners.TryGetValue(name,out var runner)&&runner.IsRunning))grid.Rows.Remove(row);
   }
   foreach(string name in settings.Characters)AddCharacter(name);
   UpdateWorkspace();Log("Accounts saved; character list updated.");
  };
  return page;
 }
 void SaveRepeatChoice(){if(updatingWorkspace)return;repeatChoices[(Selected().ToUpperInvariant(),SelectedQuest.Id)]=repeats.Checked;UpdateWorkspace();}
}
