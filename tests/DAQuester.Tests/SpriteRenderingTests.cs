using Arbiter.Imaging.Formats;
using DAQuester.Core;
using DAQuester.DarkAges.Rendering;
using Xunit;

namespace DAQuester.Tests;
public class SpriteRenderingTests
{
    static readonly DateTimeOffset Started = DateTimeOffset.UnixEpoch;
    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 6, false)]
    [InlineData(2, 6, true)]
    [InlineData(3, 1, true)]
    public void WalkingUsesOriginalDirectionalFrames(byte direction, int frame, bool flip)
    {
        var pose = SpritePose.From(new(direction, 0, Started, 456, true), Started);
        Assert.Equal("01", pose.Suffix);
        Assert.Equal(frame, pose.Frame);
        Assert.Equal(flip, pose.Flip);
        Assert.Equal("04", SpritePose.From(new(direction, 0, Started, 456, true), Started.AddMilliseconds(456)).Suffix);
    }

    [Fact]
    public void WaveUsesBodyFramesAndStopsAfterItsObservedActionCycle()
    {
        var action = new CharacterMotion(1, 22, Started, 800, false);
        Assert.Equal(new SpritePose("03", 8, false, true), SpritePose.From(action, Started));
        Assert.Equal(9, SpritePose.From(action, Started.AddMilliseconds(350)).Frame);
        Assert.Equal("04", SpritePose.From(action, Started.AddSeconds(2)).Suffix);
    }

    [Fact]
    public void InstalledIndexedArtworkKeepsPaletteAndMirrorsAroundBodyAnchor()
    {
        string directory = Path.Combine(Path.GetTempPath(), "daquester-sprite-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var palette = new byte[768];
            palette[3] = 100;
            palette[4] = 160;
            palette[5] = 200;
            WriteArchive(Path.Combine(directory, "khanpal.dat"), [("palm000.pal", palette), ("palb000.pal", palette), ("mm00101.epf", Epf()), ("mn00101.epf", Epf(18, 22))]);
            var renderer = new CharacterSpriteRenderer(directory);
            var look = new CharacterAppearance(0, 0, 0x10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, false, false);
            var up = renderer.Render(look, new("01", 0, false, false))!;
            var left = renderer.Render(look, new("01", 0, true, false))!;
            Assert.Equal(new byte[] { 100, 160, 200, 255 }, up.Rgba.AsSpan((10 * up.Width + 47) * 4, 4).ToArray());
            Assert.Equal(new byte[] { 100, 160, 200, 255 }, left.Rgba.AsSpan((10 * left.Width + 63) * 4, 4).ToArray());
            int pantsPixel = (22 * up.Width + 45) * 4 + 3;
            Assert.Equal(0, up.Rgba[pantsPixel]);
            var clothed = renderer.Render(look with { PantsColor = 1 }, new("01", 0, false, false))!;
            Assert.Equal(255, clothed.Rgba[pantsPixel]);
            var overcoat = renderer.Render(look with { PantsColor = 1, Overcoat = 1001 }, new("01", 0, false, false))!;
            Assert.Equal(0, overcoat.Rgba[pantsPixel]);
            Assert.Null(renderer.Render(look with { Hidden = true }, new("01", 0, false, false)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TruncatedEpfCannotReadBeyondItsFrameTable()
    {
        Assert.Throws<InvalidDataException>(() => EpfFile.Parse(new byte[12] { 1, 0, 1, 0, 1, 0, 0, 0, 255, 255, 0, 0 }));
    }

    [Fact]
    public void PortraitUsesOriginalHeadAndFaceWithoutBodyOrClothes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "daquester-portrait-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var palette = new byte[768];
            palette[3] = 120;
            palette[4] = 160;
            palette[5] = 200;
            WriteArchive(Path.Combine(directory, "khanpal.dat"), [
                ("palm000.pal", palette), ("palf000.pal", palette), ("palh000.pal", palette),
                ("mm00101.epf", Epf()), ("mf00101.epf", Epf(18, 6)),
                ("mo00101.epf", Epf(20, 8)), ("mh00101.epf", Epf(18, 5))]);
            var renderer = new CharacterSpriteRenderer(directory);
            var look = new CharacterAppearance(1, 1, 0x10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, false, false);
            var portrait = renderer.RenderPortrait(look)!;
            Assert.Equal(new byte[] { 120, 160, 200, 255 }, portrait.Rgba.AsSpan((8 * portrait.Width + 47) * 4, 4).ToArray());
            Assert.Equal(255, portrait.Rgba[(5 * portrait.Width + 45) * 4 + 3]);
            Assert.Equal(0, portrait.Rgba[(10 * portrait.Width + 47) * 4 + 3]);
            Assert.Null(renderer.RenderPortrait(look with { Head = 0, Face = 0 }));
            Assert.Null(renderer.RenderPortrait(look with { Hidden = true }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ZeroFacePortraitIncludesOriginalBodyHeadSkinButNotTorso()
    {
        string directory = Path.Combine(Path.GetTempPath(), "daquester-face-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var skin = new byte[768];
            skin[3] = 120; skin[4] = 160; skin[5] = 200;
            WriteArchive(Path.Combine(directory, "khanpal.dat"), [
                ("palm005.pal", skin), ("mm00101.epf", Epf(23, 24, 51)),
                ("mo00101.epf", Epf(23, 24, 15))]);
            var renderer = new CharacterSpriteRenderer(directory);
            var look = new CharacterAppearance(0, 0, 0x10, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, false, false);
            var portrait = renderer.RenderPortrait(look);
            Assert.NotNull(portrait);
            Assert.Equal(new byte[] { 120, 160, 200, 255 }, portrait.Rgba.AsSpan((33 * portrait.Width + 50) * 4, 4).ToArray());
            Assert.Equal(0, portrait.Rgba[(45 * portrait.Width + 50) * 4 + 3]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestingSpfUsesItsOriginalRgb565Palette()
    {
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data);
        writer.Write(0);
        writer.Write(1);
        writer.Write(0);
        var palette = new byte[1024];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2), 0xF800);
        writer.Write(palette);
        writer.Write(1);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1);
        writer.Write(1);
        writer.Write(0);
        writer.Write(1);
        writer.Write((byte)1);
        var frame = SpfSprite.DecodeFrame(data.ToArray(), 0, false);
        Assert.NotNull(frame);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, frame.Rgba);
    }

    [Fact]
    public void TruncatedSpfCannotReadAnUnboundedFrameTable()
    {
        var bytes = new byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 4096);
        Assert.Throws<InvalidDataException>(() => SpfSprite.DecodeFrame(bytes, 0, false));
    }

    static byte[] Epf(short left = 20, short top = 10, short height = 1)
    {
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data);
        writer.Write((short)10);
        writer.Write((short)57);
        writer.Write((short)85);
        writer.Write((short)0);
        writer.Write(10 * height);
        writer.Write(Enumerable.Repeat((byte)1, 10 * height).ToArray());
        for (int frame = 0; frame < 10; frame++)
        {
            writer.Write(top);
            writer.Write(left);
            writer.Write((short)(top + height));
            writer.Write((short)(left + 1));
            writer.Write(frame * height);
            writer.Write((frame + 1) * height);
        }

        return data.ToArray();
    }

    static void WriteArchive(string path, (string Name, byte[] Bytes)[] entries)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(entries.Length + 1);
        int offset = 4 + (entries.Length + 1) * 17;
        foreach (var entry in entries)
        {
            writer.Write(offset);
            var name = new byte[13];
            System.Text.Encoding.ASCII.GetBytes(entry.Name).CopyTo(name, 0);
            // Original archives can leave garbage after the first null terminator.
            name[12] = 99;
            writer.Write(name);
            offset += entry.Bytes.Length;
        }

        writer.Write(offset);
        writer.Write(new byte[13]);
        foreach (var entry in entries)
            writer.Write(entry.Bytes);
    }
}
