using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DAQuester.App.Services;
using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;

// A presentation overlay. Its clock never affects observations, checkpoints or commands.
public sealed partial class CompletionEffectsLayer : Control
{
    sealed record Burst(QuestCompletion Result, string QuestName, double Start, bool Animated, WriteableBitmap? Sprite, Rect SpriteBounds)
    {
        public double End => Start + (Animated ? CelebrationDuration : 3000);
    }
    sealed class TextureClip(EffectSpriteClip source) : IDisposable
    {
        public EffectSpriteClip Source { get; } = source;
        public WriteableBitmap[] Images { get; } = source.Frames.Select(frame => Bitmap(frame.Pixels)).ToArray();
        public void Dispose()
        {
            foreach (var image in Images)
                image.Dispose();
        }
    }
    const int CelebrationDuration = 1600;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly List<Burst> bursts = [];
    DesktopRuntime? runtime;
    TextureClip? spiral;
    Task? loading;
    string artworkDirectory = "";
    int assetVersion;
    double nextNotice;

    public CompletionEffectsLayer()
    {
        IsHitTestVisible = false;
        timer.Tick += (_, _) =>
        {
            double now = clock.Elapsed.TotalMilliseconds;
            foreach (var burst in bursts.Where(b => now >= b.End).ToArray())
            {
                burst.Sprite?.Dispose();
                bursts.Remove(burst);
            }
            InvalidateVisual();
            if (bursts.Count == 0)
            {
                timer.Stop();
                AutomationProperties.SetName(this, "");
            }
        };
    }

    internal void Connect(DesktopRuntime owner)
    {
        Disconnect();
        runtime = owner;
        owner.QuestCompleted += Completed;
        owner.Changed += ArtworkChanged;
        ArtworkChanged();
    }

    void Completed(QuestCompletion result) => _ = PlayAsync(result);
    void ArtworkChanged() => _ = PrepareArtworkAsync();

    internal Task PrepareArtworkAsync()
    {
        var owner = runtime;
        if (owner is null)
            return Task.CompletedTask;
        string directory = Path.GetDirectoryName(owner.Settings.ClientPath) ?? "";
        if (directory != artworkDirectory)
        {
            ClearArtwork();
            artworkDirectory = directory;
        }
        if (directory.Length == 0 || !MotionPreferences.Enabled)
            return Task.CompletedTask;
        return loading ??= LoadArtworkAsync(owner, directory, assetVersion);
    }

    async Task LoadArtworkAsync(DesktopRuntime owner, string directory, int version)
    {
        try
        {
            var clips = await Task.Run(() =>
            {
                var renderer = new EffectSpriteRenderer(directory);
                return renderer.Read(50);
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != assetVersion || runtime != owner)
                    return;
                spiral = clips is { } blue ? new(blue) : null;
                if (spiral is null)
                    owner.Log("Completion effect artwork unavailable; the verified outcome remains recorded.");
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException or ArgumentException)
        {
            if (version == assetVersion && runtime == owner)
                owner.Log("Completion effect artwork: " + e.Message);
        }
    }

    internal async Task PlayAsync(QuestCompletion result)
    {
        var owner = runtime;
        if (owner is null)
            return;
        await PrepareArtworkAsync();
        bool animated = MotionPreferences.Enabled && spiral is not null;
        WriteableBitmap? sprite = null;
        Rect spriteBounds = default;
        string directory = artworkDirectory;
        int version = assetVersion;
        if (animated && !VisibleAvatars(result.Character).Any() && directory.Length > 0)
        {
            var appearance = CharacterPreviewAppearance.From(owner.Snapshot(result.Character), owner.Memory(result.Character).Appearance);
            try
            {
                var pixels = await Task.Run(() => new CharacterSpriteRenderer(directory).Render(appearance.Look, new("01", 5, false, true)));
                if (version != assetVersion || runtime != owner)
                    return;
                if (pixels is not null)
                {
                    sprite = Bitmap(pixels);
                    spriteBounds = SpriteImageBounds.From(pixels);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException or ArgumentException)
            {
                owner.Log("Completion character artwork: " + e.Message);
            }
        }
        if (runtime != owner || version != assetVersion)
        {
            sprite?.Dispose();
            return;
        }
        double now = clock.Elapsed.TotalMilliseconds;
        bool needsNotice = !animated || spiral is null || !VisibleAvatars(result.Character).Any();
        double localStart = needsNotice ? Math.Max(now, nextNotice) : now;
        if (needsNotice)
            nextNotice = localStart + (animated ? CelebrationDuration : 3000);
        string name = owner.QuestName(result.Quest);
        bursts.Add(new(result, name, localStart, animated, sprite, spriteBounds));
        AutomationProperties.SetName(this, result.Character + ": " + name + (result.Status == "AttemptCompleted" ? " attempt completed" : " quest completed"));
        timer.Start();
        InvalidateVisual();
    }

    void ClearArtwork()
    {
        assetVersion++;
        loading = null;
        spiral?.Dispose();
        spiral = null;
        foreach (var burst in bursts)
            burst.Sprite?.Dispose();
        bursts.Clear();
        timer.Stop();
        nextNotice = 0;
    }

    internal void Disconnect()
    {
        if (runtime is { } owner)
        {
            owner.QuestCompleted -= Completed;
            owner.Changed -= ArtworkChanged;
        }
        runtime = null;
        artworkDirectory = "";
        ClearArtwork();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Disconnect();
        base.OnDetachedFromVisualTree(e);
    }
}
