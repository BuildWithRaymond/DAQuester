using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DAQuester.App.ViewModels;
using DAQuester.App.Views;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    static async Task CaptureDaydreamerAsync(Window window, MainWindowViewModel vm)
    {
        var runtime = vm.Runtime;
        runtime.SetPreviewQueue(new() { Character = "Agent", State = "Idle" });
        var current = runtime.Snapshot("Agent") with { Revision = 100, Class = null, Level = null, Hp = 1000,
            Registration = new(runtime.Snapshot("Agent").Generation, 80, 3, "This account is on auto-renewal", DateTimeOffset.UtcNow) };
        runtime.SetPreviewSnapshot(current);
        var at = DateTimeOffset.UtcNow.AddHours(-1);
        var receipt = new ServerNotice(current.Generation, 90, 3, "This is your 12th consecutive Login!", at);
        var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
        var job = new QuestJob { Character = "Agent", Quest = DaydreamerEvidence.Id, Step = "complete", CompletedAt = at,
            NextRun = at.AddHours(24), Daydreamer = new() { Streak = 12, LastCountReceipt = receipt,
                LastAcknowledgement = new(at, Status: receipt), LastSuccessAt = at, NextCheckInAt = at.AddHours(24) } };
        await journal.SaveAsync(job);
        vm.Page = "Planner";
        vm.Filter = "All";
        vm.Search = "Daydreamer";
        vm.Refresh();
        vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == DaydreamerEvidence.Id);
        window.FindControl<Expander>("questDetails")!.IsExpanded = true;

        async Task Capture(Window target, string name)
        {
            await Task.Delay(250);
            Dispatcher.UIThread.RunJobs();
            target.UpdateLayout();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)target.ClientSize.Width, (int)target.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(target);
            bitmap.Save("artifacts/screenshots/" + name + ".png");
        }

        window.Width = 1420; window.Height = 900;
        window.FindControl<CheckBox>("daydreamerContinue")!.Focus();
        await Capture(window, "daydreamer-details");
        window.Width = 900; window.Height = 560;
        await Task.Delay(100);
        window.UpdateLayout();
        window.FindControl<ScrollViewer>("libraryScroll")!.Offset = new Vector(0, 10000);
        await Capture(window, "daydreamer-details-compact");
        window.Width = 1420; window.Height = 900;
        window.FindControl<ScrollViewer>("libraryScroll")!.Offset = default;
        window.FindControl<ScrollViewer>("questDetailsScroll")!.Offset = new Vector(0, 10000);
        await Capture(window, "daydreamer-registration-warning");

        var goal = receipt with { Text = "This is your 30th consecutive Login!" };
        await journal.SaveAsync(job with { Daydreamer = job.Daydreamer! with { Streak = 30, LastCountReceipt = goal,
            FirstThirtyReceipt = goal, LastAcknowledgement = new(at, Status: goal) } });
        runtime.InvalidatePresentation();
        await runtime.SetRepeatAsync("Agent", DaydreamerEvidence.Id, true);
        vm.Refresh();
        window.Width = 1420; window.Height = 900;
        window.FindControl<ScrollViewer>("questDetailsScroll")!.Offset = default;
        await Capture(window, "daydreamer-continuation");
        var setup = new ActivityRunDialog(runtime, "Agent", runtime.Recipes[DaydreamerEvidence.Id], job);
        setup.Show(window);
        await Capture(setup, "daydreamer-schedule-setup");
        setup.Close();
    }
}
