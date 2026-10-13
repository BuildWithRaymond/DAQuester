using System.Text.Json;

namespace DAQuester.App.Controls;

internal sealed record QuestArtwork(string Id, string Label, bool Creature, int Sprite, byte Dye = 0,
    string Variant = "", bool BottomCrop = false, string[]? Aliases = null, string Source = "");

internal sealed class QuestArtworkCatalog
{
    readonly Dictionary<string, QuestArtwork> byId = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<QuestArtwork> Entries { get; }

    public QuestArtworkCatalog(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported quest artwork metadata.");
        Entries = document.RootElement.GetProperty("entries").Deserialize<QuestArtwork[]>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
        foreach (var artwork in Entries)
        {
            if (string.IsNullOrWhiteSpace(artwork.Id) || artwork.Sprite is <= 0 or > ushort.MaxValue
                || artwork.Creature && artwork.Sprite > 999 || artwork.Variant is not ("" or "a" or "b" or "c"))
                throw new InvalidDataException("Invalid quest artwork metadata.");
            byId.Add(artwork.Id, artwork);
            foreach (string alias in artwork.Aliases ?? [])
                byId.Add(alias, artwork);
        }
    }

    public QuestArtwork? Find(string? id) => id is not null && byId.TryGetValue(id, out var artwork) ? artwork : null;
}
