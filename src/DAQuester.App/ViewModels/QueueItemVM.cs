using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed class QueueItemVM : ObservableObject
{
    readonly MainWindowViewModel owner;
    public string Id
    {
        get;
    }
    public string Character
    {
        get;
    }
    public string Name => owner.Runtime.QuestName(Id);
    public int Number
    {
        get; private set;
    }
    public string Detail { get; private set; } = "One verified attempt";
    public bool CanEdit => !owner.Runtime.QueueReserves(Character);
    public bool IsActive
    {
        get; private set;
    }
    public AsyncCommand MoveUpCommand
    {
        get;
    }
    public AsyncCommand MoveDownCommand
    {
        get;
    }
    public AsyncCommand RemoveCommand
    {
        get;
    }

    public QueueItemVM(MainWindowViewModel owner, string character, string id)
    {
        this.owner = owner;
        Character = character;
        Id = id;
        MoveUpCommand = owner.Command(_ => owner.MoveQueueAsync(Character, Id, Number - 2), _ => CanEdit && Number > 1);
        MoveDownCommand = owner.Command(_ => owner.MoveQueueAsync(Character, Id, Number), _ => CanEdit && Number < owner.Runtime.Queue(Character).Quests.Length);
        RemoveCommand = owner.Command(_ => owner.Runtime.EditQueueAsync(Character, s => s with { Quests = s.Quests.Where(id => id != Id).ToArray(), ActiveQuest = s.ActiveQuest == Id ? null : s.ActiveQuest }), _ => CanEdit);
    }

    public void Refresh(int number, QuestQueueState queue)
    {
        Number = number;
        IsActive = queue.ActiveQuest == Id && queue.Locked;
        var job = owner.Runtime.Job(Character, Id);
        Detail = IsActive ? queue.State == "Paused" ? "Paused · checkpoint saved" : "In progress" : job.Pending is not null ? "Awaiting confirmation" : job.Step is not ("preflight" or "complete") ? "Resume saved progress" : "One verified attempt";
        RaiseAll();
        MoveUpCommand.Notify();
        MoveDownCommand.Notify();
        RemoveCommand.Notify();
    }
}
