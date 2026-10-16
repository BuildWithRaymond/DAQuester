using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace DAQuester.App.Controls;

// Quests and tasks share original artwork inside the same circle, independent of native sprite size.
public sealed class QuestGlyph : Control
{
    public static readonly StyledProperty<string?> QuestIdProperty = AvaloniaProperty.Register<QuestGlyph, string?>(nameof(QuestId));
    public static readonly StyledProperty<string> GameDirectoryProperty = AvaloniaProperty.Register<QuestGlyph, string>(nameof(GameDirectory), "");
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextBlock.ForegroundProperty.AddOwner<QuestGlyph>();
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<QuestGlyph>();
    static readonly Lazy<Bitmap> appArtwork = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://DAQuester/Assets/daquester.png"));
        return new Bitmap(stream);
    });
    WriteableBitmap? bitmap;
    Bitmap? customBitmap;
    Rect source;
    bool creature;
    long request;

    public string? QuestId { get => GetValue(QuestIdProperty); set => SetValue(QuestIdProperty, value); }
    public string GameDirectory { get => GetValue(GameDirectoryProperty); set => SetValue(GameDirectoryProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    static QuestGlyph() => AffectsRender<QuestGlyph>(ForegroundProperty, BackgroundProperty);

    public QuestGlyph()
    {
        Width = 36;
        Height = 36;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadArtwork();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Clear();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QuestIdProperty || change.Property == GameDirectoryProperty)
        {
            Clear();
            LoadArtwork();
        }
    }

    void Clear()
    {
        request++;
        customBitmap = null;
        bitmap?.Dispose();
        bitmap = null;
        source = default;
        InvalidateVisual();
    }

    async void LoadArtwork()
    {
        if (this.GetVisualRoot() is null)
            return;
        if (QuestId is "awakening" or "peasantsfirststeps" or "peasants-first-steps" or "cryptclimb" or "crypt-climb")
        {
            customBitmap = appArtwork.Value;
            ToolTip.SetTip(this, "Custom DAQuester journey");
            InvalidateVisual();
            return;
        }
        long version = ++request;
        try
        {
            var (artwork, pixels) = await QuestArtworkProvider.LoadAsync(QuestId, GameDirectory);
            if (version != request || this.GetVisualRoot() is null)
                return;
            if (artwork is not null && pixels is not null)
            {
                creature = artwork.Creature;
                source = SpriteImageBounds.From(pixels);
                if (creature && source.Height > source.Width)
                    source = new(source.X, artwork.BottomCrop ? source.Bottom - source.Width : source.Y, source.Width, source.Width);
                bitmap = new(new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                using var locked = bitmap.Lock();
                for (int y = 0; y < pixels.Height; y++)
                    Marshal.Copy(pixels.Rgba, y * pixels.Width * 4, locked.Address + y * locked.RowBytes, pixels.Width * 4);
            }
            ToolTip.SetTip(this, pixels is not null ? artwork!.Label
                : "Select the game client in Settings to load game artwork.");
            InvalidateVisual();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException
            or OverflowException or ArgumentException or JsonException)
        {
            if (version != request)
                return;
            Clear();
            ToolTip.SetTip(this, "Game artwork unavailable. Check the client path in Settings.");
            var runtime = DataContext switch
            {
                ViewModels.MainWindowViewModel window => window.Runtime,
                ViewModels.QuestItemVM quest => quest.Runtime,
                ViewModels.TaskItemVM task => task.Runtime,
                _ => null
            };
            runtime?.Log("Quest artwork: " + error);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        double diameter = Math.Min(Bounds.Width, Bounds.Height) - 2;
        if (diameter <= 6)
            return;
        var circle = new Rect((Bounds.Width - diameter) / 2, (Bounds.Height - diameter) / 2, diameter, diameter);
        context.DrawEllipse(Background, new Pen(Foreground, 0.75), circle.Center, diameter / 2, diameter / 2);
        using (context.PushGeometryClip(new EllipseGeometry(circle)))
        {
            if (customBitmap is not null)
                context.DrawImage(customBitmap, new Rect(customBitmap.Size), circle.Deflate(2));
            else if (bitmap is not null && source.Width > 0 && source.Height > 0)
            {
                double limit = creature ? diameter * 1.05 : diameter - 5;
                double scale = Math.Min(limit / source.Width, limit / source.Height);
                var size = source.Size * scale;
                context.DrawImage(bitmap, source, new Rect((Bounds.Width - size.Width) / 2, (Bounds.Height - size.Height) / 2, size.Width, size.Height));
            }
            else
            {
                double scale = diameter / 36;
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(circle.Center.X - 12 * scale, circle.Center.Y - 12 * scale)))
                    context.DrawGeometry(null, new Pen(Foreground, 1.3 / scale), LucideIcons.Get("ScrollText"));
            }
        }
    }
}
