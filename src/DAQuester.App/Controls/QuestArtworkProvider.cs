using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;

// One worker cache serves library and journey icons. Bitmaps remain owned by their controls.
internal static class QuestArtworkProvider
{
    static readonly object gate = new();
    static readonly Lazy<QuestArtworkCatalog> catalog = new(() => new(Path.Combine(AppSettings.Content, "quest-artwork.json")));
    static readonly Dictionary<QuestArtwork, SpritePixels?> pixels = [];
    static string loadedDirectory = "";
    static QuestSpriteRenderer? renderer;

    public static Task<(QuestArtwork? Artwork, SpritePixels? Pixels)> LoadAsync(string? quest, string directory)
        => Task.Run(() =>
        {
            var artwork = catalog.Value.Find(quest);
            if (artwork is null || directory.Length == 0)
                return (artwork, (SpritePixels?)null);
            lock (gate)
            {
                if (renderer is null || !directory.Equals(loadedDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    renderer = new(directory);
                    loadedDirectory = directory;
                    pixels.Clear();
                }
                if (!pixels.TryGetValue(artwork, out var image))
                {
                    image = artwork.Creature ? renderer.RenderCreature(artwork.Sprite, artwork.Variant)
                        : renderer.RenderItem(artwork.Sprite, artwork.Dye);
                    pixels[artwork] = image;
                }
                return (artwork, image);
            }
        });
}
