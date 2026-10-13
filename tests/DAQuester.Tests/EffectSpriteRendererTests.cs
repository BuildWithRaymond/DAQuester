using System.Text;
using DAQuester.DarkAges.Rendering;
using Xunit;

namespace DAQuester.Tests;

public class EffectSpriteRendererTests
{
    static string Archive(string? sequence = null, byte[]? anchors = null, bool alternatePalette = false)
    {
        string directory = Path.Combine(Path.GetTempPath(), "daquester-effect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var palette = new byte[768];
        palette[3] = alternatePalette ? (byte)240 : (byte)10;
        palette[4] = 20;
        palette[5] = 250;
        palette[6] = 255; palette[8] = 255;
        using var epf = new MemoryStream();
        using (var writer = new BinaryWriter(epf, Encoding.ASCII, true))
        {
            writer.Write((short)2); writer.Write((short)6); writer.Write((short)8); writer.Write((short)0);
            writer.Write(4); writer.Write(new byte[] { 1, 0, 2, 0 });
            for (int frame = 0; frame < 2; frame++)
            {
                writer.Write((short)1); writer.Write((short)1); writer.Write((short)2); writer.Write((short)3);
                writer.Write(frame * 2); writer.Write((frame + 1) * 2);
            }
        }
        var table = Encoding.ASCII.GetBytes("50\n" + string.Concat(Enumerable.Repeat("0 1\n", 49)) + (sequence ?? "0 1 0") + "\n");
        (string Name, byte[] Bytes)[] entries =
        [
            ("efct050.epf", epf.ToArray()), ("efct050.tbl", anchors ?? [3, 0, 7, 0, 3, 0, 7, 0]),
            ("effect.tbl", table), ("effpal.tbl", Encoding.ASCII.GetBytes(alternatePalette ? "50 2\n" : "")),
            (alternatePalette ? "eff002.pal" : "eff000.pal", palette)
        ];
        using var archive = new BinaryWriter(File.Create(Path.Combine(directory, "roh.dat")));
        archive.Write(entries.Length + 1);
        int offset = 4 + (entries.Length + 1) * 17;
        foreach (var entry in entries)
        {
            archive.Write(offset);
            var name = new byte[13]; Encoding.ASCII.GetBytes(entry.Name).CopyTo(name, 0); archive.Write(name);
            offset += entry.Bytes.Length;
        }
        archive.Write(offset); archive.Write(new byte[13]);
        foreach (var entry in entries) archive.Write(entry.Bytes);
        return directory;
    }

    static EffectSpriteClip? Read(string directory, int id) => new EffectSpriteRenderer(directory).Read(id);

    [Fact]
    public void Original_sequence_anchor_and_transparency_are_preserved()
    {
        string directory = Archive();
        try
        {
            var clip = Read(directory, 50);
            Assert.NotNull(clip);
            Assert.Equal(new[] { 0, 1, 0 }, clip.Sequence);
            Assert.Equal(240, clip.DurationMilliseconds);
            var frame = clip.Frames[0];
            Assert.Equal(3, frame.OriginX);
            Assert.Equal(7, frame.OriginY);
            var pixels = frame.Pixels;
            Assert.Equal(new byte[] { 10, 20, 250, 255 }, pixels.Rgba.Skip(28).Take(4));
            Assert.Equal(0, pixels.Rgba[35]);
            var transparent = clip.Frames[1].Pixels;
            Assert.All(transparent.Rgba, value => Assert.Equal(0, value));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Palette_mapping_is_used_and_missing_effects_return_no_artwork()
    {
        string directory = Archive(alternatePalette: true);
        try
        {
            var clip = Read(directory, 50);
            Assert.NotNull(clip);
            Assert.Equal(240, clip.Frames[0].Pixels.Rgba[28]);
            Assert.Null(Read(directory, 90));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Invalid_sequences_and_truncated_anchors_are_rejected(bool sequence)
    {
        string directory = sequence ? Archive("0 999") : Archive(anchors: [3, 0, 7]);
        try
        {
            Assert.Throws<InvalidDataException>(() => Read(directory, 50));
        }
        finally { Directory.Delete(directory, true); }
    }
}
