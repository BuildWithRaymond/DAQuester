using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;

public sealed partial class CompletionEffectsLayer
{
    static readonly IBrush NoticeSurface = new SolidColorBrush(Color.Parse("#151E2B"));
    static readonly IBrush NoticeLine = new SolidColorBrush(Color.Parse("#2D3B4D"));
    static readonly IBrush Text = new SolidColorBrush(Color.Parse("#EDF0F3"));
    static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#D9B879"));

    IEnumerable<CharacterSpritePreview> VisibleAvatars(string character) => this.GetVisualRoot() is Window window
        ? window.GetVisualDescendants().OfType<CharacterSpritePreview>().Where(preview => preview.IsEffectivelyVisible
            && preview.Bounds.Width > 0 && preview.Snapshot?.Appearance?.Hidden != true
            && character.Equals(preview.Snapshot?.Name, StringComparison.OrdinalIgnoreCase))
        : [];

    public override void Render(DrawingContext context)
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        double now = clock.Elapsed.TotalMilliseconds;
        foreach (var burst in bursts)
        {
            if (now >= burst.End)
                continue;
            double localAge = now - burst.Start;
            if (localAge < 0 || localAge >= (burst.Animated ? CelebrationDuration : 3000))
                continue;
            var avatars = VisibleAvatars(burst.Result.Character).ToArray();
            if (burst.Animated && spiral is { } blue)
                foreach (var avatar in avatars)
                    if (avatar.TranslatePoint(avatar.CompletionAnchor, this) is { } anchor)
                        DrawEffect(context, blue, localAge, anchor, avatar.IsPortrait ? avatar.Bounds.Width / 75 : Math.Min(1, avatar.Bounds.Width / 112));
            if (avatars.Length == 0 || !burst.Animated || spiral is null)
                DrawNotice(context, burst, localAge);
        }
    }

    static void DrawEffect(DrawingContext context, TextureClip clip, double age, Point anchor, double scale)
    {
        int tick = (int)(age / EffectSpriteClip.FrameMilliseconds);
        if (age < 0 || tick >= clip.Source.Sequence.Length)
            return;
        int frame = clip.Source.Sequence[tick];
        var image = clip.Images[frame];
        var origin = clip.Source.Frames[frame];
        context.DrawImage(image, new Rect(image.Size), new Rect(anchor.X - origin.OriginX * scale,
            anchor.Y - origin.OriginY * scale, image.Size.Width * scale, image.Size.Height * scale));
    }

    void DrawNotice(DrawingContext context, Burst burst, double age)
    {
        double duration = burst.Animated ? CelebrationDuration : 3000;
        double opacity = Math.Clamp(Math.Min(age + 80, duration - age) / 180, 0, 1);
        double width = Math.Min(370, Bounds.Width - 32);
        var panel = new Rect(Bounds.Width - width - 20, Bounds.Height - 146, width, 112);
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(NoticeSurface, new Pen(NoticeLine, 1), panel, 8, 8);
            if (burst.Sprite is { } sprite)
            {
                var destination = new Rect(panel.X + 4, panel.Bottom - sprite.Size.Height - 4, sprite.Size.Width, sprite.Size.Height);
                context.DrawImage(sprite, new Rect(sprite.Size), destination);
                if (burst.Animated && spiral is { } blue)
                    DrawEffect(context, blue, age, new(destination.Center.X, destination.Y + burst.SpriteBounds.Bottom), 1);
            }
            double textX = panel.X + (burst.Sprite is null ? 16 : 120);
            var title = Label(burst.Result.Character + (burst.Result.Status == "AttemptCompleted" ? " · attempt complete" : " · quest complete"), 12, Gold);
            title.MaxTextWidth = panel.Right - textX - 12;
            var quest = Label(burst.QuestName, 13, Text);
            quest.MaxTextWidth = panel.Right - textX - 12;
            context.DrawText(title, new(textX, panel.Y + 30));
            context.DrawText(quest, new(textX, panel.Y + 54));
        }
    }

    static FormattedText Label(string text, double size, IBrush brush) => new(text, CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush);

    static WriteableBitmap Bitmap(SpritePixels pixels)
    {
        var bitmap = new WriteableBitmap(new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var target = bitmap.Lock();
        for (int y = 0; y < pixels.Height; y++)
            Marshal.Copy(pixels.Rgba, y * pixels.Width * 4, target.Address + y * target.RowBytes, pixels.Width * 4);
        return bitmap;
    }
}
