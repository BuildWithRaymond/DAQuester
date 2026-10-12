using Avalonia;
using DAQuester.DarkAges.Rendering;

namespace DAQuester.App.Controls;

// Cropping is used for portraits and decorative accents, never the live sprite's frame anchor.
internal static class SpriteImageBounds
{
    public static Rect From(SpritePixels pixels)
    {
        int left = pixels.Width, top = pixels.Height, right = -1, bottom = -1;
        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                if (pixels.Rgba[(y * pixels.Width + x) * 4 + 3] == 0)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < left ? default : new(left, top, right - left + 1, bottom - top + 1);
    }
}
