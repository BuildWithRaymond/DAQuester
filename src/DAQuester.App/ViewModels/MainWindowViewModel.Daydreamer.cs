using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    internal const string DaydreamerRegistrationNotice =
        "Registration is required throughout all 30 days. A 30-day code needs every day's check-in; a missed day may require a second code. " +
        "After the first mark, each further 30-day cycle can earn another numbered mark. " +
        "A player-reported bug may reset the mark number to 1 around mark 27; the exact threshold is unverified.";

    public bool DaydreamerRepeatVisible => SelectedQuest?.RecipeId == DaydreamerEvidence.Id;
    public bool CanChangeDaydreamerRepeat => DaydreamerRepeatVisible && CharacterName.Length > 0
        && !Runtime.QueueReserves(CharacterName) && !Runtime.SchedulerReserves(CharacterName)
        && !Runtime.IsRunning(CharacterName) && !Runtime.RunnerBusy(CharacterName)
        && !Runtime.ControlActions.Contains(CharacterName, StringComparer.OrdinalIgnoreCase);

    public bool RepeatDaydreamerAfterGoal
    {
        get => CharacterName.Length > 0 && Runtime.RepeatEnabled(CharacterName, DaydreamerEvidence.Id);
        set
        {
            if (CanChangeDaydreamerRepeat && value != RepeatDaydreamerAfterGoal)
                _ = RunAsync(() => Runtime.SetRepeatAsync(CharacterName, DaydreamerEvidence.Id, value));
        }
    }
}
