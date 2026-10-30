using System.Text.Json;
using DAQuester.App;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class DaydreamerPresentationTests
{
    const string Id = "avid-daydreamer";
    static QuestDefinition Recipe() => new(Id, 1, "Avid Temuairian Daydreamer", "synthetic", [],
        [new("preflight", "daydreamer-preflight", "run"), new("run", "daydreamer-check-in", "complete")], 24);

    [Fact]
    public void Completing_one_daily_attempt_displays_the_server_count_toward_thirty()
    {
        var job = JsonSerializer.Deserialize<QuestJob>("""
            {"Quest":"avid-daydreamer","Step":"complete","CompletedAt":"2026-10-28T12:00:00Z","Daydreamer":{"Streak":12}}
            """)!;
        var progress = QuestProgress.From(Recipe(), job);
        Assert.Equal(30, progress.Total);
        Assert.Equal(12, progress.Completed);
        Assert.Contains("12", progress.Label);
        Assert.Contains("check-in", progress.Label);
    }

    [Fact]
    public async Task Quest_details_explain_registration_and_reported_repeat_rollover_without_a_class_gate()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(runtime.Snapshot("Elara") with { Class = null, Level = null });
            var vm = new MainWindowViewModel(runtime) { Filter = "All", LegendMarksOnly = true };
            var quest = Assert.Single(vm.Quests, q => q.RecipeId == Id);
            Assert.Equal("Avid Temuairian Daydreamer", quest.Name);
            Assert.True(quest.HasLegendReward);
            Assert.True(quest.HasNotice);
            Assert.Contains("30", quest.NoticeText);
            Assert.Contains("registration", quest.NoticeText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("27", quest.NoticeText);
            Assert.Contains("reported", quest.NoticeText, StringComparison.OrdinalIgnoreCase);
            Assert.False(runtime.RepeatEnabled("Elara", Id));
            Assert.Contains("reported", quest.Level, StringComparison.OrdinalIgnoreCase);
        }
        finally { await runtime.ShutdownAsync(); }
    }
}
