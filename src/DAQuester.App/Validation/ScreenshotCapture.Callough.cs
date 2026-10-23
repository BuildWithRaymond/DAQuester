using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.ViewModels;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    static async Task CaptureCalloughAsync(Window window, MainWindowViewModel vm)
    {
        var runtime = vm.Runtime;
        foreach (var schedule in runtime.SchedulerState.Entries) await runtime.RemoveScheduleAsync(schedule.Id);
        runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 20, Level = 3,
            LegendsKnown = true, LegendsRevision = 20, Legends = ["Loved by Mileth Mundanes (1) - Deoch 223, Summer"] });
        await runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow, true, true);
        var at = DateTimeOffset.UtcNow;
        var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
        await journal.SaveAsync(new() { Character = "Elara", Quest = "spare-a-stick", Step = "complete", Status = "PermanentlyCompleted", CompletedAt = at,
            PermanentCompletion = new(at, new(7, 12, 99, 1, "Callough", "You remind me of my own little child.", true, 177, 82, 0, []) { ObservedAt = at }) });
        await runtime.TickSchedulerAsync();
        vm.Page = "Scheduler";
        window.Width = 1420; window.Height = 900;
        async Task Capture(string name)
        {
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            if (window.Width <= 900)
            {
                window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "schedulerBody").Offset = new Vector(0, 100000);
                await Task.Delay(150);
                window.UpdateLayout();
            }
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/" + name + ".png");
        }
        void Click(string caption) => window.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == caption)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Capture("callough-obtained");
        Click("Try for more marks");
        await Capture("callough-confirm");
        Click("Keep stopped");
        if (runtime.SchedulerState.Entries.Single().Status != "Completed" || runtime.SchedulerState.Entries.Single().CalloughRetryAfter is not null)
            throw new InvalidOperationException("Declining changed the completed schedule.");
        window.Width = 900; window.Height = 560;
        await Capture("callough-compact");
        Click("Try for more marks");
        await Capture("callough-confirm-compact");
        Click("Resume for more marks");
        for (int wait = 0; runtime.SchedulerState.Entries.Single().CalloughRetryAfter != at && wait < 200; wait++) await Task.Delay(50);
        var saved = runtime.SchedulerState.Entries.Single();
        if (saved.Status != "Armed" || saved.CalloughRetryAfter != at || runtime.SchedulerEnabled || journal.Load("Elara", "spare-a-stick").PermanentCompletion is null)
            throw new InvalidOperationException("More-mark consent was not saved safely.");
        await Capture("callough-approved-compact");
        File.WriteAllText("artifacts/screenshots/callough-review.json", JsonSerializer.Serialize(new
        {
            isolated = true, declinedStayedCompleted = true, approvedStayedDisabled = true,
            evidenceRetained = true, cooldownPreserved = saved.Due >= at.AddHours(8), clientsLaunched = false
        }));
    }
}
