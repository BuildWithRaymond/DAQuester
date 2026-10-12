using Arbiter.Imaging.Formats;
using DAQuester.Core;

namespace DAQuester.DarkAges.Rendering;
public sealed partial class CharacterSpriteRenderer
{
    readonly Dictionary<ushort, MpfFile?> monsters = [];
    readonly Dictionary<string, byte[]?> restSprites = [];
    void DrawEmotion(byte[] output, CharacterAppearance look, SpritePose pose)
    {
        var epf = Sprite("emot01.epf");
        var palette = Palette($"palm{look.SkinColor:D3}.pal");
        if (epf is null || palette is null || pose.EmoteFrame >= epf.Frames.Count || epf.Frames[pose.EmoteFrame] is not { } frame)
            return;
        for (int y = 0; y < frame.Height; y++)
            for (int x = 0; x < frame.Width; x++)
            {
                byte index = frame.Pixels.Span[y * frame.Width + x];
                if (index == 0)
                    continue;
                int targetX = Padding + frame.Left + x, targetY = frame.Top + y;
                if (pose.Flip)
                    targetX = 110 - targetX;
                if ((uint)targetX >= Width || (uint)targetY >= Height)
                    continue;
                palette.AsSpan(index * 3, 3).CopyTo(output.AsSpan((targetY * Width + targetX) * 4, 3));
                output[(targetY * Width + targetX) * 4 + 3] = 255;
            }
    }

    SpritePixels? RenderMonster(ushort id, SpritePose pose, CharacterMotion? motion, byte dye)
    {
        if (!monsters.TryGetValue(id, out var mpf))
        {
            if (monsters.Count >= 16)
                monsters.Clear();
            mpf = catalog.Read($"mns{id:D3}.mpf")is { } bytes ? MpfFile.Parse(bytes) : null;
            monsters[id] = mpf;
        }

        if (mpf is null)
            return null;
        var palette = Palette($"mns{mpf.PaletteNumber:D3}.pal");
        if (palette is null)
            return null;
        double elapsed = motion is null ? double.MaxValue : Math.Max(0, (DateTimeOffset.UtcNow - motion.StartedAt).TotalMilliseconds);
        int attackStart = motion?.Animation switch
        {
            131 when mpf.Attack2FrameCount > 0 => mpf.Attack2FrameIndex,
            133 when mpf.Attack3FrameCount > 0 => mpf.Attack3FrameIndex,
            _ => mpf.AttackFrameIndex
        };
        int attackCount = motion?.Animation switch
        {
            131 when mpf.Attack2FrameCount > 0 => mpf.Attack2FrameCount,
            133 when mpf.Attack3FrameCount > 0 => mpf.Attack3FrameCount,
            _ => mpf.AttackFrameCount
        };
        int start = mpf.WalkFrameIndex, count = mpf.WalkFrameCount, tick = 0;
        if (motion?.Walking == true && elapsed < motion.DurationMilliseconds)
            tick = Math.Min(Math.Max(0, count - 1), (int)(elapsed * count / Math.Max(1, motion.DurationMilliseconds)));
        else if (motion is { Animation: > 0 } && attackCount > 0 && elapsed < attackCount * 300)
        {
            start = attackStart;
            count = attackCount;
            tick = (int)(elapsed / 300);
        }
        else
        {
            start = mpf.OptionalAnimationFrameCount > 0 ? mpf.StandingFrameIndex : mpf.WalkFrameIndex;
            count = mpf.OptionalAnimationFrameCount > 0 ? mpf.OptionalAnimationFrameCount : mpf.WalkFrameCount;
            int cycle = mpf.StandingFrameCount > 0 ? mpf.StandingFrameCount : mpf.OptionalAnimationFrameCount;
            tick = cycle > 0 ? (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / mpf.AnimationIntervalMs % cycle) : 0;
        }

        int directionOffset = pose.Front && start + count + tick < mpf.Frames.Count ? count : 0;
        int frameIndex = start + directionOffset + tick;
        if (frameIndex < 0 || frameIndex >= mpf.Frames.Count || mpf.Frames[frameIndex] is not { } frame)
            return null;
        int width = Math.Max(mpf.PixelWidth, frame.Left + frame.Width), height = Math.Max(mpf.PixelHeight, frame.Top + frame.Height);
        if (width is <= 0 or > 1024 || height is <= 0 or > 1024)
            throw new InvalidDataException("Invalid transformed sprite canvas.");
        var output = new byte[width * height * 4];
        for (int y = 0; y < frame.Height; y++)
            for (int x = 0; x < frame.Width; x++)
            {
                byte index = frame.Pixels.Span[y * frame.Width + x];
                if (index == 0)
                    continue;
                int targetX = frame.Left + x, targetY = frame.Top + y;
                if (pose.Flip)
                    targetX = width - 1 - targetX;
                var color = palette.AsSpan(index * 3, 3);
                if (dye != 0 && index is >= 98 and <= 103 && dyes.TryGetValue(0, out var original) && original.Length >= 18 && palette.AsSpan(98 * 3, 18).SequenceEqual(original.AsSpan(0, 18)) && dyes.TryGetValue(dye, out var colors) && colors.Length >= 18)
                    color = colors.AsSpan((index - 98) * 3, 3);
                color.CopyTo(output.AsSpan((targetY * width + targetX) * 4, 3));
                output[(targetY * width + targetX) * 4 + 3] = 255;
            }

        return new(width, height, output);
    }

    SpritePixels? RenderRest(CharacterAppearance look, SpritePose pose)
    {
        char gender = look.Body is 0x20 or 0x40 or 0x60 or 0x90 ? 'w' : 'm';
        bool emotion = pose.Front && pose.EmoteFrame >= 0;
        string name = $"{gender}_r_{look.RestPosition:D3}{(emotion ? "e" : "")}.spf";
        if (!restSprites.TryGetValue(name, out var bytes))
        {
            bytes = catalog.Read(name);
            restSprites[name] = bytes;
        }

        if (bytes is null)
            return null;
        var decoded = SpfSprite.DecodeFrame(bytes, emotion ? pose.EmoteFrame : pose.Front ? 1 : 0, false);
        if (decoded is null)
            return null;
        var output = new byte[Width * Height * 4];
        for (int y = 0; y < Math.Min(Height, decoded.Height); y++)
            for (int x = 0; x < decoded.Width; x++)
            {
                int targetX = Padding + x;
                if (pose.Flip)
                    targetX = 110 - targetX;
                if ((uint)targetX >= Width)
                    continue;
                decoded.Rgba.AsSpan((y * decoded.Width + x) * 4, 4).CopyTo(output.AsSpan((y * Width + targetX) * 4, 4));
            }

        return new(Width, Height, output);
    }
}
