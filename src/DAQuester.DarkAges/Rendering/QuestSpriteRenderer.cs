using Arbiter.Imaging.Formats;

namespace DAQuester.DarkAges.Rendering;

// Static quest artwork is presentation only; it never creates observed game state.
public sealed class QuestSpriteRenderer
{
    readonly DatSpriteCatalog catalog;
    readonly Dictionary<int, EpfFile?> items = [];
    readonly Dictionary<string, MpfFile?> creatures = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, byte[]?> palettes = new(StringComparer.OrdinalIgnoreCase);
    readonly List<(int First, int Last, int Palette)> itemPalettes = [];
    readonly Dictionary<byte, byte[]> dyes = [];
    const int ItemsPerBank = 266;

    public QuestSpriteRenderer(string directory)
    {
        catalog = new(directory, ["legend", "hades"]);
        using (var reader = Text("itempal.tbl"))
        {
            while (reader?.ReadLine() is { } line)
            {
                var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length == 3 && int.TryParse(fields[0], out int first)
                    && int.TryParse(fields[1], out int last) && int.TryParse(fields[2], out int palette)
                    && first > 0 && last >= first && last <= ushort.MaxValue && palette is >= 0 and <= 999)
                    itemPalettes.Add((first, last, palette));
            }
        }
        using var colors = Text("color0.tbl");
        if (!int.TryParse(NextLine(colors), out int count) || count != 6)
            return;
        while (NextLine(colors) is { } line)
        {
            if (!byte.TryParse(line, out byte id))
                continue;
            var rgb = new byte[count * 3];
            bool valid = true;
            for (int i = 0; i < count; i++)
            {
                var fields = NextLine(colors)?.Split(',', StringSplitOptions.TrimEntries);
                if (fields?.Length != 3 || !byte.TryParse(fields[0], out rgb[i * 3])
                    || !byte.TryParse(fields[1], out rgb[i * 3 + 1]) || !byte.TryParse(fields[2], out rgb[i * 3 + 2]))
                    valid = false;
            }
            if (valid)
                dyes[id] = rgb;
        }
    }

    public SpritePixels? RenderItem(int id, byte dye = 0)
    {
        if (id is <= 0 or > ushort.MaxValue)
            return null;
        int bank = (id - 1) / ItemsPerBank + 1, index = (id - 1) % ItemsPerBank;
        if (!items.TryGetValue(bank, out var epf))
        {
            epf = catalog.Read($"item{bank:D3}.epf") is { } bytes ? EpfFile.Parse(bytes) : null;
            items[bank] = epf;
        }
        if (epf is null)
            return null;
        ValidateCanvas(epf.PixelWidth, epf.PixelHeight);
        if (index >= epf.Frames.Count || epf.Frames[index] is not { } frame)
            return null;
        int paletteId = itemPalettes.FirstOrDefault(p => id >= p.First && id <= p.Last).Palette;
        var palette = Palette($"item{paletteId:D3}.pal");
        return palette is null ? null : Decode(frame.Width, frame.Height, frame.Pixels.Span, palette, dye);
    }

    public SpritePixels? RenderCreature(int id, string variant = "")
    {
        if (id is <= 0 or > 999 || variant is not ("" or "a" or "b" or "c"))
            return null;
        string name = $"mns{id:D3}{variant}.mpf";
        if (!creatures.TryGetValue(name, out var mpf))
        {
            mpf = catalog.Read(name) is { } bytes ? MpfFile.Parse(bytes) : null;
            creatures[name] = mpf;
        }
        if (mpf is null)
            return null;
        ValidateCanvas(mpf.PixelWidth, mpf.PixelHeight);
        // A stable front-facing idle frame matches the guide portraits without animating library rows.
        int start = mpf.OptionalAnimationFrameCount > 0 ? mpf.StandingFrameIndex : mpf.WalkFrameIndex;
        int count = mpf.OptionalAnimationFrameCount > 0 ? mpf.OptionalAnimationFrameCount : mpf.WalkFrameCount;
        int front = start + count < mpf.Frames.Count ? start + count : start;
        if (front < 0 || front >= mpf.Frames.Count || mpf.Frames[front] is not { } frame)
            return null;
        var palette = Palette($"mns{mpf.PaletteNumber:D3}.pal");
        return palette is null ? null : Decode(frame.Width, frame.Height, frame.Pixels.Span, palette, 0);
    }

    SpritePixels Decode(int width, int height, ReadOnlySpan<byte> indices, byte[] palette, byte dye)
    {
        ValidateCanvas(width, height);
        if (indices.Length < width * height)
            throw new InvalidDataException("Quest sprite pixels exceed their frame.");
        bool recolor = dye != 0 && dyes.TryGetValue(0, out var original)
            && palette.AsSpan(98 * 3, 18).SequenceEqual(original) && dyes.ContainsKey(dye);
        var output = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            byte index = indices[i];
            // Palette index zero is transparent, even when its RGB is white. White fur is opaque.
            if (index == 0)
                continue;
            var rgb = recolor && index is >= 98 and <= 103
                ? dyes[dye].AsSpan((index - 98) * 3, 3) : palette.AsSpan(index * 3, 3);
            rgb.CopyTo(output.AsSpan(i * 4, 3));
            output[i * 4 + 3] = 255;
        }
        return new(width, height, output);
    }

    static void ValidateCanvas(int width, int height)
    {
        if (width is < 1 or > 1024 || height is < 1 or > 1024)
            throw new InvalidDataException("Invalid quest sprite canvas.");
    }

    StreamReader? Text(string name) => catalog.Read(name) is { } bytes ? new(new MemoryStream(bytes)) : null;

    static string? NextLine(StreamReader? reader)
    {
        // Legacy game tables use CR-CR-LF, which StreamReader exposes as extra empty lines.
        while (reader?.ReadLine() is { } line)
            if (!string.IsNullOrWhiteSpace(line))
                return line;
        return null;
    }

    byte[]? Palette(string name)
    {
        if (palettes.TryGetValue(name, out var palette))
            return palette;
        palette = catalog.Read(name);
        if (palette?.Length != 768)
            palette = null;
        palettes[name] = palette;
        return palette;
    }
}
