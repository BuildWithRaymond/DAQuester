using System.Diagnostics;
using DAQuester.Core;
namespace DAQuester.App;
public sealed partial class MainForm {
 readonly List<Process> launchedClients=[];
 UpdateCoordinator updates=null!;
 bool installExitRequested;
 bool QuestActionsRunning=>runners.Values.Any(r=>r.IsRunning)||controlActions.Count>0||calibrationTasks.Values.Any(t=>!t.IsCompleted)||permanentTasks.Values.Any(t=>!t.IsCompleted);
 bool ConnectedGame=>sessions.Values.Any(s=>s.Snapshot.Connected);
 bool LaunchedClientRunning=>launchedClients.Any(p=>{try{return !p.HasExited;}catch(InvalidOperationException){return false;}});
 void InitializeUpdates(){
  updates=new(new VelopackUpdates(),IsSmoke){AutomaticChecks=settings.AutomaticUpdateChecks};
  Shown+=async(_,_)=>{if(!IsSmoke)await updates.StartAsync();};
  FormClosed+=(_,_)=>{foreach(var process in launchedClients)process.Dispose();};
 }
 void InstallUpdate(){updates.RequestInstall(QuestActionsRunning,ConnectedGame,LaunchedClientRunning);installExitRequested=true;Close();}
}
