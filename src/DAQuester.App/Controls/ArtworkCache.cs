using System.Security.Cryptography;
using System.Text;
using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;

internal sealed class ArtworkCache(string directory)
{
    const int Magic = 0x31415144;
    static readonly Dictionary<string, ArtworkCache> userCaches = new(StringComparer.OrdinalIgnoreCase);
    readonly object gate = new();
    readonly Dictionary<string, SpritePixels> images = [];
    readonly Dictionary<string, Task<SpritePixels?>> pending = [];
    public string Directory { get; } = directory;
    public Action<Exception>? ReportError { get; set; }

    public static ArtworkCache ForUserData
    {
        get
        {
            string path = Path.Combine(AppSettings.DataPath, "artwork", "v1");
            lock (userCaches)
            {
                if (!userCaches.TryGetValue(path, out var cache))
                    userCaches[path] = cache = new(path);
                return cache;
            }
        }
    }

    // A memory hit completes synchronously, so rebuilt/reattached controls never
    // wait for a thread-pool slot. Only the first reader loads or renders pixels.
    public Task<SpritePixels?> LoadAsync(string key, Func<SpritePixels?> render)
    {
        lock (gate)
        {
            if (images.TryGetValue(key, out var image))
                return Task.FromResult<SpritePixels?>(image);
            if (pending.TryGetValue(key, out var loading))
                return loading;
            loading = Task.Run(() =>
            {
                try
                {
                    string path = Path.Combine(Directory, Hash(key) + ".rgba");
                    var pixels = Read(path);
                    if (pixels is null)
                    {
                        pixels = render();
                        if (pixels is not null)
                            Save(path, pixels);
                    }
                    if (pixels is not null)
                        lock (gate)
                            images[key] = pixels;
                    return pixels;
                }
                finally
                {
                    lock (gate)
                        pending.Remove(key);
                }
            });
            pending[key] = loading;
            return loading;
        }
    }

    SpritePixels? Read(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Magic)
                throw new InvalidDataException("Invalid artwork cache header.");
            int width = reader.ReadInt32(), height = reader.ReadInt32();
            if (width is <= 0 or > 1024 || height is <= 0 or > 1024 || stream.Length != 12L + width * height * 4L)
                throw new InvalidDataException("Invalid artwork cache dimensions.");
            return new(width, height, reader.ReadBytes(width * height * 4));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ReportError?.Invoke(error);
            return null;
        }
    }

    void Save(string path, SpritePixels pixels)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            using (var stream = File.Create(temporary))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(pixels.Width);
                writer.Write(pixels.Height);
                writer.Write(pixels.Rgba);
            }
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A read-only/full Data folder must not hide successfully rendered art.
            ReportError?.Invoke(error);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                ReportError?.Invoke(error);
            }
        }
    }

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static string SourceKey(string directory)
    {
        var files = System.IO.Directory.EnumerateFiles(directory, "*.dat")
            .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith("khan", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileNameWithoutExtension(path).Equals("legend", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileNameWithoutExtension(path).Equals("hades", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .Select(file => $"{file.Name.ToUpperInvariant()}:{file.Length}:{file.LastWriteTimeUtc.Ticks}");
        // Versioned rendered pixels only; no account names or raw DAT entries.
        return Hash("v1|" + Path.GetFullPath(directory).ToUpperInvariant() + "|" + string.Join('|', files));
    }
}
