using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using DAQuester.App.Controls;

namespace DAQuester.App.Views;

public partial class MainWindow
{
    readonly JourneyMotion journeyMotion = new();
    readonly Stopwatch journeyClock = Stopwatch.StartNew();
    readonly DispatcherTimer journeyTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    readonly TranslateTransform expandedShift = new();
    readonly TranslateTransform compactShift = new();
    readonly ScaleTransform compactScale = new();
    readonly RotateTransform toggleTurn = new();

    void InitializeJourneyMotion()
    {
        this.FindControl<Grid>("journeyLayout")!.RenderTransform = expandedShift;
        var mini = this.FindControl<Grid>("journeyMiniBar")!;
        mini.RenderTransform = new TransformGroup { Children = [compactScale, compactShift] };
        mini.RenderTransformOrigin = new RelativePoint(.5, .5, RelativeUnit.Relative);
        var chevron = this.FindControl<LineIcon>("journeyChevron")!;
        chevron.RenderTransform = toggleTurn;
        chevron.RenderTransformOrigin = new RelativePoint(.5, .5, RelativeUnit.Relative);
        journeyMotion.Target(viewModel.JourneyCollapsed, journeyClock.Elapsed.TotalMilliseconds, false);
        journeyTimer.Tick += (_, _) => ApplyJourneyMotion();
        Closed += (_, _) => journeyTimer.Stop();
    }

    void AnimateJourney()
    {
        PreserveJourneyFocus();
        journeyMotion.Target(viewModel.JourneyCollapsed, journeyClock.Elapsed.TotalMilliseconds, MotionPreferences.Enabled);
        ApplyJourneyMotion();
        if (journeyMotion.Active(journeyClock.Elapsed.TotalMilliseconds))
            journeyTimer.Start();
    }

    void ApplyJourneyMotion() => ApplyJourneyMotion(journeyClock.Elapsed.TotalMilliseconds);

    void ApplyJourneyMotion(double now)
    {
        double amount = journeyMotion.Position(now);
        bool active = journeyMotion.Active(now);
        double expandedHeight = Bounds.Width < 1160 ? 300 : Math.Clamp((Bounds.Height - 150) * .25, 270, 300);
        this.FindControl<Border>("journeyCard")!.Height = expandedHeight + (80 - expandedHeight) * amount;
        this.FindControl<Grid>("plannerLayout")!.MinHeight = 620 - 260 * amount;
        var expanded = this.FindControl<Grid>("journeyLayout")!;
        expanded.Height = expandedHeight;
        expanded.IsVisible = amount < 1;
        expanded.IsHitTestVisible = !active && !viewModel.JourneyCollapsed;
        KeyboardNavigation.SetTabNavigation(expanded, active ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        expanded.Opacity = 1 - SmoothStep(.02, .42, amount);
        expandedShift.Y = -28 * amount;
        var mini = this.FindControl<Grid>("journeyMiniBar")!;
        mini.IsVisible = amount > 0;
        mini.IsHitTestVisible = !active && viewModel.JourneyCollapsed;
        KeyboardNavigation.SetTabNavigation(mini, active ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        mini.Opacity = SmoothStep(.18, .72, amount);
        compactShift.Y = 18 * (1 - amount);
        double arrival = SmoothStep(.5, 1, amount);
        compactScale.ScaleX = compactScale.ScaleY = .94 + .06 * (arrival + .45 * Math.Sin(Math.PI * arrival));
        toggleTurn.Angle = 180 * amount;
        this.FindControl<Button>("journeyToggle")!.Margin = new Thickness(10, 12 + 12 * amount, 12, 12);
        var fold = this.FindControl<JourneyFoldAccent>("journeyFold")!;
        fold.Amount = amount;
        fold.Intensity = journeyMotion.Activity(now);
        fold.InvalidateVisual();
        if (!active)
            journeyTimer.Stop();
    }

    static double SmoothStep(double start, double end, double value)
    {
        double t = Math.Clamp((value - start) / (end - start), 0, 1);
        return t * t * (3 - 2 * t);
    }

}
