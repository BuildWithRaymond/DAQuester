using System.Collections.ObjectModel;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    readonly List<TaskItemVM> taskCatalog = [];
    string taskSearch = "", taskCategory = "All categories", taskFilter = "All";
    string? preferredTask;
    TaskItemVM? selectedTask;
    bool syncingTasks, tasksCompact, taskDetailsOpen;
    public ObservableCollection<TaskItemVM> Tasks { get; } = [];
    public ObservableCollection<string> TaskCategories { get; } = [];
    public string[] TaskFilters { get; } = ["All", "Ready", "Running", "Scheduled", "Needs attention"];
    public string TaskSearch { get => taskSearch; set { if (Set(ref taskSearch, value ?? "")) FilterTasks(); } }
    public string TaskCategory { get => taskCategory; set { if (value is not null && Set(ref taskCategory, value)) FilterTasks(); } }
    public string TaskFilter { get => taskFilter; set { if (value is not null && Set(ref taskFilter, value)) FilterTasks(); } }
    public TaskItemVM? SelectedTask
    {
        get => selectedTask;
        set
        {
            if (syncingTasks || value is not null && !Tasks.Contains(value)) return;
            if (value is not null) preferredTask = value.Id;
            if (!Set(ref selectedTask, value)) return;
            RaiseTaskLayout();
        }
    }
    public bool TasksCompact { get => tasksCompact; set { if (Set(ref tasksCompact, value)) { taskDetailsOpen = false; RaiseTaskLayout(); } } }
    public bool ShowTaskCatalog => !TasksCompact || !taskDetailsOpen;
    public bool ShowTaskDetails => SelectedTask is not null && (!TasksCompact || taskDetailsOpen);
    public bool TasksEmpty => Tasks.Count == 0;
    public string TaskCount => $"{Tasks.Count} of {taskCatalog.Count} {(taskCatalog.Count == 1 ? "task" : "tasks")}";
    public string TaskCharacter => CharacterName.Length == 0 ? "Choose a character" : "For " + CharacterName;
    public TaskItemVM? RunningTask => taskCatalog.FirstOrDefault(t => t.IsRunning);
    public bool ShowRunningTask => RunningTask is { } running && (!ShowTaskDetails || SelectedTask != running);
    public string RunningTaskText => RunningTask is { } task ? task.Name + " · " + task.Character : "";
    public AsyncCommand TaskPrimaryCommand { get; private set; } = null!;
    public AsyncCommand TaskStopCommand { get; private set; } = null!;
    public RelayCommand OpenTaskCommand { get; private set; } = null!;
    public RelayCommand BackToTasksCommand { get; private set; } = null!;
    public RelayCommand ClearTaskFiltersCommand { get; private set; } = null!;
    public RelayCommand OpenRunningTaskCommand { get; private set; } = null!;

    void InitializeTaskCatalog()
    {
        TaskPrimaryCommand = Command(async parameter =>
        {
            if (parameter is not TaskItemVM row) return;
            string character = CharacterName;
            if (OwnsRunningTask(row)) await Runtime.PauseQuestAsync(character, "Paused by player");
            else await Runtime.StartQuestAsync(character, row.Id, TaskItemVM.HasProgress(Runtime.Recipes[row.Id], Runtime.Job(character, row.Id)));
        }, parameter => parameter is TaskItemVM row && CanRunTask(row), allowConcurrent: true);
        TaskStopCommand = Command(parameter => Runtime.PauseQuestAsync(CharacterName, "Stopped by player"),
            parameter => parameter is TaskItemVM row && OwnsRunningTask(row));
        OpenTaskCommand = new(parameter =>
        {
            if (parameter is not TaskItemVM row || !Tasks.Contains(row)) return;
            SelectedTask = row;
            taskDetailsOpen = true;
            RaiseTaskLayout();
        });
        BackToTasksCommand = new(_ => { taskDetailsOpen = false; RaiseTaskLayout(); });
        ClearTaskFiltersCommand = new(_ => ClearTaskFilters());
        OpenRunningTaskCommand = new(_ =>
        {
            if (RunningTask is not { } row) return;
            ClearTaskFilters();
            OpenTaskCommand.Execute(row);
        });
    }

    bool OwnsRunningTask(TaskItemVM row) => taskCatalog.Contains(row) && row.Character.Equals(CharacterName, StringComparison.OrdinalIgnoreCase) &&
        Runtime.IsRunning(CharacterName) && Runtime.RunningQuestId(CharacterName) == row.Id;
    bool CanRunTask(TaskItemVM row)
    {
        if (!taskCatalog.Contains(row) || !row.Character.Equals(CharacterName, StringComparison.OrdinalIgnoreCase)) return false;
        if (OwnsRunningTask(row)) return true;
        if (Runtime.TaskBlocker(CharacterName) is not null) return false;
        var job = Runtime.Job(CharacterName, row.Id);
        if (TaskItemVM.HasProgress(Runtime.Recipes[row.Id], job)) return true;
        var available = Runtime.Availability(CharacterName, row.Id);
        return available.Ready || available.Status == "Not checked";
    }

    void RefreshTasks()
    {
        foreach (var recipe in Runtime.Recipes.Values.Where(r => r.IsTask))
            if (!taskCatalog.Any(t => t.Id == recipe.Id)) taskCatalog.Add(new(this, recipe));
        taskCatalog.RemoveAll(t => !Runtime.Recipes.TryGetValue(t.Id, out var recipe) || !recipe.IsTask);
        taskCatalog.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Sync(TaskCategories, ["All categories", .. taskCatalog.Select(t => t.Category).Distinct().Order()]);
        string? blocker = Runtime.TaskBlocker(CharacterName);
        foreach (var row in taskCatalog) row.Refresh(CharacterName, blocker);
        FilterTasks();
    }

    void ClearTaskFilters()
    {
        taskSearch = "";
        taskCategory = "All categories";
        taskFilter = "All";
        FilterTasks();
    }

    void FilterTasks()
    {
        var visible = taskCatalog.Where(t => t.Matches(TaskSearch.Trim()) && (TaskCategory == "All categories" || t.Category == TaskCategory) &&
            (TaskFilter switch { "Ready" => t.IsReady, "Running" => t.IsRunning, "Scheduled" => t.IsScheduled, "Needs attention" => t.NeedsAttention, _ => true })).ToArray();
        syncingTasks = true;
        try
        {
            Sync(Tasks, visible);
            var next = visible.FirstOrDefault(t => t.Id == preferredTask) ?? visible.FirstOrDefault();
            if (selectedTask != next) taskDetailsOpen = false;
            selectedTask = next;
        }
        finally { syncingTasks = false; }
        RaiseAll();
    }

    void RaiseTaskLayout()
    {
        Raise(nameof(ShowTaskCatalog));
        Raise(nameof(ShowTaskDetails));
        Raise(nameof(ShowRunningTask));
    }
}
