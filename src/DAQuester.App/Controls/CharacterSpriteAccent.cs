using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace DAQuester.App.Controls;

// Reuses the foreground bitmap, so the accent always shows the same animation frame.
public sealed class CharacterSpriteAccent : Control
{
    public static readonly StyledProperty<CharacterSpritePreview?> SourceProperty = AvaloniaProperty.Register<CharacterSpriteAccent, CharacterSpritePreview?>(nameof(Source));
    CharacterSpritePreview? subscribedSource;

    public CharacterSpritePreview? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Unsubscribe();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
            Subscribe();
    }

    void Subscribe()
    {
        Unsubscribe();
        if (this.GetVisualRoot() is not null)
        {
            subscribedSource = Source;
            if (subscribedSource is not null)
                subscribedSource.FrameChanged += FrameChanged;
        }
        InvalidateVisual();
    }

    void Unsubscribe()
    {
        if (subscribedSource is not null)
            subscribedSource.FrameChanged -= FrameChanged;
        subscribedSource = null;
    }

    void FrameChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Source?.DrawAccent(context, Bounds.Size);
    }
}
