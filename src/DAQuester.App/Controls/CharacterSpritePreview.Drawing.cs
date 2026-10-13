using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace DAQuester.App.Controls;

public sealed partial class CharacterSpritePreview
{
    public override void Render(DrawingContext context)
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        if (IsPortrait)
        {
            DrawPortrait(context);
            return;
        }

        if (bitmap is null)
        {
            var icon = LucideIcons.Get("Users");
            using (context.PushTransform(Matrix.CreateScale(1.1, 1.1) * Matrix.CreateTranslation(42, 24)))
                context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#607083")), 1.3), icon);
            return;
        }

        context.DrawImage(bitmap, new Rect(bitmap.Size), SpriteDestination());
    }

    Rect SpriteDestination()
    {
        double scale = Math.Min(1, Math.Min(Bounds.Width / bitmap!.Size.Width, (Bounds.Height - 4) / bitmap.Size.Height));
        var size = bitmap.Size * scale;
        return new((Bounds.Width - size.Width) / 2, Bounds.Height - size.Height - 4, size.Width, size.Height);
    }

    internal Point CompletionAnchor
    {
        get
        {
            if (IsPortrait || bitmap is null || contentBounds.Height <= 0)
                return new(Bounds.Width / 2, Bounds.Height - 4);
            var destination = SpriteDestination();
            return new(destination.Center.X, destination.Y + contentBounds.Bottom * destination.Height / bitmap.Size.Height);
        }
    }

    internal void DrawAccent(DrawingContext context, Size area)
    {
        if (bitmap is null || contentBounds.Width <= 0 || contentBounds.Height <= 0)
            return;
        var destination = accentLayout.Destination(contentBounds, bitmap.Size, SpriteDestination(), Bounds.Size, area);
        context.DrawImage(bitmap, contentBounds, destination);
    }

    void DrawPortrait(DrawingContext context)
    {
        double diameter = Math.Min(Bounds.Width, Bounds.Height) - 2;
        if (diameter <= 6)
            return;
        var circle = new Rect((Bounds.Width - diameter) / 2, (Bounds.Height - diameter) / 2, diameter, diameter);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#1C2838")), new Pen(new SolidColorBrush(Color.Parse("#665739")), 1), circle.Center, diameter / 2, diameter / 2);
        using (context.PushGeometryClip(new EllipseGeometry(circle)))
        {
            if (bitmap is not null && contentBounds.Width > 0 && contentBounds.Height > 0)
            {
                double scale = Math.Min((diameter - 6) / contentBounds.Width, (diameter - 6) / contentBounds.Height);
                var size = contentBounds.Size * scale;
                context.DrawImage(bitmap, contentBounds, new Rect((Bounds.Width - size.Width) / 2, (Bounds.Height - size.Height) / 2, size.Width, size.Height));
            }
            else
            {
                var icon = LucideIcons.Get("Users");
                double scale = diameter / 36;
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(Bounds.Width / 2 - 12 * scale, Bounds.Height / 2 - 12 * scale)))
                    context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#A0AEC0")), 1.3 / scale), icon);
            }
        }
    }
}
