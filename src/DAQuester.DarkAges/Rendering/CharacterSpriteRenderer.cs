using Arbiter.Imaging.Formats;
using DAQuester.Core;

namespace DAQuester.DarkAges.Rendering;
public sealed record SpritePixels(int Width, int Height, byte[] Rgba);
// Composes original indexed layers with their original offsets, palettes and dyes.
// A renderer is owned by one preview worker; its caches are not shared across threads.
public sealed partial class CharacterSpriteRenderer
{
    readonly DatSpriteCatalog catalog;
    readonly Dictionary<string, EpfFile?> sprites = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, byte[]?> palettes = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<(char Kind, int Sprite, char Gender), int> paletteIds = [];
    readonly Dictionary<(char Kind, int Sprite), int> paletteRanges = [];
    readonly Dictionary<byte, byte[]> dyes = [];
    const int Width = 112, Height = 100, Padding = 27;
    public CharacterSpriteRenderer(string directory)
    {
        catalog = new(directory);
        foreach (string name in catalog.Names.Where(n => n.Length == 8 && n.StartsWith("pal", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".tbl", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = Text(name);
            while (reader?.ReadLine()is { } line)
            {
                var fields = line.Split((char[]? )null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length is not (2 or 3) || !int.TryParse(fields[0], out int id) || !int.TryParse(fields[1], out int palette))
                    continue;
                if (fields.Length == 3 && int.TryParse(fields[2], out int rangePalette) && rangePalette >= 0 && id >= 0 && palette >= id && palette <= 9999)
                {
                    for (int sprite = id; sprite <= palette; sprite++)
                        paletteRanges[(char.ToLowerInvariant(name[3]), sprite)] = rangePalette;
                    continue;
                }

                char gender = fields.Length == 2 ? '*' : fields[2] switch
                {
                    "-1" => 'm',
                    "-2" => 'w',
                    _ => '?'
                };
                if (gender != '?' && palette >= 0)
                    paletteIds[(char.ToLowerInvariant(name[3]), id, gender)] = palette;
            }
        }

        using var dyeReader = Text("color0.tbl");
        if (int.TryParse(dyeReader?.ReadLine(), out int count) && count is> 0 and <= 256)
        {
            while (dyeReader?.ReadLine()is { } line)
            {
                if (!byte.TryParse(line, out byte id))
                    continue;
                var colors = new byte[count * 3];
                bool valid = true;
                for (int index = 0; index < count; index++)
                {
                    var parts = dyeReader.ReadLine()?.Split(',', StringSplitOptions.TrimEntries);
                    if (parts?.Length != 3 || !byte.TryParse(parts[0], out colors[index * 3]) || !byte.TryParse(parts[1], out colors[index * 3 + 1]) || !byte.TryParse(parts[2], out colors[index * 3 + 2]))
                        valid = false;
                }

                if (valid)
                    dyes[id] = colors;
            }
        }
    }

    StreamReader? Text(string name) => catalog.Read(name)is { } bytes ? new(new MemoryStream(bytes)) : null;
    EpfFile? Sprite(string name)
    {
        if (sprites.TryGetValue(name, out var sprite))
            return sprite;
        if (sprites.Count >= 96)
            sprites.Clear();
        sprite = catalog.Read(name)is { } bytes ? EpfFile.Parse(bytes) : null;
        sprites[name] = sprite;
        return sprite;
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

    public SpritePixels? Render(CharacterAppearance look, SpritePose pose, CharacterMotion? motion = null)
        => RenderLayers(look, pose, motion, portrait: false);

    public SpritePixels? RenderPortrait(CharacterAppearance look)
        => RenderLayers(look, new("01", 5, false, true), null, portrait: true);

    SpritePixels? RenderLayers(CharacterAppearance look, SpritePose pose, CharacterMotion? motion, bool portrait)
    {
        if (look.Hidden)
            return null;
        if (look.Monster is { } monster)
            return RenderMonster(monster, pose, motion, look.HairColor);
        if (!portrait && look.RestPosition != 0)
            return RenderRest(look, pose);
        char gender = look.Body is 0x20 or 0x40 or 0x60 or 0x90 ? 'w' : 'm';
        var output = new byte[Width * Height * 4];
        bool bodyDrawn = false;
        bool headDrawn = false;
        // Face zero uses head skin from the body. This silhouette clips that skin
        // without painting a different face or including the torso.
        var headMask = portrait ? Sprite($"{gender}o{Math.Max(1, (int)look.Face):D3}01.epf")?.Frames.ElementAtOrDefault(5) : null;
        bool InHeadMask(int x, int y)
        {
            if (headMask is null)
                return false;
            int column = x - Padding - headMask.Left, row = y - headMask.Top;
            return (uint)column < headMask.Width && (uint)row < headMask.Height && headMask.Pixels.Span[row * headMask.Width + column] != 0;
        }

        void Draw(char kind, int id, byte dye = 0, bool skin = false, char? overrideGender = null, bool headSkin = false)
        {
            if (id < 0 || id > 9999)
                return;
            char sex = overrideGender ?? gender;
            var epf = Sprite($"{sex}{kind}{id:D3}{pose.Suffix}.epf");
            int frameIndex = pose.Frame;
            if (pose.Suffix == "04")
            {
                int perDirection = (epf?.Frames.Count ?? 0) / 2;
                if (perDirection > 0)
                    frameIndex = (pose.Front ? perDirection : 0) + pose.Frame % perDirection;
                else
                {
                    epf = Sprite($"{sex}{kind}{id:D3}01.epf");
                    frameIndex = pose.Front ? 5 : 0;
                }
            }

            if (epf is null || frameIndex >= epf.Frames.Count || epf.Frames[frameIndex] is not { } frame)
                return;
            char paletteKind = kind switch
            {
                'a' or 'b' or 'n' => 'b',
                'g' or 'j' => 'c',
                's' => 'p',
                _ => kind
            };
            int paletteId = paletteIds.GetValueOrDefault((paletteKind, id, sex), paletteIds.GetValueOrDefault((paletteKind, id, '*'), paletteRanges.GetValueOrDefault((paletteKind, id))));
            var palette = Palette(skin ? $"palm{look.SkinColor:D3}.pal" : $"pal{paletteKind}{paletteId:D3}.pal");
            if (palette is null)
                return;
            bool dyeable = dyes.TryGetValue(0, out var originalDye) && originalDye.Length >= 18 && palette.AsSpan(98 * 3, 18).SequenceEqual(originalDye.AsSpan(0, 18));
            int offsetX = Padding + (kind is 'w' or 'p' or 'c' or 'g' ? -27 : 0);
            for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                {
                    byte index = frame.Pixels.Span[y * frame.Width + x];
                    if (index == 0)
                        continue;
                    int targetX = offsetX + frame.Left + x, targetY = frame.Top + y;
                    if (pose.Flip)
                        targetX = 110 - targetX;
                    if ((uint)targetX >= Width || (uint)targetY >= Height)
                        continue;
                    if (headSkin && !InHeadMask(targetX, targetY))
                        continue;
                    var color = palette.AsSpan(index * 3, 3);
                    if (dyeable && dye != 0 && index is >= 98 and <= 103 && dyes.TryGetValue(dye, out var colors) && colors.Length >= 18)
                        color = colors.AsSpan((index - 98) * 3, 3);
                    if (color[0] == 255 && color[1] == 0 && color[2] == 255)
                        continue;
                    int offset = (targetY * Width + targetX) * 4;
                    color.CopyTo(output.AsSpan(offset, 3));
                    output[offset + 3] = look.Translucent || look.Body is 0x50 or 0x60 ? (byte)128 : (byte)255;
                    if (kind is 'm' or 'b')
                        bodyDrawn = true;
                    if (headSkin || kind is 'o' or 'f' or 'h' or 'e')
                        headDrawn = true;
                }
        }

        void Accessories(char kind)
        {
            if (look.Accessory1 > 0)
                Draw(kind, look.Accessory1, look.Accessory1Color);
            if (look.Accessory2 > 0)
                Draw(kind, look.Accessory2, look.Accessory2Color);
            if (look.Accessory3 > 0)
                Draw(kind, look.Accessory3, look.Accessory3Color);
        }

        void Head(char kind)
        {
            if (look.Head > 0)
                Draw(kind, look.Head, look.HairColor, overrideGender: look.Head == 103 ? 'm' : null);
        }

        void Armor()
        {
            int torso = look.Overcoat > 0 ? look.Overcoat : look.Armor;
            int arms = look.Overcoat > 0 ? look.Overcoat : look.Arms;
            byte dye = look.Overcoat > 0 ? look.OvercoatColor : (byte)0;
            if (torso > 0)
                Draw(torso >= 1000 ? 'i' : 'u', torso >= 1000 ? torso - 1000 : torso, dye);
            if (arms > 0)
                Draw(arms >= 1000 ? 'j' : 'a', arms >= 1000 ? arms - 1000 : arms, dye);
        }

        int body = look.Body switch
        {
            0x00 => 0,
            0x30 or 0x40 => 2,
            0x70 => 4,
            0x80 or 0x90 => 5,
            0xA0 => 10,
            0xB0 => 11,
            _ => 1
        };
        if (portrait)
        {
            if (body is not (0 or 5))
                Draw('b', body, headSkin: true);
            Head('f');
            if (body is 1 or 5)
                Draw('m', body, skin: true, headSkin: true);
            if (look.Face > 0)
                Draw('o', look.Face, skin: true);
            Head('h');
            Head('e');
            return headDrawn ? new(Width, Height, output) : null;
        }

        if (body is not (0 or 5))
            Draw('b', body);
        if (pose.Front)
            Head('f');
        Accessories('g');
        if (!pose.Front && look.Shield > 0)
            Draw('s', look.Shield, overrideGender: 'm');
        if (body is 1 or 5)
            Draw('m', body, skin: true);
        // Zero packed pants color removes the part; an overcoat replaces ordinary pants.
        if (look.PantsColor > 0 && look.Overcoat == 0)
            Draw('n', 1, look.PantsColor);
        if (look.Face > 0)
            Draw('o', look.Face, skin: true);
        if (pose.Front && pose.EmoteFrame >= 0)
            DrawEmotion(output, look, pose);
        if (look.Boots > 0)
            Draw('l', look.Boots, look.BootsColor);
        if (pose.Front)
            Head('h');
        Armor();
        Head('e');
        if (!pose.Front)
        {
            Head('h');
            Head('f');
        }

        if (look.Weapon > 0)
        {
            Draw('w', look.Weapon);
            Draw('p', look.Weapon);
        }

        if (pose.Front && look.Shield > 0)
            Draw('s', look.Shield, overrideGender: 'm');
        Accessories('c');
        return bodyDrawn ? new(Width, Height, output) : null;
    }
}
