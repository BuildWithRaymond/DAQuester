using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    QuestJob ResearchJob => Presentation.Job(CharacterName, WizardyResearchEvidence.Id);
    public string ResearchStatus => Runtime.IsRunning(CharacterName) ? Runtime.RunningQuestId(CharacterName) == WizardyResearchEvidence.Id ? "Research in progress" : "Character is busy"
        : ResearchJob.Research?.Cleanup is not null ? "Final popup needs cleanup" : ResearchJob.Pending is not null ? "Check the last submitted action"
        : Runtime.Availability(CharacterName, WizardyResearchEvidence.Id).Status;
    public string ResearchReason => DesktopRuntime.PlayerReason(ResearchJob.Pending is not null || ResearchJob.Research?.Cleanup is not null ? ResearchJob.Reason
        : Runtime.TaskBlocker(CharacterName) ?? Runtime.Availability(CharacterName, WizardyResearchEvidence.Id).Reason);
    public string ResearchSupplies => Runtime.Snapshot(CharacterName) is { InventoryKnown: true } s ? $"{s.Count(WizardyResearchEvidence.Parchment):N0} parchment · {s.Gold:N0} gold"
        : "Connect to read parchment and gold.";
    public string ResearchAssistant => "Assistant: " + (ResearchJob.ResearchOptions?.Assistant ?? "Alone");
    public string ResearchProgress => $"{ResearchJob.Research?.Attempts.Sum(a => a.Songs.Values.Sum()) ?? 0:N0} Songs created · {ResearchJob.Research?.Deposits.Sum(d => d.Quantity) ?? 0:N0} Songs banked";
    public string ResearchNext => ResearchJob.NextRun is { } due ? "Next research check: " + due.ToLocalTime().ToString("MMM d, h:mm tt") : "A fresh research visit runs until the game requires rest.";
    public string ResearchSchedule => TaskScheduleText.Describe(Runtime, CharacterName, WizardyResearchEvidence.Id);
    bool ResearchUnfinished => ResearchJob.Pending is not null || ResearchJob.Research?.Cleanup is not null || ResearchJob.Research?.Stage > 0;
    public bool CanChangeResearchChoices => CharacterName.Length > 0 && !ResearchUnfinished && Runtime.TaskBlocker(CharacterName) is null;
    public AsyncCommand RunResearchCommand { get; private set; } = null!;
    public AsyncCommand ResumeResearchCommand { get; private set; } = null!;
    public AsyncCommand PauseResearchCommand { get; private set; } = null!;
    public AsyncCommand StopResearchCommand { get; private set; } = null!;
    void InitializeTasks()
    {
        InitializeTaskCatalog();
        RunResearchCommand = Command(_ => Runtime.StartQuestAsync(CharacterName, WizardyResearchEvidence.Id),
            _ => CanChangeResearchChoices && (Runtime.Availability(CharacterName, WizardyResearchEvidence.Id).Ready ||
                Runtime.Availability(CharacterName, WizardyResearchEvidence.Id).Status == "Not checked"), allowConcurrent: true);
        ResumeResearchCommand = Command(_ => Runtime.StartQuestAsync(CharacterName, WizardyResearchEvidence.Id, true),
            _ => ResearchUnfinished && Runtime.TaskBlocker(CharacterName) is null, allowConcurrent: true);
        PauseResearchCommand = Command(_ => Runtime.PauseQuestAsync(CharacterName, "Paused by player"),
            _ => Runtime.IsRunning(CharacterName) && Runtime.RunningQuestId(CharacterName) == WizardyResearchEvidence.Id);
        StopResearchCommand = Command(_ => Runtime.PauseQuestAsync(CharacterName, "Stopped by player"),
            _ => Runtime.IsRunning(CharacterName) && Runtime.RunningQuestId(CharacterName) == WizardyResearchEvidence.Id);
    }
}
