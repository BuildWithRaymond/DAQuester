using Avalonia;
using DAQuester.App.Controls;
using DAQuester.App.ViewModels;
using Xunit;

namespace DAQuester.AppTests;

public class QuestTrailTests
{
    [Theory]
    [InlineData(240)]
    [InlineData(300)]
    public void LongJourneyReservesRowsInsteadOfShrinkingMarkers(double width)
    {
        var trail = new QuestTrail { Steps = new(32, 1, 2, "Stage 2 of 32") };

        trail.Measure(new Size(width, double.PositiveInfinity));

        // Thirty-two readable markers need several rows in a narrow journey card.
        Assert.InRange(trail.DesiredSize.Height, 72, 120);
        Assert.InRange(trail.DesiredSize.Width, width - 1, width);
    }

    [Fact]
    public void NarrowerCardAddsRowsAndChangingQuestMeasuresAgain()
    {
        var trail = new QuestTrail { Steps = new(32, 1, 2, "Stage 2 of 32") };
        trail.Measure(new Size(300, double.PositiveInfinity));
        double wideHeight = trail.DesiredSize.Height;

        trail.Measure(new Size(180, double.PositiveInfinity));
        Assert.True(trail.DesiredSize.Height > wideHeight);

        trail.Steps = new(3, 1, 2, "Step 2 of 3");
        trail.Measure(new Size(180, double.PositiveInfinity));
        Assert.InRange(trail.DesiredSize.Height, 20, 28);
    }

    [Theory]
    [InlineData(180)]
    [InlineData(240)]
    [InlineData(300)]
    public void WrappedStagesHaveRoomForNormalDotsAndStayInReadingOrder(double width)
    {
        var layout = QuestTrailLayout.Create(32, width);
        double diameter = QuestTrailLayout.Radius * 2;
        var first = layout.Center(0);
        var rowEnd = layout.Center(layout.Columns - 1);
        Assert.InRange(first.X, diameter / 2, diameter);
        Assert.InRange(rowEnd.X, width - diameter, width - diameter / 2);

        for (int index = 0; index < layout.Count; index++)
        {
            var center = layout.Center(index);
            Assert.InRange(center.X, diameter / 2, width - diameter / 2);
            Assert.InRange(center.Y, diameter / 2, layout.Height - diameter / 2);
            if (index == 0)
                continue;
            var previous = layout.Center(index - 1);
            if (center.Y == previous.Y)
                Assert.True(center.X - previous.X >= diameter * 2);
            else
                Assert.True(center.Y - previous.Y >= diameter * 2);
        }
    }
}
