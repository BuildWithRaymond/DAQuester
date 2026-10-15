using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;
public class JourneyProgressTests
{
    [Fact]
    public void Progress_counts_the_observed_NPC_continuation_instead_of_the_opening_tail()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content", "evidence"))) root = root.Parent;
        Assert.NotNull(root);
        var recipe = System.Text.Json.JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(root.FullName,
            "content/recipes/cap-band-of-danaan.v1.json")))!;
        using var capture = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "content/evidence/datester-7.41/danaan-blanchefleur-varuna-spirits.json")));
        var job = System.Text.Json.JsonSerializer.Deserialize<QuestJob>(capture.RootElement.GetProperty("Checkpoint").GetRawText())!;
        job = DanaanReplyContinuation.Select(job, recipe, job.Route!.SubmittedPage!, job.Route.LastReply!);
        Assert.Equal(27, QuestProgress.From(recipe, job).Total);
        Assert.Equal(11, QuestProgress.From(recipe, job).Current);
    }

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
