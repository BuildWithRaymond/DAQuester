using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public AsyncCommand AddJourneyToQueueCommand { get; private set; } = null!;
    public AsyncCommand ChangeChoicesCommand { get; private set; } = null!;
    public bool ChangeChoicesVisible => DesktopRuntime.HasChoices(JourneyQuestId);
    bool CanChangeChoices => ChangeChoicesVisible && CharacterName.Length > 0 && Runtime.Snapshot(CharacterName).Connected
        && !Runtime.QueueReserves(CharacterName) && !Runtime.IsRunning(CharacterName) && !Runtime.RunnerBusy(CharacterName)
        && !Runtime.ControlActions.Contains(CharacterName, StringComparer.OrdinalIgnoreCase)
        && !Runtime.ChoicesLocked(CharacterName, JourneyQuestId);
    QuestItemVM? JourneyLibraryItem => library.FirstOrDefault(q => q.RecipeId == JourneyQuestId);
    public string AddJourneyToQueueLabel => JourneyLibraryItem?.IsQueued == true ? "Already in queue" : "Add to queue";
    bool CanAddJourneyToQueue => JourneyLibraryItem is { CanQueue: true, IsQueued: false };

    Task AddJourneyToQueueAsync() => CanAddJourneyToQueue && JourneyLibraryItem is { } quest
        ? ToggleQuestAsync(quest) : Task.CompletedTask;

    public string QueueHeading => CharacterName.Length == 0 ? "Your queue" : CharacterName + "’s queue";
    public string QueueSummary => QueueItems.Count == 1 ? "1 quest in queue" : $"{QueueItems.Count} quests in queue";
    public string QueueHint => QueueLocked ? "Stop this run to edit its order." : "Drag to reorder · Ctrl + ↑ / ↓";
    public string QueueReadiness => Runtime.QueueRunning(CharacterName) ? "Running for " + CharacterName : QueueState.State == "Paused" ? DesktopRuntime.PlayerReason(QueueState.Reason) : Runtime.QueueStartReason(CharacterName, QueueState) is { Length: > 0 } reason ? reason : QueueState.Reason == "Queue completed" ? "Journey complete · progress saved" : "All selected quests are ready";
    public string QueuePrimaryLabel => Runtime.QueueRunning(CharacterName) ? "Pause run" : QueueState.State == "Paused" ? "Resume run" : $"Start queue · {QueueItems.Count}";
    public bool QueueLocked => Runtime.QueueReserves(CharacterName);
    public bool QueueEmpty => QueueItems.Count == 0;

    QuestQueueState QueueState => Runtime.Queue(CharacterName);

    public bool FastMode
    {
        get => Runtime.Settings.FastModeEnabled(CharacterName);
        set
        {
            if (!refreshing && CharacterName.Length > 0 && value != FastMode)
                try
                {
                    Runtime.SetFastMode(CharacterName, value);
                }
                catch (Exception e)
                {
                    ReportError(e);
                }
        }
    }

    public string FastModeLabel => "Fast mode · " + (FastMode ? Runtime.FastModeDescription(CharacterName) : "slows near players");

    string JourneyQuestId => QueueState.Locked && QueueState.ActiveQuest is { } active ? active : Runtime.RunningQuestId(CharacterName) ?? SelectedQuest?.RecipeId ?? "";

    QuestDefinition? JourneyQuest => Runtime.Recipes.GetValueOrDefault(JourneyQuestId);

    QuestJob JourneyJob => JourneyQuest is { } quest && CharacterName.Length > 0 ? Runtime.Job(CharacterName, quest.Id) : new()
    {
        Character = CharacterName
    };
    public bool RepeatVisible => JourneyQuest?.Repeatable == true;
    public bool RepeatEnabled => RepeatVisible && JourneyQuestId != "perfect-hairstyle" && !QueueLocked && !Runtime.IsRunning(CharacterName) && Runtime.RunningQuestId(CharacterName) is null && !Runtime.RunnerBusy(CharacterName) && !Runtime.ControlActions.Contains(CharacterName, StringComparer.OrdinalIgnoreCase) && !QuestRunPolicy.Finished(JourneyQuest!, JourneyJob);

    public bool Repeat
    {
        get => RepeatVisible && !QuestRunPolicy.Finished(JourneyQuest!, JourneyJob) && (JourneyQuestId == "perfect-hairstyle" || Runtime.RepeatEnabled(CharacterName, JourneyQuestId));
        set
        {
            if (!refreshing && RepeatEnabled && value != Repeat)
            {
                string character = CharacterName, id = JourneyQuestId;
                _ = RunAsync(() => Runtime.SetRepeatAsync(character, id, value));
            }
        }
    }

    public string RepeatLabel => JourneyQuestId == "perfect-hairstyle" ? "Weekly until Suomi legend" : "Repeat every 8 hours";
    public string StartQuestLabel => QuestRunPolicy.AttemptCompleted(JourneyJob) ? JourneyQuest is { } quest && QuestRunPolicy.NextAttempt(quest, JourneyJob) > DateTimeOffset.UtcNow && Repeat ? "Schedule individual quest" : "Run individual quest again" : "Start individual quest";
    public string JourneyTitle => JourneyQuest?.Name ?? SelectedQuest?.Name ?? "Your next journey";
    public string JourneyIcon => JourneyQuestId;

    public string JourneyState
    {
        get
        {
            var job = JourneyJob;
            var s = Runtime.Snapshot(CharacterName);
            if (job.Pending is not null)
                return "Awaiting confirmation";
            if (JourneyQuestId == ShinewoodFaerieEvidence.Id && job.FaeriePetWait is not null)
                return "Pet wait · one deoch";
            if (JourneyQuest is { } quest && (QuestRunPolicy.Finished(quest, job) || Runtime.Memory(CharacterName).CompletedLegend(quest.Id, s) is not null))
                return "Completed";
            if (Runtime.RunningQuestId(CharacterName) is not null || Runtime.IsRunning(CharacterName) || Runtime.QueueRunning(CharacterName))
                return job.Status switch
                {
                    "Calibrating" => "Calibration in progress",
                    "Scheduled" => JourneyQuestId == AltarWorshipEvidence.Id ? "Waiting for next offering" : "Next attempt scheduled",
                    _ => "Quest in progress"
                };
            if (job.CompletedAt is not null)
                return "Attempt recorded";
            if (QueueState.State == "Paused" || job.Status == "Paused" && job.Reason != "Ready")
                return "Paused · progress saved";
            if (!s.Connected)
                return "Offline";
            return SelectedQuest?.Status ?? "Choose a quest";
        }
    }

    public string JourneyMessage
    {
        get
        {
            var job = JourneyJob;
            if (CharacterName.Length == 0)
                return "Add your character in Characters. Their queue and progress stay with them.";
            if (job.Pending is not null)
                return "Your last action is awaiting confirmation. Resume checks its result before continuing.";
            if (job.CompletionText is { Length: > 0 } text)
                return DesktopRuntime.PlayerReason(text);
            if (JourneyQuest is { } quest && Runtime.Memory(CharacterName).CompletedLegend(quest.Id, Runtime.Snapshot(CharacterName)) is { } legend)
                return "Legend already recorded: " + legend;
            if (job.Reason is { Length: > 0 } reason && reason != "Ready")
                return DesktopRuntime.PlayerReason(reason);
            return SelectedQuest?.Reason ?? "Select quests from the library to plan your next run.";
        }
    }

    public QuestProgress JourneySteps => QuestProgress.From(JourneyQuest, JourneyJob);
    public CharacterSnapshot JourneyCharacter => Runtime.Snapshot(CharacterName);
    public CharacterAppearance? JourneyRememberedAppearance => Runtime.Memory(CharacterName).Appearance;
    public string JourneyGameDirectory => Path.GetDirectoryName(Runtime.Settings.ClientPath) ?? "";
    public double JourneyProgress => JourneySteps.Percent;
    public bool JourneyAnimating => Runtime.Snapshot(CharacterName).Connected && JourneySteps.Current > 0 && JourneyJob.Status is not ("Paused" or "Scheduled" or "Completed") && Runtime.IsRunning(CharacterName);
    public string JourneyRewards => JourneyQuestId == AltarWorshipEvidence.Id && JourneyJob.CompletedAt is null ? AltarGoldLeft(JourneyJob) : JourneyJob.Rewards.Count > 0 ? string.Join(" · ", JourneyJob.Rewards.Select(p => $"{p.Value:N0} {p.Key}")) : "Confirmed rewards are saved to your journal.";
    static string AltarGoldLeft(QuestJob job) => Math.Max(0, AltarWorshipEvidence.Goal - AltarWorshipEvidence.Made(job)) is var left and > 0 ? (left * AltarWorshipEvidence.ShieldPrice).ToString("N0") + " gold for the " + left + " offering" + (left == 1 ? "" : "s") + " left" : "Confirmed rewards are saved to your journal.";
    public string JourneyNext => JourneyQuestId == AltarWorshipEvidence.Id && AltarWorshipEvidence.NextOffering(JourneyJob) is { } offering ? "Next offering · " + offering.ToLocalTime().ToString("MMM d, h:mm tt") : QueueState.Quests.FirstOrDefault(id => id != QueueState.ActiveQuest) is { } next ? "Next · " + Runtime.QuestName(next) : JourneyQuest is { } quest && QuestRunPolicy.NextAttempt(quest, JourneyJob) is { } due ? "Next attempt · " + due.ToLocalTime().ToString("MMM d, h:mm tt") : "Progress saved for each character";
}
