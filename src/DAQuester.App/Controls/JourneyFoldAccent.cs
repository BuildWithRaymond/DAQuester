using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DAQuester.App.Controls;

// A brief glint traces the closing scroll edge; it conveys motion, never quest progress.
public sealed class JourneyFoldAccent : Control
{
    static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#D9B879"));
    public double Amount { get; set; }
    public double Intensity { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Intensity <= .001 || Bounds.Width < 32)
            return;
        double width = Bounds.Width - 32, y = Bounds.Height - 2;
        double head = 16 + width * Amount;
        using (context.PushOpacity(Intensity * .3))
            context.DrawRectangle(null, new Pen(Gold, 1), new Rect(1, 1, Bounds.Width - 2, Bounds.Height - 2), 8, 8);
        for (int index = 0; index < 24; index++)
        {
            double x = 16 + width * index / 24;
            double glow = Math.Max(0, 1 - Math.Abs(x - head) / (width * .3));
            using (context.PushOpacity(Intensity * glow))
            {
                context.DrawLine(new Pen(Gold, 5), new(x, y + 1), new(x + width / 24 + 1, y + 1));
                context.DrawLine(new Pen(Gold, 1), new(x, y - 2), new(x + width / 24 + 1, y - 2));
            }
        }
        using (context.PushOpacity(Intensity * .8))
        {
            context.DrawLine(new Pen(Gold, 1), new(head - 9, y - 5), new(head + 9, y - 5));
            context.DrawLine(new Pen(Gold, 1), new(head, y - 11), new(head, y + 1));
            context.DrawEllipse(Gold, null, new(head, y - 5), 2, 2);
        }
    }
}
