using System.Collections.Concurrent;
using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;

// Portraits are static presentation pixels. They never restore a live snapshot.
internal static class CharacterArtworkProvider
{
    sealed class Source(string directory)
    {
        public readonly object Gate = new();
        public readonly string Key = ArtworkCache.SourceKey(directory);
        public readonly Lazy<CharacterSpriteRenderer> Renderer = new(() => new(directory));
    }
    static readonly ConcurrentDictionary<string, Lazy<Source>> sources = new(StringComparer.OrdinalIgnoreCase);

    public static Task<SpritePixels?> LoadPortraitAsync(CharacterAppearance look, string directory)
    {
        if (directory.Length == 0 || look.Hidden)
            return Task.FromResult<SpritePixels?>(null);
        var source = sources.GetOrAdd(directory, path => new(() => new Source(path)));
        var cache = ArtworkCache.ForUserData;
        string appearance = JsonSerializer.Serialize(look);
        // The source index is first fingerprinted off-thread. Subsequent reads,
        // including memory hits, can use it without another queued worker.
        if (source.IsValueCreated)
            return Load(source.Value);
        return Task.Run(async () => await Load(source.Value));

        Task<SpritePixels?> Load(Source loaded) => cache.LoadAsync("portrait|" + loaded.Key + "|" + appearance, () =>
        {
            lock (loaded.Gate)
                return loaded.Renderer.Value.RenderPortrait(look);
        });
    }
}
