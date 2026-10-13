using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    IEnumerable<QuestSchedule> UnfinishedSchedules => Runtime.SchedulerState.Entries.Where(e => e.Status != "Completed");
    public bool SchedulerIndicatorVisible => true;
    public bool SchedulerIndicatorOff => !Runtime.SchedulerEnabled;
    public bool SchedulerIndicatorWarning => Runtime.SchedulerEnabled && (Runtime.ConnectionError is not null || Runtime.SchedulerState.LoginBlocks.Length > 0
        || UnfinishedSchedules.Any(e => e.Status is "Paused" or "Needs attention"));
    public bool SchedulerIndicatorReady => Runtime.SchedulerEnabled && !SchedulerIndicatorWarning;
    public string SchedulerIndicatorLabel => SchedulerIndicatorOff ? "Off"
        : Runtime.SchedulerState.LoginBlocks.Length > 0 || UnfinishedSchedules.Any(e => e.Status == "Needs attention") ? "Needs attention"
        : SchedulerIndicatorWarning ? "Paused"
        : UnfinishedSchedules.Any(e => e.Status == "Running") ? "Running" : "Active";
    public string SchedulerIndicatorTooltip => SchedulerIndicatorOff ? "Scheduler is off. Saved schedules will not launch characters."
        : Runtime.ConnectionError is not null ? "Scheduler is paused because the connection is unavailable. Check Settings before scheduled logins can run."
        : $"Scheduler: {SchedulerIndicatorLabel.ToLowerInvariant()} · {UnfinishedSchedules.Count(e => e.Status == "Armed")} scheduled · {UnfinishedSchedules.Count(e => e.Status == "Running")} running · {UnfinishedSchedules.Count(e => e.Status == "Paused")} paused · {UnfinishedSchedules.Count(e => e.Status == "Needs attention")} need attention · {Runtime.SchedulerState.LoginBlocks.Length} login blocks. Open Scheduler to review.";
}
