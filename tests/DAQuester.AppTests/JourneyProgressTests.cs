using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;
public class JourneyProgressTests
{
    static readonly QuestDefinition Quest = new("test", 1, "Test quest", "", [], [new("prepare", "prepare", "travel"), new("travel", "travel", "confirm"), new("confirm", "confirm", "complete")]);
    [Theory]
    [InlineData("prepare", 0, "Step 1 of 3")]
    [InlineData("travel", 1, "Step 2 of 3")]
    [InlineData("confirm", 2, "Step 3 of 3")]
    [InlineData("complete", 3, "3 of 3 steps complete")]
    [InlineData("obsolete", 0, "3 steps · checkpoint unavailable")]
    public void CountsRecipeStepsWithoutInventingCompletion(string checkpoint, int completed, string label)
    {
        var progress = QuestProgress.From(Quest, new() { Step = checkpoint });
        Assert.Equal(3, progress.Total);
        Assert.Equal(completed, progress.Completed);
        Assert.Equal(label, progress.Label);
    }

    [Fact]
    public void PendingTerminalActionDoesNotDisplayCompletedProgress()
    {
        var progress = QuestProgress.From(Quest, new() { Step = "complete", Pending = new("test", 1, 1, 0, 0, 0, DateTimeOffset.UtcNow) });
        Assert.Equal(0, progress.Completed);
        Assert.NotEqual(100, progress.Percent);
    }

    [Fact]
    public void ManualGuideHasNoInventedStepCount()
    {
        Assert.Equal("Select an automatic quest to see its steps", QuestProgress.From(null, new()).Label);
    }
}
