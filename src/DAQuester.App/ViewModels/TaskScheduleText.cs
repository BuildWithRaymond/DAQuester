using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

internal static class TaskScheduleText
{
    internal static QuestSchedule? Saved(DesktopRuntime runtime, string character, string quest) => runtime.SchedulerState.Entries
        .Where(e => e.Character.Equals(character, StringComparison.OrdinalIgnoreCase) && e.Quest == quest && e.Profile == runtime.Settings.ServerProfile)
        .OrderBy(e => e.Status == "Completed").ThenByDescending(e => e.Due).FirstOrDefault();

    internal static string Describe(DesktopRuntime runtime, string character, string quest)
    {
        string scope = character.Length == 0 ? "the selected character" : character;
        var saved = Saved(runtime, character, quest);
        string text = saved is null ? $"No automatic schedule for {scope}." : saved.Status == "Completed"
            ? $"{scope}: saved schedule completed." : $"{scope}: {saved.Status} · {(saved.Recurring ? "Repeating" : "One run")} · {saved.Due.ToLocalTime():MMM d, h:mm tt}.";
        if (saved?.Reason.Length > 0) text += " " + DesktopRuntime.PlayerReason(saved.Reason);
        if (saved is null)
        {
            var others = runtime.SchedulerState.Entries.Where(e => e.Quest == quest && e.Profile == runtime.Settings.ServerProfile && e.Status != "Completed" &&
                !e.Character.Equals(character, StringComparison.OrdinalIgnoreCase)).Select(e => e.Character).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
            if (others.Length > 0) text += " Saved for: " + string.Join(", ", others) + ".";
        }
        return text + (runtime.SchedulerEnabled ? " Scheduler is on." : " Scheduler is off; saved schedules will not run automatically.");
    }
}
