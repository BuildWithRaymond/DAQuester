using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed class CharacterItemVM : ObservableObject
{
 readonly MainWindowViewModel owner;
 string name, password = "";
 public string OriginalName { get; private set; }
 public string DisplayName => OriginalName.Length > 0 ? OriginalName : Name.Length > 0 ? Name : "New character";
 public string Name { get => name; set { if (Set(ref name, value)) { RaiseAll(); NotifyCommands(); owner.AccountEdited(); } } }
 public string Password { get => password; set { if (Set(ref password, value)) { RaiseAll(); NotifyCommands(); owner.AccountEdited(); } } }
 public string Initial => DisplayName[..1].ToUpperInvariant();
 public bool IsSelected => owner.SelectedCharacter == this;
 public bool Saved => OriginalName.Length > 0 && Name.Equals(OriginalName, StringComparison.OrdinalIgnoreCase) && Password.Length == 0;
 public string Connection => owner.Runtime.Snapshot(OriginalName).Connected ? "Connected" : "Offline";
 public string LevelText
 {
  get
  {
   var s = owner.Runtime.Snapshot(OriginalName); var m = owner.Runtime.Memory(OriginalName);
   return s.Connected && s.Id is not null && s.Level is { } current ? $"Level {current} · confirmed" : m.Level is { } last ? $"Level {last} · last observed" : "Level not observed";
  }
 }
 public string LegendText
 {
  get
  {
   var s = owner.Runtime.Snapshot(OriginalName); var m = owner.Runtime.Memory(OriginalName);
   var legends = s.Connected && s.Id is not null && s.LegendsKnown ? s.Legends.ToArray() : m.Legends;
   return legends.Length > 0 ? string.Join("\n", legends) : m.LegendsObservedAt is not null || s.LegendsKnown ? "No quest legends observed" : "Legends not observed";
  }
 }
 public string HomeText => OriginalName.Length == 0 ? "Home not set" : owner.Runtime.Queue(OriginalName).Home is { } home ? "Home · " + home.Label : "Home not set";
 public string LoginText => Password.Length > 0 ? "New password · unsaved" : Name != OriginalName ? "Unsaved character edits" : OriginalName.Length > 0 && CredentialVault.Exists(OriginalName) ? "Password saved securely" : "Manual sign-in";
 public string? LaunchBlocker => Saved ? owner.Runtime.LaunchBlocker(OriginalName) : "Save this character’s edits before launching.";
 public string LaunchTooltip => LaunchBlocker ?? owner.Runtime.LaunchHint(OriginalName) ?? "Launch Dark Ages";
 public string LaunchStatus => LaunchBlocker ?? owner.Runtime.LaunchState(OriginalName);
 public AsyncCommand LaunchCommand { get; }
 public AsyncCommand SetHomeCommand { get; }
 public RelayCommand SelectCommand { get; }
 public RelayCommand RemoveCommand { get; }
 public CharacterItemVM(MainWindowViewModel owner, string original)
 {
  this.owner = owner; OriginalName = name = original;
  LaunchCommand = owner.Command(_ => owner.Runtime.LaunchAsync(OriginalName), _ => LaunchBlocker is null);
  SetHomeCommand = owner.Command(_ => owner.Runtime.SetHomeAsync(OriginalName), _ => Saved && owner.Runtime.Snapshot(OriginalName) is { Connected: true, Map: not null, Position: not null } && !owner.Runtime.QueueReserves(OriginalName));
  SelectCommand = new(_ => { owner.SelectedCharacter = this; owner.Page = "Planner"; }, _ => OriginalName.Length > 0);
  RemoveCommand = new(_ => owner.RemoveCharacter(this));
 }
 public void Accept(string saved) { OriginalName = name = saved; password = ""; RaiseAll(); NotifyCommands(); }
 public void Refresh() { RaiseAll(); NotifyCommands(); }
 void NotifyCommands() { LaunchCommand.Notify(); SetHomeCommand.Notify(); SelectCommand.Notify(); }
}

public sealed class QuestItemVM : ObservableObject
{
 readonly MainWindowViewModel owner;
 readonly string recipeId;
 QuestAvailability availability = new("Not checked", "Choose a character", false);
 bool queued;
 public string Id { get; }
 public string RecipeId => recipeId;
 public string Name { get; }
 public string Rewards { get; }
 public string Requirements { get; }
 public string Level { get; }
 public string Source { get; }
 public string Description { get; }
 public string Region { get; }
 public string Status => availability.Status;
 public string Reason => availability.Reason;
 public bool IsReady => availability.Ready;
 public bool IsCompleted => Status == "Completed";
 public bool IsMuted => Status is "Unavailable" or "Blocked" or "Cooldown";
 public bool CanQueue => owner.CharacterName.Length > 0 && !owner.QueueLocked && (availability.CanQueue || queued) && recipeId.Length > 0;
 public bool IsQueued { get => queued; set { if (value != queued) _ = owner.ToggleQuestAsync(this); } }
 public RelayCommand OpenGuideCommand { get; }
 public QuestItemVM(MainWindowViewModel owner, string id, string recipeId, string name, string rewards, string requirements, string level, string source, string description, string region)
 {
  this.owner = owner; Id = id; this.recipeId = recipeId; Name = name; Rewards = rewards; Requirements = requirements; Level = level; Source = source; Description = description; Region = region;
  OpenGuideCommand = new(_ => owner.OpenGuide(Source), _ => Uri.TryCreate(Source, UriKind.Absolute, out var u) && u.Scheme is "https" or "http");
 }
 public void Refresh(string character, QuestQueueState queue)
 {
  availability = owner.Runtime.Availability(character, recipeId.Length == 0 ? Id : recipeId); queued = queue.Quests.Contains(recipeId); RaiseAll();
 }
}

public sealed class QueueItemVM : ObservableObject
{
 readonly MainWindowViewModel owner;
 public string Id { get; }
 public string Character { get; }
 public string Name => owner.Runtime.QuestName(Id);
 public int Number { get; private set; }
 public string Detail { get; private set; } = "One verified attempt";
 public bool CanEdit => !owner.Runtime.QueueReserves(Character);
 public bool IsActive { get; private set; }
 public AsyncCommand MoveUpCommand { get; }
 public AsyncCommand MoveDownCommand { get; }
 public AsyncCommand RemoveCommand { get; }
 public QueueItemVM(MainWindowViewModel owner, string character, string id)
 {
  this.owner = owner; Character = character; Id = id;
  MoveUpCommand = owner.Command(_ => owner.MoveQueueAsync(Character, Id, Number - 2), _ => CanEdit && Number > 1);
  MoveDownCommand = owner.Command(_ => owner.MoveQueueAsync(Character, Id, Number), _ => CanEdit && Number < owner.Runtime.Queue(Character).Quests.Length);
  RemoveCommand = owner.Command(_ => owner.Runtime.EditQueueAsync(Character, s => s with { Quests = s.Quests.Where(id => id != Id).ToArray(), ActiveQuest = s.ActiveQuest == Id ? null : s.ActiveQuest }), _ => CanEdit);
 }
 public void Refresh(int number, QuestQueueState queue)
 {
  Number = number; IsActive = queue.ActiveQuest == Id && queue.Locked;
  var job = owner.Runtime.Job(Character, Id);
  Detail = IsActive ? queue.State == "Paused" ? "Paused · checkpoint saved" : "In progress" : job.Pending is not null ? "Awaiting confirmation" : job.Step is not ("preflight" or "complete") ? "Resume saved progress" : "One verified attempt";
  RaiseAll(); MoveUpCommand.Notify(); MoveDownCommand.Notify(); RemoveCommand.Notify();
 }
}

public sealed record HistoryItemVM(string Character, string Quest, string Outcome, DateTimeOffset At, string Summary)
{
 public string When => At.ToLocalTime().ToString("MMM d, h:mm tt");
}
