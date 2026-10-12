using Avalonia;
using DAQuester.App.Controls;
using DAQuester.DarkAges.Rendering;
using Xunit;

namespace DAQuester.AppTests;

public class SpriteImageBoundsTests
{
    [Fact]
    public void CropsOnlyTransparentPaddingAndKeepsTranslucentPixels()
    {
        var rgba = new byte[4 * 3 * 4];
        rgba[1 * 4 + 3] = 1;
        rgba[(2 * 4 + 3) * 4 + 3] = 255;
        Assert.Equal(new Rect(1, 0, 3, 3), SpriteImageBounds.From(new(4, 3, rgba)));
    }

    [Fact]
    public void EmptyArtworkHasNoPortraitOrAccentBounds()
    {
        Assert.Equal(default, SpriteImageBounds.From(new SpritePixels(2, 2, new byte[16])));
    }
}
