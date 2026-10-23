using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed class TaskItemVM : ObservableObject
{
    readonly MainWindowViewModel owner;
    readonly QuestDefinition recipe;
    internal DesktopRuntime Runtime => owner.Runtime;
    public string GameDirectory => Path.GetDirectoryName(Runtime.Settings.ClientPath) ?? "";
    QuestJob job = new();
    QuestAvailability availability = new("Not checked", "Choose a character", false);
    string? blocker;
    QuestSchedule? savedSchedule;
    public string Character { get; private set; } = "";
    public string Id => recipe.Id;
    public string Name => recipe.Name;
    public string Category => recipe.TaskPresentation?.Category ?? "Other";
    public string Location => recipe.TaskPresentation?.Location ?? "";
    public string Summary => recipe.TaskPresentation?.Summary ?? "";
    public string Requirements => recipe.TaskPresentation?.Requirements ?? "Check the current requirements before starting.";
    public string HowItWorks => recipe.TaskPresentation?.HowItWorks ?? "";
    public bool HasInstructions => HowItWorks.Length > 0;
    public bool IsResearch => Id == WizardyResearchEvidence.Id;
    public bool IsAltar => Id == AltarWorshipEvidence.Id;
    public string Progress => IsAltar ? AltarWorshipEvidence.Progress(job) : "";
    public string Rewards => !IsAltar ? "" : owner.Runtime.CharacterClass(Character) is { } characterClass ? AltarWorshipEvidence.Rewards(characterClass)
        : AltarWorshipEvidence.LegendClasses + " earn the legend mark; other classes receive a Red Jade Ring and 50,000 experience. Connect to read this character's class.";
    bool WaitingForOffering => IsAltar && AltarWorshipEvidence.NextOffering(job) > DateTimeOffset.UtcNow;
    public bool IsRunning { get; private set; }
    public bool NeedsReview => job.Pending is not null || job.Research?.NeedsRecovery == true || job.Route?.CleanupPending == true;
    public bool Unfinished => HasProgress(recipe, job);
    internal static bool HasProgress(QuestDefinition recipe, QuestJob job) => job.Pending is not null || job.Research?.NeedsRecovery == true || job.Route?.CleanupPending == true ||
        (job.Step != "preflight" || recipe.Id == AltarWorshipEvidence.Id && AltarWorshipEvidence.Made(job) > 0) &&
        !QuestRunPolicy.AttemptCompleted(job) && !QuestRunPolicy.Finished(recipe, job);
    public bool IsReady => !IsRunning && !Unfinished && blocker is null && availability.Ready;
    public bool IsScheduled => savedSchedule is { Status: not "Completed" };
    public bool NeedsAttention => !IsRunning && (NeedsReview || Unfinished && !WaitingForOffering || availability.Status == "Blocked" || savedSchedule?.Status == "Needs attention");
    public string Status => IsRunning ? "Running" : NeedsReview ? "Needs attention" : WaitingForOffering ? "Waiting for next offering" : Unfinished ? "Paused" : availability.Status == "Cooldown" ? "Resting" : availability.Status;
    public string Reason => DesktopRuntime.PlayerReason(IsRunning ? (job.Reason.Length > 0 ? job.Reason : "Task is in progress.")
        : NeedsReview ? (job.Reason.Length > 0 ? job.Reason : availability.Reason) : blocker ?? availability.Reason);
    public string PrimaryAction => IsRunning ? "Pause" : Unfinished ? "Resume" : "Run task";
    public string RepeatLabel => IsAltar ? "10 offerings · 3 hours apart" : recipe.Repeatable && recipe.CooldownHours > 0 ? $"Every {recipe.CooldownHours:g} hours" : "One-time task";
    public string NextCheck => (IsAltar ? AltarWorshipEvidence.NextOffering(job) : QuestRunPolicy.NextAttempt(recipe, job)) is { } due
        ? $"{(IsAltar ? "Next offering" : "Next check")} {due.ToLocalTime():MMM d, h:mm tt}" : "";
    public bool HasNextCheck => NextCheck.Length > 0;
    public string Schedule => TaskScheduleText.Describe(owner.Runtime, Character, Id);
    public string ScheduleHint => savedSchedule is null ? RepeatLabel : savedSchedule.Status == "Completed" ? "Schedule completed" :
        $"{savedSchedule.Status} · {savedSchedule.Due.ToLocalTime():MMM d, h:mm tt}";
    public bool HasSettings => IsResearch || DesktopRuntime.HasChoices(Id);
    public bool CanChangeSettings => HasSettings && !Unfinished && !IsRunning && blocker is null;
    public string SettingsLabel => IsResearch ? "Assistant choices" : "Task choices";

    internal TaskItemVM(MainWindowViewModel owner, QuestDefinition recipe) { this.owner = owner; this.recipe = recipe; }
    internal void Refresh(string character, string? taskBlocker)
    {
        Character = character;
        job = owner.Presentation.Job(character, Id);
        availability = owner.Presentation.TaskAvailability(character, Id);
        blocker = taskBlocker;
        IsRunning = owner.Runtime.IsRunning(character) && owner.Runtime.RunningQuestId(character) == Id;
        savedSchedule = TaskScheduleText.Saved(owner.Runtime, character, Id);
        RaiseAll();
    }

    internal bool Matches(string search) => string.Join(' ', Name, Category, Location, Summary, Requirements).Contains(search, StringComparison.OrdinalIgnoreCase);
}
