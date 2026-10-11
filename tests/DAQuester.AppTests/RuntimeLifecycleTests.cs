using System.Reflection;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Control;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public sealed class RuntimeLifecycleTests
{
 static DesktopRuntime Runtime(bool preview = false)
 {
  AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
  var settings = AppSettings.Load(); settings.Characters = ["Elara"]; settings.ClientPath = ""; settings.Save();
  return new(preview: preview, updateBackend: new NoUpdates());
 }

 [Fact]
 public async Task ProductionLaunchBlockerReportsReadinessAndMissingClientWithoutLaunching()
 {
  var runtime = Runtime();
  try
  {
   Assert.Contains("connection isn't ready", runtime.LaunchBlocker("Elara"));
   typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.Ready))!.SetValue(runtime, true);
   Assert.Contains("application not found", runtime.LaunchBlocker("Elara"));
   Assert.False(runtime.LaunchPending("Elara")); Assert.False(runtime.QuestActionsRunning);
  }
  finally { await runtime.ShutdownAsync(); }
 }

 [Fact]
 public async Task ClientPreflightCachesImageValidationUntilPathOrWriteTimeChanges()
 {
  var runtime = Runtime();
  typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.Ready))!.SetValue(runtime, true);
  string first = Path.Combine(AppSettings.DataPath, "synthetic-client.exe"), second = Path.Combine(AppSettings.DataPath, "another-synthetic-client.exe");
  File.WriteAllText(first, "An unsupported synthetic image, never executed."); File.WriteAllText(second, "Another unsupported synthetic image, never executed.");
  runtime.Settings.ClientPath = first;
  try
  {
   string? unsupported = runtime.LaunchBlocker("Elara"); Assert.Contains("supported 7.41 client", unsupported);
   // An unchanged image need not be read again when another operation holds its file handle.
   using (var locked = new FileStream(first, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.Equal(unsupported, runtime.LaunchBlocker("Elara"));
   File.SetLastWriteTimeUtc(first, File.GetLastWriteTimeUtc(first).AddSeconds(10));
   using (var locked = new FileStream(first, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.Contains("couldn't be read", runtime.LaunchBlocker("Elara"));
   runtime.Settings.ClientPath = second; Assert.Contains("supported 7.41 client", runtime.LaunchBlocker("Elara"));
   runtime.Settings.ClientPath = Path.Combine(AppSettings.DataPath, "missing-client.exe"); Assert.Contains("application not found", runtime.LaunchBlocker("Elara"));
   Assert.False(runtime.QuestActionsRunning);
  }
  finally { await runtime.ShutdownAsync(); }
 }

 [Fact]
 public async Task OwnedClientFocusRemainsAvailableWithoutRevalidatingAnImage()
 {
  var runtime = Runtime(); typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.Ready))!.SetValue(runtime, true);
  var owned = (Dictionary<string, Process>)typeof(DesktopRuntime).GetField("ownedClients", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
  // Observe only this test process's liveness. LaunchAsync and native focus are never invoked.
  using var process = Process.GetCurrentProcess(); owned.Add("Elara", process);
  try { Assert.Null(runtime.LaunchBlocker("Elara")); Assert.Contains("focuses", runtime.LaunchHint("Elara")); }
  finally { owned.Remove("Elara"); await runtime.ShutdownAsync(); }
 }

 [Fact]
 public async Task ConnectedCharacterUsesItsGameWindowInsteadOfLaunchingAnotherClient()
 {
  var runtime = Runtime(); typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.Ready))!.SetValue(runtime, true);
  using var client = new TcpClient(); using var remote = new TcpClient();
  await using var connection = new ProxyConnection(client, remote, 7, _ => throw new InvalidOperationException("Synthetic launcher tests never connect."));
  typeof(DAQuester.DarkAges.Protocol.StateTracker).GetProperty("Snapshot")!.SetValue(connection.Tracker, new CharacterSnapshot { Name = "Elara", Connected = true, Generation = 7 });
  var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
  var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!; sessions.Add("Elara", session);
  try { Assert.Contains("already connected", runtime.LaunchBlocker("Elara")); Assert.Null(runtime.LaunchHint("Elara")); }
  finally { sessions.Remove("Elara"); await runtime.ShutdownAsync(); }
 }

 [Fact]
 public async Task BlankManualLaunchUsesTheManualPendingKeyOnlyWithAnEmptySavedAccountList()
 {
  var runtime = Runtime(); typeof(DesktopRuntime).GetProperty(nameof(DesktopRuntime.Ready))!.SetValue(runtime, true);
  var pending = (Dictionary<string, CancellationTokenSource>)typeof(DesktopRuntime).GetField("pendingLaunches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
  var states = (Dictionary<string, string>)typeof(DesktopRuntime).GetField("launchStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
  using var owner = new CancellationTokenSource();
  try
  {
   Assert.Contains("select", runtime.LaunchBlocker(""), StringComparison.OrdinalIgnoreCase);
   runtime.Settings.Characters = []; Assert.Contains("application not found", runtime.LaunchBlocker(""));
   pending.Add("(manual)", owner); states[""] = "Client open · sign in manually";
   Assert.True(runtime.LaunchPending("")); Assert.Equal(states[""], runtime.LaunchBlocker(""));
   pending.Remove("(manual)"); pending.Add("Elara", owner); states["Elara"] = "Waiting for login screen…";
   Assert.True(runtime.LaunchPending("Elara")); Assert.Equal(states["Elara"], runtime.LaunchBlocker("Elara"));
  }
  finally { pending.Clear(); await runtime.ShutdownAsync(); }
 }

 [Fact]
 public async Task PreviewLaunchRemainsInteractiveButCannotReachAProxyOrClient()
 {
  var runtime = Runtime(preview: true);
  try
  {
   Assert.Null(runtime.LaunchBlocker("Elara")); Assert.Null(runtime.LaunchBlocker(""));
   Assert.Contains("Preview cannot launch", runtime.LaunchHint("Elara"));
   var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.LaunchAsync("Elara")); Assert.Contains("Preview cannot launch", error.Message);
   Assert.False(runtime.Ready); Assert.False(runtime.QuestActionsRunning); Assert.False(runtime.LaunchedClientRunning);
  }
  finally { await runtime.ShutdownAsync(); }
 }

 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public async Task DiagnosticWriteFailureIsReportedOncePerFailurePeriodWithoutLosingMemoryLogs(bool blockedByDirectory)
 {
  var runtime = Runtime(preview: true); string path = Path.Combine(AppSettings.DataPath, "diagnostics.log");
  List<string> emitted = []; runtime.Diagnostic += emitted.Add; FileStream? locked = null;
  void Block() { if (blockedByDirectory) { File.Delete(path); Directory.CreateDirectory(path); } else locked = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None); }
  void Unblock() { if (blockedByDirectory && Directory.Exists(path)) Directory.Delete(path); locked?.Dispose(); locked = null; }
  try
  {
   Block(); runtime.Log("first diagnostic"); runtime.Log("second diagnostic");
   Assert.Contains(runtime.Logs, line => line.EndsWith("first diagnostic")); Assert.Contains(runtime.Logs, line => line.EndsWith("second diagnostic"));
   Assert.Single(runtime.Logs, line => line.Contains("diagnostics.log not written:")); Assert.Single(emitted, line => line.Contains("diagnostics.log not written:"));
   Unblock(); runtime.Log("write recovered"); Assert.Contains("write recovered", File.ReadAllText(path));
   Block(); runtime.Log("third diagnostic");
   Assert.Equal(2, runtime.Logs.Count(line => line.Contains("diagnostics.log not written:"))); Assert.Equal(2, emitted.Count(line => line.Contains("diagnostics.log not written:")));
   Assert.Contains(runtime.Logs, line => line.EndsWith("third diagnostic"));
  }
  finally { Unblock(); await runtime.ShutdownAsync(); }
 }

 [Fact]
 public async Task ExpiredOrdinaryAttemptIsReadyForANewRunWhileUnfinishedProgressCanResume()
 {
  var runtime = Runtime(preview: true);
  runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, Level = 11, Hp = 10, Gold = 100, InventoryKnown = true, Weight = 1, MaxWeight = 100 });
  Assert.True(runtime.Calibration.Ready);
  var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
  var ordinary = new QuestJob { Character = "Elara", Quest = "spare-a-stick", Step = "complete", Status = "Completed", CompletedAt = DateTimeOffset.UtcNow.AddHours(-9) }; await journal.SaveAsync(ordinary);
  try
  {
   var availability = runtime.Availability("Elara", ordinary.Quest); Assert.Equal("Ready", availability.Status); Assert.True(availability.CanQueue);
   var vm = new MainWindowViewModel(runtime); vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == ordinary.Quest);
   Assert.Equal("Run again", vm.StartQuestLabel); Assert.True(vm.StartQuestCommand.CanExecute(null)); Assert.False(vm.ResumeQuestCommand.CanExecute(null));
   await journal.SaveAsync(ordinary with { Step = "gather", CompletedAt = null }); vm.Refresh();
   Assert.Equal("Resume ready", runtime.Availability("Elara", ordinary.Quest).Status); Assert.True(vm.ResumeQuestCommand.CanExecute(null));
  }
  finally { await runtime.ShutdownAsync(); }
 }

 [Fact]
 public async Task RunningJourneyKeepsItsQuestDefinitionWhenAnotherLibraryRowIsSelected()
 {
  var (runtime, runner, active) = await RunningPreview();
  using (active)
  try
  {
   var vm = new MainWindowViewModel(runtime); vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "mundane-birth");
   Assert.Equal("Spare a Stick", vm.JourneyTitle); Assert.Equal(50, vm.JourneyProgress);
   Assert.Equal("100 Gold", vm.JourneyRewards); Assert.True(vm.RepeatVisible);
   Assert.Equal("mundane-birth", vm.SelectedQuest.RecipeId); Assert.True(vm.PauseQuestCommand.CanExecute(null));
   Assert.Equal("spare-a-stick", runtime.RunningQuestId("Elara")); Assert.Null(runtime.RunningQuestId("Wren"));
   typeof(QuestRunner).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, null); vm.Refresh();
   Assert.Null(runtime.RunningQuestId("Elara")); Assert.Equal("Mundane Birth", vm.JourneyTitle);
  }
  finally
  {
   typeof(QuestRunner).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, null); await runtime.ShutdownAsync();
  }
 }

 [Fact]
 public async Task RunningJobCannotSupplyAnotherQuestsCheckpointOrInventItsHistory()
 {
  var (runtime, runner, active) = await RunningPreview();
  using (active)
  try
  {
   var other = runtime.Job("Elara", "mundane-birth");
   Assert.Equal("mundane-birth", other.Quest); Assert.Null(other.CompletedAt); Assert.Empty(other.Rewards);
   var vm = new MainWindowViewModel(runtime); var history = Assert.Single(vm.History);
   Assert.Equal("Spare a Stick", history.Quest); Assert.Equal("Elara", history.Character);
   runtime.SetPreviewQueue(new() { Character = "Elara", Quests = ["mundane-birth"] }); vm.Refresh();
   Assert.Equal("One verified attempt", Assert.Single(vm.QueueItems).Detail);
  }
  finally
  {
   typeof(QuestRunner).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, null); await runtime.ShutdownAsync();
  }
 }

 [Fact]
 public async Task CalibrationProjectsItsQuestOnlyWhileCaptureIsActive()
 {
  var runtime = Runtime(preview: true); var snapshot = new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, Level = 11, Hp = 10 };
  runtime.SetPreviewSnapshot(snapshot); var session = new PresentationOnlySession(snapshot);
  var capture = new AutomaticCalibration(session, new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")), new ReplayRecorder(), runtime.Calibration, []);
  typeof(AutomaticCalibration).GetProperty(nameof(AutomaticCalibration.Job))!.SetValue(capture, capture.Job with { Status = "Calibrating", Step = "gather", Reason = "Automatic evidence capture" });
  const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
  var runs = (Dictionary<string, AutomaticCalibration>)typeof(DesktopRuntime).GetField("calibrationRuns", privateInstance)!.GetValue(runtime)!;
  var tasks = (Dictionary<string, Task>)typeof(DesktopRuntime).GetField("calibrationTasks", privateInstance)!.GetValue(runtime)!;
  var controls = (Dictionary<string, CancellationTokenSource>)typeof(DesktopRuntime).GetField("controlActions", privateInstance)!.GetValue(runtime)!;
  using var owner = new CancellationTokenSource(); controls.Add("Elara", owner);
  var pending = new TaskCompletionSource(); runs.Add("Elara", capture); tasks.Add("Elara", pending.Task);
  try
  {
   Assert.Equal("spare-a-stick", runtime.RunningQuestId("Elara"));
   var vm = new MainWindowViewModel(runtime); vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "mundane-birth");
   Assert.Equal("Spare a Stick", vm.JourneyTitle); Assert.Equal("Calibration in progress", vm.JourneyState);
   Assert.True(vm.PauseQuestCommand.CanExecute(null)); Assert.True(vm.StopQuestCommand.CanExecute(null)); Assert.False(vm.RepeatEnabled);
   pending.SetResult(); Assert.Null(runtime.RunningQuestId("Elara"));
  }
  finally { pending.TrySetResult(); controls.Remove("Elara"); await runtime.ShutdownAsync(); }
 }

 static async Task<(DesktopRuntime Runtime, QuestRunner Runner, CancellationTokenSource Active)> RunningPreview()
 {
  var runtime = Runtime(preview: true); var snapshot = new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, Level = 11, Hp = 10 };
  runtime.SetPreviewSnapshot(snapshot);
  var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
  var job = new QuestJob { Character = "Elara", Quest = "spare-a-stick", Step = "gather", Status = "Running", Reason = "Collecting branches", CompletedAt = DateTimeOffset.UtcNow.AddHours(-9), Rewards = new() { ["Gold"] = 100 } }; await journal.SaveAsync(job);
  var runner = new QuestRunner(new PresentationOnlySession(snapshot), journal, runtime.Recipes[job.Quest], runtime.Calibration);
  // Present a real owner's state without invoking RunAsync, executing commands, or making a connection.
  typeof(QuestRunner).GetProperty(nameof(QuestRunner.Job))!.SetValue(runner, job);
  var active = new CancellationTokenSource(); typeof(QuestRunner).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, active);
  var runners = (Dictionary<string, QuestRunner>)typeof(DesktopRuntime).GetField("runners", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!; runners.Add("Elara", runner);
  return (runtime, runner, active);
 }

 [Fact]
 public async Task ClosedRuntimeRejectsNewOwnersBeforeAnyConnectionOrClientCanStart()
 {
  var runtime = Runtime(); await runtime.ShutdownAsync();
  await AssertClosedAdmissions(runtime);
  Assert.False(runtime.Ready);
  Assert.Empty(new FileQuestQueueStore(Path.Combine(AppSettings.DataPath, "queues")).Load("Elara").Quests);
 }

 [Fact]
 public async Task ShutdownCancelsAndDrainsLocalOwnerWhileRejectingNewAdmissions()
 {
  var runtime = Runtime();
  // Stand in only for the local cancellation owner. No session or game command is created.
  var owners = (Dictionary<string, CancellationTokenSource>)typeof(DesktopRuntime).GetField("controlActions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
  using var owner = new CancellationTokenSource(); owners.Add("Elara", owner);
  var shutdown = runtime.ShutdownAsync();
  try
  {
   Assert.True(owner.IsCancellationRequested);
   Assert.False(shutdown.IsCompleted);
   Assert.Same(shutdown, runtime.ShutdownAsync());
   await AssertClosedAdmissions(runtime);
  }
  finally { owners.Remove("Elara"); await shutdown; }
 }

 [Fact]
 public async Task FastModePreservesConnectionProfileSavedForTheNextRestart()
 {
  var runtime = Runtime(preview: true);
  var pending = AppSettings.Load(); pending.RemoteAddress = "127.0.0.2"; pending.RemotePorts = [3610, 3611, 3612]; pending.LocalPorts = [4910, 4911, 4912]; pending.MovementMilliseconds = 735; pending.Save();
  runtime.SetFastMode("Elara", true);
  var persisted = AppSettings.Load();
  Assert.Equal(pending.RemoteAddress, persisted.RemoteAddress); Assert.Equal(pending.RemotePorts, persisted.RemotePorts); Assert.Equal(pending.LocalPorts, persisted.LocalPorts); Assert.Equal(pending.MovementMilliseconds, persisted.MovementMilliseconds);
  Assert.True(persisted.FastModeEnabled("Elara")); Assert.True(runtime.Settings.FastModeEnabled("Elara"));
  await runtime.ShutdownAsync();
 }

 [Theory]
 [InlineData("entered-muisir-village", 30, "Completed")]
 [InlineData("spare-a-stick", 12, "Blocked")]
 public async Task PendingSubmissionCanReconcileEvenWhenAnotherNewAttemptIsBlocked(string quest, int level, string newAttemptStatus)
 {
  var runtime = Runtime(preview: true);
  runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, Level = level, Hp = 10, LegendsKnown = true, LegendsRevision = 5, Legends = quest == "entered-muisir-village" ? [MuisirEvidence.Legend] : [] });
  var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
  var pending = new QuestJob { Character = "Elara", Quest = quest, Step = "confirm", Pending = new(quest == "entered-muisir-village" ? "muisir-turn-in" : "turn-in", 7, 1, 0, 0, 0, DateTimeOffset.UtcNow) };
  await journal.SaveAsync(pending);
  runtime.SetPreviewQueue(new() { Character = "Elara", Quests = [quest], State = "Paused", ActiveQuest = quest, ActiveResume = true });

  var availability = runtime.Availability("Elara", quest);
  Assert.Equal("Resume ready", availability.Status); Assert.False(availability.CanQueue); Assert.True(availability.Ready);
  Assert.Equal("", runtime.QueueStartReason("Elara", runtime.Queue("Elara")));

  await journal.SaveAsync(pending with { Step = "preflight", Pending = null });
  availability = runtime.Availability("Elara", quest);
  Assert.Equal(newAttemptStatus, availability.Status); Assert.False(availability.CanQueue); Assert.False(availability.Ready);
  await runtime.ShutdownAsync();
 }

 [Theory]
 [InlineData("entered-muisir-village", 1, true)]
 [InlineData("spare-a-stick", 11, true)]
 [InlineData("spare-a-stick", 11, false)]
 public async Task UnrestrictedPendingSubmissionRemainsSelectableAndNeedsLiveStateToResume(string quest, int level, bool connected)
 {
  var runtime = Runtime(preview: true);
  runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = connected, Generation = 7, Revision = 5, Level = level, Hp = 10, LegendsKnown = true, LegendsRevision = 5 });
  var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
  await journal.SaveAsync(new() { Character = "Elara", Quest = quest, Step = "confirm", Pending = new(quest == "entered-muisir-village" ? "muisir-turn-in" : "turn-in", 7, 1, 0, 0, 0, DateTimeOffset.UtcNow) });
  var availability = runtime.Availability("Elara", quest);
  Assert.True(availability.CanQueue); Assert.Equal(connected, availability.Ready);
  Assert.Equal(connected ? "Resume ready" : "Not checked", availability.Status);
  await runtime.ShutdownAsync();
 }

 [Fact]
 public async Task PreparingAnotherQuestKeepsExclusiveCharacterOwnershipAcrossCheckpointSave()
 {
  var runtime = Runtime(); using var client = new TcpClient(); using var remote = new TcpClient();
  await using var connection = new ProxyConnection(client, remote, 7, _ => throw new InvalidOperationException("Synthetic ownership tests never connect."));
  typeof(DAQuester.DarkAges.Protocol.StateTracker).GetProperty("Snapshot")!.SetValue(connection.Tracker, new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, Level = 11, Hp = 10 });
  var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
  const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
  var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", privateInstance)!.GetValue(runtime)!;
  var runners = (Dictionary<string, QuestRunner>)typeof(DesktopRuntime).GetField("runners", privateInstance)!.GetValue(runtime)!;
  sessions.Add("Elara", session);
  var old = new QuestRunner(session, new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")), runtime.Recipes["spare-a-stick"], runtime.Calibration); runners.Add("Elara", old);
  // Hold only the old runner's checkpoint gate. Neither runner is ever executed.
  var checkpoint = (SemaphoreSlim)typeof(QuestRunner).GetField("owner", privateInstance)!.GetValue(old)!; await checkpoint.WaitAsync();
  var prepare = typeof(DesktopRuntime).GetMethod("PrepareRunnerAsync", privateInstance)!;
  Task<QuestRunner> Prepare(string id) => (Task<QuestRunner>)prepare.Invoke(runtime, ["Elara", runtime.Recipes[id], null, null, false])!;
  var first = Prepare("perfect-hairstyle"); Task<QuestRunner>? second = null;
  try
  {
   Assert.False(first.IsCompleted); Assert.True(runtime.QuestActionsRunning);
   second = Prepare("entered-muisir-village");
   var error = await Assert.ThrowsAsync<QuestBlockedException>(() => second.WaitAsync(TimeSpan.FromSeconds(1)));
   Assert.Contains("preparing", error.Message, StringComparison.OrdinalIgnoreCase);
   await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SetRepeatAsync("Elara", "spare-a-stick", true));
   Assert.Same(old, runners["Elara"]); Assert.False(runtime.RepeatEnabled("Elara", "spare-a-stick"));
  }
  finally
  {
   checkpoint.Release(); var prepared = await first;
   if (second is not null) try { await second; } catch (QuestBlockedException) { }
   Assert.Same(prepared, runners["Elara"]); Assert.Equal("perfect-hairstyle", prepared.Job.Quest);
   await runtime.ShutdownAsync();
  }
 }

 [Fact]
 public async Task ChoicesOpenedBeforeAnotherPreparationCannotOverwriteItsCheckpoint()
 {
  var runtime = Runtime(); using var client = new TcpClient(); using var remote = new TcpClient();
  await using var connection = new ProxyConnection(client, remote, 7, _ => throw new InvalidOperationException("Synthetic ownership tests never connect."));
  typeof(DAQuester.DarkAges.Protocol.StateTracker).GetProperty("Snapshot")!.SetValue(connection.Tracker, new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, Level = 11, Hp = 10 });
  var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
  const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
  var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", privateInstance)!.GetValue(runtime)!;
  var runners = (Dictionary<string, QuestRunner>)typeof(DesktopRuntime).GetField("runners", privateInstance)!.GetValue(runtime)!;
  sessions.Add("Elara", session);
  var old = new QuestRunner(session, new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")), runtime.Recipes["spare-a-stick"], runtime.Calibration); runners.Add("Elara", old);
  var choice = new TaskCompletionSource<QuestChoiceResult?>(TaskCreationOptions.RunContinuationsAsynchronously); runtime.ChooseQuestAsync = (_, _, _, _) => choice.Task;
  var configure = typeof(DesktopRuntime).GetMethod("ConfigureQuestAsync", privateInstance)!;
  var configured = (Task<bool>)configure.Invoke(runtime, ["Elara", runtime.Recipes["mundane-birth"], false])!;
  var checkpoint = (SemaphoreSlim)typeof(QuestRunner).GetField("owner", privateInstance)!.GetValue(old)!; await checkpoint.WaitAsync();
  var prepare = typeof(DesktopRuntime).GetMethod("PrepareRunnerAsync", privateInstance)!;
  var preparing = (Task<QuestRunner>)prepare.Invoke(runtime, ["Elara", runtime.Recipes["perfect-hairstyle"], null, null, false])!;
  try
  {
   Assert.False(preparing.IsCompleted);
   choice.SetResult(new(new("Spring", 20, "Mileth")));
   await Assert.ThrowsAsync<QuestBlockedException>(() => configured);
   Assert.Null(runtime.Job("Elara", "mundane-birth").BirthOptions); Assert.Same(old, runners["Elara"]);
  }
  finally
  {
   checkpoint.Release(); await preparing; await runtime.ShutdownAsync();
  }
 }

 [Theory]
 [InlineData(true)]
 [InlineData(false)]
 public async Task LocalActionWaitingForCheckpointCannotEnterAfterShutdownBegins(bool calibrate)
 {
  var runtime = Runtime(); using var client = new TcpClient(); using var remote = new TcpClient();
  await using var connection = new ProxyConnection(client, remote, 7, _ => throw new InvalidOperationException("Synthetic ownership tests never connect."));
  typeof(DAQuester.DarkAges.Protocol.StateTracker).GetProperty("Snapshot")!.SetValue(connection.Tracker, new CharacterSnapshot { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, Level = 11, Hp = 10 });
  var session = new GameSession(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
  const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
  var sessions = (Dictionary<string, GameSession>)typeof(DesktopRuntime).GetField("sessions", privateInstance)!.GetValue(runtime)!;
  var runners = (Dictionary<string, QuestRunner>)typeof(DesktopRuntime).GetField("runners", privateInstance)!.GetValue(runtime)!;
  sessions.Add("Elara", session);
  var old = new QuestRunner(session, new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")), runtime.Recipes["spare-a-stick"], runtime.Calibration); runners.Add("Elara", old);
  var checkpoint = (SemaphoreSlim)typeof(QuestRunner).GetField("owner", privateInstance)!.GetValue(old)!; await checkpoint.WaitAsync();
  var control = typeof(DesktopRuntime).GetMethod("ControlAsync", privateInstance)!;
  Task<object?> operation = calibrate ? runtime.StartAutomaticCalibration("Elara") : (Task<object?>)control.Invoke(runtime, [new ControlRequest("command", "Elara", JsonSerializer.SerializeToElement(new GameCommand("inspect-self")))])!;
  var shutdown = runtime.ShutdownAsync();
  Assert.False(operation.IsCompleted); Assert.False(shutdown.IsCompleted);
  checkpoint.Release();
  try
  {
   var error = await Record.ExceptionAsync(async () => { await operation; }); Assert.NotNull(error);
   Assert.Contains("closing", error.Message, StringComparison.OrdinalIgnoreCase);
  }
  finally { await shutdown; }
 }

 static async Task AssertClosedAdmissions(DesktopRuntime runtime)
 {
  foreach (var operation in new Func<Task>[]{
   ()=>runtime.StartAsync(),
   ()=>runtime.LaunchAsync("Elara"),
   ()=>runtime.StartQuestAsync("Elara","mundane-birth"),
   ()=>runtime.SetRepeatAsync("Elara","spare-a-stick",true),
   ()=>runtime.StartQueueAsync("Elara"),
   ()=>runtime.StartAutomaticCalibration("Elara"),
   ()=>runtime.EditQueueAsync("Elara",queue=>queue with{Quests=["mundane-birth"]}),
   ()=>runtime.SetHomeAsync("Elara")
  })
  {
   var error = await Assert.ThrowsAsync<InvalidOperationException>(operation);
   Assert.Contains("closing", error.Message, StringComparison.OrdinalIgnoreCase);
  }
 }

 sealed class NoUpdates : IUpdateBackend
 {
  public bool IsInstalled => false;
  public string? PendingVersion => null;
  public Task<string?> CheckAsync() => throw new InvalidOperationException("Lifecycle tests never check updates.");
  public Task DownloadAsync() => throw new InvalidOperationException("Lifecycle tests never download updates.");
  public void PrepareExit() => throw new InvalidOperationException("Lifecycle tests never install updates.");
 }

 sealed class PresentationOnlySession(CharacterSnapshot snapshot) : IGameSession
 {
  public CharacterSnapshot Snapshot => snapshot;
  public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
  public event Action<CharacterSnapshot>? Changed { add { } remove { } }
  public event Action<string>? Interrupted { add { } remove { } }
  public Task ExecuteAsync(GameCommand command, CancellationToken cancellation) => throw new InvalidOperationException("Presentation tests never execute game commands.");
  public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation) => throw new InvalidOperationException("Presentation tests never wait on a game connection.");
 }
}
