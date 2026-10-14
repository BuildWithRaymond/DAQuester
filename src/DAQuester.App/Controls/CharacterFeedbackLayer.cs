using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.Core;

namespace DAQuester.App.Controls;

// Local feedback stays above the sprite and never changes quest state or counters.
public sealed class CharacterFeedbackLayer : Control
{
    public static readonly StyledProperty<CharacterSnapshot?> SnapshotProperty = AvaloniaProperty.Register<CharacterFeedbackLayer, CharacterSnapshot?>(nameof(Snapshot));
    public static readonly StyledProperty<double> AnchorFromRightProperty = AvaloniaProperty.Register<CharacterFeedbackLayer, double>(nameof(AnchorFromRight));
    public CharacterSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value);
    }
    public double AnchorFromRight
    {
        get => GetValue(AnchorFromRightProperty); set => SetValue(AnchorFromRightProperty, value);
    }
    readonly CharacterFeedbackCursor cursor = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly List<(CharacterFeedback Event, double Start)> effects = [];
    (string? Name, uint? Id, long Generation)? displayedIdentity;
    static readonly IBrush gold = new SolidColorBrush(Color.Parse("#D9B879"));
    static readonly IBrush shadow = new SolidColorBrush(Color.Parse("#0D131E"));
    public CharacterFeedbackLayer()
    {
        IsHitTestVisible = false;
        timer.Tick += (_, _) =>
        {
            Poll();
            effects.RemoveAll(e => clock.Elapsed.TotalMilliseconds - e.Start >= 1800);
            InvalidateVisual();
            if (effects.Count == 0)
            {
                timer.Stop();
                AutomationProperties.SetName(this, "");
            }
        };
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        cursor.Consume(Snapshot, DateTimeOffset.UtcNow);
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        effects.Clear();
        cursor.Consume(null, DateTimeOffset.UtcNow);
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SnapshotProperty && this.GetVisualRoot() is not null)
            Poll();
    }
    void Poll()
    {
        var identity = Snapshot is { } s ? (s.Name, s.Id, s.Generation) : ((string?, uint?, long)?)null;
        if (displayedIdentity != identity)
            effects.Clear();
        displayedIdentity = identity;
        if (!IsEffectivelyVisible)
        {
            cursor.Consume(Snapshot, DateTimeOffset.UtcNow);
            effects.Clear();
            return;
        }
        if (Snapshot is null || !Snapshot.Connected || effects.Any(e => e.Event.Generation != Snapshot.Generation))
            effects.Clear();
        foreach (var feedback in cursor.Consume(Snapshot, DateTimeOffset.UtcNow))
        {
            effects.Add((feedback, clock.Elapsed.TotalMilliseconds));
            if (effects.Count > 5)
                effects.RemoveAt(0);
            AutomationProperties.SetName(this, Label(feedback));
            timer.Start();
        }
    }
    static string Label(CharacterFeedback e) => e.Kind == "Level" ? $"Level {e.Value}!" : $"+{e.Value:N0} XP" + (e.Kind == "Kill" ? " · Kill" : "");
    public override void Render(DrawingContext context)
    {
        // Feedback is presentation only. It consumes observed events without
        // creating quest receipts; reduced-motion mode keeps the same readable text.
        base.Render(context);
        if (Snapshot is null || !Snapshot.Connected)
            return;
        bool animated = MotionPreferences.Enabled;
        for (int i = 0; i < effects.Count; i++)
        {
            var (feedback, start) = effects[i];
            double t = Math.Clamp((clock.Elapsed.TotalMilliseconds - start) / 1800, 0, 1);
            if (t >= 1)
                continue;
            double opacity = animated ? Math.Min(1, (1 - t) * 4) : 1;
            double rise = animated ? 22 * t : 0;
            bool level = feedback.Kind == "Level";
            double x = AnchorFromRight > 0 ? Bounds.Width - AnchorFromRight : Bounds.Width / 2;
            double y = 28 + i * 18 - rise;
            var text = new FormattedText(Label(feedback), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold), level ? 15 : 11, gold);
            using (context.PushOpacity(opacity))
            {
                if (level && animated)
                    for (int ray = 0; ray < 10; ray++)
                    {
                        double angle = ray * Math.PI / 5 - Math.PI / 2;
                        double radius = 14 + 24 * t;
                        var a = new Point(x + Math.Cos(angle) * radius, y + Math.Sin(angle) * radius);
                        var b = new Point(x + Math.Cos(angle) * (radius + 7), y + Math.Sin(angle) * (radius + 7));
                        context.DrawLine(new Pen(gold, 1.5), a, b);
                    }
                double left = Math.Clamp(x - text.Width / 2, 2, Math.Max(2, Bounds.Width - text.Width - 2));
                context.DrawRectangle(shadow, null, new Rect(left - 3, y - 3, text.Width + 6, text.Height + 6), 4, 4);
                context.DrawText(text, new Point(left, y));
            }
        }
    }
}
