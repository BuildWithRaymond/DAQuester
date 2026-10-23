using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class CalloughMarkProgressTests
{
    static readonly DateTimeOffset At = new(2026, 10, 18, 4, 0, 0, TimeSpan.Zero);
    static readonly Dialog Reply = new(7, 12, 99, 1, "Callough", "You remind me of my own little child.", true, 177, 82, 0, []) { ObservedAt = At };
    static QuestJob Obtained => new() { Character = "Elara", Quest = "spare-a-stick", Step = "complete", Status = "PermanentlyCompleted", CompletedAt = At, PermanentCompletion = new(At, Reply) };

    [Fact]
    public void Explicit_retry_archives_the_outcome_and_preserves_its_cooldown()
    {
        var resumed = CalloughMarkProgress.Resume(Obtained, At);
        Assert.Null(resumed.PermanentCompletion);
        Assert.Equal(Reply, Assert.Single(resumed.CalloughOutcomes).Dialog);
        Assert.Equal(At, resumed.CompletedAt);
        Assert.Equal(At.AddHours(8), resumed.NextRun);
        Assert.True(resumed.Repeat);
        Assert.False(QuestRunPolicy.Finished(new("spare-a-stick", 1, "Stick", "", [], []) { Repeatable = true }, resumed));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("changed-outcome")]
    [InlineData("other-quest")]
    public void Retry_cannot_override_unfinished_or_different_work(string scenario)
    {
        var job = scenario switch
        {
            "pending" => Obtained with { Pending = new("turn-in", 7, 10, 0, 6, 0, At) },
            "other-quest" => Obtained with { Quest = "mundane-birth" },
            _ => Obtained
        };
        Assert.Throws<QuestBlockedException>(() => CalloughMarkProgress.Resume(job, scenario == "changed-outcome" ? At.AddMinutes(-1) : At));
    }

    [Fact]
    public async Task Archived_page_cannot_stop_a_retry_or_claim_another_outcome()
    {
        var resumed = CalloughMarkProgress.Resume(Obtained, At);
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(resumed);
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Name = "Elara", Generation = 7, Revision = 12, Dialog = Reply });
        bool stopped = false;
        var result = await PermanentQuestCompletion.ObserveAsync(journal, session.Snapshot, () => { stopped = true; return Task.CompletedTask; });
        Assert.Null(result);
        Assert.False(stopped);
        Assert.Null(journal.Load("Elara", "spare-a-stick").PermanentCompletion);
    }

    [Fact]
    public void The_next_outcome_stops_again_and_retains_the_previous_evidence()
    {
        var resumed = CalloughMarkProgress.Resume(Obtained, At);
        var next = PermanentQuestCompletion.Record(resumed, Reply with { Revision = 50, ObservedAt = At.AddDays(1) });
        Assert.NotNull(next.PermanentCompletion);
        Assert.False(next.Repeat);
        Assert.Equal(At.AddDays(1), next.CompletedAt);
        Assert.Contains(next.CalloughOutcomes, o => o.Dialog == Reply);
    }

    [Theory]
    [InlineData("Loved by Mileth Mundanes (12) - Deoch 223, Summer", true)]
    [InlineData("Loved by Mileth Mundanes - Deoch 223, Summer", true)]
    [InlineData("Loved by Suomi Mundanes (12)", false)]
    [InlineData("Not Loved by Mileth Mundanes (12)", false)]
    [InlineData("Loved by Mileth Mundanes (unknown)", false)]
    public void Legend_display_uses_the_observed_mark_without_inventing_a_number(string text, bool matches)
    {
        Assert.Equal(matches ? text : null, CalloughMarkProgress.Legend([text]));
    }
}
