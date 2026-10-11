using System.Diagnostics;
using System.Net;
using DAQuester.Core;
using DAQuester.DarkAges.Launcher;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
 readonly List<Process> launchedClients = [];
 public string LaunchState(string name) => launchStates.GetValueOrDefault(name, Snapshot(name).Connected ? "Connected" : "Offline");
 public bool LaunchPending(string name) => pendingLaunches.ContainsKey(name);
 readonly Dictionary<string, Process> ownedClients = new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string, CancellationTokenSource> pendingLaunches = new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string, string> launchStates = new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string, Task<int>> launchTasks = new(StringComparer.OrdinalIgnoreCase);
 public Task<int> LaunchAsync(string name, bool manualOnly = false) { if (launchTasks.TryGetValue(name, out var active) && !active.IsCompleted) return active; var task = LaunchOwnedCharacterAsync(name, manualOnly); launchTasks[name] = task; return task; }
 async Task<int> LaunchOwnedCharacterAsync(string name, bool manualOnly)
 {
  RequireOpen();
  if (Preview) throw new InvalidOperationException("Preview cannot launch game clients.");
  if (name.Length > 0 && !name.All(char.IsAsciiLetter)) throw new InvalidOperationException("Enter a character name using letters only.");
  if (!Ready) throw new InvalidOperationException("The game connection isn't ready. Check the connection profile in Developer mode.");
  string key = name.Length > 0 ? name : "(manual)";
  if (pendingLaunches.ContainsKey(key)) throw new InvalidOperationException("This character's client is already launching.");
  if (ownedClients.TryGetValue(key, out var existing) && !existing.HasExited) { LoginWindowInput.Focus(existing.Id); return existing.Id; }
  if (name.Length > 0 && sessions.TryGetValue(name, out var current) && current.Snapshot.Connected) throw new InvalidOperationException("This character is already connected. Use its existing game window.");
  using var cancellation = new CancellationTokenSource(); pendingLaunches[key] = cancellation;
  SavedLoginTicket? ticket = null;
  CancellationTokenRegistration ticketCancellation = default;
  void Progress(string text) { launchStates[name] = text; Log(name + ": " + text); Notify(); }
  var authenticated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
  try
  {
   bool saved = !manualOnly && name.Length > 0 && CredentialVault.Exists(name);
   if (saved) { ticket = new(name, () => CredentialVault.Load(name)); ticketCancellation = cancellation.Token.Register(ticket.Cancel); }
   int port = await host.StartOwnedAsync(new(IPAddress.Parse(Settings.RemoteAddress), Settings.RemotePorts[0]), ticket, c => c.Authenticated += connection => authenticated.TrySetResult(connection.Tracker.Snapshot.Name ?? ""), cancellation.Token);
   Progress("Launching Dark Ages…");
   int pid = await Task.Run(() => { cancellation.Token.ThrowIfCancellationRequested(); return ClientLauncher.Launch(Settings.ClientPath, port); }, cancellation.Token);
   var process = Process.GetProcessById(pid); launchedClients.Add(process); ownedClients[key] = process;
   if (!saved) { Progress("Client open · sign in manually"); return pid; }
   Progress("Waiting for login screen…");
   using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token); timeout.CancelAfter(TimeSpan.FromSeconds(90));
   while (true) { timeout.Token.ThrowIfCancellationRequested(); process.Refresh(); if (process.HasExited) throw new InvalidOperationException("Game client closed before login."); if (process.MainWindowHandle != IntPtr.Zero) break; await Task.Delay(50, timeout.Token); }
   using var form = new NativeLoginForm(pid); await LoginFormAutomation.Run(form, name, ticket!.Ticket, Progress, timeout.Token);
   Progress("Login submitted · waiting for character…");
   string actual = await authenticated.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellation.Token);
   if (!actual.Equals(name, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The client connected as another character. Check its login before running quests.");
   Progress("Connected · ready for quests"); return pid;
  }
  catch (OperationCanceledException) { Progress(cancellation.IsCancellationRequested ? "Launch cancelled · sign in manually" : "Login timed out · sign in manually"); throw new InvalidOperationException(launchStates[name]); }
  catch (TimeoutException) { Progress("Login not confirmed · check the game window"); throw new InvalidOperationException(launchStates[name]); }
  catch { Progress("Launch unavailable · check the game window"); throw; }
  finally { ticket?.Cancel(); ticketCancellation.Dispose(); pendingLaunches.Remove(key); Notify(); }
 }
 void CancelLaunches(string? name = null) { foreach (var pending in pendingLaunches.Where(p => name is null || p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).ToArray()) pending.Cancel(); }
 async Task AwaitLaunchesAsync(string? name = null) { try { await Task.WhenAll(launchTasks.Where(p => name is null || p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).Where(t => !t.IsCompleted).ToArray()); } catch (Exception) { Log("Pending character launch stopped."); } }
}
