using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.Core;
using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;
// Loading and compositing run off the UI thread. A stale character/generation cannot publish a frame.
public sealed partial class CharacterSpritePreview : Control
{
    public static readonly StyledProperty<CharacterSnapshot?> SnapshotProperty = AvaloniaProperty.Register<CharacterSpritePreview, CharacterSnapshot?>(nameof(Snapshot));
    public static readonly StyledProperty<string> GameDirectoryProperty = AvaloniaProperty.Register<CharacterSpritePreview, string>(nameof(GameDirectory), "");
    public static readonly StyledProperty<bool> IsPortraitProperty = AvaloniaProperty.Register<CharacterSpritePreview, bool>(nameof(IsPortrait));
    public static readonly StyledProperty<CharacterAppearance?> RememberedAppearanceProperty = AvaloniaProperty.Register<CharacterSpritePreview, CharacterAppearance?>(nameof(RememberedAppearance));
    readonly DispatcherTimer timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(33)
    };
    CharacterSpriteRenderer? renderer;
    WriteableBitmap? bitmap;
    Rect contentBounds;
    (CharacterAppearance Look, SpritePose Pose)? drawn;
    string loadedDirectory = "", failedDirectory = "";
    bool busy;
    long requestVersion;
    public CharacterSnapshot? Snapshot { get => GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }
    public string GameDirectory { get => GetValue(GameDirectoryProperty); set => SetValue(GameDirectoryProperty, value); }
    public bool IsPortrait { get => GetValue(IsPortraitProperty); set => SetValue(IsPortraitProperty, value); }
    public CharacterAppearance? RememberedAppearance { get => GetValue(RememberedAppearanceProperty); set => SetValue(RememberedAppearanceProperty, value); }
    internal event EventHandler? FrameChanged;

    public CharacterSpritePreview()
    {
        Width = 112;
        Height = 112;
        ToolTip.SetTip(this, "Original game artwork · your character appears here once loaded.");
        timer.Tick += (_, _) => UpdateFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        timer.Start();
        UpdateFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        Clear();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SnapshotProperty || change.Property == GameDirectoryProperty || change.Property == IsPortraitProperty || change.Property == RememberedAppearanceProperty)
        {
            var before = change.OldValue as CharacterSnapshot;
            var after = Snapshot;
            bool characterChanged = before?.Connected != after?.Connected || before?.Generation != after?.Generation || before?.Id != after?.Id || before?.Appearance != after?.Appearance;
            if (change.Property == GameDirectoryProperty || change.Property == IsPortraitProperty || change.Property == RememberedAppearanceProperty || characterChanged)
                Clear();
            if (GameDirectory.Length == 0)
                ToolTip.SetTip(this, "Select the game client in Settings to load original artwork.");
            else if (CharacterPreviewAppearance.From(Snapshot, RememberedAppearance).IsDefault)
                ToolTip.SetTip(this, "Original game artwork · default outfit until your character loads.");
            else if (CharacterPreviewAppearance.From(Snapshot, RememberedAppearance).IsRemembered)
                ToolTip.SetTip(this, "Original game artwork · last observed outfit.");
            else if (Snapshot?.Appearance?.Hidden == true)
                ToolTip.SetTip(this, "This character is hidden.");
            UpdateFrame();
        }
    }

    void Clear()
    {
        requestVersion++;
        bitmap?.Dispose();
        bitmap = null;
        contentBounds = default;
        drawn = null;
        InvalidateVisual();
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    async void UpdateFrame()
    {
        var snapshot = Snapshot;
        var preview = CharacterPreviewAppearance.From(snapshot, RememberedAppearance);
        var look = preview.Look;
        if (busy || this.GetVisualRoot()is null || !IsEffectivelyVisible || look.Hidden || GameDirectory.Length == 0)
            return;
        string directory = GameDirectory;
        if (failedDirectory == directory)
            return;
        bool portrait = IsPortrait;
        var pose = portrait ? new SpritePose("01", 5, false, true) : SpritePose.From(preview.Motion, DateTimeOffset.UtcNow);
        if (!portrait && look.Monster is not null)
            pose = pose with
            {
                Frame = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 75 % 100000)
            };
        if (drawn == (look, pose))
            return;
        busy = true;
        long version = requestVersion;
        try
        {
            var pixels = await Task.Run(() =>
            {
                if (renderer is null || loadedDirectory != directory)
                {
                    renderer = new(directory);
                    loadedDirectory = directory;
                }

                return portrait ? renderer.RenderPortrait(look) : renderer.Render(look, pose, preview.Motion);
            });
            if (version != requestVersion || Snapshot?.Generation != snapshot?.Generation || Snapshot?.Id != snapshot?.Id || this.GetVisualRoot()is null)
                return;
            bitmap?.Dispose();
            bitmap = null;
            contentBounds = default;
            if (pixels is not null)
            {
                contentBounds = SpriteImageBounds.From(pixels);
                bitmap = new(new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                using var locked = bitmap.Lock();
                for (int y = 0; y < pixels.Height; y++)
                    Marshal.Copy(pixels.Rgba, y * pixels.Width * 4, locked.Address + y * locked.RowBytes, pixels.Width * 4);
            }

            drawn = (look, pose);
            ToolTip.SetTip(this, pixels is null ? "Game artwork unavailable. Check the client path in Settings." : preview.IsDefault ? "Original game artwork · default outfit until your character loads." : preview.IsRemembered ? "Original game artwork · last observed outfit." : "Original game sprite · observed actions");
            InvalidateVisual();
            FrameChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)when (e is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException or ArgumentException)
        {
            failedDirectory = directory;
            Clear();
            ToolTip.SetTip(this, "Game artwork unavailable. Check the client path in Settings.");
            var runtime = DataContext switch
            {
                ViewModels.MainWindowViewModel window => window.Runtime,
                ViewModels.CharacterItemVM character => character.Runtime,
                ViewModels.HistoryItemVM history => history.Runtime,
                _ => null
            };
            runtime?.Log("Character sprite preview: " + e);
        }
        finally
        {
            busy = false;
        }
    }

}
