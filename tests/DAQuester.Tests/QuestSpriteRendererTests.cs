using System.Text;
using DAQuester.DarkAges.Rendering;
using Xunit;

namespace DAQuester.Tests;

public sealed class QuestSpriteRendererTests : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "daquester-quest-art-" + Guid.NewGuid().ToString("N"));

    public QuestSpriteRendererTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void ItemBanksAreOneBasedAndUse266FramesWithTheirPaletteRanges()
    {
        Archive("legend.dat", ("item001.epf", Epf(266)), ("item002.epf", Epf(1)),
            ("item000.pal", Palette(10)), ("item001.pal", Palette(90)),
            ("itempal.tbl", Encoding.ASCII.GetBytes("267 532 1\n")));
        var renderer = new QuestSpriteRenderer(directory);
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, renderer.RenderItem(266)!.Rgba.AsSpan(4, 4).ToArray());
        Assert.Equal(new byte[] { 90, 20, 30, 255 }, renderer.RenderItem(267)!.Rgba.AsSpan(4, 4).ToArray());
        Assert.Null(renderer.RenderItem(0));
        Assert.Null(renderer.RenderItem(268));
    }

    [Fact]
    public void WolfBackgroundUsesIndexZeroWhileWhiteFurStaysOpaque()
    {
        Archive("hades.dat", ("mns002.mpf", Mpf()), ("mns007.pal", Palette(10)));
        string archive = Path.Combine(directory, "hades.dat");
        var before = File.ReadAllBytes(archive);
        var pixels = new QuestSpriteRenderer(directory).RenderCreature(2)!;
        Assert.Equal(0, pixels.Rgba[3]);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pixels.Rgba.AsSpan(8, 4).ToArray());
        Assert.Equal(before, File.ReadAllBytes(archive));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\r\n")]
    public void ColoredQuestItemsApplyOnlyTheOriginalDyeIndices(string newline)
    {
        var palette = Palette(10);
        for (int i = 98; i <= 103; i++) palette.AsSpan(i * 3, 3).Fill(30);
        string colors = "6\n0\n" + string.Concat(Enumerable.Repeat("30,30,30\n", 6))
            + "58\n" + string.Concat(Enumerable.Repeat("40,60,200\n", 6));
        Archive("legend.dat", ("item001.epf", Epf(1, [0, 98, 2])),
            ("item000.pal", palette), ("color0.tbl", Encoding.ASCII.GetBytes(colors.Replace("\n", newline))));
        var pixels = new QuestSpriteRenderer(directory).RenderItem(1, 58)!;
        Assert.Equal(new byte[] { 40, 60, 200, 255 }, pixels.Rgba.AsSpan(4, 4).ToArray());
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pixels.Rgba.AsSpan(8, 4).ToArray());
    }

    [Fact]
    public void MultipartDragonLoadsItsNamedHeadArtworkAndMissingArtReturnsNull()
    {
        Archive("hades.dat", ("mns083a.mpf", Mpf()), ("mns007.pal", Palette(10)));
        var renderer = new QuestSpriteRenderer(directory);
        Assert.NotNull(renderer.RenderCreature(83, "a"));
        Assert.Null(renderer.RenderCreature(83));
        Assert.Null(renderer.RenderItem(70));
    }

    [Fact]
    public void InvalidArchiveArtworkDoesNotAllocateAnUnboundedCanvas()
    {
        var malformed = Epf(1);
        malformed[2] = 255;
        malformed[3] = 127;
        Archive("legend.dat", ("item001.epf", malformed), ("item000.pal", Palette(10)));
        Assert.Throws<InvalidDataException>(() => new QuestSpriteRenderer(directory).RenderItem(1));
    }

    [Fact]
    public void Legend_marks_use_installed_eight_shape_frames_and_keep_index_zero_transparent()
    {
        Archive("setoa.dat", ("legends.epf", Epf(8)));
        Archive("legend.dat", ("legend.pal", Palette(70)));
        var renderer = new LegendMarkRenderer(directory);
        Assert.Equal(new byte[] { 70, 20, 30, 255 }, renderer.Render(0)!.Rgba.AsSpan(4, 4).ToArray());
        Assert.Equal(0, renderer.Render(7)!.Rgba[3]);
        Assert.Null(renderer.Render(8));
        Assert.Null(renderer.Render(-1));
    }

    static byte[] Palette(byte red)
    {
        var palette = new byte[768];
        palette.AsSpan(0, 3).Fill(255);
        palette[3] = red; palette[4] = 20; palette[5] = 30;
        palette.AsSpan(6, 3).Fill(255);
        return palette;
    }

    static byte[] Epf(int count, byte[]? pixels = null)
    {
        pixels ??= [0, 1, 2];
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data);
        writer.Write((short)count); writer.Write((short)3); writer.Write((short)1); writer.Write((short)0);
        writer.Write(count * 3);
        for (int i = 0; i < count; i++) writer.Write(pixels);
        for (int i = 0; i < count; i++)
        {
            writer.Write((short)0); writer.Write((short)0); writer.Write((short)1); writer.Write((short)3);
            writer.Write(i * 3); writer.Write((i + 1) * 3);
        }
        return data.ToArray();
    }

    static byte[] Mpf()
    {
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data);
        writer.Write((byte)2); writer.Write((short)3); writer.Write((short)1); writer.Write(3);
        writer.Write(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 });
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)3); writer.Write((short)1);
        writer.Write((short)0); writer.Write((short)0); writer.Write(0);
        writer.Write((short)-1); writer.Write((short)-1); writer.Write(new byte[8]); writer.Write(7);
        writer.Write(new byte[] { 0, 1, 2 });
        return data.ToArray();
    }

    void Archive(string name, params (string Name, byte[] Bytes)[] entries)
    {
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, name)));
        writer.Write(entries.Length + 1);
        int offset = 4 + (entries.Length + 1) * 17;
        foreach (var entry in entries)
        {
            writer.Write(offset);
            var bytes = new byte[13]; Encoding.ASCII.GetBytes(entry.Name).CopyTo(bytes, 0); writer.Write(bytes);
            offset += entry.Bytes.Length;
        }
        writer.Write(offset); writer.Write(new byte[13]);
        foreach (var entry in entries) writer.Write(entry.Bytes);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
