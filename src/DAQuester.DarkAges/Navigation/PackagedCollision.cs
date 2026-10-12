using System.Text.Json;

namespace DAQuester.DarkAges.Navigation;

public partial class MapRepository
{
    Dictionary<int, byte[]>? packagedCollision;
    sealed record CollisionPack(int SchemaVersion, string ServerProfile, string SotpSha256, CollisionMap[] Maps, int[] MissingMapIds);
    sealed record CollisionMap(int Id, int Width, int Height, string BlockedBits, string SourceSha256);
    Grid? LoadPackaged(int id)
    {
        packagedCollision ??= ReadPackagedCollision();
        if (!packagedCollision.TryGetValue(id, out var bits))
            return null;
        var r = Record(id);
        var grid = new Grid(r.Width, r.Height, (x, y) =>
        {
            int i = y * r.Width + x;
            return (bits[i / 8] & (1 << (i % 8))) != 0;
        });
        grids[id] = grid;
        return grid;
    }

    Dictionary<int, byte[]> ReadPackagedCollision()
    {
        if (string.IsNullOrWhiteSpace(collisionProfile) || collisionProfile is "." or ".." || collisionProfile.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new InvalidDataException("The collision server profile name is invalid.");
        string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(routeFile))!, "maps", collisionProfile + ".walkability.json");
        if (!File.Exists(path))
            return [];
        try
        {
            var pack = JsonSerializer.Deserialize<CollisionPack>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Packaged collision data is empty.");
            if (pack.SchemaVersion != 1 || pack.ServerProfile != collisionProfile || !Digest(pack.SotpSha256) || pack.Maps is null || pack.MissingMapIds is null)
                throw new InvalidDataException("Packaged collision format or server profile is invalid.");
            Dictionary<int, byte[]> result = [];
            foreach (var map in pack.Maps)
            {
                if (map is null || !records.TryGetValue(map.Id, out var r) || map.Width != r.Width || map.Height != r.Height || map.Width <= 0 || map.Height <= 0 || !Digest(map.SourceSha256) || map.BlockedBits is null)
                    throw new InvalidDataException("Packaged collision map dimensions or source digest are invalid.");
                int cells = checked(map.Width * map.Height);
                var bits = Convert.FromBase64String(map.BlockedBits);
                if (bits.Length != checked((cells + 7) / 8) || cells % 8 != 0 && (bits[^1] >> (cells % 8)) != 0 || !result.TryAdd(map.Id, bits))
                    throw new InvalidDataException("Packaged collision bit length, padding or map IDs are invalid.");
            }

            HashSet<int> missing = [];
            if (pack.MissingMapIds.Any(id => !records.ContainsKey(id) || result.ContainsKey(id) || !missing.Add(id)))
                throw new InvalidDataException("Packaged collision missing-map IDs are invalid.");
            return result;
        }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Packaged collision data is malformed.", e);
        }
    }

    static bool Digest(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
