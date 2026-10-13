using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;

public sealed class LegendMarkGlyph : Control
{
    public static readonly StyledProperty<int> IconProperty = AvaloniaProperty.Register<LegendMarkGlyph, int>(nameof(Icon));
    public static readonly StyledProperty<string> GameDirectoryProperty = AvaloniaProperty.Register<LegendMarkGlyph, string>(nameof(GameDirectory), "");
    static readonly object gate = new();
    static readonly Dictionary<int, SpritePixels?> images = [];
    static string loadedDirectory = "";
    static LegendMarkRenderer? renderer;
    WriteableBitmap? bitmap;
    long request;

    public int Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string GameDirectory { get => GetValue(GameDirectoryProperty); set => SetValue(GameDirectoryProperty, value); }

    public LegendMarkGlyph() { Width = 17; Height = 16; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Load();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Clear();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == GameDirectoryProperty)
        {
            Clear();
            Load();
        }
    }

    void Clear()
    {
        request++;
        bitmap?.Dispose();
        bitmap = null;
        InvalidateVisual();
    }

    async void Load()
    {
        if (this.GetVisualRoot() is null || GameDirectory.Length == 0)
            return;
        long version = ++request;
        string directory = GameDirectory;
        int icon = Icon;
        try
        {
            var pixels = await Task.Run(() =>
            {
                lock (gate)
                {
                    if (renderer is null || !directory.Equals(loadedDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        renderer = new(directory);
                        loadedDirectory = directory;
                        images.Clear();
                    }
                    if (!images.TryGetValue(icon, out var image))
                        images[icon] = image = renderer.Render(icon);
                    return image;
                }
            });
            if (version != request || this.GetVisualRoot() is null || pixels is null)
                return;
            bitmap?.Dispose();
            bitmap = new(new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using var locked = bitmap.Lock();
            for (int y = 0; y < pixels.Height; y++)
                Marshal.Copy(pixels.Rgba, y * pixels.Width * 4, locked.Address + y * locked.RowBytes, pixels.Width * 4);
            InvalidateVisual();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException or ArgumentException)
        {
            if (version != request)
                return;
            Clear();
            var runtime = DataContext switch
            {
                ViewModels.MainWindowViewModel window => window.Runtime,
                ViewModels.QuestItemVM quest => quest.Runtime,
                _ => null
            };
            runtime?.Log("Legend artwork: " + error);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        if (bitmap is not null)
            context.DrawImage(bitmap, new Rect(bitmap.Size), new Rect(Bounds.Size));
        else
        {
            using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 24, Bounds.Height / 24)))
                context.DrawGeometry(null, new Pen(Brushes.Peru, 1.5), LucideIcons.Get("ScrollText"));
        }
    }
}
