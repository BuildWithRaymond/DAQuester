using DAQuester.App.Controls;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class CharacterPreviewTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void DefaultCharacterOnlyAppearsBeforeLiveAppearanceLoads(bool connected, bool hasAppearance, bool hidden, bool expected)
    {
        var snapshot = new CharacterSnapshot
        {
            Connected = connected,
            Appearance = hasAppearance ? new(1, 1, 0x10, 2, 0, 1, 1, 6, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, hidden, false) : null
        };

        Assert.Equal(expected, CharacterPreviewAppearance.From(snapshot).IsDefault);
    }

    [Fact]
    public void EmptyCharacterBoxSelectsClothedDefaultWithoutInventingAccountData()
    {
        var snapshot = new CharacterSnapshot();
        var preview = CharacterPreviewAppearance.From(snapshot);
        Assert.True(preview.IsDefault);
        Assert.True(preview.Look.PantsColor > 0);
        Assert.False(preview.Look.Hidden);
        Assert.Null(snapshot.Appearance);
        Assert.Null(snapshot.Name);
        Assert.Null(snapshot.Motion);
        Assert.True(CharacterPreviewAppearance.From(null).IsDefault);
    }

    [Fact]
    public void LoadedCharacterKeepsExactObservedOutfitAndAction()
    {
        var look = CharacterPreviewAppearance.From(null).Look with { Armor = 15, PantsColor = 0 };
        var motion = new CharacterMotion(3, 1, DateTimeOffset.UtcNow, 350, false);
        var preview = CharacterPreviewAppearance.From(new() { Connected = true, Appearance = look, Motion = motion });
        Assert.False(preview.IsDefault);
        Assert.Same(look, preview.Look);
        Assert.Same(motion, preview.Motion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RememberedOutfitShowsIdleUntilFreshAppearanceArrives(bool connected)
    {
        var look = CharacterPreviewAppearance.From(null).Look with { Head = 9, Face = 0 };
        var snapshot = new CharacterSnapshot { Connected = connected };
        var preview = CharacterPreviewAppearance.From(snapshot, look);
        Assert.Same(look, preview.Look);
        Assert.True(preview.IsRemembered);
        Assert.False(preview.IsDefault);
        Assert.False(preview.Motion!.Walking);
        Assert.Equal(0, preview.Motion.Animation);
        Assert.Null(snapshot.Appearance);
    }

    [Fact]
    public void LiveLookOverridesRememberedOutfit()
    {
        var saved = CharacterPreviewAppearance.From(null).Look;
        var live = saved with { Head = 9, Face = 0 };
        var preview = CharacterPreviewAppearance.From(new() { Connected = true, Appearance = live }, saved);
        Assert.Same(live, preview.Look);
        Assert.False(preview.IsRemembered);
    }
}
