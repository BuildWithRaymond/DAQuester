using System.Buffers.Binary;

namespace DAQuester.DarkAges.Rendering;
// SPF stores an RGB565 palette or RGB565 pixels, followed by a second RGB555 copy.
// The installed client uses the first copy. Layout follows DALib (MIT; see notices).
public static class SpfSprite
{
    public static SpritePixels? DecodeFrame(ReadOnlySpan<byte> data, int frameIndex, bool flip)
    {
        if (data.Length < 16)
            throw new InvalidDataException("Truncated SPF header.");
        uint format = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        if (format > 1)
            throw new InvalidDataException("Unknown SPF pixel format.");
        int paletteSize = format == 0 ? 1024 : 0;
        int tableStart = 16 + paletteSize;
        if (data.Length < tableStart)
            throw new InvalidDataException("Truncated SPF palette.");
        int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data[(12 + paletteSize)..]));
        if (count is < 0 or > 4096 || (long)tableStart + count * 32L + 4 > data.Length)
            throw new InvalidDataException("Invalid SPF frame table.");
        if (frameIndex < 0 || frameIndex >= count)
            return null;
        var entry = data.Slice(tableStart + frameIndex * 32, 32);
        int left = BinaryPrimitives.ReadUInt16LittleEndian(entry), top = BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]);
        int right = BinaryPrimitives.ReadUInt16LittleEndian(entry[4..]), bottom = BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]);
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[16..]));
        int stride = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[20..]));
        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[24..]));
        int pixelSize = format == 0 ? 1 : 2;
        int width = right - left, height = bottom - top;
        int dataStart = tableStart + count * 32 + 4;
        if (width <= 0 || height <= 0)
            return null;
        if (right > 1024 || bottom > 1024 || stride < width * pixelSize || (long)stride * height > length || start < 0 || (long)dataStart + start + length > data.Length)
            throw new InvalidDataException("Invalid SPF pixel bounds.");
        var output = new byte[right * bottom * 4];
        var pixels = data.Slice(dataStart + start, length);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int source = y * stride + x * pixelSize;
                byte paletteIndex = pixels[source];
                ushort color = format == 0 ? BinaryPrimitives.ReadUInt16LittleEndian(data[(12 + paletteIndex * 2)..]) : BinaryPrimitives.ReadUInt16LittleEndian(pixels[source..]);
                if (format == 0 && paletteIndex == 0 || color is 0 or 0xF81F)
                    continue;
                int targetX = flip ? right - 1 - (left + x) : left + x;
                int offset = ((top + y) * right + targetX) * 4;
                int red = color >> 11, green = color >> 5 & 63, blue = color & 31;
                output[offset] = (byte)(red * 255 / 31);
                output[offset + 1] = (byte)(green * 255 / 63);
                output[offset + 2] = (byte)(blue * 255 / 31);
                output[offset + 3] = 255;
            }

        return new(right, bottom, output);
    }
}
