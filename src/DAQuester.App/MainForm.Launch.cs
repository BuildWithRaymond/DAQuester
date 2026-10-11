using System.Diagnostics;
using System.Net;
using DAQuester.Core;
using DAQuester.DarkAges.Launcher;

namespace DAQuester.App;

public sealed partial class MainForm {
 readonly Dictionary<string,Process> ownedClients=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,CancellationTokenSource> pendingLaunches=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,string> launchStates=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Task<int>> launchTasks=new(StringComparer.OrdinalIgnoreCase);
 (string Path,DateTime Written,string? Error) clientCheck;
 // Hashing the 3 MB client on every refresh is too slow; cache per path and write time.
 string? ClientBlocker(){
  string path=settings.ClientPath;
  if(path.Length==0||!File.Exists(path))return "Dark Ages application not found. Set its path in Settings.";
  var written=File.GetLastWriteTimeUtc(path);
  if(clientCheck.Path!=path||clientCheck.Written!=written){
   string? error=null;
   try{ClientLauncher.Validate(path);}
   catch(InvalidOperationException){error="This Dark Ages application isn't a supported 7.41 client. Choose it in Settings.";}
   catch(Exception e) when(e is IOException or UnauthorizedAccessException){error="The Dark Ages application couldn't be read. Check its path in Settings.";}
   clientCheck=(path,written,error);
  }
  return clientCheck.Error;
 }
 bool OwnsLiveClient(string name)=>ownedClients.TryGetValue(name,out var client)&&!client.HasExited;
 // Why Launch is blocked for this character, in the order the launch itself checks; null means it can run.
 internal string? LaunchBlocker(string name){
  if(IsSmoke)return "Preview can't launch game clients.";
  if(name.Length==0)return "Add a character on the Characters page, then select it here.";
  if(!ready)return "The game connection isn't ready. Check the connection profile in Developer mode.";
  if(pendingLaunches.ContainsKey(name))return launchStates.GetValueOrDefault(name,"Launching Dark Ages…");
  if(OwnsLiveClient(name))return null;
  if(sessions.TryGetValue(name,out var session)&&session.Snapshot.Connected)return name+" is already connected. Use its game window.";
  return ClientBlocker();
 }
 internal string? LaunchHint(string name)=>OwnsLiveClient(name)?name+" is connected. Launch focuses its game window.":null;
 Task<int> LaunchCharacterAsync(string name,bool manualOnly=false){if(launchTasks.TryGetValue(name,out var active)&&!active.IsCompleted)return active;var task=LaunchOwnedCharacterAsync(name,manualOnly);launchTasks[name]=task;return task;}
 async Task<int> LaunchOwnedCharacterAsync(string name,bool manualOnly){
  if(IsSmoke)throw new InvalidOperationException("Preview cannot launch game clients.");
  if(name.Length>0&&!name.All(char.IsAsciiLetter))throw new InvalidOperationException("Enter a character name using letters only.");
  if(!ready)throw new InvalidOperationException("The game connection isn't ready. Check the connection profile in Developer mode.");
  string key=name.Length>0?name:"(manual)";
  if(pendingLaunches.ContainsKey(key))throw new InvalidOperationException("This character's client is already launching.");
  if(ownedClients.TryGetValue(key,out var existing)&&!existing.HasExited){LoginWindowInput.Focus(existing.Id);return existing.Id;}
  if(name.Length>0&&sessions.TryGetValue(name,out var current)&&current.Snapshot.Connected)throw new InvalidOperationException("This character is already connected. Use its existing game window.");
  using var cancellation=new CancellationTokenSource();pendingLaunches[key]=cancellation;
  SavedLoginTicket? ticket=null;
  CancellationTokenRegistration ticketCancellation=default;
  void Progress(string text){launchStates[name]=text;Log(name+": "+text);UpdateWorkspace();}
  var authenticated=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
  try{
   bool saved=!manualOnly&&name.Length>0&&CredentialVault.Exists(name);
   if(saved){ticket=new(name,()=>CredentialVault.Load(name));ticketCancellation=cancellation.Token.Register(ticket.Cancel);}
   int port=await host.StartOwnedAsync(new(IPAddress.Parse(settings.RemoteAddress),settings.RemotePorts[0]),ticket,c=>c.Authenticated+=connection=>authenticated.TrySetResult(connection.Tracker.Snapshot.Name??""),cancellation.Token);
   Progress("Launching Dark Ages…");
   int pid=await Task.Run(()=>{cancellation.Token.ThrowIfCancellationRequested();return ClientLauncher.Launch(settings.ClientPath,port);},cancellation.Token);
   var process=Process.GetProcessById(pid);launchedClients.Add(process);ownedClients[key]=process;
   if(!saved){Progress("Client open · sign in manually");return pid;}
   Progress("Waiting for login screen…");
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);timeout.CancelAfter(TimeSpan.FromSeconds(90));
   while(true){timeout.Token.ThrowIfCancellationRequested();process.Refresh();if(process.HasExited)throw new InvalidOperationException("Game client closed before login.");if(process.MainWindowHandle!=IntPtr.Zero)break;await Task.Delay(50,timeout.Token);}
   using var form=new NativeLoginForm(pid);await LoginFormAutomation.Run(form,name,ticket!.Ticket,Progress,timeout.Token);
   Progress("Login submitted · waiting for character…");
   string actual=await authenticated.Task.WaitAsync(TimeSpan.FromSeconds(30),cancellation.Token);
   if(!actual.Equals(name,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The client connected as another character. Check its login before running quests.");
   Progress("Connected · ready for quests");return pid;
  }catch(OperationCanceledException){Progress(cancellation.IsCancellationRequested?"Launch cancelled · sign in manually":"Login timed out · sign in manually");throw new InvalidOperationException(launchStates[name]);}
   catch(TimeoutException){Progress("Login not confirmed · check the game window");throw new InvalidOperationException(launchStates[name]);}
   catch{Progress("Launch unavailable · check the game window");throw;}
  finally{ticket?.Cancel();ticketCancellation.Dispose();pendingLaunches.Remove(key);UpdateWorkspace();}
 }
 void CancelLaunches(string? name=null){foreach(var pending in pendingLaunches.Where(p=>name is null||p.Key.Equals(name,StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value).ToArray())pending.Cancel();}
 async Task AwaitLaunches(string? name=null){try{await Task.WhenAll(launchTasks.Where(p=>name is null||p.Key.Equals(name,StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value).Where(t=>!t.IsCompleted).ToArray());}catch(Exception){Log("Pending character launch stopped.");}}
}
