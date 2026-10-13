using DAQuester.App.Controls;
using Xunit;

namespace DAQuester.AppTests;

public sealed class LegendRewardTests
{
    [Fact]
    public void Multiple_legend_shapes_match_source_order_and_single_shape_repeats_for_alternatives()
    {
        var parts = LegendRewardParts.Split("Gold + \"Saved daughter\" + \"Eased suffering\"", "legendshield.png, legendheart.png");
        Assert.Equal(new int?[] { 7, null, 6, null }, parts.Select(p => p.Icon));
        Assert.Equal("\"Saved daughter\" + \"Eased suffering\" · Gold", string.Concat(parts.Select(p => p.Text)));
        Assert.All(LegendRewardParts.Split("\"Alley\" or \"Garden\"", "legendshield.png").Where(p => p.Icon is not null), p => Assert.Equal(7, p.Icon));
    }

    [Theory]
    [InlineData("Chance: \"Loved\" + XP; otherwise Ring", "\"Loved\" (chance) + XP; otherwise Ring")]
    [InlineData("Stick Wooden Shield 3,500 Experience And a Chance of: \"Helped\" + Oak Stick", "\"Helped\" + Oak Stick (chance) · Stick Wooden Shield 3,500 Experience")]
    [InlineData("All Classes: Ring + XP Priests: \"Worshipper\" + Priest ring", "\"Worshipper\" + Priest ring (Priests only) · All Classes: Ring + XP")]
    public void Legend_leads_without_losing_reward_conditions(string input, string expected)
    {
        var parts = LegendRewardParts.Split(input, "legendaisling.png");
        Assert.Equal(0, parts[0].Icon);
        Assert.Equal(expected, string.Concat(parts.Select(p => p.Text)));
    }

    [Fact]
    public void Missing_shape_evidence_never_invents_icons_for_quoted_items()
    {
        var parts = LegendRewardParts.Split("\"Dirk\" + gold", "");
        Assert.All(parts, p => Assert.Null(p.Icon));
        Assert.Equal("\"Dirk\" + gold", string.Concat(parts.Select(p => p.Text)));
    }
}
