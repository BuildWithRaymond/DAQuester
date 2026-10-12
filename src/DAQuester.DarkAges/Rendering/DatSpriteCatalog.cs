using System.Buffers.Binary;
using System.Text;

namespace DAQuester.DarkAges.Rendering;
// Indexes installed artwork. Entries are opened read-only on demand, never extracted to disk.
public sealed class DatSpriteCatalog
{
    sealed record Entry(string Archive, long Offset, int Length);
    readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<string> Names => entries.Keys;

    public DatSpriteCatalog(string directory)
    {
        foreach (string path in Directory.EnumerateFiles(directory, "*.dat").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("khan", StringComparison.OrdinalIgnoreCase) && !name.Equals("legend", StringComparison.OrdinalIgnoreCase) && !name.Equals("hades", StringComparison.OrdinalIgnoreCase))
                continue;
            AddArchive(path);
        }
    }

    void AddArchive(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Span<byte> header = stackalloc byte[4];
        stream.ReadExactly(header);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (count is < 1 or > 100000 || 4L + count * 17L > stream.Length)
            throw new InvalidDataException("Invalid sprite archive directory.");
        var table = new byte[count * 17];
        stream.ReadExactly(table);
        for (int index = 0; index < count - 1; index++)
        {
            uint start = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(index * 17));
            uint end = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan((index + 1) * 17));
            if (start < 4L + table.Length || end < start || end > stream.Length || end - start > 16 * 1024 * 1024)
                throw new InvalidDataException("Invalid sprite archive entry bounds.");
            var nameBytes = table.AsSpan(index * 17 + 4, 13);
            int terminator = nameBytes.IndexOf((byte)0);
            string name = Encoding.ASCII.GetString(terminator < 0 ? nameBytes : nameBytes[..terminator]);
            entries[name] = new(path, start, (int)(end - start));
        }
    }

    public byte[]? Read(string name)
    {
        if (!entries.TryGetValue(name, out var entry))
            return null;
        using var stream = File.Open(entry.Archive, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (entry.Offset + entry.Length > stream.Length)
            throw new InvalidDataException("Sprite archive changed while being read.");
        stream.Position = entry.Offset;
        var data = new byte[entry.Length];
        stream.ReadExactly(data);
        return data;
    }
}
