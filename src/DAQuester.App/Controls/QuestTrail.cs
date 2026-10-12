using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.ViewModels;

namespace DAQuester.App.Controls;
// Each marker represents a recipe step. The shimmer indicates activity, never completion.
public sealed class QuestTrail : Control
{
    public static readonly StyledProperty<QuestProgress?> StepsProperty = AvaloniaProperty.Register<QuestTrail, QuestProgress?>(nameof(Steps));
    public static readonly StyledProperty<bool> IsRunningProperty = AvaloniaProperty.Register<QuestTrail, bool>(nameof(IsRunning));
    readonly DispatcherTimer timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(33)
    };
    readonly Stopwatch clock = Stopwatch.StartNew();
    static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#D9B879"));
    static readonly IBrush Track = new SolidColorBrush(Color.Parse("#2D3B4D"));
    static readonly IBrush Glow = new SolidColorBrush(Color.Parse("#554B37"));
    static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#0D131E"));
    static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#A0AEC0"));
    public QuestProgress? Steps { get => GetValue(StepsProperty); set => SetValue(StepsProperty, value); }
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }

    static QuestTrail() => AffectsRender<QuestTrail>(StepsProperty, IsRunningProperty);
    public QuestTrail() => timer.Tick += (_, _) => InvalidateVisual();
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRunningProperty || change.Property == IsVisibleProperty)
            UpdateTimer();
    }

    void UpdateTimer()
    {
        if (IsRunning && IsVisible && this.GetVisualRoot()is not null)
            timer.Start();
        else
            timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        if (Steps is not { Total: > 0 } steps || Bounds.Width < 20)
            return;
        double left = 10, right = Bounds.Width - 10, y = Bounds.Height / 2;
        double X(int step) => left + (right - left) * step / Math.Max(1, steps.Total - 1);
        double spacing = (right - left) / Math.Max(1, steps.Total - 1);
        double radius = Math.Min(6, spacing / 3);
        context.DrawLine(new Pen(Track, 2), new(left, y), new(right, y));
        if (steps.Completed > 0)
            context.DrawLine(new Pen(Gold, 2), new(left, y), new(X(Math.Min(steps.Completed, steps.Total - 1)), y));
        for (int index = 0; index < steps.Total; index++)
        {
            var center = new Point(X(index), y);
            bool completed = index < steps.Completed;
            bool current = steps.Current == index + 1;
            if (current)
            {
                double pulse = IsRunning ? (Math.Sin(clock.Elapsed.TotalSeconds * 3.6) + 1) / 2 : 0;
                context.DrawEllipse(Glow, null, center, radius + 2 + pulse * 2, radius + 2 + pulse * 2);
            }

            context.DrawEllipse(completed || current ? Gold : Track, new Pen(completed || current ? Gold : Muted, 1), center, radius, radius);
            if (spacing >= 18)
            {
                var number = new FormattedText((index + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, completed || current ? Ink : Muted);
                context.DrawText(number, new Point(center.X - number.Width / 2, center.Y - number.Height / 2));
            }
        }

        if (IsRunning && steps.Current > 0)
        {
            double phase = clock.Elapsed.TotalSeconds % 1.6 / 1.6;
            double start = X(Math.Max(0, steps.Current - 2)), end = X(steps.Current - 1);
            context.DrawEllipse(Gold, null, new(start + (end - start) * phase, y), 2, 2);
        }
    }
}
