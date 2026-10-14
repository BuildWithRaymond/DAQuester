using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    bool journeyCollapsed;
    public RelayCommand ToggleJourneyCommand { get; private set; } = null!;
    public event Action? JourneyCollapsing;
    public bool JourneyCollapsed
    {
        get => journeyCollapsed;
        set
        {
            if (value && !journeyCollapsed)
                JourneyCollapsing?.Invoke();
            if (Set(ref journeyCollapsed, value))
            {
                Runtime.Settings.JourneyCollapsed = value;
                try { Runtime.Settings.Save(); }
                catch (Exception error) { ReportError(error); }
                Raise(nameof(JourneyExpanded));
                Raise(nameof(JourneyToggleLabel));
                Raise(nameof(JourneyToggleIcon));
            }
        }
    }
    public bool JourneyExpanded => !JourneyCollapsed;
    public string JourneyToggleLabel => JourneyCollapsed ? "Expand current journey" : "Collapse current journey";
    public string JourneyToggleIcon => JourneyCollapsed ? "ChevronUp" : "ChevronDown";
    public bool JourneyHasSteps => JourneySteps.Total > 0;
    public string CompactJourneySummary => JourneyState
        + (JourneyHasSteps ? " · " + JourneySteps.Label : "");
    public AsyncCommand AddSelectedQuestToQueueCommand { get; private set; } = null!;
    public AsyncCommand ChangeChoicesCommand { get; private set; } = null!;
    string SelectedQuestId => SelectedQuest?.RecipeId ?? "";
    public bool SelectedQuestAutomatic => SelectedQuestId.Length > 0;
    public bool ChangeChoicesVisible => DesktopRuntime.HasChoices(SelectedQuestId);
    bool CanChangeChoices => ChangeChoicesVisible && CharacterName.Length > 0 && Runtime.Snapshot(CharacterName).Connected
        && !Runtime.QueueReserves(CharacterName) && !Runtime.IsRunning(CharacterName) && !Runtime.RunnerBusy(CharacterName)
        && !Runtime.SchedulerReserves(CharacterName)
        && !Runtime.ControlActions.Contains(CharacterName, StringComparer.OrdinalIgnoreCase)
        && !Runtime.ChoicesLocked(CharacterName, SelectedQuestId);
    public string AddSelectedQuestToQueueLabel => SelectedQuest?.IsQueued == true ? "Already in queue" : "Add to queue";
    bool CanAddSelectedQuestToQueue => SelectedQuest is { CanQueue: true, IsQueued: false };

    Task AddSelectedQuestToQueueAsync() => CanAddSelectedQuestToQueue && SelectedQuest is { } quest
        ? ToggleQuestAsync(quest) : Task.CompletedTask;

    internal static string PlayerQuestReason(string reason) => reason.Contains("calibration", StringComparison.OrdinalIgnoreCase)
        ? "Automatic run is unavailable for this quest. Open the quest guide for manual play."
        : DesktopRuntime.PlayerReason(reason);

    public string QueueHeading => CharacterName.Length == 0 ? "Your queue" : CharacterName + "’s queue";
    public string QueueSummary => QueueItems.Count == 1 ? "1 quest in queue" : $"{QueueItems.Count} quests in queue";
    public string QueueHint => QueueLocked ? "Stop this run to edit its order." : "Drag to reorder · Ctrl + ↑ / ↓";
    public string QueueReadiness => Runtime.QueueRunning(CharacterName) ? "Running for " + CharacterName : QueueState.State == "Paused" ? PlayerQuestReason(QueueState.Reason) : Runtime.QueueStartReason(CharacterName, QueueState) is { Length: > 0 } reason ? PlayerQuestReason(reason) : QueueState.Reason == "Queue completed" ? "Journey complete · progress saved" : "All selected quests are ready";
    public string QueuePrimaryLabel => Runtime.QueueRunning(CharacterName) ? "Pause queue" : QueueState.State == "Paused" ? "Resume queue" : $"Run queue · {QueueItems.Count}";
    public string QueuePrimaryIcon => Runtime.QueueRunning(CharacterName) ? "Pause" : "Play";
    public string QueuePrimaryTooltip => Runtime.QueueRunning(CharacterName) ? "Pause the queue and keep progress."
        : Runtime.QueueBusy(CharacterName) ? "Wait for this character's queue to finish saving or preparing."
        : Runtime.QueueStartReason(CharacterName, QueueState) is { Length: > 0 } reason ? PlayerQuestReason(reason)
        : QueueState.State == "Paused" ? "Resume the queue; saved actions are checked before continuing."
        : "Run the selected quests in order for " + CharacterName + ".";
    public bool QueueLocked => Runtime.QueueReserves(CharacterName);
    public bool QueueEmpty => QueueItems.Count == 0;

    QuestQueueState QueueState => Runtime.Queue(CharacterName);

    string? ScheduledJourneyPhase => Runtime.SchedulerRunPhase(CharacterName);
    string JourneyQuestId => Runtime.SchedulerQuestId(CharacterName) ?? Runtime.RunningQuestId(CharacterName)
        ?? (QueueState.Locked ? QueueState.ActiveQuest : null)
        ?? (Runtime.QueueRunning(CharacterName) ? QueueState.Deferred.OrderBy(d => d.Due).FirstOrDefault()?.Quest : null) ?? "";
    public bool HasCurrentJourney => JourneyQuestId.Length > 0 || QueueState.Locked || Runtime.QueueRunning(CharacterName);

    QuestDefinition? JourneyQuest => Runtime.Recipes.GetValueOrDefault(JourneyQuestId);

    QuestJob JourneyJob => JourneyQuest is { } quest && CharacterName.Length > 0 ? Runtime.Job(CharacterName, quest.Id) : new()
    {
        Character = CharacterName
    };
    public string JourneyTitle => JourneyQuest?.Name ?? (HasCurrentJourney ? "Journey in progress" : "No journey running");
    public string JourneyIcon => JourneyQuestId;

    public string JourneyState
    {
        get
        {
            if (!HasCurrentJourney)
                return "Idle";
            if (ScheduledJourneyPhase is "Launching")
                return "Launch in progress";
            if (ScheduledJourneyPhase is "Preparing")
                return "Preparing scheduled quest";
            if (ScheduledJourneyPhase is "Closing")
                return "Closing game window";
            var job = JourneyJob;
            var s = Runtime.Snapshot(CharacterName);
            if (job.Pending is not null)
                return "Awaiting confirmation";
            if (job.Route?.CleanupPending == true)
                return "Closing final quest popup";
            if (JourneyQuestId == FiskEvidence.Id && job.FiskWait is { Verified: true } bouquet && (job.NextRun ?? bouquet.Due) > DateTimeOffset.UtcNow)
                return "Bouquet waiting";
            if (JourneyQuestId == ShinewoodFaerieEvidence.Id && job.FaeriePetWait is not null)
                return "Pet wait · one deoch";
            if (JourneyQuest is { } quest && (QuestRunPolicy.Finished(quest, job) || Runtime.Memory(CharacterName).CompletedLegend(quest.Id, s) is not null))
                return "Completed";
            if (Runtime.RunningQuestId(CharacterName) is not null || Runtime.IsRunning(CharacterName) || Runtime.QueueRunning(CharacterName) || Runtime.SchedulerQuestId(CharacterName) is not null)
                return job.Status switch
                {
                    "Calibrating" => "Checking quest requirements",
                    "Scheduled" => JourneyQuestId == AltarWorshipEvidence.Id ? "Waiting for next offering" : "Next attempt scheduled",
                    _ => "Quest in progress"
                };
            if (job.CompletedAt is not null)
                return "Attempt recorded";
            if (QueueState.State == "Paused" || job.Status == "Paused" && job.Reason != "Ready")
                return "Paused · progress saved";
            if (!s.Connected)
                return "Offline";
            return "Quest in progress";
        }
    }

    public string JourneyMessage
    {
        get
        {
            var job = JourneyJob;
            if (CharacterName.Length == 0)
                return "Add your character in Characters. Their queue and progress stay with them.";
            if (!HasCurrentJourney)
                return "Add quests to the queue, then choose Run queue to begin. One quest is enough.";
            if (ScheduledJourneyPhase is "Launching")
                return "Scheduler is launching this character for its scheduled quest.";
            if (ScheduledJourneyPhase is "Preparing")
                return "Scheduler is checking fresh character state and quest requirements.";
            if (ScheduledJourneyPhase is "Closing")
                return "Scheduler is closing its game window and saving this attempt's progress.";
            if (job.Pending is not null)
                return "Your last action is awaiting confirmation. Resume checks its result before continuing.";
            if (JourneyQuestId == FiskEvidence.Id && job.FiskWait is { Verified: true } bouquet && (job.NextRun ?? bouquet.Due) > DateTimeOffset.UtcNow)
                return "Viveka is preparing the bouquet. Your paid wait is saved; collection will be checked after " +
                    (job.NextRun ?? bouquet.Due).ToLocalTime().ToString("MMM d, h:mm tt") + ".";
            if (job.CompletionText is { Length: > 0 } text)
                return DesktopRuntime.PlayerReason(text);
            if (JourneyQuest is { } quest && Runtime.Memory(CharacterName).CompletedLegend(quest.Id, Runtime.Snapshot(CharacterName)) is { } legend)
                return "Legend already recorded: " + legend;
            if (JourneyQuestId is NewCharacterPath.FirstStepsId or NewCharacterPath.ClimbId && Runtime.CurrentHuntingActivity(CharacterName, JourneyQuestId) is { } activity)
                return HuntingMessage(activity, Runtime.Snapshot(CharacterName));
            if (job.Reason is { Length: > 0 } reason && reason != "Ready" && (ScheduledJourneyPhase is null || job.Status != "Paused"))
                return PlayerQuestReason(reason);
            if (ScheduledJourneyPhase is not null)
                return "Your scheduled quest is in progress. Scheduler closes its game window when this attempt ends.";
            return QueueState.Locked && QueueState.Reason is { Length: > 0 } queueReason ? PlayerQuestReason(queueReason) : "Your journey is in progress. Confirmed actions are saved as you go.";
        }
    }

    public bool HasJourneyLevelSummary => JourneyLevelSummary.Length > 0;
    public string JourneyLevelSummary
    {
        get
        {
            int target = JourneyQuestId switch { NewCharacterPath.FirstStepsId => NewCharacterPath.FirstStepsLevel, NewCharacterPath.ClimbId => NewCharacterPath.ClimbLevel, _ => 0 };
            if (!HasCurrentJourney || target == 0)
                return "";
            var s = Runtime.Snapshot(CharacterName);
            if (s.Level is not { } level)
                return "Level not observed · goal level " + target;
            if (!s.Connected)
                return "Last observed: level " + level + " · goal level " + target;
            return $"Level {level} → {target}" + (level >= target ? " · goal reached" : s.ToNextLevel is { } xp ? $" · {xp:N0} XP to level {level + 1}" : " · waiting for XP");
        }
    }

    internal static string HuntingMessage(HuntingActivity activity, CharacterSnapshot s)
    {
        string place = s.MapName is { Length: > 0 } name ? " · " + name : "";
        string creature = activity.Target is { Name.Length: > 0 } target ? target.Name
            : s.Map is 1 or 2 ? activity.Target?.Sprite switch { 7 => "Mouse", 53 => "Spider", 52 => "Centipede", 47 => "Bat", 55 => "Scorpion", _ => "nearby creature" } : "nearby creature";
        return activity.Kind switch
        {
            "Attacking" => "Attacking " + creature + place,
            "Approaching" => "Moving toward " + creature + place,
            "Searching" => "Looking for monsters" + place,
            "Resting" => $"Resting · {s.Hp:N0}/{s.MaxHp:N0} HP" + place,
            "Traveling" => "Traveling to " + activity.Detail,
            "Equipping" => "Equipping " + activity.Detail,
            "Buying" => "Buying " + activity.Detail,
            "Repairing" => "Repairing gear at " + activity.Detail,
            "Stats" => "Allocating unspent stat points",
            _ => "Waiting to act" + place
        };
    }

    public QuestProgress JourneySteps => HasCurrentJourney ? QuestProgress.From(JourneyQuest, JourneyJob) : new(0, 0, 0, "No journey running");
    public CharacterSnapshot JourneyCharacter => Runtime.Snapshot(CharacterName);
    public CharacterAppearance? JourneyRememberedAppearance => Runtime.Memory(CharacterName).Appearance;
    public string JourneyGameDirectory => Path.GetDirectoryName(Runtime.Settings.ClientPath) ?? "";
    public double JourneyProgress => JourneySteps.Percent;
    public bool JourneyAnimating => Runtime.Snapshot(CharacterName).Connected && JourneySteps.Current > 0 && JourneyJob.Status is not ("Paused" or "Scheduled" or "Completed") && Runtime.IsRunning(CharacterName);
    public string JourneyRewards => !HasCurrentJourney ? "" : JourneyQuestId == AltarWorshipEvidence.Id && JourneyJob.CompletedAt is null ? AltarGoldLeft(JourneyJob) : JourneyJob.Rewards.Count > 0 ? string.Join(" · ", JourneyJob.Rewards.Select(p => $"{p.Value:N0} {p.Key}")) : "Confirmed rewards are saved to your journal.";
    static string AltarGoldLeft(QuestJob job) => Math.Max(0, AltarWorshipEvidence.Goal - AltarWorshipEvidence.Made(job)) is var left and > 0 ? (left * AltarWorshipEvidence.ShieldPrice).ToString("N0") + " gold for the " + left + " offering" + (left == 1 ? "" : "s") + " left" : "Confirmed rewards are saved to your journal.";
    public string JourneyNext
    {
        get
        {
            if (!HasCurrentJourney) return "";
            if (JourneyQuestId == FiskEvidence.Id && JourneyJob.FiskWait is { Verified: true } bouquet)
                return "Bouquet check · " + (JourneyJob.NextRun ?? bouquet.Due).ToLocalTime().ToString("MMM d, h:mm tt");
            if (JourneyQuestId == AltarWorshipEvidence.Id && AltarWorshipEvidence.NextOffering(JourneyJob) is { } offering)
                return "Next offering · " + offering.ToLocalTime().ToString("MMM d, h:mm tt");
            if (ScheduledJourneyPhase is null && QueueState.Locked && QueueState.Quests.FirstOrDefault(id => id != QueueState.ActiveQuest) is { } next)
                return "Next · " + Runtime.QuestName(next);
            return JourneyQuest is { } quest && QuestRunPolicy.NextAttempt(quest, JourneyJob) is { } due
                ? "Next attempt · " + due.ToLocalTime().ToString("MMM d, h:mm tt")
                : "Progress saved for each character";
        }
    }
}
