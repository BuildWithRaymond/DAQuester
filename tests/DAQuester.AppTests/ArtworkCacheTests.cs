using DAQuester.App.Controls;
using DAQuester.DarkAges.Rendering;
using Xunit;

namespace DAQuester.AppTests;

public sealed class ArtworkCacheTests
{
    static string DirectoryPath() => Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"), "artwork");
    static SpritePixels Image(byte red = 12) => new(1, 1, [red, 34, 56, 255]);

    [Fact]
    public async Task Cached_images_return_immediately_without_rendering_again()
    {
        var cache = new ArtworkCache(DirectoryPath());
        var image = Image();
        Assert.Same(image, await cache.LoadAsync("portrait", () => image));
        var cached = cache.LoadAsync("portrait", () => throw new InvalidOperationException("Rendered twice"));
        Assert.True(cached.IsCompletedSuccessfully);
        Assert.Same(image, await cached);
    }

    [Fact]
    public async Task Saved_pixels_survive_a_new_cache_instance()
    {
        string directory = DirectoryPath();
        await new ArtworkCache(directory).LoadAsync("quest", () => Image());
        var saved = await new ArtworkCache(directory).LoadAsync("quest", () => throw new InvalidOperationException("Lost saved artwork"));
        Assert.NotNull(saved);
        Assert.Equal(1, saved.Width);
        Assert.Equal(1, saved.Height);
        Assert.Equal(Image().Rgba, saved.Rgba);
    }

    [Fact]
    public async Task Concurrent_requests_share_one_render()
    {
        var cache = new ArtworkCache(DirectoryPath());
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int renders = 0;
        SpritePixels Render()
        {
            Interlocked.Increment(ref renders);
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            return Image();
        }
        var first = cache.LoadAsync("face", Render);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var second = cache.LoadAsync("face", Render);
        release.Set();
        var images = await Task.WhenAll(first, second);
        Assert.Equal(1, renders);
        Assert.Same(images[0], images[1]);
    }

    [Fact]
    public async Task Different_sources_and_appearances_do_not_share_pixels()
    {
        var cache = new ArtworkCache(DirectoryPath());
        await cache.LoadAsync("source-one/face-one", () => Image(1));
        await cache.LoadAsync("source-two/face-one", () => Image(2));
        await cache.LoadAsync("source-one/face-two", () => Image(3));
        var reopened = new ArtworkCache(cache.Directory);
        Assert.Equal(1, (await reopened.LoadAsync("source-one/face-one", () => null))!.Rgba[0]);
        Assert.Equal(2, (await reopened.LoadAsync("source-two/face-one", () => null))!.Rgba[0]);
        Assert.Equal(3, (await reopened.LoadAsync("source-one/face-two", () => null))!.Rgba[0]);
    }

    [Fact]
    public async Task Corrupt_file_is_rebuilt_and_missing_artwork_can_be_retried()
    {
        string directory = DirectoryPath();
        var cache = new ArtworkCache(directory);
        Assert.Null(await cache.LoadAsync("missing", () => null));
        Assert.NotNull(await cache.LoadAsync("missing", () => Image()));
        string file = Assert.Single(System.IO.Directory.GetFiles(directory));
        File.WriteAllBytes(file, [1, 2, 3]);
        var rebuilt = await new ArtworkCache(directory).LoadAsync("missing", () => Image(99));
        Assert.Equal(99, rebuilt!.Rgba[0]);
        var saved = await new ArtworkCache(directory).LoadAsync("missing", () => null);
        Assert.Equal(99, saved!.Rgba[0]);
    }

    [Fact]
    public async Task Cache_write_failure_keeps_rendered_artwork_usable_and_reports_diagnostic()
    {
        string directory = DirectoryPath();
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
        File.WriteAllText(directory, "A file blocks the cache directory.");
        var errors = new List<Exception>();
        var cache = new ArtworkCache(directory) { ReportError = errors.Add };
        var image = Image();
        Assert.Same(image, await cache.LoadAsync("face", () => image));
        Assert.NotEmpty(errors);
        Assert.Same(image, await cache.LoadAsync("face", () => null));
    }
}
