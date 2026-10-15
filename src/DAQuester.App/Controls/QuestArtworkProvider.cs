using System.Collections.Concurrent;
using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;

// One worker cache serves library and journey icons. Bitmaps remain owned by their controls.
internal static class QuestArtworkProvider
{
    static readonly Lazy<QuestArtworkCatalog> catalog = new(() => new(Path.Combine(AppSettings.Content, "quest-artwork.json")));
    sealed class Source(string directory)
    {
        public readonly object Gate = new();
        public readonly string Key = ArtworkCache.SourceKey(directory);
        public readonly Lazy<QuestSpriteRenderer> Renderer = new(() => new(directory));
    }
    static readonly ConcurrentDictionary<string, Lazy<Source>> sources = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<(QuestArtwork? Artwork, SpritePixels? Pixels)> LoadAsync(string? quest, string directory)
    {
        var artwork = catalog.Value.Find(quest);
        if (artwork is null || directory.Length == 0)
            return (artwork, null);
        var source = sources.GetOrAdd(directory, path => new(() => new Source(path)));
        var cache = ArtworkCache.ForUserData;
        if (source.IsValueCreated)
            return (artwork, await Load(source.Value));
        return (artwork, await Task.Run(async () => await Load(source.Value)));

        Task<SpritePixels?> Load(Source loaded) => cache.LoadAsync(
            $"quest|{loaded.Key}|{artwork.Creature}|{artwork.Sprite}|{artwork.Dye}|{artwork.Variant}", () =>
        {
            lock (loaded.Gate)
            {
                var renderer = loaded.Renderer.Value;
                return artwork.Creature ? renderer.RenderCreature(artwork.Sprite, artwork.Variant)
                    : renderer.RenderItem(artwork.Sprite, artwork.Dye);
            }
        });
    }

    public static Task PreloadAsync(string directory) => directory.Length == 0 ? Task.CompletedTask
        : Task.WhenAll(catalog.Value.Entries.Select(artwork => LoadAsync(artwork.Id, directory)));
}
