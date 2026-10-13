using System.Buffers.Binary;
using System.Text;
using Arbiter.Imaging.Formats;

namespace DAQuester.DarkAges.Rendering;

public sealed record EffectSpriteFrame(SpritePixels Pixels, int OriginX, int OriginY);
public sealed record EffectSpriteClip(EffectSpriteFrame[] Frames, int[] Sequence)
{
    public const int FrameMilliseconds = 80;
    public int DurationMilliseconds => Sequence.Length * FrameMilliseconds;
}

// Original installed effect artwork, sequences, palettes and anchors. No game actions.
public sealed class EffectSpriteRenderer(string directory)
{
    readonly DatSpriteCatalog catalog = new(directory, effectsOnly: true);

    public EffectSpriteClip? Read(int id)
    {
        if (id is < 1 or > 999 || catalog.Read($"efct{id:D3}.epf") is not { } bytes
            || catalog.Read($"efct{id:D3}.tbl") is not { } anchors
            || catalog.Read("effect.tbl") is not { } sequences)
            return null;
        var epf = EpfFile.Parse(bytes);
        if (epf.PixelWidth is < 1 or > 1024 || epf.PixelHeight is < 1 or > 1024
            || epf.Frames.Count is < 1 or > 256 || (long)epf.PixelWidth * epf.PixelHeight * epf.Frames.Count > 8 * 1024 * 1024
            || anchors.Length != epf.Frames.Count * 4)
            throw new InvalidDataException("Effect dimensions or anchors are invalid.");
        var rows = Encoding.ASCII.GetString(sequences).Split('\n');
        if (!int.TryParse(rows[0].Trim(), out int count) || count is < 1 or > 4096 || rows.Length <= id || id > count)
            return null;
        var sequence = rows[id].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(value =>
            int.TryParse(value, out int frame) && frame >= 0 && frame < epf.Frames.Count ? frame
                : throw new InvalidDataException("Effect sequence refers to an invalid frame.")).ToArray();
        if (sequence.Length is < 1 or > 1024)
            throw new InvalidDataException("Effect sequence length is invalid.");
        var palette = catalog.Read($"eff{PaletteId(id):D3}.pal");
        if (palette?.Length != 768)
            return null;
        var frames = new EffectSpriteFrame[epf.Frames.Count];
        for (int index = 0; index < frames.Length; index++)
        {
            var pixels = new byte[epf.PixelWidth * epf.PixelHeight * 4];
            if (epf.Frames[index] is { } frame)
            {
                if (frame.Left + frame.Width > epf.PixelWidth || frame.Top + frame.Height > epf.PixelHeight)
                    throw new InvalidDataException("Effect frame exceeds its canvas.");
                for (int y = 0; y < frame.Height; y++)
                    for (int x = 0; x < frame.Width; x++)
                    {
                        byte colorIndex = frame.Pixels.Span[y * frame.Width + x];
                        var color = palette.AsSpan(colorIndex * 3, 3);
                        if (colorIndex == 0 || color[0] == 0 && color[1] == 0 && color[2] == 0
                            || color[0] == 255 && color[1] == 0 && color[2] == 255)
                            continue;
                        int target = ((frame.Top + y) * epf.PixelWidth + frame.Left + x) * 4;
                        color.CopyTo(pixels.AsSpan(target, 3));
                        pixels[target + 3] = 255;
                    }
            }
            frames[index] = new(new(epf.PixelWidth, epf.PixelHeight, pixels),
                BinaryPrimitives.ReadInt16LittleEndian(anchors.AsSpan(index * 4)),
                BinaryPrimitives.ReadInt16LittleEndian(anchors.AsSpan(index * 4 + 2)));
        }
        return new(frames, sequence);
    }

    int PaletteId(int id)
    {
        if (catalog.Read("effpal.tbl") is not { } bytes)
            return 0;
        foreach (string line in Encoding.ASCII.GetString(bytes).Split('\n'))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 2 && int.TryParse(fields[0], out int effect) && effect == id
                && int.TryParse(fields[1], out int palette) && palette is >= 0 and <= 999)
                return palette;
            if (fields.Length == 3 && int.TryParse(fields[0], out int start) && int.TryParse(fields[1], out int end)
                && id >= start && id <= end && int.TryParse(fields[2], out int rangePalette) && rangePalette is >= 0 and <= 999)
                return rangePalette;
        }
        return 0;
    }
}
