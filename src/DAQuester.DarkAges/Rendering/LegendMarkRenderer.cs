using Arbiter.Imaging.Formats;

namespace DAQuester.DarkAges.Rendering;

// Installed legend shapes are presentation only, never proof of a quest reward.
public sealed class LegendMarkRenderer
{
    readonly EpfFile? shapes;
    readonly byte[]? palette;

    public LegendMarkRenderer(string directory)
    {
        var catalog = new DatSpriteCatalog(directory, ["setoa", "legend"]);
        shapes = catalog.Read("legends.epf") is { } bytes ? EpfFile.Parse(bytes) : null;
        palette = catalog.Read("legend.pal");
    }

    public SpritePixels? Render(int icon)
    {
        if (icon is < 0 or > 7 || shapes is null || icon >= shapes.Frames.Count
            || shapes.Frames[icon] is not { } frame || palette?.Length != 768)
            return null;
        if (frame.Width is < 1 or > 64 || frame.Height is < 1 or > 64)
            throw new InvalidDataException("Invalid legend mark dimensions.");
        var rgba = new byte[frame.Width * frame.Height * 4];
        for (int i = 0; i < frame.Width * frame.Height; i++)
        {
            int color = frame.Pixels.Span[i];
            if (color == 0)
                continue;
            palette.AsSpan(color * 3, 3).CopyTo(rgba.AsSpan(i * 4, 3));
            rgba[i * 4 + 3] = 255;
        }
        return new(frame.Width, frame.Height, rgba);
    }
}
