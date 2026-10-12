using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DAQuester.App.Controls;
// All interface icons share Lucide geometry and the same stroke treatment.
public class LineIcon : Control
{
    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<LineIcon, string>(nameof(Icon), "ScrollText");
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextBlock.ForegroundProperty.AddOwner<LineIcon>();
    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    static LineIcon()
    {
        AffectsRender<LineIcon>(IconProperty, ForegroundProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 2 || Bounds.Height <= 2)
            return;
        var geometry = LucideIcons.Get(Icon);
        var box = geometry.Bounds;
        double scale = Math.Min((Bounds.Width - 2) / Math.Max(1, box.Width), (Bounds.Height - 2) / Math.Max(1, box.Height));
        var transform = Matrix.CreateTranslation(-box.X, -box.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation((Bounds.Width - box.Width * scale) / 2, (Bounds.Height - box.Height * scale) / 2);
        using (context.PushTransform(transform))
            context.DrawGeometry(null, new Pen(Foreground, 1.6 / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
    }
}
