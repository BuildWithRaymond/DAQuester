using DAQuester.Core;

namespace DAQuester.App.ViewModels;

// Each row reads its own character. Selecting a dashboard row never retargets a run.
public sealed class DashboardCharacterVM : ObservableObject
{
    readonly MainWindowViewModel owner;
    QuestQueueState queue = new();
    QuestDefinition? quest;
    QuestJob job = new();
    string questId = "";
    bool running;
    internal Services.DesktopRuntime Runtime => owner.Runtime;
    public string Name { get; }
    public CharacterSnapshot Snapshot { get; private set; } = new();
    public CharacterAppearance? RememberedAppearance => owner.Runtime.Memory(Name).Appearance;
    public string GameDirectory => Path.GetDirectoryName(owner.Runtime.Settings.ClientPath) ?? "";
    QuestQueueState Queue => queue;
    public bool Running => running;
    public string QuestId => questId;
    QuestDefinition? Quest => quest;
    QuestJob Job => job;
    public bool HasJourney => QuestId.Length > 0;
    public bool IsSelected => owner.SelectedDashboardCharacter == this;
    public bool NeedsAttention => !Running && (Job.Pending is not null || Job.Status is "Blocked" or "Failed" || Queue.State == "Paused" && Queue.Reason is not ("Paused by player" or "Paused" or "Ready"));
    public string Status => Running ? "Running" : !Snapshot.Connected ? "Offline" : NeedsAttention ? "Needs attention" : Queue.Locked ? "Paused" : "Idle";
    public string FilterState => Status == "Needs attention" ? "Attention" : Status == "Paused" ? "Idle" : Status;
    public bool IsRunning => Status == "Running";
    public bool IsAttention => Status == "Needs attention";
    public string LevelAndClass
    {
        get
        {
            var s = Snapshot;
            var memory = owner.Runtime.Memory(Name);
            var level = s.Connected ? s.Level : memory.Level ?? s.Level;
            var cls = s.Connected ? s.DisplayClass ?? s.Class : memory.Class ?? s.Class;
            string text = level is { } n ? "Level " + n : "Level not observed";
            if (!string.IsNullOrWhiteSpace(cls)) text += " Â· " + cls;
            if (!s.Connected && level is not null) text += " Â· last observed";
            return text;
        }
    }
    public string JourneyTitle => Quest?.Name ?? (Snapshot.Connected ? "Ready for a new journey" : "No journey running");
    public string JourneyCaption => HasJourney ? "CURRENT JOURNEY" : "STATUS";
    public string Message
    {
        get
        {
            if (Job.Pending is not null) return "Awaiting confirmation Â· resume checks your saved action.";
            if (HasJourney && owner.Runtime.CurrentHuntingActivity(Name, QuestId) is { } activity)
                return MainWindowViewModel.HuntingMessage(activity, Snapshot);
            if (HasJourney && Job.Reason is { Length: > 0 } reason && reason != "Ready")
                return MainWindowViewModel.PlayerQuestReason(reason);
            if (Queue.Locked && Queue.Reason.Length > 0) return MainWindowViewModel.PlayerQuestReason(Queue.Reason);
            if (HasJourney) return Running ? "Quest in progress Â· confirmed steps are saved." : "Paused Â· progress saved.";
            return Snapshot.Connected ? "Open the planner to choose your next quest." : "Launch this character to begin.";
        }
    }
    public string Location => Snapshot.MapName is { Length: > 0 } map ? (Snapshot.Connected ? map : "Last observed: " + map) : "Location not observed";
    public QuestProgress Progress => QuestProgress.From(Quest, Job);
    public string ProgressLabel => HasJourney ? Progress.Label : "No active quest";
    public bool HasSteps => HasJourney && Progress.Total > 0;
    public JourneyOverviewVM JourneyOverview => owner.OverviewFor(Name);
    public string QueueSummary => Queue.Quests.Length == 0 ? "No queued quests" : $"{Queue.Quests.Length} queued Â· {owner.Runtime.QuestName(Queue.Quests.FirstOrDefault(id => id != QuestId) ?? Queue.Quests[0])}";
    public IReadOnlyList<JourneyPlanRowVM> QueueEntries => JourneyOverview.Rows;
    public string QueueHeading => $"Queue ({Queue.Quests.Length})";
    public string PrimaryLabel => Running ? "Pause" : Queue.State == "Paused" ? "Resume" : "Run queue";
    public string PrimaryIcon => Running ? "Pause" : "Play";
    public string PrimaryTooltip => Running ? owner.Runtime.SchedulerReserves(Name) ? "Pause Scheduler and this scheduled quest." : "Pause this character and keep progress."
        : owner.Runtime.QueueStartReason(Name, Queue) is { Length: > 0 } reason ? MainWindowViewModel.PlayerQuestReason(reason) : "Run this character's queued quests.";
    bool CanRun => Running || !owner.Runtime.RunnerBusy(Name) && !owner.Runtime.QueueBusy(Name) && owner.Runtime.QueueStartReason(Name, Queue).Length == 0;
    public RelayCommand SelectCommand { get; }
    public RelayCommand PlannerCommand { get; }
    public AsyncCommand PrimaryCommand { get; }
    public DashboardCharacterVM(MainWindowViewModel owner, string name)
    {
        this.owner = owner;
        Name = name;
        ReadState();
        SelectCommand = new(_ => owner.SelectedDashboardCharacter = this);
        PlannerCommand = new(_ =>
        {
            owner.SelectedCharacter = owner.Characters.FirstOrDefault(c => c.OriginalName.Equals(Name, StringComparison.OrdinalIgnoreCase));
            owner.Page = "Planner";
        });
        PrimaryCommand = owner.Command(_ => Running ? owner.Runtime.PauseQuestAsync(Name, "Paused by player") : owner.Runtime.StartQueueAsync(Name), _ => CanRun, allowConcurrent: true);
    }
    void ReadState()
    {
        Snapshot = owner.Runtime.Snapshot(Name);
        queue = owner.Runtime.Queue(Name);
        running = owner.Runtime.IsRunning(Name) || owner.Runtime.QueueRunning(Name) || owner.Runtime.SchedulerReserves(Name) || owner.Runtime.RunnerBusy(Name) || owner.Runtime.QueueBusy(Name);
        questId = owner.Runtime.SchedulerQuestId(Name) ?? owner.Runtime.RunningQuestId(Name) ?? (queue.Locked ? queue.ActiveQuest : null) ?? "";
        quest = owner.Runtime.Recipes.GetValueOrDefault(questId);
        // One journal read per refresh, rather than one for every bound caption.
        job = quest is null ? new() : owner.Runtime.Job(Name, questId);
    }
    internal void Refresh() { ReadState(); RaiseAll(); PrimaryCommand.Notify(); }
}
