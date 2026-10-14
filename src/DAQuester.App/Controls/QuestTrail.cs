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
    static readonly Pen TrackPen = new(Track, 2);
    static readonly Pen GoldPen = new(Gold, 2);
    static readonly Pen GoldOutline = new(Gold, 1);
    static readonly Pen MutedOutline = new(Muted, 1);
    readonly List<(FormattedText Reached, FormattedText Pending)> labels = [];
    public QuestProgress? Steps
    {
        get => GetValue(StepsProperty); set => SetValue(StepsProperty, value);
    }
    public bool IsRunning
    {
        get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value);
    }

    static QuestTrail()
    {
        AffectsRender<QuestTrail>(StepsProperty, IsRunningProperty);
        AffectsMeasure<QuestTrail>(StepsProperty);
    }
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
        if (IsRunning && IsVisible && this.GetVisualRoot() is not null)
            timer.Start();
        else
            timer.Stop();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = QuestTrailLayout.Create(Steps?.Total ?? 0, availableSize.Width);
        return new Size(layout.Width, layout.Height);
    }

    public override void Render(DrawingContext context)
    {
        if (Steps is not { Total: > 0 } steps || Bounds.Width < 24)
            return;
        var layout = QuestTrailLayout.Create(steps.Total, Bounds.Width);
        const double radius = QuestTrailLayout.Radius;

        // Typography does not change with the pulse. Shape each number once, rather
        // than allocating and measuring every label on every animation frame.
        while (labels.Count < steps.Total)
        {
            string text = (labels.Count + 1).ToString(CultureInfo.InvariantCulture);
            FormattedText Label(IBrush brush) => new(text, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, brush);
            labels.Add((Label(Ink), Label(Muted)));
        }

        // Each row reads left to right. Do not draw a diagonal across a row break:
        // that would cut through other stages and obscure the current marker.
        for (int index = 1; index < steps.Total; index++)
            if (index % layout.Columns != 0)
                context.DrawLine(index <= steps.Completed ? GoldPen : TrackPen, layout.Center(index - 1), layout.Center(index));

        for (int index = 0; index < steps.Total; index++)
        {
            var center = layout.Center(index);
            bool completed = index < steps.Completed;
            bool current = steps.Current == index + 1;
            if (current)
            {
                double pulse = IsRunning ? (Math.Sin(clock.Elapsed.TotalSeconds * 3.6) + 1) / 2 : 0;
                context.DrawEllipse(Glow, null, center, radius + 2 + pulse * 2, radius + 2 + pulse * 2);
            }

            context.DrawEllipse(completed || current ? Gold : Track, completed || current ? GoldOutline : MutedOutline, center, radius, radius);
            var number = completed || current ? labels[index].Reached : labels[index].Pending;
            context.DrawText(number, new Point(center.X - number.Width / 2, center.Y - number.Height / 2));
        }

        if (IsRunning && steps.Current > 1 && steps.Current <= steps.Total && (steps.Current - 1) % layout.Columns != 0)
        {
            double phase = clock.Elapsed.TotalSeconds % 1.6 / 1.6;
            var start = layout.Center(steps.Current - 2);
            var end = layout.Center(steps.Current - 1);
            context.DrawEllipse(Gold, null, new(start.X + (end.X - start.X) * phase, end.Y), 2, 2);
        }
    }
}

// Keep markers at their normal 12 px diameter. Available width changes the number
// of columns, never the dot size; measurement reserves space for all resulting rows.
internal readonly record struct QuestTrailLayout(int Count, int Columns, double Width)
{
    public const double Radius = 6;
    const double Inset = 12, Pitch = 24;
    public int Rows => Count == 0 ? 0 : (Count + Columns - 1) / Columns;
    public double Height => Rows * Pitch;

    public static QuestTrailLayout Create(int count, double width)
    {
        count = Math.Max(0, count);
        if (!double.IsFinite(width))
            width = Math.Max(1, count) * Pitch;
        width = Math.Max(0, width);
        int columns = Math.Min(count, Math.Max(1, (int)Math.Floor(Math.Max(0, width - Inset * 2) / Pitch) + 1));
        return new(count, columns, width);
    }

    public Point Center(int index)
    {
        // A partial final row keeps the same columns, so numbering remains easy to scan.
        double x = Columns <= 1 ? Width / 2 : Inset + (index % Columns) * (Width - Inset * 2) / (Columns - 1);
        return new(x, Inset + (index / Columns) * Pitch);
    }
}
