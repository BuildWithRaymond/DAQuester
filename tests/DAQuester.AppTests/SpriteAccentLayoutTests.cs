using System.Reflection;
using Avalonia;
using DAQuester.App.Controls;
using Xunit;

namespace DAQuester.AppTests;

public class SpriteAccentLayoutTests
{
    static readonly Size Bitmap = new(112, 100);
    static readonly Size Preview = new(112, 112);
    static readonly Size Area = new(500, 240);
    static readonly Rect Foreground = new(0, 8, 112, 100);

    [Theory]
    [InlineData(20, 20, 70, 65)]
    [InlineData(20, 20, 75, 65)]
    [InlineData(45, 30, 20, 42)]
    [InlineData(35, 15, 40, 70)]
    public void ChangingFrameOutlineDoesNotMoveAnUnchangedBodyPixel(double x, double y, double width, double height)
    {
        var layout = new SpriteAccentLayout();
        var idle = new Rect(40, 25, 30, 55);
        var moving = new Rect(x, y, width, height);
        var bodyPixel = new Point(55, 60);
        var before = layout.Destination(idle, Bitmap, Foreground, Preview, Area);
        var after = layout.Destination(moving, Bitmap, Foreground, Preview, Area);

        Assert.Equal(Project(bodyPixel, idle, before), Project(bodyPixel, moving, after));
        Assert.Equal(5.5, after.Width / moving.Width);
        Assert.Equal(5.5, after.Height / moving.Height);
    }

    [Fact]
    public void CanvasCenterStaysBehindSmallSpriteDespiteAnAsymmetricWeapon()
    {
        var layout = new SpriteAccentLayout();
        var content = new Rect(35, 25, 65, 55);
        var destination = layout.Destination(content, Bitmap, Foreground, Preview, Area);

        Assert.Equal(Area.Width - Preview.Width / 2, Project(new Point(Bitmap.Width / 2, 80), content, destination).X);
    }

    [Fact]
    public void ClearingPreviewReanchorsTheNextCharacter()
    {
        var preview = new CharacterSpritePreview();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var layout = (SpriteAccentLayout)typeof(CharacterSpritePreview).GetField("accentLayout", flags)!.GetValue(preview)!;
        layout.Destination(new Rect(40, 25, 30, 55), Bitmap, Foreground, Preview, Area);

        typeof(CharacterSpritePreview).GetMethod("Clear", flags)!.Invoke(preview, null);
        var next = new Rect(35, 20, 40, 45);

        Assert.Equal(new SpriteAccentLayout().Destination(next, Bitmap, Foreground, Preview, Area),
            layout.Destination(next, Bitmap, Foreground, Preview, Area));
    }

    [Fact]
    public void ResizingMovesTheAnchorWithTheForegroundWithoutChangingEnlargement()
    {
        var layout = new SpriteAccentLayout();
        var content = new Rect(40, 25, 30, 55);
        layout.Destination(content, Bitmap, Foreground, Preview, Area);
        var preview = new Size(80, 80);
        var area = new Size(320, 210);
        var foreground = new Rect(0, 76 - 100 * 80d / 112, 80, 100 * 80d / 112);
        var destination = layout.Destination(content, Bitmap, foreground, preview, area);
        var anchor = Project(new Point(56, 80), content, destination);

        Assert.Equal(area.Width - preview.Width / 2, anchor.X);
        Assert.Equal(area.Height - preview.Height + foreground.Top + 80 * foreground.Width / Bitmap.Width, anchor.Y, 10);
        Assert.Equal(5.5, destination.Width / content.Width);
    }

    static Point Project(Point pixel, Rect source, Rect destination) => new(
        destination.Left + (pixel.X - source.Left) * destination.Width / source.Width,
        destination.Top + (pixel.Y - source.Top) * destination.Height / source.Height);
}
