using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed class QuestItemVM : ObservableObject
{
    readonly MainWindowViewModel owner;
    readonly string recipeId;
    QuestAvailability availability = new("Not checked", "Choose a character", false);
    bool queued;
    public string Id
    {
        get;
    }
    public string RecipeId => recipeId;
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
    public string Reason => availability.Reason;
    public bool IsReady => availability.Ready;
    public bool IsCompleted => Status == "Completed";
    public bool IsMuted => Status is "Unavailable" or "Blocked" or "Cooldown";
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

    public QuestItemVM(MainWindowViewModel owner, string id, string recipeId, string name, string rewards, string requirements, string level, string source, string description, string region)
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
        OpenGuideCommand = new(_ => owner.OpenGuide(Source), _ => Uri.TryCreate(Source, UriKind.Absolute, out var u) && u.Scheme is "https" or "http");
    }

    public void Refresh(string character, QuestQueueState queue)
    {
        availability = owner.Runtime.Availability(character, recipeId.Length == 0 ? Id : recipeId);
        queued = queue.Quests.Contains(recipeId);
        RaiseAll();
    }
}
