using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed class QuestItemVM : ObservableObject
{
    readonly MainWindowViewModel owner;
    readonly string recipeId;
    QuestAvailability availability = new("Not checked", "Choose a character", false);
    bool queued;
    internal DesktopRuntime Runtime => owner.Runtime;
    public string GameDirectory => Path.GetDirectoryName(owner.Runtime.Settings.ClientPath) ?? "";
    public string Id
    {
        get;
    }
    public string RecipeId => recipeId;
    public bool IsGuideOnly => recipeId.Length == 0;
    public bool HasLegendReward { get; }
    public string LegendEvidence { get; }
    public string Name
    {
        get;
    }
    public string Rewards
    {
        get;
    }
    public string Requirements
    {
        get;
    }
    public string Level
    {
        get;
    }
    public string Source
    {
        get;
    }
    public string Description
    {
        get;
    }
    public string Region
    {
        get;
    }
    public string Status => availability.Status;
    public string Reason => MainWindowViewModel.PlayerQuestReason(availability.Reason);
    public string StatusLabel => Status switch { "Unavailable" => "Guide only", "Choices" => "Choices needed", _ => Status };
    public string StatusIcon => Status switch
    {
        "Ready" or "Completed" => "Check",
        "Resume ready" => "Play",
        "Choices" => "GitBranch",
        "Blocked" => "X",
        "Cooldown" or "Next offering" => "RotateCcw",
        "Unavailable" => "BookOpen",
        _ => "CircleHelp"
    };
    public bool RowReady => IsReady && Status != "Choices";
    public bool StatusReady => RowReady || IsCompleted;
    public bool StatusWarning => Status is "Blocked" or "Cooldown" or "Choices" || IsWarning;
    public bool QueueEligible => recipeId.Length > 0 && availability.CanQueue;
    public bool ShowQueueCheckbox => !IsCompleted || IsQueued;
    public bool ShowCompletionCheck => IsCompleted && !IsQueued;
    public string SelectionExplanation => Status == "Not checked" && QueueEligible ? "Can add to queue. " + Reason : Reason;
    public string QueueActionName => (IsQueued ? "Remove from queue: " : "Queue: ") + Name;
    public string QueueTooltip => recipeId.Length == 0 ? Reason
        : owner.CharacterName.Length == 0 ? "Add or select a character to plan a queue."
        : owner.QueueLocked ? "Stop this run before editing the queue. " + Reason
        : IsQueued ? "Remove from queue. " + Reason
        : QueueEligible ? "Add to queue. " + Reason
        : SelectionExplanation;
    public bool IsReady => availability.Ready;
    public bool IsCompleted => Status == "Completed";
    public bool IsMuted => Status is "Unavailable" or "Blocked" or "Cooldown";
    public bool IsWarning => availability.Warning;
    string? noticeTitle, noticeText, offerings;
    bool noticeAlert;
    public bool HasNotice => noticeTitle is not null;
    public string NoticeTitle => noticeTitle ?? "";
    public string NoticeText => noticeText ?? "";
    public bool NoticeAlert => noticeAlert;
    public bool HasOfferings => offerings is { Length: > 0 };
    public string Offerings => offerings ?? "";
    public bool CanQueue => owner.CharacterName.Length > 0 && !owner.QueueLocked && (availability.CanQueue || queued) && recipeId.Length > 0;

    public bool IsQueued
    {
        get => queued;
        set
        {
            if (value != queued)
                _ = owner.ToggleQuestAsync(this);
        }
    }

    public RelayCommand OpenGuideCommand
    {
        get;
    }

    public QuestItemVM(MainWindowViewModel owner, string id, string recipeId, string name, string rewards, string requirements, string level, string source, string description, string region, bool hasLegendReward = false, string legendEvidence = "")
    {
        this.owner = owner;
        Id = id;
        this.recipeId = recipeId;
        Name = name;
        Rewards = rewards;
        Requirements = requirements;
        Level = level;
        Source = source;
        Description = description;
        Region = region;
        HasLegendReward = hasLegendReward;
        LegendEvidence = legendEvidence;
        OpenGuideCommand = new(_ => owner.OpenGuide(Source), _ => Uri.TryCreate(Source, UriKind.Absolute, out var u) && u.Scheme is "https" or "http");
    }

    public void Refresh(string character, QuestQueueState queue)
    {
        availability = owner.Runtime.Availability(character, recipeId.Length == 0 ? Id : recipeId);
        queued = queue.Quests.Contains(recipeId);
        if (recipeId == AltarWorshipEvidence.Id)
            RefreshAltar(character);
        RaiseAll();
    }

    void RefreshAltar(string character)
    {
        string? characterClass = owner.Runtime.CharacterClass(character);
        noticeAlert = characterClass is not null && !AltarWorshipEvidence.IsPriest(characterClass);
        noticeTitle = noticeAlert ? character + " is a " + characterClass + " and won't get the legend mark" : "Legend mark for Priests only";
        noticeText = noticeAlert
            ? "Only Priests earn \"" + AltarWorshipEvidence.Legend + "\". " + character + " will get a Red Jade Ring and 50,000 experience after the 10th offering."
            : "Priests earn \"" + AltarWorshipEvidence.Legend + "\" and a Jade or Amethyst Ring with 100,000 experience. Other classes get a Red Jade Ring and 50,000 experience.";
        var made = character.Length == 0 ? [] : owner.Runtime.Job(character, recipeId).AltarOfferings;
        offerings = string.Join("\n", made.Select((o, i) => (i + 1) + ". " + o.At.ToLocalTime().ToString("MMM d, h:mm tt") + " · " + (o.Paid > 0 ? "bought for " + o.Paid.ToString("N0") : "shield from the bag") + (o.Experience.Length > 0 ? " · " + o.Experience : "")));
    }
}
