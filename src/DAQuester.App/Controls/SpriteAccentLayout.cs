using Avalonia;

namespace DAQuester.App.Controls;

internal sealed class SpriteAccentLayout
{
    Point? anchor;

    public void Reset() => anchor = null;

    public Rect Destination(Rect content, Size bitmap, Rect foreground, Size preview, Size area, double verticalOffset = 0)
    {
        const double enlargement = 5.5;
        // Keep the canvas center and the first frame's foot reference fixed. A
        // moving weapon or limb may change the crop, but must not move the body.
        anchor ??= new Point(bitmap.Width / 2, content.Bottom);
        var origin = anchor.Value;
        double scale = foreground.Width / bitmap.Width;
        double center = area.Width - preview.Width + foreground.Left + origin.X * scale;
        double baseline = area.Height - preview.Height + foreground.Top + origin.Y * scale;
        var size = content.Size * enlargement;
        return new(center + (content.Left - origin.X) * enlargement,
            baseline + (content.Top - origin.Y) * enlargement + verticalOffset, size.Width, size.Height);
    }
}
