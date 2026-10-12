using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
 public DesktopRuntime Runtime { get; }
 readonly List<QuestItemVM> library = [];
 readonly List<AsyncCommand> commands = [];
 readonly HashSet<string> removedAccounts = new(StringComparer.OrdinalIgnoreCase);
 bool refreshing;
 string page = "Planner", search = "", filter = "All", errorMessage = "", noticeMessage = "", clientPath, accountStatus = "";
 bool automaticUpdateChecks;
 CharacterItemVM? selectedCharacter;
 QuestItemVM? selectedQuest;
 public ObservableCollection<CharacterItemVM> Characters { get; } = [];
 public ObservableCollection<QuestItemVM> Quests { get; } = [];
 public ObservableCollection<QueueItemVM> QueueItems { get; } = [];
 public ObservableCollection<HistoryItemVM> History { get; } = [];
 public CharacterItemVM? SelectedCharacter { get => selectedCharacter; set { if (Set(ref selectedCharacter, value)) Refresh(); } }
 public QuestItemVM? SelectedQuest { get => selectedQuest; set { if (Set(ref selectedQuest, value)) { if (value?.RecipeId is { Length: > 0 } id) Runtime.SelectedQuestId = id; Refresh(); } } }
 public QueueItemVM? SelectedQueueItem { get; set; }
 public string CharacterName => SelectedCharacter?.OriginalName ?? "";
 public string Page { get => page; set { if (Set(ref page, value)) { RaiseAll(); Refresh(); } } }
 public bool IsPlanner => Page == "Planner";
 public bool IsCharacters => Page == "Characters";
 public bool IsHistory => Page == "History";
 public bool IsSettings => Page == "Settings";
 public string PageTitle => Page switch { "History" => "Run history", _ => Page == "Planner" ? "Quest planner" : Page };
 public string PageSubtitle => Page switch { "Characters" => "Every character. Their own journey.", "History" => "Verified outcomes and saved attempts.", "Settings" => "A companion that works your way.", _ => "Your quests. Your order. Your next adventure." };
 public string Search { get => search; set { if (Set(ref search, value)) Refresh(); } }
 public string Filter { get => filter; set { if (Set(ref filter, value)) Refresh(); } }
 public bool IsAllFilter => Filter == "All";
 public bool IsReadyFilter => Filter == "Ready";
 public bool IsCompletedFilter => Filter == "Completed";
 public bool IsUnavailableFilter => Filter == "Unavailable";
 public int AllCount => library.Count;
 public int ReadyCount => library.Count(q => q.IsReady);
 public int CompletedCount => library.Count(q => q.IsCompleted);
 public int UnavailableCount => library.Count(q => q.IsMuted);
 public string LibrarySummary => $"{Quests.Count} of {AllCount} quests · {Runtime.Recipes.Count} automatic journeys";
 public bool EmptyLibrary => Quests.Count == 0;
 public bool CharactersEmpty => Characters.Count == 0;
 public bool HistoryEmpty => History.Count == 0;
 public string SelectedCharacterInfo => SelectedCharacter?.LevelText ?? "Choose your character";
 public string? LaunchBlocker => Runtime.Settings.Characters.Length == 0 ? Runtime.LaunchBlocker("") : SelectedCharacter?.Saved == true ? Runtime.LaunchBlocker(CharacterName) : "Save this character’s edits before launching.";
 public string LaunchTooltip => LaunchBlocker ?? Runtime.LaunchHint(Runtime.Settings.Characters.Length == 0 ? "" : CharacterName) ?? (Runtime.Settings.Characters.Length == 0 ? "Open Dark Ages without entering a login." : "Launch Dark Ages");
 public string ConnectionStatus => CharacterName.Length == 0 ? "No character selected" : Runtime.Snapshot(CharacterName).Connected ? "Connected · fresh state" : Runtime.Preview ? "Offline · sample workspace" : Runtime.ConnectionError is not null ? "Connection unavailable · check Settings" : "Offline · connect to begin";
 public string QueueHeading => CharacterName.Length == 0 ? "Your queue" : CharacterName + "’s queue";
 public string QueueHint => QueueLocked ? "Stop this run to edit its order." : "Drag to reorder · Ctrl + ↑ / ↓";
 public string QueueReadiness => Runtime.QueueRunning(CharacterName) ? "Running for " + CharacterName : QueueState.State == "Paused" ? DesktopRuntime.PlayerReason(QueueState.Reason) : Runtime.QueueStartReason(CharacterName, QueueState) is { Length: > 0 } reason ? reason : QueueState.Reason is "Queue completed" or "Home arrival verified" ? "Journey complete · progress saved" : "All selected quests are ready";
 public string QueuePrimaryLabel => Runtime.QueueRunning(CharacterName) ? "Pause run" : QueueState.State == "Paused" || QueueState.HomePending ? "Resume run" : $"Start queue · {QueueItems.Count}";
 public bool QueueLocked => Runtime.QueueReserves(CharacterName);
 public bool QueueEmpty => QueueItems.Count == 0;
 QuestQueueState QueueState => Runtime.Queue(CharacterName);
 public bool ReturnHome { get => QueueState.ReturnHome; set { if (!refreshing && value != ReturnHome) { string character = CharacterName; _ = RunAsync(() => Runtime.EditQueueAsync(character, s => s with { ReturnHome = value })); } } }
 public bool CanReturnHome => !QueueLocked && QueueState.Home is not null;
 public bool FastMode { get => Runtime.Settings.FastModeEnabled(CharacterName); set { if (!refreshing && CharacterName.Length > 0 && value != FastMode) try { Runtime.SetFastMode(CharacterName, value); } catch (Exception e) { ReportError(e); } } }
 public string FastModeLabel => "Fast mode · " + (FastMode ? Runtime.FastModeDescription(CharacterName) : "slows near players");
 string JourneyQuestId => QueueState.Locked && QueueState.ActiveQuest is { } active ? active : Runtime.RunningQuestId(CharacterName) ?? SelectedQuest?.RecipeId ?? "";
 QuestDefinition? JourneyQuest => Runtime.Recipes.GetValueOrDefault(JourneyQuestId);
 QuestJob JourneyJob => JourneyQuest is { } q && CharacterName.Length > 0 ? Runtime.Job(CharacterName, q.Id) : new() { Character = CharacterName };
 public bool RepeatVisible => JourneyQuest?.Repeatable == true;
 public bool RepeatEnabled => RepeatVisible && JourneyQuestId != "perfect-hairstyle" && !QueueLocked && !Runtime.IsRunning(CharacterName) && Runtime.RunningQuestId(CharacterName) is null && !Runtime.RunnerBusy(CharacterName) && !Runtime.ControlActions.Contains(CharacterName, StringComparer.OrdinalIgnoreCase) && !QuestRunPolicy.Finished(JourneyQuest!, JourneyJob);
 public bool Repeat { get => RepeatVisible && !QuestRunPolicy.Finished(JourneyQuest!, JourneyJob) && (JourneyQuestId == "perfect-hairstyle" || Runtime.RepeatEnabled(CharacterName, JourneyQuestId)); set { if (!refreshing && RepeatEnabled && value != Repeat) { string character = CharacterName, id = JourneyQuestId; _ = RunAsync(() => Runtime.SetRepeatAsync(character, id, value)); } } }
 public string RepeatLabel => JourneyQuestId == "perfect-hairstyle" ? "Weekly until Suomi legend" : "Repeat every 8 hours";
 public string StartQuestLabel => QuestRunPolicy.AttemptCompleted(JourneyJob) ? JourneyQuest is { } q && QuestRunPolicy.NextAttempt(q, JourneyJob) > DateTimeOffset.UtcNow && Repeat ? "Schedule" : "Run again" : "Start quest";
 public string JourneyTitle => QueueState.HomePending ? "Return home" : JourneyQuest?.Name ?? SelectedQuest?.Name ?? "Your next journey";
 public string JourneyState
 {
  get
  {
   var j = JourneyJob; var s = Runtime.Snapshot(CharacterName);
   if (QueueState.HomePending) return QueueState.State == "Paused" ? "Paused · home arrival unconfirmed" : "Returning home";
   if (j.Pending is not null) return "Awaiting confirmation";
   if (JourneyQuestId == ShinewoodFaerieEvidence.Id && j.FaeriePetWait is not null) return "Pet wait · one deoch";
   if (JourneyQuest is { } q && (QuestRunPolicy.Finished(q, j) || Runtime.Memory(CharacterName).CompletedLegend(q.Id, s) is not null)) return "Completed";
   if (Runtime.RunningQuestId(CharacterName) is not null || Runtime.IsRunning(CharacterName) || Runtime.QueueRunning(CharacterName)) return j.Status switch { "Calibrating" => "Calibration in progress", "Scheduled" => "Next attempt scheduled", _ => "Quest in progress" };
   if (j.CompletedAt is not null) return "Attempt recorded";
   if (QueueState.State == "Paused" || j.Status == "Paused" && j.Reason != "Ready") return "Paused · progress saved";
   if (!s.Connected) return "Offline";
   return SelectedQuest?.Status ?? "Choose a quest";
  }
 }
 public string JourneyMessage
 {
  get
  {
   var j = JourneyJob; if (CharacterName.Length == 0) return "Add your character in Characters. Their queue and progress stay with them.";
   if (QueueState.HomePending) return "Resume verifies arrival at " + (QueueState.Home?.Label ?? "your saved home") + " before finishing.";
   if (j.Pending is not null) return "Your last action is awaiting confirmation. Resume checks its result before continuing.";
   if (j.CompletionText is { Length: > 0 } text) return DesktopRuntime.PlayerReason(text);
   if (JourneyQuest is { } q && Runtime.Memory(CharacterName).CompletedLegend(q.Id, Runtime.Snapshot(CharacterName)) is { } legend) return "Legend already recorded: " + legend;
   if (j.Reason is { Length: > 0 } reason && reason != "Ready") return DesktopRuntime.PlayerReason(reason);
   return SelectedQuest?.Reason ?? "Select quests from the library to plan your next run.";
  }
 }
 public double JourneyProgress
 {
  get
  {
   var q = JourneyQuest; if (q is null || q.Steps.Length == 0) return 0;
   var j = JourneyJob; if (j.Pending is null && j.Step == "complete") return 100;
   int index = Array.FindIndex(q.Steps, s => s.Id == j.Step); return Math.Max(0, index) * 100d / q.Steps.Length;
  }
 }
 public string JourneyConversation => Runtime.CurrentDialog(CharacterName) is { } d ? d.Name + "\n" + d.Text + (d.Choices.Length == 0 ? "" : "\n" + string.Join("\n", d.Choices.Select(c => c.Text))) : "Quest conversations appear here when you meet each character.";
 public string JourneyRewards => JourneyJob.Rewards.Count > 0 ? string.Join(" · ", JourneyJob.Rewards.Select(p => $"{p.Value:N0} {p.Key}")) : "Confirmed rewards are saved to your journal.";
 public string JourneyNext => QueueState.Quests.FirstOrDefault(id => id != QueueState.ActiveQuest) is { } next ? "Next · " + Runtime.QuestName(next) : QueueState.ReturnHome && QueueState.Home is { } home ? "Next · " + home.Label : JourneyQuest is { } q && QuestRunPolicy.NextAttempt(q, JourneyJob) is { } due ? "Next attempt · " + due.ToLocalTime().ToString("MMM d, h:mm tt") : "Progress saved for each character";
 public string ClientPath { get => clientPath; set => Set(ref clientPath, value); }
 public bool AutomaticUpdateChecks { get => automaticUpdateChecks; set => Set(ref automaticUpdateChecks, value); }
 public string VersionText => "DAQuester " + UpdateVersion.Current;
 public string UpdateStatus => Runtime.Updates.Status;
 public string? AvailableUpdate => Runtime.Updates.PendingVersion ?? Runtime.Updates.AvailableVersion;
 public bool CanCheckUpdates => Runtime.Updates.Available && !Runtime.Updates.Working;
 public bool CanDownloadUpdate => CanCheckUpdates && Runtime.Updates.AvailableVersion is not null && Runtime.Updates.PendingVersion is null;
 public bool CanInstallUpdate => CanCheckUpdates && Runtime.Updates.PendingVersion is not null && !Runtime.QuestActionsRunning && !Runtime.ConnectedGame && !Runtime.LaunchedClientRunning;
 public string AccountStatus { get => accountStatus; set => Set(ref accountStatus, value); }
 public string ErrorMessage { get => errorMessage; set { if (Set(ref errorMessage, value)) Raise(nameof(HasError)); } }
 public bool HasError => ErrorMessage.Length > 0;
 public string NoticeMessage { get => noticeMessage; set { if (Set(ref noticeMessage, value)) Raise(nameof(HasNotice)); } }
 public bool HasNotice => NoticeMessage.Length > 0;
 public bool InstallRequested { get; private set; }
 public event Action? RequestClose;
 public Func<Task<string?>>? PickClientAsync { get; set; }
 public RelayCommand NavigateCommand { get; }
 public RelayCommand FilterCommand { get; }
 public RelayCommand DismissErrorCommand { get; }
 public RelayCommand DismissNoticeCommand { get; }
 public RelayCommand AddCharacterCommand { get; }
 public RelayCommand DiscardAccountsCommand { get; }
 public AsyncCommand LaunchSelectedCommand { get; }
 public AsyncCommand StartQueueCommand { get; }
 public AsyncCommand StopQueueCommand { get; }
 public AsyncCommand ClearQueueCommand { get; }
 public AsyncCommand StopAllCommand { get; }
 public AsyncCommand StartQuestCommand { get; }
 public AsyncCommand ResumeQuestCommand { get; }
 public AsyncCommand PauseQuestCommand { get; }
 public AsyncCommand StopQuestCommand { get; }
 public AsyncCommand SaveAccountsCommand { get; }
 public AsyncCommand SaveSettingsCommand { get; }
 public AsyncCommand CheckUpdatesCommand { get; }
 public AsyncCommand DownloadUpdateCommand { get; }
 public AsyncCommand InstallUpdateCommand { get; }
 public AsyncCommand BrowseClientCommand { get; }
 public AsyncCommand ChangeChoicesCommand { get; }
 public MainWindowViewModel(DesktopRuntime runtime)
 {
  Runtime = runtime; clientPath = runtime.Settings.ClientPath; automaticUpdateChecks = runtime.Settings.AutomaticUpdateChecks;
  NavigateCommand = new(p => Page = p as string ?? "Planner"); FilterCommand = new(p => Filter = p as string ?? "All"); DismissErrorCommand = new(_ => ErrorMessage = ""); DismissNoticeCommand = new(_ => NoticeMessage = "");
  AddCharacterCommand = new(_ => { var item = new CharacterItemVM(this, ""); Characters.Add(item); SelectedCharacter = item; AccountEdited(); Raise(nameof(CharactersEmpty)); });
  DiscardAccountsCommand = new(_ => { removedAccounts.Clear(); Characters.Clear(); selectedCharacter = null; LoadCharacters(); Refresh(); AccountStatus = "Edits discarded."; });
  LaunchSelectedCommand = Command(_ => runtime.Settings.Characters.Length == 0
   ? runtime.LaunchAsync("", manualOnly: true)
   : runtime.LaunchAsync(CharacterName), _ => LaunchBlocker is null);
  StartQueueCommand = Command(_ => runtime.StartQueueAsync(CharacterName), _ => runtime.QueueRunning(CharacterName) || !runtime.QueueBusy(CharacterName) && runtime.QueueStartReason(CharacterName, QueueState).Length == 0, allowConcurrent: true);
  StopQueueCommand = Command(_ => runtime.PauseQueueAsync(CharacterName, true), _ => QueueLocked);
  ClearQueueCommand = Command(_ => runtime.EditQueueAsync(CharacterName, s => s with { Quests = [], ActiveQuest = null }), _ => !QueueLocked && !QueueEmpty);
  StopAllCommand = Command(_ => runtime.StopOwnersAsync());
  StartQuestCommand = Command(_ => runtime.StartQuestAsync(CharacterName, JourneyQuestId), _ => CanStartQuest(false));
  ResumeQuestCommand = Command(_ => runtime.StartQuestAsync(CharacterName, JourneyQuestId, true), _ => CanStartQuest(true));
  PauseQuestCommand = Command(_ => runtime.PauseQuestAsync(CharacterName, "Paused by player"), _ => CanPauseQuest);
  StopQuestCommand = Command(_ => runtime.PauseQuestAsync(CharacterName, "Stopped; progress saved"), _ => CanPauseQuest);
  ChangeChoicesCommand = Command(_ => runtime.ChangeQuestChoicesAsync(CharacterName, JourneyQuestId), _ => CanChangeChoices);
  SaveAccountsCommand = Command(_ => { SaveAccounts(); return Task.CompletedTask; });
  SaveSettingsCommand = Command(_ => { SaveSettings(); return Task.CompletedTask; });
  CheckUpdatesCommand = Command(_ => runtime.Updates.CheckAsync(), _ => CanCheckUpdates);
  DownloadUpdateCommand = Command(_ => runtime.Updates.DownloadAsync(), _ => CanDownloadUpdate);
  InstallUpdateCommand = Command(_ => { runtime.RequestInstall(); InstallRequested = true; RequestClose?.Invoke(); return Task.CompletedTask; }, _ => CanInstallUpdate);
  BrowseClientCommand = Command(async _ => { if (PickClientAsync is not null && await PickClientAsync() is { } path) ClientPath = path; });
  LoadCharacters(); LoadLibrary(); SelectedCharacter = Characters.FirstOrDefault(); SelectedQuest = library.FirstOrDefault();
  Runtime.Changed += Refresh; Refresh();
 }
 internal AsyncCommand Command(Func<object?, Task> action, Func<object?, bool>? can = null, bool allowConcurrent = false)
 {
  var command = new AsyncCommand(async p => { await action(p); Refresh(); }, can, ReportError, allowConcurrent); commands.Add(command); return command;
 }
 public async Task RunAsync(Func<Task> action) { try { await action(); } catch (Exception e) { ReportError(e); } finally { Refresh(); } }
 public void ReportError(Exception error) { ErrorMessage = DesktopRuntime.PlayerReason(error.Message); Runtime.Log(error.Message); Refresh(); }
 public bool ChangeChoicesVisible => DesktopRuntime.HasChoices(JourneyQuestId);
 bool CanChangeChoices => ChangeChoicesVisible && CharacterName.Length > 0 && Runtime.Snapshot(CharacterName).Connected && !Runtime.QueueReserves(CharacterName) && !Runtime.IsRunning(CharacterName) && !Runtime.RunnerBusy(CharacterName) && !Runtime.ControlActions.Contains(CharacterName, StringComparer.OrdinalIgnoreCase) && !Runtime.ChoicesLocked(CharacterName, JourneyQuestId);
 bool CanPauseQuest => Runtime.RunningQuestId(CharacterName) is not null || Runtime.IsRunning(CharacterName) || Runtime.RunnerBusy(CharacterName) || Runtime.ControlActions.Contains(CharacterName, StringComparer.OrdinalIgnoreCase) || QueueLocked;
 bool CanStartQuest(bool resume)
 {
  var q = JourneyQuest; if (q is null || CharacterName.Length == 0 || !Runtime.Snapshot(CharacterName).Connected || Runtime.QueueReserves(CharacterName) || Runtime.IsRunning(CharacterName) || Runtime.RunnerBusy(CharacterName) || Runtime.ControlActions.Contains(CharacterName, StringComparer.OrdinalIgnoreCase)) return false;
  var j = JourneyJob; if (QuestRunPolicy.Finished(q, j) && j.Pending is null) return false;
  if (resume) return (j.Pending is not null || j.Step != "preflight" || j.Reason != "Ready" || j.NextRun is not null) && (!QuestRunPolicy.AttemptCompleted(j) || Repeat || j.Pending is not null);
  var a = Runtime.Availability(CharacterName, q.Id); return j.Pending is null && (a.Ready || Repeat && QuestRunPolicy.NextAttempt(q, j) > DateTimeOffset.UtcNow);
 }
 void LoadCharacters() { foreach (string name in Runtime.Characters) if (!removedAccounts.Contains(name) && !Characters.Any(c => c.OriginalName.Equals(name, StringComparison.OrdinalIgnoreCase))) Characters.Add(new(this, name)); }
 void LoadLibrary()
 {
  using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.Content, "catalog.json")));
  foreach (var q in doc.RootElement.GetProperty("entries").EnumerateArray())
  {
   string Get(string key) => q.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
   string name = Get("name"); var recipe = Runtime.Recipes.Values.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
   var (description, region) = Details(recipe?.Id);
   library.Add(new(this, Get("id"), recipe?.Id ?? "", name, Get("rewards"), Get("requiredItems"), Get("level"), Get("source"), description, region));
  }
  library.Sort((a, b) => { int automatic = (b.RecipeId.Length > 0).CompareTo(a.RecipeId.Length > 0); return automatic != 0 ? automatic : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture); });
 }
 static (string, string) Details(string? id) => id switch
 {
  "spare-a-stick" => ("Gather six branches for Callough. Each ordinary attempt verifies its rewards; a permanent outcome ends repeats.", "MILETH · CALLOUGH"),
  "mundane-birth" => ("Choose your season, age and hometown. Riona records a lasting birth legend.", "MILETH · RIONA"),
  "perfect-hairstyle" => ("Visit Eeva for a ponytail. Weekly attempts continue until your own Suomi legend is awarded.", "SUOMI · EEVA"),
  "entered-muisir-village" => ("Bring Kiril five Undine Mushrooms. Verify your Muisir legend and four Shrinking Potions.", "UNDINE · KIRIL"),
  "emerald-dawn" => ("Recover an Emerald Knight Insignia, hear Berg's story, then give it to the Knights, the Necromancers, or sell it to Skan.", "SUOMI · SHINEWOOD"),
  "shinewood-faerie" => ("Try for a Pet Faerie and learn eligible missing spells. Prepare a safe return before entering the forest.", "SHINEWOOD · FAERIE"),
  _ => ("This quest is a guide for manual play. Open its source for the full journey.", "QUEST GUIDE")
 };
 public void Refresh()
 {
  if (refreshing) return; refreshing = true;
  try
  {
   LoadCharacters(); if (selectedCharacter is null) selectedCharacter = Characters.FirstOrDefault();
   var queue = QueueState; foreach (var character in Characters) character.Refresh(); foreach (var quest in library) quest.Refresh(CharacterName, queue);
   var visible = library.Where(q => (Search.Length == 0 || string.Join(' ', q.Name, q.Rewards, q.Requirements, q.Level, q.Reason).Contains(Search, StringComparison.OrdinalIgnoreCase)) && (Filter switch { "Ready" => q.IsReady, "Completed" => q.IsCompleted, "Unavailable" => q.IsMuted, _ => true })).ToArray();
   Sync(Quests, visible);
   var rows = queue.Quests.Select((id, index) => { var row = QueueItems.FirstOrDefault(r => r.Character.Equals(CharacterName, StringComparison.OrdinalIgnoreCase) && r.Id == id) ?? new QueueItemVM(this, CharacterName, id); row.Refresh(index + 1, queue); return row; }).ToArray();
   Sync(QueueItems, rows); RefreshHistory(); RaiseAll(); foreach (var command in commands) command.Notify();
  }
  finally { refreshing = false; }
 }
 static void Sync<T>(ObservableCollection<T> collection, IReadOnlyList<T> desired)
 {
  for (int i = collection.Count - 1; i >= 0; i--) if (!desired.Contains(collection[i])) collection.RemoveAt(i);
  for (int i = 0; i < desired.Count; i++) { if (i < collection.Count && EqualityComparer<T>.Default.Equals(collection[i], desired[i])) continue; int old = collection.IndexOf(desired[i]); if (old >= 0) collection.Move(old, i); else collection.Insert(i, desired[i]); }
 }
 void RefreshHistory()
 {
  var rows = new List<HistoryItemVM>(); foreach (string character in Runtime.Characters)
  {
   var queue = Runtime.Queue(character); rows.AddRange(queue.History.Select(h => new HistoryItemVM(character, Runtime.QuestName(h.Quest), h.Outcome switch { "Completed" => "Completed", "AttemptCompleted" => "Attempt recorded", "PermanentlyCompleted" => "Completed", _ => h.Outcome }, h.At, DesktopRuntime.PlayerReason(h.Summary))));
   foreach (var recipe in Runtime.Recipes.Values)
   {
    var job = Runtime.Job(character, recipe.Id);
    foreach (var attempt in job.HairstyleAttempts) if (!queue.History.Any(h => h.Quest == recipe.Id && h.At == attempt.CompletedAt)) rows.Add(new(character, recipe.Name, attempt.LegendReward ? "Completed" : "Attempt recorded", attempt.CompletedAt, string.Join(" · ", attempt.Rewards.Select(r => $"{r.Value:N0} {r.Key}"))));
    if (job.CompletedAt is { } at && !queue.History.Any(h => h.Quest == recipe.Id && h.At == at)) rows.Add(new(character, recipe.Name, QuestRunPolicy.Finished(recipe, job) ? "Completed" : "Attempt recorded", at, DesktopRuntime.PlayerReason(job.CompletionText ?? "Verified outcome saved")));
   }
  }
  Sync(History, rows.DistinctBy(r => (r.Character, r.Quest, r.At)).OrderByDescending(r => r.At).ToArray());
 }
 public async Task ToggleQuestAsync(QuestItemVM quest)
 {
  string character = CharacterName, id = quest.RecipeId; bool removing = quest.IsQueued;
  if (!quest.CanQueue) { quest.Raise(nameof(QuestItemVM.IsQueued)); return; }
  await RunAsync(() => Runtime.EditQueueAsync(character, s => s with { Quests = removing ? s.Quests.Where(q => q != id).ToArray() : s.Quests.Append(id).ToArray(), ActiveQuest = removing && s.ActiveQuest == id ? null : s.ActiveQuest }));
 }
 public async Task MoveQueueAsync(string character, string id, int target)
 {
  if (!character.Equals(CharacterName, StringComparison.OrdinalIgnoreCase) || QueueLocked) return;
  await RunAsync(() => Runtime.MoveQueueAsync(character, id, target)); SelectedQueueItem = QueueItems.FirstOrDefault(r => r.Id == id); Raise(nameof(SelectedQueueItem));
 }
 internal void AccountEdited() => AccountStatus = "Unsaved changes. Save accounts to apply them.";
 internal void RemoveCharacter(CharacterItemVM item) { if (item.OriginalName.Length > 0) removedAccounts.Add(item.OriginalName); Characters.Remove(item); if (SelectedCharacter == item) selectedCharacter = Characters.FirstOrDefault(); AccountEdited(); RaiseAll(); }
 void SaveAccounts()
 {
  var settings = Runtime.Settings;
  var previousNames = Characters.Select(c => c.OriginalName).Where(n => n.Length > 0).ToArray();
  var names = AccountPersistence.Save(Characters.Select(c => new AccountEdit(c.OriginalName, c.Name, c.Password)), settings.Characters,
   CredentialVault.Load, CredentialVault.Save, n => { var value = AppSettings.Load(); value.Characters = n; value.Save(); settings.Characters = n; }, n => !Runtime.QueueReserves(n) && !Runtime.IsRunning(n) && !Runtime.RunnerBusy(n) && !Runtime.LaunchPending(n) && !Runtime.ControlActions.Contains(n, StringComparer.OrdinalIgnoreCase));
  foreach (string previous in previousNames.Where(n => !names.Contains(n, StringComparer.OrdinalIgnoreCase))) removedAccounts.Add(previous);
  foreach (string saved in names) removedAccounts.Remove(saved);
  int index = 0; foreach (var item in Characters.Where(c => c.OriginalName.Length > 0 || c.Name.Length > 0 || c.Password.Length > 0).ToArray()) item.Accept(names[index++]);
  foreach (var blank in Characters.Where(c => c.OriginalName.Length == 0).ToArray()) Characters.Remove(blank);
  AccountStatus = $"Saved {names.Length} accounts. Passwords stay encrypted by Windows."; Refresh();
 }
 void SaveSettings()
 {
  var settings = Runtime.Settings; string oldPath = settings.ClientPath; bool oldChecks = settings.AutomaticUpdateChecks;
  settings.ClientPath = ClientPath.Trim(); settings.AutomaticUpdateChecks = AutomaticUpdateChecks;
  try { var persisted = AppSettings.Load(); persisted.ClientPath = settings.ClientPath; persisted.AutomaticUpdateChecks = AutomaticUpdateChecks; persisted.Save(); Runtime.Updates.AutomaticChecks = AutomaticUpdateChecks; NoticeMessage = oldPath != settings.ClientPath ? "Settings saved. Restart DAQuester to apply the game client path." : "Settings saved."; }
  catch { settings.ClientPath = oldPath; settings.AutomaticUpdateChecks = oldChecks; throw; }
 }
 internal void OpenGuide(string source) { if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") try { Process.Start(new ProcessStartInfo(source) { UseShellExecute = true }); } catch (Exception e) { ReportError(e); } }
}
