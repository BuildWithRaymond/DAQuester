using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.App.Views;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.App.Validation;

// Drives the actual view/viewmodel against isolated files and synthetic state. No game actions or listeners.
internal static class UiChecks
{
 public static async Task RunAsync(Window window, MainWindowViewModel vm)
 {
  string result = Program.IsScreenshots ? "artifacts/screenshots.txt" : "artifacts/app-smoke.txt";
  string evidence = Program.IsScreenshots ? "artifacts/screenshots.json" : "artifacts/app-smoke.json";
  var checks = new List<string>(); var renders = new List<string>(); var states = new HashSet<string>();
  var viewports = new Dictionary<string, Size>();
  Directory.CreateDirectory("artifacts/screenshots"); File.WriteAllText(result, "Running Avalonia synthetic UI checks.");
  try
  {
   void Check(bool valid, string description) { if (!valid) throw new InvalidOperationException(description); checks.Add(description); }
   T Require<T>(string name) where T : Control => window.FindControl<T>(name) ?? throw new InvalidOperationException("Missing Avalonia control: " + name);
   T Modal<T>(Window target, string name) where T : Control => target.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
   async Task Settle() { Dispatcher.UIThread.RunJobs(); await Task.Delay(35); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
   async Task Click(string name) { var button = Require<Button>(name); Check(button.IsEffectivelyEnabled, "Action enabled: " + name); new ButtonAutomationPeer(button).Invoke(); await Settle(); }
   async Task Page(string name) { await Click("nav" + name); Check(vm.Page == name, "In-place navigation: " + name); states.Add(name.ToLowerInvariant()); }
   async Task Capture(string name, double scale = 1, Window? target = null)
   {
    target ??= window;
    await Settle(); target.UpdateLayout();
    Exception? layoutError = null;
    if (!Program.IsHeadless) { try { await AwaitNativeLayout(target); } catch (InvalidOperationException error) { layoutError = error; } }
    viewports[name] = target.ClientSize;
    string path = "artifacts/screenshots/" + name + ".png";
    if (Program.IsHeadless && scale == 1) { using var image = target.CaptureRenderedFrame() ?? throw new InvalidOperationException("Avalonia produced no rendered frame."); image.Save(path); CheckDarkSurface(image, name); }
    else
    {
     var size = target.ClientSize;
     using var image = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale)), new Vector(96 * scale, 96 * scale));
     image.Render(target); image.Save(path); CheckDarkSurface(image, name);
    }
    if (new FileInfo(path).Length < 1000) throw new InvalidOperationException("Avalonia render contains no meaningful pixels: " + name);
    if (layoutError is not null) throw layoutError;
    CheckVisibleActions(target);
    renders.Add(path);
   }
   async Task CapturePopup(ComboBox combo, string name)
   {
    combo.IsDropDownOpen = true; await Settle();
    var popup = combo.GetVisualDescendants().OfType<Popup>().Concat(combo.GetLogicalDescendants().OfType<Popup>()).FirstOrDefault(p => p.Name == "PART_Popup") ?? throw new InvalidOperationException("Missing attached ComboBox popup: " + name);
    Check(popup.IsOpen && popup.Child?.GetVisualRoot() != null, "ComboBox opens an attached styled popup: " + name);
    var child = popup.Child!; child.UpdateLayout(); var size = child.Bounds.Size;
    Check(size.Width > 0 && size.Height > 0, "ComboBox popup has visible content: " + name);
    string path = "artifacts/screenshots/" + name + ".png";
    using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)), new Vector(96, 96))) { bitmap.Render(child); bitmap.Save(path); CheckDarkSurface(bitmap, name); }
    Check(new FileInfo(path).Length >= 1000, "ComboBox popup contains actual rendered items: " + name); renders.Add(path);
    combo.IsDropDownOpen = false; await Settle();
   }
   async Task FixtureQueue(QuestQueueState state) { await new FileQuestQueueStore(Path.Combine(AppSettings.DataPath, "queues")).SaveAsync(state); vm.Runtime.SetPreviewQueue(state); vm.Refresh(); await Settle(); }
   async Task Select(string name) { vm.SelectedCharacter = vm.Characters.Single(c => c.Name == name); await Settle(); Check(vm.SelectedCharacter?.Name == name, "Character selection: " + name); }

   Check(vm.Runtime.Preview, "Validation runtime is preview only");
   Check(Path.GetFullPath(AppSettings.DataPath).StartsWith(Path.GetFullPath("artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "User Data is isolated under ignored artifacts");
   string emptyClientCache = Path.Combine(AppSettings.DataPath, "empty-client-cache"); Directory.CreateDirectory(emptyClientCache);
   var packagedMaps = new MapRepository(emptyClientCache, Path.Combine(AppSettings.Content, "routes.json"), vm.Runtime.Settings.ServerProfile);
   var packagedRoute = packagedMaps.Plan(500, new(33, 37), 413, new(3, 13));
   Check(packagedRoute.Steps.Count > 0 && packagedRoute.Steps.Last().Map == 413 && packagedRoute.Cost > 0 && !Directory.EnumerateFiles(emptyClientCache, "*", SearchOption.AllDirectories).Any(), "Shipped functional grids plan Mileth to Suomi Tavern with an empty client map cache");
   Check(!packagedMaps.Load(97).Blocked(new(46, 21)), "Packaged walkability preserves the established map-97 entrance override");
   await vm.Runtime.StartAsync(); Check(!vm.Runtime.Ready && !vm.Runtime.ConnectedGame, "Preview startup opens no game listeners or sessions");
   if (Program.IsScreenshots)
   {
    var handle = window.TryGetPlatformHandle();
    Check(handle is { HandleDescriptor: "HWND" } && handle.Handle != IntPtr.Zero, "Native screenshot validation uses only its own Avalonia window handle");
    Check(DwmGetWindowAttribute(handle!.Handle, 20, out int darkChrome, sizeof(int)) == 0 && darkChrome == 1, "The native Windows title bar opts into immersive dark mode");
   }
   await QueueOwnershipChecks.RunAsync(vm.Runtime).WaitAsync(TimeSpan.FromSeconds(30));
   Check(true, "Manual movement and disconnect pause queue advancement between entries");
   Check(true, "Stop cancels all queue owners despite a failed checkpoint save");
   Check(true, "Stop preserves queue edits and home locations after pending saves settle");
   Check(vm.Characters.Count == 0, "Fresh accounts remain empty without invented characters");
   Check(Require<Button>("launchSelected").IsEffectivelyEnabled, "An empty workspace offers the manual client Launch action");
   await vm.LaunchSelectedCommand.ExecuteAsync(null);
   Check(vm.HasError && vm.ErrorMessage.Contains("Preview cannot launch") && !vm.Runtime.LaunchedClientRunning && vm.Characters.Count == 0, "Empty-workspace Launch reaches the guarded manual launcher without adding accounts or starting a client");
   vm.DismissErrorCommand.Execute(null);
   Check(!vm.AutomaticUpdateChecks, "Update checks default off");
   var updateBackend = SyntheticUpdateBackend.Current ?? throw new InvalidOperationException("Validation must inject the synthetic update backend.");
   Check(updateBackend.Checks == 0 && updateBackend.Downloads == 0 && updateBackend.Installs == 0, "Preview startup performs no update check, download or install");
   window.Width = 1450; window.Height = 930; await Page("Planner"); states.Add("empty"); await Capture("planner-empty");
   await Page("Characters"); await Capture("characters-empty"); await Page("History"); await Capture("history-empty");
   await Page("Settings"); await Capture("settings-default");

   var now = new DateTimeOffset(2026, 10, 11, 14, 30, 0, TimeSpan.Zero);
   vm.Runtime.Settings.Characters = ["Agent", "Elara", "Wren"]; vm.Runtime.Settings.Save();
   var snapshot = new CharacterSnapshot
   {
    Name = "Agent",
    Id = 42,
    Connected = true,
    Generation = 91,
    Revision = 12,
    Level = 99,
    Hp = 400,
    Mana = 200,
    Gold = 999,
    Experience = 250000,
    Class = "Wizard",
    IsMaster = true,
    IsPure = true,
    Weight = 7,
    MaxWeight = 100,
    InventoryKnown = true,
    SpellsKnown = true,
    LegendsKnown = true,
    LegendsRevision = 12,
    Map = 3082,
    MapName = "Rucesion Threshold",
    Position = new(12, 18),
    Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Rucesion Song", 3)),
    Legends = ImmutableArray.Create("Entered Muisir Village - Deoch 223, Spring")
   };
   var memory = new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), vm.Runtime.Settings.ServerProfile);
   var observedCharacters = new[] { snapshot, snapshot with { Name = "Elara", Id = 43, Level = 11, Legends = [] }, snapshot with { Name = "Wren", Id = 44, Level = 10, Connected = false, Legends = [] } };
   foreach (var observed in observedCharacters) await memory.ObserveAsync(observed.Name!, observed with { Connected = true });
   foreach (var observed in observedCharacters) vm.Runtime.SetPreviewSnapshot(observed);
   vm.Refresh(); await Settle(); await Select("Agent"); states.Add("populated");
   await CapturePopup(Require<ComboBox>("characterSwitcher"), "character-switcher-menu");
   // Exercise production readiness presentation without ever starting its listeners or launch commands.
   var blockedRuntime = new DesktopRuntime(preview: false, updateBackend: updateBackend);
   try
   {
    var blockedVm = new MainWindowViewModel(blockedRuntime);
    window.DataContext = blockedVm; await Settle();
    Check(!blockedRuntime.Preview && !blockedRuntime.Ready && !blockedRuntime.ConnectedGame && !blockedRuntime.LaunchedClientRunning, "The launcher-readiness fixture has no listeners, sessions or launched clients");
    Check(blockedVm.SelectedCharacter?.Name == "Agent" && blockedVm.LaunchBlocker?.Contains("connection", StringComparison.OrdinalIgnoreCase) == true, "The selected saved account explains the unavailable game connection");
    var headerLaunch = Require<Button>("launchSelected");
    Check(!headerLaunch.IsEffectivelyEnabled && !blockedVm.LaunchSelectedCommand.CanExecute(null), "Connection readiness disables the actual header Launch action");
    Check(Equals(ToolTip.GetTip(headerLaunch), blockedVm.LaunchBlocker) && ToolTip.GetShowOnDisabled(headerLaunch) && AutomationProperties.GetHelpText(headerLaunch) == blockedVm.LaunchBlocker, "The disabled header Launch exposes its connection reason to hover and accessibility");
    await Capture("planner-launch-blocked");
    blockedVm.NavigateCommand.Execute("Characters"); await Settle();
    var cardLaunches = window.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "launchCharacter").ToArray();
    var cardStatuses = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "cardLaunchStatus").ToArray();
    Check(cardLaunches.Length == 3 && cardLaunches.All(b => !b.IsEffectivelyEnabled), "Connection readiness disables every saved character's actual Launch action");
    Check(cardLaunches.All(b => b.DataContext is CharacterItemVM character && !character.LaunchCommand.CanExecute(null) && character.LaunchBlocker?.Contains("connection", StringComparison.OrdinalIgnoreCase) == true && Equals(ToolTip.GetTip(b), character.LaunchBlocker) && ToolTip.GetShowOnDisabled(b) && AutomationProperties.GetHelpText(b) == character.LaunchBlocker), "Each disabled character Launch exposes its own explicit connection reason");
    Check(cardStatuses.Length == 3 && cardStatuses.All(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && t.Bounds.Height > 0 && t.DataContext is CharacterItemVM character && t.Text == character.LaunchBlocker), "Every character card renders its launch-blocking reason as visible status text");
    await Capture("characters-launch-blocked"); states.Add("launch-blocked");
    Check(updateBackend.Checks == 0 && updateBackend.Downloads == 0 && updateBackend.Installs == 0, "The unstarted readiness fixture performs no update actions");
   }
   finally
   {
    window.DataContext = vm;
    await blockedRuntime.ShutdownAsync(); vm.Refresh(); await Settle();
   }
   Check(vm.Runtime.Memory("Agent").Level == 99, "Observed character level persists");
   Check(vm.Runtime.Memory("Agent").Legends.Contains(snapshot.Legends[0]), "Observed own legend persists");
   Check(vm.Runtime.Memory("Elara").Level == 11 && !vm.Runtime.Memory("Elara").Legends.Contains(snapshot.Legends[0]), "Observed memory remains per character");
   Check(!vm.Runtime.Availability("Agent", "spare-a-stick").CanQueue, "Level 99 blocks a new Spare a Stick queue");
   Check(vm.Runtime.Availability("Agent", "entered-muisir-village").Status == "Completed", "Own Muisir legend shows Completed");
   Check(!vm.Runtime.Availability("Wren", "shinewood-faerie").CanQueue, "Level 10 blocks a new Shinewood Faerie queue");
   vm.Runtime.SetPreviewSnapshot(snapshot with { Connected = false });
   Check(!vm.Runtime.Availability("Agent", "spare-a-stick").CanQueue && vm.Runtime.Availability("Agent", "entered-muisir-village").Status == "Completed", "Offline remembered restrictions remain enforced");
   vm.Runtime.SetPreviewSnapshot(snapshot); vm.Refresh(); await Page("Planner");
   Check(vm.Quests.Count >= 70, "Planner contains the complete quest catalog");
   Require<TextBox>("plannerSearch").Text = "Shinewood Faerie"; await Settle(); Check(vm.Quests.Count == 1, "Search filters actual quest catalog rows");
   await Page("Settings"); await Page("Planner"); Check(vm.Search == "Shinewood Faerie" && vm.Quests.Count == 1, "Search survives page navigation");
   Require<TextBox>("plannerSearch").Text = ""; await Settle();
   Require<TextBox>("plannerSearch").Focus(); await Settle(); Check(Require<TextBox>("plannerSearch").Watermark == "Search quests, rewards, or requirements", "Search uses one native Avalonia watermark"); await Capture("planner-search-focused-empty");
   if (Program.IsHeadless)
   {
    var field = Require<TextBox>("plannerSearch"); field.Focus(); window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None); await Settle();
    Check(window.FocusManager?.GetFocusedElement() is Control focused && focused != field && focused.IsEffectivelyVisible, "Tab navigation reaches visible controls on the current page"); await Capture("planner-keyboard-focus");
    var navigation = Require<Button>("navCharacters"); var center = navigation.TranslatePoint(new Point(navigation.Bounds.Width / 2, navigation.Bounds.Height / 2), window)!.Value;
    window.MouseMove(center); await Settle(); Check(navigation.Classes.Contains(":pointerover"), "Pointer hover reaches the actual styled navigation control"); await Capture("planner-navigation-hover");
    window.MouseDown(center, MouseButton.Left); await Settle(); Check(navigation.Classes.Contains(":pressed"), "Pointer press reaches the actual styled navigation control"); await Capture("planner-navigation-pressed"); window.MouseUp(center, MouseButton.Left); await Page("Planner");
   }
   await Click("filterCompleted"); Check(vm.Quests.Count > 0, "Completed filter includes observed own completion"); states.Add("completed"); await Capture("planner-completed");
   await Click("filterReady"); await Settle();
   var readyRows = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("questRow") && b.Classes.Contains("ready")).ToArray();
   Check(readyRows.Length > 0, "Ready and choice rows use ready styling");
   Check(readyRows.All(b => b.Background is ISolidColorBrush brush && brush.Color.G > brush.Color.R && brush.Color.G > brush.Color.B), "Ready row surfaces have a restrained green tint");
   await Capture("planner-ready"); await Click("filterAll");
   states.Add("blocked"); await Capture("planner-blocked");

   await Select("Elara"); await FixtureQueue(new() { Character = "Elara" });
   await vm.ToggleQuestAsync(vm.Quests.Single(q => q.RecipeId == "shinewood-faerie"));
   await vm.ToggleQuestAsync(vm.Quests.Single(q => q.RecipeId == "mundane-birth")); await Settle();
   Check(vm.Runtime.Queue("Elara").Quests.SequenceEqual(new[] { "shinewood-faerie", "mundane-birth" }), "Queue membership follows selected character");
   await vm.MoveQueueAsync("Elara", "mundane-birth", 0); await Settle();
   Check(new FileQuestQueueStore(Path.Combine(AppSettings.DataPath, "queues")).Load("ELARA").Quests.SequenceEqual(new[] { "mundane-birth", "shinewood-faerie" }), "Queue ordering persists case-insensitively");
   var queueRows = window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "queueRow").ToArray();
   Check(queueRows.Length == 2, "Both queued quests render as editable rows");
   queueRows[0].Focus();
   if (Program.IsHeadless) { window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Control); window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Control); }
   else queueRows[0].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down, KeyModifiers = KeyModifiers.Control });
   await Settle();
   Check(vm.Runtime.Queue("Elara").Quests[1] == "mundane-birth", "Ctrl+Down reorders the focused queue item");
   Check(window.FocusManager?.GetFocusedElement() is Border { Name: "queueRow" }, "Keyboard reordering retains queue row focus");
   await Capture("planner-queue-keyboard");
   if (Program.IsHeadless)
   {
    var grips = window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "queueGrip").ToArray();
    var from = grips[1].TranslatePoint(new Point(4, 12), window)!.Value; var to = grips[0].TranslatePoint(new Point(4, 4), window)!.Value;
    window.MouseDown(from, MouseButton.Left); window.MouseMove(to, RawInputModifiers.LeftMouseButton); await Settle();
    var insertion = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name is "insertionBefore" or "insertionAfter" && b.IsEffectivelyVisible);
    var insertionBounds = new Rect(insertion.TranslatePoint(default, window)!.Value, insertion.Bounds.Size);
    var queueViewport = Require<ScrollViewer>("queueScroll");
    var queueViewportBounds = new Rect(queueViewport.TranslatePoint(default, window)!.Value, queueViewport.Bounds.Size);
    Check(insertion.Bounds.Height >= 2 && insertion.Bounds.Width > 20 && queueViewportBounds.Contains(insertionBounds), "Pointer dragging renders its insertion indicator inside the queue viewport");
    await Capture("planner-drag-insertion"); window.MouseUp(to, MouseButton.Left); await Settle();
    Check(vm.Runtime.Queue("Elara").Quests[0] == "mundane-birth", "Pointer drop persists the new queue order");
   }
   await Select("Agent"); Check(vm.QueueItems.Count == 0, "Switching characters isolates queue ownership");
   var otherOrder = vm.Runtime.Queue("Elara").Quests.ToArray(); await vm.MoveQueueAsync("Elara", "mundane-birth", 0); Check(vm.Runtime.Queue("Elara").Quests.SequenceEqual(otherOrder), "Cross-character queue reorder requests are rejected");
   await Select("Elara"); Check(vm.QueueItems.Count == 2, "Returning to a character restores its saved queue");
   vm.FastMode = true; Check(AppSettings.Load().FastModeEnabled("ELARA"), "Fast mode saves for the selected character");
   await Select("Agent"); Check(!vm.FastMode, "Fast mode does not leak across characters"); await Select("Elara"); Check(vm.FastMode, "Character switch restores fast mode choice"); vm.FastMode = false;
   vm.Runtime.SetPreviewSnapshot(snapshot with { Name = "Elara", Id = 43, Level = 11, Connected = false, Legends = [] }); vm.Refresh(); await Settle();
   Check(!vm.Runtime.Snapshot("Elara").Connected && !vm.Runtime.QueueRunning("Elara") && !vm.Runtime.QueueBusy("Elara") && vm.Runtime.QueueStartReason("Elara", vm.Runtime.Queue("Elara")).Length > 0, "Disconnected state reaches the selected character's queue guard");
   Check(!vm.StartQueueCommand.CanExecute(null) && ReferenceEquals(Require<Button>("queueStart").Command, vm.StartQueueCommand), "The displayed queue Start action binds to the disconnected command guard");
   Check(!Require<Button>("queueStart").IsEffectivelyEnabled, "Disconnected characters cannot start a queue"); states.Add("disconnected"); await Capture("planner-disconnected");
   vm.Runtime.SetPreviewSnapshot(snapshot with { Name = "Elara", Id = 43, Level = 11, Legends = [] });
   await Click("queueClear"); Check(vm.QueueItems.Count == 0 && new FileQuestQueueStore(Path.Combine(AppSettings.DataPath, "queues")).Load("Elara").Quests.Length == 0, "Clear removes the selected character's persisted queue");

   // Durable attempt receipts retain the same single-run and repeat policies after restoring the app.
   var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
   var previousCalibration = vm.Runtime.Calibration;
   vm.Runtime.SaveCalibration(JsonSerializer.Serialize(previousCalibration with { Validated = true, Patrol = [new(1, 1)], Accepted = ["Synthetic accepted"], Completed = ["Synthetic completed"], EvidenceFiles = ["isolated synthetic fixture"] }, AppSettings.Json));
   var completedAttempt = new QuestJob { Character = "Elara", Quest = "spare-a-stick", Step = "complete", Status = "Completed", CompletedAt = DateTimeOffset.UtcNow.AddHours(-9), CompletionText = "Ordinary attempt completed. Branches and experience verified." };
   await journal.SaveAsync(completedAttempt); vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "spare-a-stick"); vm.Refresh(); await Settle();
   Check(vm.StartQuestCommand.CanExecute(null) && vm.StartQuestLabel == "Run again" && !vm.ResumeQuestCommand.CanExecute(null) && !vm.PauseQuestCommand.CanExecute(null) && !vm.StopQuestCommand.CanExecute(null), "A completed ordinary attempt offers Run again without Resume, Pause or Stop");
   Require<Expander>("journeyControls").IsExpanded = true; await Settle(); Require<Button>("startQuest").Focus(); await Settle();
   var startAction = Require<Button>("startQuest");
   var actionPoint = startAction.TranslatePoint(default, window)!.Value;
   var actionBounds = new Rect(actionPoint, startAction.Bounds.Size);
   var actionViewport = startAction.GetVisualAncestors().OfType<ScrollViewer>().First();
   var viewportBounds = new Rect(actionViewport.TranslatePoint(default, window)!.Value, actionViewport.Bounds.Size);
   Check(viewportBounds.Contains(actionBounds), "Keyboard focus reveals Run again inside the journey viewport"); await Capture("planner-repeatable-actions");
   var cooldownAttempt = completedAttempt with { CompletedAt = DateTimeOffset.UtcNow.AddHours(-1), NextRun = DateTimeOffset.UtcNow.AddHours(7) };
   await journal.SaveAsync(cooldownAttempt); vm.Refresh(); await Settle();
   Check(!vm.StartQuestCommand.CanExecute(null) && vm.Runtime.Availability("Elara", "spare-a-stick").Status == "Cooldown", "Run again respects the ordinary eight-hour cooldown");
   vm.Runtime.SetPreviewSnapshot(snapshot with { Name = "Elara", Id = 43, Level = 11, Connected = false, Legends = [] }); vm.Refresh(); await Settle();
   string dueLabel = QuestRunPolicy.NextAttempt(vm.Runtime.Recipes["spare-a-stick"], cooldownAttempt)!.Value.ToLocalTime().ToString("MMM d, h:mm tt");
   Check(vm.JourneyState == "Attempt recorded" && vm.JourneyMessage.Contains("completed", StringComparison.OrdinalIgnoreCase) && vm.JourneyNext.Contains(dueLabel) && !vm.StartQuestCommand.CanExecute(null), "An offline ordinary attempt retains its completion receipt and next deadline"); await Capture("planner-cooldown-offline");
   vm.Runtime.SetPreviewSnapshot(snapshot with { Name = "Elara", Id = 43, Level = 11, Legends = [] });
   await vm.Runtime.SetRepeatAsync("Elara", "spare-a-stick", true); vm.Refresh(); await Settle();
   Check(vm.RepeatVisible && vm.RepeatEnabled && vm.Repeat && vm.StartQuestCommand.CanExecute(null) && vm.StartQuestLabel == "Schedule", "Optional repeat exposes Schedule during cooldown without treating the attempt as ready"); await Capture("planner-repeat-schedule");
   await vm.Runtime.SetRepeatAsync("Elara", "spare-a-stick", false);
   await journal.SaveAsync(new() { Character = "Elara", Quest = "spare-a-stick", Step = "turn-in", Status = "Running", Reason = "Saved before completion" }); vm.Refresh(); await Settle();
   Check(!vm.Runtime.IsRunning("Elara") && vm.StartQuestCommand.CanExecute(null) && vm.ResumeQuestCommand.CanExecute(null) && !vm.PauseQuestCommand.CanExecute(null), "A restored Running journal without a live owner leaves Start and Resume available"); await Capture("planner-restored-running");
   vm.Runtime.SetPreviewRunning("Elara", true); vm.Refresh(); await Settle();
   Check(!vm.StartQuestCommand.CanExecute(null) && !vm.ResumeQuestCommand.CanExecute(null) && vm.PauseQuestCommand.CanExecute(null) && vm.StopQuestCommand.CanExecute(null), "A live single runner locks Start and Resume while exposing Pause and Stop"); await Capture("planner-single-running");
   await vm.PauseQuestCommand.ExecuteAsync(null); Check(!vm.Runtime.IsRunning("Elara"), "Pause releases the synthetic single-run owner");
   await journal.SaveAsync(new() { Character = "Elara", Quest = "mundane-birth", Step = "complete", CompletedAt = DateTimeOffset.UtcNow, Status = "Completed" }); vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "mundane-birth"); vm.Refresh();
   Check(!vm.StartQuestCommand.CanExecute(null) && !vm.ResumeQuestCommand.CanExecute(null), "A durable one-time birth completion remains final");
   await journal.SaveAsync(new() { Character = "Elara", Quest = "perfect-hairstyle", Step = "complete", CompletedAt = DateTimeOffset.UtcNow, LegendReward = true, Status = "Completed" }); vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "perfect-hairstyle"); vm.Refresh();
   Check(!vm.StartQuestCommand.CanExecute(null) && !vm.ResumeQuestCommand.CanExecute(null) && !vm.Repeat && !vm.RepeatEnabled, "The verified Suomi legend ends weekly hairstyle attempts"); await Capture("planner-hairstyle-completed");
   var refusal = new Dialog(1, 1, 99, 1, "Callough", "You remind me of my child", true, 177, 1, 0, []);
   await journal.SaveAsync(completedAttempt with { PermanentCompletion = new(DateTimeOffset.UtcNow, refusal), Repeat = true, CompletionText = "Callough confirmed this goal is complete." }); vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "spare-a-stick"); vm.Refresh(); await Settle();
   Check(!vm.StartQuestCommand.CanExecute(null) && !vm.ResumeQuestCommand.CanExecute(null) && !vm.Repeat && !vm.RepeatEnabled, "Permanent Callough evidence ends all further ordinary attempts"); await Capture("planner-permanent-completed");
   foreach (string id in new[] { "spare-a-stick", "mundane-birth", "perfect-hairstyle" }) await journal.SaveAsync(new() { Character = "Elara", Quest = id });
   vm.Runtime.SaveCalibration(JsonSerializer.Serialize(previousCalibration, AppSettings.Json)); Require<Expander>("journeyControls").IsExpanded = false;

   await Select("Agent");
   var originalQueue = vm.Runtime.Queue("Agent").Quests.ToArray();
   foreach (string id in new[] { "spare-a-stick", "entered-muisir-village" }) await vm.ToggleQuestAsync(vm.Quests.Single(q => q.RecipeId == id));
   Check(vm.Runtime.Queue("Agent").Quests.SequenceEqual(originalQueue), "Blocked and completed quests reject queue selection");
   foreach (string id in new[] { "mundane-birth", "entered-muisir-village", "emerald-dawn", "shinewood-faerie" })
   {
    vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == id); Check(!vm.RepeatVisible && !vm.Repeat, "One-time and Faerie quests do not offer automatic repeats: " + id);
   }
   vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "perfect-hairstyle");
   Check(vm.RepeatVisible && vm.Repeat && !vm.RepeatEnabled && vm.RepeatLabel == "Weekly until Suomi legend", "Hairstyle repeats weekly until the confirmed Suomi legend");
   vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "entered-muisir-village");
   Check(vm.JourneyState == "Completed" && !vm.StartQuestCommand.CanExecute(null), "Observed nonrepeatable completion disables Start");
   await journal.SaveAsync(new() { Character = "Agent", Quest = "entered-muisir-village", Step = "confirm", Pending = new("final-dialog-cleanup", 91, 12, 0, 0, 0, now), Status = "Paused" }); vm.Refresh();
   Check(vm.JourneyState == "Awaiting confirmation" && vm.ResumeQuestCommand.CanExecute(null), "Pending final-dialog cleanup retains Resume despite the completed legend");
   Check(!vm.Runtime.Availability("Agent", "entered-muisir-village").CanQueue, "A completed legend still rejects a new queue while its pending action can reconcile");
   await FixtureQueue(new() { Character = "Agent", Quests = ["entered-muisir-village"], State = "Paused", ActiveQuest = "entered-muisir-village", ActiveResume = true });
   Check(vm.Runtime.QueueStartReason("Agent", vm.Runtime.Queue("Agent")) == "", "Pending completed queue reconciles before advancement");
   await FixtureQueue(new() { Character = "Agent" }); await journal.SaveAsync(new() { Character = "Agent", Quest = "entered-muisir-village" });
   var waitReply = new Dialog(1, 2, 17, 4, "Faerie", "Sorry, no faeries are willing to bond with you. Come back in one deoch.", true, 1173, 54, 0, []);
   await journal.SaveAsync(new() { Character = "Agent", Quest = "shinewood-faerie", Step = "complete", Status = "Completed", CompletedAt = now, FaeriePetWait = waitReply, FaerieUnavailable = [ShinewoodFaerieEvidence.Pet], CompletionText = "No Pet Faerie this visit; wait one deoch before trying again; Rucesion Song" });
   vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "shinewood-faerie"); vm.Runtime.SetPreviewSnapshot(snapshot with { Connected = false }); vm.Refresh(); await Settle();
   Check(vm.JourneyState.Contains("one deoch", StringComparison.OrdinalIgnoreCase) && vm.JourneyMessage.Contains("No Pet Faerie"), "Saved Faerie failure explains its wait offline without claiming a pet reward"); await Capture("faerie-pet-wait-offline");
   vm.Runtime.SetPreviewSnapshot(snapshot); await journal.SaveAsync(new() { Character = "Agent", Quest = "shinewood-faerie" }); vm.Refresh();

   // Exercise account edits and Windows encryption exclusively inside this run's unique data folder.
   await Page("Characters");
   vm.Characters.Single(c => c.Name == "Agent").Password = "SyntheticAccountPassword"; await vm.SaveAccountsCommand.ExecuteAsync(null);
   Check(CredentialVault.Load("Agent") == "SyntheticAccountPassword", "Saved login uses isolated Windows-encrypted credentials");
   var credentialFiles = Directory.GetFiles(Path.Combine(AppSettings.DataPath, "logins"));
   Check(credentialFiles.All(p => !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(p)).Contains("SyntheticAccountPassword")), "Saved password files contain no plaintext credentials");
   await vm.SaveAccountsCommand.ExecuteAsync(null); Check(CredentialVault.Load("Agent") == "SyntheticAccountPassword", "Blank account password preserves the existing credential");
   CredentialVault.Save("Elara", "SyntheticRenamePassword"); vm.Characters.Single(c => c.Name == "Elara").Name = "ElaraPrime"; await vm.SaveAccountsCommand.ExecuteAsync(null);
   Check(CredentialVault.Load("ElaraPrime") == "SyntheticRenamePassword", "Renaming a saved character retains its encrypted password");
   vm.Characters.Single(c => c.Name == "ElaraPrime").Name = "Elara"; await vm.SaveAccountsCommand.ExecuteAsync(null);
   var duplicate = vm.Characters.Single(c => c.Name == "Wren"); duplicate.Name = "agent"; await vm.SaveAccountsCommand.ExecuteAsync(null); await Settle();
   Check(vm.HasError && AppSettings.Load().Characters.SequenceEqual(new[] { "Agent", "Elara", "Wren" }), "Case-insensitive duplicate accounts block the batch before saving"); await Capture("characters-validation"); duplicate.Name = "Wren"; vm.DismissErrorCommand.Execute(null);
   for (int i = 0; i < 17; i++) { vm.AddCharacterCommand.Execute(null); vm.SelectedCharacter!.Name = "Sample" + (char)('a' + i); }
   await vm.SaveAccountsCommand.ExecuteAsync(null); Check(vm.Characters.Count == 20 && AppSettings.Load().Characters.Length == 20, "The account editor persists a complete twenty-character batch");
   window.Width = 1024; window.Height = 768; await Capture("characters-bulk-compact"); window.Width = 1450; window.Height = 930;
   foreach (var character in vm.Characters.ToArray()) character.RemoveCommand.Execute(null); await vm.SaveAccountsCommand.ExecuteAsync(null); vm.Refresh(); await Settle();
   Check(vm.Characters.Count == 0 && AppSettings.Load().Characters.Length == 0, "Removing all accounts preserves a genuine empty workspace"); await Capture("characters-empty-after-save");
   vm.Runtime.Settings.Characters = ["Agent", "Elara", "Wren"]; vm.Runtime.Settings.Save(); vm.DiscardAccountsCommand.Execute(null); await Select("Agent");

   var receipts = new[] { new QueueOutcome("entered-muisir-village", "Completed", now, "Entered Muisir Village recorded. Five mushrooms consumed; four Shrinking Potions received."), new QueueOutcome("mundane-birth", "Completed", now.AddMinutes(-24), "Mundane birth recorded: Winter, age 25, Undine. Riona confirmed the chosen birth details and the matching legend was observed.") };
   await FixtureQueue(new() { Character = "Agent", Quests = ["shinewood-faerie", "perfect-hairstyle", "emerald-dawn"], Home = new(3082, new(12, 18), "Rucesion Threshold"), ReturnHome = true, History = receipts });
   await Select("Agent"); await Page("Planner"); await Capture("planner");
   await Page("Characters");
   Check(window.GetVisualDescendants().OfType<Button>().Count(b => b.Name == "launchCharacter") == 3, "Each saved character has its own Launch button"); await Capture("characters");
   foreach (var character in vm.Characters) { await character.LaunchCommand.ExecuteAsync(null); Check(vm.HasError && vm.ErrorMessage.Contains("Preview cannot launch"), "Per-character Launch reaches the existing guarded launcher: " + character.Name); vm.DismissErrorCommand.Execute(null); }
   await Page("History"); Check(vm.History.Count >= 2, "Run history displays durable queue receipts"); await Capture("history");
   await Page("Settings"); await Capture("settings");
   Check(!Require<CheckBox>("automaticUpdates").IsChecked.GetValueOrDefault(), "Automatic update checks remain opt-in");
   Check(!Require<Button>("downloadUpdate").IsEffectivelyEnabled && !Require<Button>("installUpdate").IsEffectivelyEnabled, "Download and install start disabled");
   vm.AutomaticUpdateChecks = true; await vm.SaveSettingsCommand.ExecuteAsync(null); Check(AppSettings.Load().AutomaticUpdateChecks, "Optional startup update checks persist");
   await vm.Runtime.Updates.StartAsync(); Check(updateBackend.Checks == 1 && updateBackend.Downloads == 0 && updateBackend.Installs == 0, "Opted-in startup checks never download or install"); await Capture("settings-update-opt-in");
   vm.AutomaticUpdateChecks = false; await vm.SaveSettingsCommand.ExecuteAsync(null);
   updateBackend.DelayedCheck = new(TaskCreationOptions.RunContinuationsAsynchronously); var checking = vm.CheckUpdatesCommand.ExecuteAsync(null); await Settle();
   Check(vm.Runtime.Updates.Working && !Require<Button>("checkUpdates").IsEffectivelyEnabled && !Require<Button>("downloadUpdate").IsEffectivelyEnabled && !Require<Button>("installUpdate").IsEffectivelyEnabled, "Update loading state prevents duplicate or dependent actions"); states.Add("loading"); await Capture("settings-update-checking");
   updateBackend.DelayedCheck.SetResult(updateBackend.NextVersion); await checking; updateBackend.DelayedCheck = null; await Settle();
   Check(Require<Button>("downloadUpdate").IsEffectivelyEnabled && !Require<Button>("installUpdate").IsEffectivelyEnabled, "Checking exposes Download without authorizing Install"); Check(updateBackend.Checks == 2 && updateBackend.Downloads == 0, "An update check never downloads automatically"); await Capture("settings-update-available");
   await Click("downloadUpdate"); Check(!Require<Button>("downloadUpdate").IsEffectivelyEnabled && Require<Button>("installUpdate").IsEffectivelyEnabled, "Downloading requires a separate Install action"); await Capture("settings-update-ready");
   Check(!vm.Runtime.Updates.PrepareExit(false, false, false), "Downloading never authorizes install on exit");
   Check(updateBackend.Downloads == 1 && updateBackend.Installs == 0, "Explicit Download prepares exactly one package without install");
   updateBackend.Failure = new IOException("Connection unavailable"); await Click("checkUpdates"); Check(vm.UpdateStatus.Contains("Connection unavailable") && Require<Button>("checkUpdates").IsEffectivelyEnabled, "Update errors remain readable and retryable"); await Capture("settings-update-error"); updateBackend.Failure = null;
   Check(window.FindControl<TextBox>("diagnostics") is null && window.FindControl<CheckBox>("developerMode") is null, "Protocol diagnostics and Developer mode are absent from the window");

   await Page("Planner");
   await FixtureQueue(vm.Runtime.Queue("Agent") with { State = "Running", ActiveQuest = "shinewood-faerie", ActiveStartedAt = now, Reason = "Travelling to Shinewood" }); vm.Runtime.SetPreviewQueueRunning("Agent", true); vm.Refresh(); await Settle();
   states.Add("running"); Check(vm.Runtime.Queue("Agent").Locked, "Running queues lock edits"); Check(vm.PauseQuestCommand.CanExecute(null), "Running ownership enables the explicit Pause action"); await Capture("planner-running");
   await vm.PauseQuestCommand.ExecuteAsync(null); await Settle(); Check(vm.Runtime.Queue("Agent").State == "Paused" && !vm.Runtime.QueueRunning("Agent"), "Pause saves the checkpoint and releases active work");
   await FixtureQueue(vm.Runtime.Queue("Agent") with { State = "Paused", Reason = "Manual movement paused the journey. Resume checks the last action before continuing.", ActiveResume = true });
   states.Add("paused"); Check(vm.Runtime.Queue("Agent").Locked, "Paused checkpoints keep current quest ownership"); Check(vm.QueuePrimaryLabel == "Resume run", "A paused queue exposes Resume"); await Capture("planner-paused");
   await vm.StopQueueCommand.ExecuteAsync(null); await Settle(); Check(vm.Runtime.Queue("Agent").State == "Idle" && !vm.QueueLocked, "Stop saves progress and releases queue edits");
   await FixtureQueue(vm.Runtime.Queue("Agent") with { State = "Idle", ActiveQuest = null, ActiveStartedAt = null, ActiveResume = false, Reason = "Ready when connected" });
   vm.ErrorMessage = "The queue could not be saved. Your last saved progress is kept. Check the folder and try again."; await Settle(); states.Add("error"); await Capture("planner-error"); vm.ErrorMessage = "";

   // Choice and safety dialogs retain explicit choices and the Cancel default.
   var birth = new QuestChoiceDialog("Agent", vm.Runtime.Recipes["mundane-birth"], new() { Character = "Agent", Quest = "mundane-birth" }, null);
   var birthResult = birth.ShowDialog<QuestChoiceResult?>(window); await Settle();
   var season = Modal<ComboBox>(birth, "birthSeason"); var town = Modal<ComboBox>(birth, "birthTown"); var age = Modal<NumericUpDown>(birth, "birthAge"); var confirm = Modal<Button>(birth, "choiceConfirm");
   Check(season.SelectedIndex == -1 && town.SelectedIndex == -1 && season.Items.Count == 4 && town.Items.Count == 6 && !confirm.IsEffectivelyEnabled && age.Minimum == 12 && age.Maximum == 40, "Birth requires explicit season and town with age 12–40"); await Capture("birth-choices-empty", target: birth);
   await CapturePopup(season, "birth-season-menu"); await CapturePopup(town, "birth-town-menu");
   season.SelectedItem = "Fall"; town.SelectedItem = "Piet"; age.Value = 25.5m; await Settle(); Check(!confirm.IsEffectivelyEnabled, "Fractional birth ages cannot be confirmed"); age.Value = 25; await Settle(); Check(confirm.IsEffectivelyEnabled, "Valid explicit birth choices enable confirmation"); await Capture("birth-choices", target: birth); confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check((await birthResult)?.BirthOptions == new MundaneBirthOptions("Fall", 25, "Piet"), "Birth dialog returns exactly the chosen settings");
   foreach (int outcomeIndex in Enumerable.Range(0, EmeraldDawnOptions.Outcomes.Length))
   {
    var emerald = new QuestChoiceDialog("Agent", vm.Runtime.Recipes["emerald-dawn"], new() { Character = "Agent", Quest = "emerald-dawn" }, null);
    var emeraldResult = emerald.ShowDialog<QuestChoiceResult?>(window); await Settle();
    var outcome = Modal<ComboBox>(emerald, "emeraldOutcome"); var druid = Modal<CheckBox>(emerald, "emeraldVisitDruid"); confirm = Modal<Button>(emerald, "choiceConfirm");
    Check(outcome.SelectedIndex == -1 && !confirm.IsEffectivelyEnabled && druid.IsChecked == false, "Emerald outcome is explicit and Gans visit defaults off");
    outcome.SelectedIndex = outcomeIndex; druid.IsChecked = true; await Settle(); Check(confirm.IsEffectivelyEnabled, "Explicit Emerald outcome enables confirmation"); await Capture("emerald-choices-" + EmeraldDawnOptions.Outcomes[outcomeIndex], target: emerald); confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check((await emeraldResult)?.EmeraldOptions == new EmeraldDawnOptions(EmeraldDawnOptions.Outcomes[outcomeIndex], true), "Emerald dialog returns the exact outcome and optional visit");
    var saved = new QuestChoiceDialog("Agent", vm.Runtime.Recipes["emerald-dawn"], new() { Character = "Agent", Quest = "emerald-dawn", EmeraldOptions = new(EmeraldDawnOptions.Outcomes[outcomeIndex], true), Pending = new("insignia-submit", 91, 12, 0, 0, 0, now) }, null);
    var savedResult = saved.ShowDialog<QuestChoiceResult?>(window); await Settle();
    Check(Modal<ComboBox>(saved, "emeraldOutcome").SelectedIndex == outcomeIndex && Modal<CheckBox>(saved, "emeraldVisitDruid").IsChecked == true, "Saved Emerald outcome and Gans preference restore exactly");
    Check(!Modal<ComboBox>(saved, "emeraldOutcome").IsEffectivelyEnabled && !Modal<CheckBox>(saved, "emeraldVisitDruid").IsEffectivelyEnabled, "Pending Emerald submissions preserve locked choices"); await Capture("emerald-choices-saved-" + EmeraldDawnOptions.Outcomes[outcomeIndex], target: saved); Modal<Button>(saved, "choiceCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await savedResult;
   }
   foreach (string? routeError in new string?[] { null, "A verified return route is unavailable" })
   {
    var warning = new QuestChoiceDialog("Agent", vm.Runtime.Recipes["shinewood-faerie"], new() { Character = "Agent", Quest = "shinewood-faerie" }, routeError);
    var warningResult = warning.ShowDialog<QuestChoiceResult?>(window); await Settle(); var cancel = Modal<Button>(warning, "faerieCancel"); var proceed = Modal<Button>(warning, "faerieContinue");
    Check(cancel.IsDefault && cancel.IsCancel && cancel.IsFocused, "Faerie safety warning defaults to focused Cancel");
    Check(proceed.IsEffectivelyEnabled == (routeError is null), "Faerie departure requires a verified return route"); await Capture(routeError is null ? "faerie-safety-warning" : "faerie-return-blocked", target: warning);
    if (Program.IsHeadless) { warning.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None); } else cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(await warningResult is null, "Default Cancel rejects unsafe walking departure");
   }
   string sampleClients = Path.Combine(AppSettings.DataPath, "sample-clients"); Directory.CreateDirectory(sampleClients);
   var picker = new ClientPickerDialog(sampleClients); var pickerResult = picker.ShowDialog<string?>(window); await Settle(); await picker.LoadDirectoryAsync(sampleClients); await Settle();
   Check(!Modal<Button>(picker, "clientChoose").IsEffectivelyEnabled, "An empty client folder cannot choose an application"); Modal<TextBox>(picker, "clientFolderPath").Text = "Sample clients"; await Capture("client-picker-empty", target: picker);
   await picker.LoadDirectoryAsync(Path.Combine(sampleClients, "missing-folder")); await Settle(); Check(Modal<TextBlock>(picker, "clientPickerStatus").Classes.Contains("error"), "Client folder errors remain readable and retryable"); Modal<TextBox>(picker, "clientFolderPath").Text = "Unavailable sample folder"; await Capture("client-picker-error", target: picker);
   string syntheticClient = Path.Combine(sampleClients, "SyntheticClient.exe"); File.WriteAllBytes(syntheticClient, [0, 1, 2]); await picker.LoadDirectoryAsync(sampleClients, syntheticClient); await Settle();
   Check(Modal<Button>(picker, "clientChoose").IsEffectivelyEnabled, "A selected existing application enables the explicit client choice"); Modal<TextBox>(picker, "clientFolderPath").Text = "Sample clients"; await Capture("client-picker-selected", target: picker); Modal<Button>(picker, "clientChoose").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(await pickerResult == syntheticClient && !vm.Runtime.LaunchedClientRunning, "Client selection returns the exact path without launching the application");

   foreach (var page in new[] { "Planner", "Characters", "History", "Settings" })
   {
    window.Width = 1024; window.Height = 768; await Page(page); await Capture(page.ToLowerInvariant() + "-compact");
    window.Width = 900; window.Height = 600; await Settle(); await Capture(page.ToLowerInvariant() + "-small");
    window.Height = 560; await Settle(); await Capture(page.ToLowerInvariant() + "-minimum");
   }
   window.Width = 1450; window.Height = 930;
   foreach (var page in new[] { "Planner", "Characters", "History", "Settings" })
   {
    await Page(page); foreach (double scale in new[] { 1.25, 1.5, 2.0 }) await Capture(page.ToLowerInvariant() + "-scale-" + (int)(scale * 100), scale);
   }
   Check(!vm.Runtime.LaunchedClientRunning, "Synthetic verification launched no game client");
   var manifest = new { framework = "Avalonia", renderer = Program.IsHeadless ? "headless-skia" : "native", isolated = true, synthetic = true, failed = 0, checks, states = states.Order().ToArray(), renders, viewports };
   File.WriteAllText(evidence, JsonSerializer.Serialize(manifest, AppSettings.Json));
   File.WriteAllText(result, $"Avalonia: {checks.Count} behavioral checks and {renders.Count} actual rendered screenshots passed. All four screens, empty, disconnected, running, paused, completed, blocked and error states verified using isolated synthetic data. No game connection, client launch or update install.");
   // Restore the four documentation captures to populated views after the state matrix.
   await vm.Runtime.ShutdownAsync();
   if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(0);
  }
  catch (Exception error)
  {
   File.WriteAllText(result, error.ToString()); File.WriteAllText(evidence, JsonSerializer.Serialize(new { framework = "Avalonia", isolated = true, synthetic = true, failed = 1, checks, states, renders, viewports, error = error.ToString() }, AppSettings.Json));
   try { await vm.Runtime.ShutdownAsync(); } catch { }
   if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(1);
  }
 }

 static async Task AwaitNativeLayout(Window window)
 {
  var timeout = Stopwatch.StartNew(); Size previous = default; int stable = 0;
  while (timeout.Elapsed < TimeSpan.FromSeconds(2))
  {
   Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
   var size = window.ClientSize;
   bool valid = size.Width > 0 && size.Height > 0 && window.IsMeasureValid && window.IsArrangeValid && window.Bounds.Size == size;
   if (window.Content is Control content)
   {
    var origin = content.TranslatePoint(default, window);
    valid &= content.IsMeasureValid && content.IsArrangeValid && origin is { } point && point.X >= -1 && point.Y >= -1 && point.X + content.Bounds.Width <= size.Width + 1 && point.Y + content.Bounds.Height <= size.Height + 1;
   }
   stable = valid && size == previous ? stable + 1 : 0;
   if (stable >= 3) return;
   previous = size; await Task.Delay(20);
  }
  throw new InvalidOperationException($"Native layout did not settle within its accepted viewport; client={window.ClientSize}, window={window.Bounds}, content={(window.Content as Control)?.Bounds}, requested={window.Width}x{window.Height}, scaling={window.RenderScaling}");
 }

 static void CheckVisibleActions(Window window)
 {
  foreach (var action in window.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && (b.Name is null || !b.Name.StartsWith("PART_", StringComparison.Ordinal))))
  {
   if (action.Bounds.Width <= 0 || action.Bounds.Height <= 0) throw new InvalidOperationException("An action has no visible layout: " + (action.Name ?? action.Content));
   if (action.GetVisualAncestors().OfType<ScrollViewer>().Any()) continue;
   var origin = action.TranslatePoint(default, window); if (origin is null) continue;
   var bounds = new Rect(origin.Value, action.Bounds.Size);
   if (bounds.X < -1 || bounds.Y < -1 || bounds.Right > window.ClientSize.Width + 1 || bounds.Bottom > window.ClientSize.Height + 1) throw new InvalidOperationException($"Action clipped at current window size: {action.Name ?? action.Content}; action={bounds}, client={window.ClientSize}, window={window.Bounds}, requested={window.Width}x{window.Height}, scaling={window.RenderScaling}");
  }
 }

 static void CheckDarkSurface(Bitmap bitmap, string name)
 {
  int width = bitmap.PixelSize.Width, height = bitmap.PixelSize.Height, stride = width * 4, total = stride * height;
  var pointer = Marshal.AllocHGlobal(total);
  try
  {
   bitmap.CopyPixels(new PixelRect(0, 0, width, height), pointer, total, stride);
   var bytes = new byte[total]; Marshal.Copy(pointer, bytes, 0, total);
   int bright = 0;
   for (int y = 0; y < height; y += 4) for (int x = 0; x < width; x += 4) { int i = y * stride + x * 4; if (bytes[i] > 230 && bytes[i + 1] > 230 && bytes[i + 2] > 230 && bytes[i + 3] > 200) bright++; }
   if (bright > (width / 4d) * (height / 4d) * .08) throw new InvalidOperationException("A solid light surface appeared in the fully dark interface: " + name);
  }
  finally { Marshal.FreeHGlobal(pointer); }
 }

 [DllImport("dwmapi.dll")]
 static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
