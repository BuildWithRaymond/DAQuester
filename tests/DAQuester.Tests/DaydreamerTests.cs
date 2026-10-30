using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DaydreamerTests
{
    const string Greeting = "Thank you for dropping by. I'll make sure to check you in!";
    const string AlreadyDone = "You have already checked in today.";
    const string Denied = "This feature is for registered users only.";

    sealed class Session : SimulatedSession, IDaydreamerSession, IGameSession
    {
        public Func<Dialog, Task<DaydreamerSubmission>>? Submit;
        public Func<Func<CharacterSnapshot, bool>, long, TimeSpan, CancellationToken, Task<CharacterSnapshot>>? Waiting;
        public int Submissions;
        public Task<DaydreamerSubmission> SubmitDaydreamerAsync(Dialog page, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Submissions++;
            return Submit!(page);
        }
        public new Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken ct) =>
            Waiting?.Invoke(predicate, after, timeout, ct) ?? base.WaitAsync(predicate, after, timeout, ct);
    }

    sealed class Journal : IQuestJournal
    {
        public QuestJob? Saved;
        public QuestJob Load(string character, string quest) => Saved ?? new() { Character = character, Quest = quest };
        public Task SaveAsync(QuestJob job, CancellationToken ct = default) { Saved = job; return Task.CompletedTask; }
    }

    static ServerNotice Status(int count, long generation = 1, long revision = 20, DateTimeOffset? at = null) =>
        new(generation, revision, 3, $"This is your {count}th consecutive Login!", at ?? DateTimeOffset.UtcNow);
    static Session Ready()
    {
        var s = new Session();
        s.Set(s.Snapshot with { Name = "Lancelot", Map = DaydreamerEvidence.Map, Position = DaydreamerEvidence.Approach,
            Registration = new(1, 1, 3, "This account is on auto-renewal", DateTimeOffset.UtcNow),
            Entities = ImmutableDictionary<uint, Entity>.Empty.Add(77, new(77, "Celesta", new(49, 18), true)) });
        return s;
    }
    static Dialog Page(Session s, string text = Greeting, bool next = true) => new(s.Snapshot.Generation, s.Snapshot.Revision + 1,
        77, 1, "Celesta", text, true, 0x1235, 7, 0, [], HasNext: next) { ObservedAt = DateTimeOffset.UtcNow };
    static Dialog Menu(Session s) => new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 77, 1, "Celesta", "Welcome", false,
        0, 0, 0, [new(" Avid Daydreamer ", 0x1234, 1)]) { ObservedAt = DateTimeOffset.UtcNow };
    static QuestContext Context(Session s, Journal journal, QuestJob? job = null) => new(s, journal, new(), job ??
        new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id });
    static Task Run(QuestContext c) => new DaydreamerActions().ExecuteAsync(c, new("check-in", "daydreamer-check-in", "complete"), default);
    static void Menus(Session s)
    {
        s.OnCommand = command =>
        {
            if (command.Kind == "open") s.Set(s.Snapshot with { Dialog = Menu(s) });
            if (command.Kind == "choose") s.Set(s.Snapshot with { Dialog = Page(s) });
            if (command.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
    }
    static void SubmitCount(Session s, int count, Journal journal)
    {
        s.Submit = page =>
        {
            Assert.Equal("daydreamer-submit", journal.Saved?.Pending?.Kind);
            Assert.Equal(page, journal.Saved?.Daydreamer?.Source);
            var actual = new DaydreamerSubmission(page, s.Snapshot.Revision, DateTimeOffset.UtcNow);
            var result = Status(count, page.Generation, s.Snapshot.Revision + 1);
            s.Set(s.Snapshot with { DaydreamerStatus = result, ActiveStatus = result, Dialog = Page(s, "Your check-in is recorded.", false) });
            return Task.FromResult(actual);
        };
    }

    [Theory]
    [InlineData("This is your 1st consecutive Login!", 1)]
    [InlineData("This is your 2nd consecutive Login!", 2)]
    [InlineData("This is your 30th consecutive Login!", 30)]
    [InlineData("This is your 30 consecutive Login!", 30)]
    [InlineData("This is your 0th consecutive Login!", null)]
    [InlineData("Player: This is your 30th consecutive Login!", null)]
    [InlineData("This is your 999999999999999999999 consecutive Login!", null)]
    public void Exact_server_count_requires_a_positive_bounded_integer(string text, int? expected) =>
        Assert.Equal(expected, DaydreamerEvidence.Count(new(1, 1, 3, text, DateTimeOffset.UtcNow)));

    [Fact]
    public void Chat_cannot_prove_count_or_registration()
    {
        Assert.Null(DaydreamerEvidence.Count(Status(30) with { Type = 0 }));
        Assert.False(DaydreamerEvidence.IsRegistration(new(1, 1, 0, "This account is on auto-renewal", DateTimeOffset.UtcNow)));
        Assert.False(DaydreamerEvidence.IsRegistration(new(1, 1, 3, "Player: Your expiration date is 9-11", DateTimeOffset.UtcNow)));
    }

    [Theory]
    [InlineData("2026-03-08T06:00:00Z", "2026-03-09T06:00:00Z")]
    [InlineData("2026-11-01T05:00:00Z", "2026-11-02T06:00:00Z")]
    [InlineData("2026-09-09T00:07:00Z", "2026-09-10T00:07:00Z")]
    public void Daily_wait_requires_twenty_four_real_hours_and_next_civil_day(string at, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), DaydreamerEvidence.NextEligible(DateTimeOffset.Parse(at), "America/Chicago"));

    [Fact]
    public async Task Daily_check_in_saves_submission_and_the_actual_count_before_closing()
    {
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        SubmitCount(s, 4, journal);
        await Run(c);
        Assert.Equal(4, c.Job.Daydreamer?.Streak);
        Assert.Equal(1, s.Submissions);
        Assert.Null(c.Job.Pending);
        Assert.False(c.Job.Daydreamer!.CleanupPending);
        Assert.Equal(c.Job.Daydreamer.LastAcknowledgement!.At, c.Job.CompletedAt);
        Assert.Equal(c.Job.Daydreamer.NextCheckInAt, c.Job.NextRun);
        Assert.True(c.Job.NextRun >= c.Job.CompletedAt!.Value.AddHours(24));
        Assert.False(DaydreamerEvidence.GoalVerified(c.Job));
        Assert.True(DaydreamerEvidence.AttemptVerified(c.Job));
        var selected = Assert.Single(s.Commands, x => x.Kind == "choose");
        Assert.Equal((ushort)0x1234, selected.Dialog!.Choices.Single().Pursuit);
    }

    [Fact]
    public async Task Count_followed_by_unrelated_status_is_not_lost()
    {
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        s.Submit = page =>
        {
            var sent = new DaydreamerSubmission(page, s.Snapshot.Revision, DateTimeOffset.UtcNow);
            var count = Status(12, page.Generation, s.Snapshot.Revision + 1);
            s.Set(s.Snapshot with { DaydreamerStatus = count, ActiveStatus = count, Dialog = null });
            s.Set(s.Snapshot with { DaydreamerStatus = null, ActiveStatus = count with { Revision = s.Snapshot.Revision + 1, Text = "Unrelated active message" } });
            return Task.FromResult(sent);
        };
        await Run(c);
        Assert.Equal(12, c.Job.Daydreamer?.Streak);
    }

    [Fact]
    public async Task Thirty_completes_without_reading_a_legend_and_defaults_to_no_more_check_ins()
    {
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        SubmitCount(s, 30, journal);
        await Run(c);
        Assert.True(DaydreamerEvidence.GoalVerified(c.Job));
        Assert.NotNull(c.Job.Daydreamer?.FirstThirtyReceipt);
        Assert.Null(c.Job.NextRun);
        Assert.False(c.Job.Repeat);
        Assert.DoesNotContain(s.Commands, x => x.Kind == "inspect-self");
        await Run(c);
        Assert.Equal(1, s.Submissions);
    }

    [Fact]
    public async Task Explicit_continuation_preserves_first_goal_and_schedules_another_observed_check_in()
    {
        var first = Status(30, at: DateTimeOffset.UtcNow.AddDays(-2));
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal, new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id, Repeat = true,
            Daydreamer = new() { Streak = 30, FirstThirtyReceipt = first } });
        Menus(s);
        SubmitCount(s, 31, journal);
        await Run(c);
        Assert.Equal(first, c.Job.Daydreamer?.FirstThirtyReceipt);
        Assert.Equal(31, c.Job.Daydreamer?.Streak);
        Assert.NotNull(c.Job.NextRun);
    }

    [Fact]
    public async Task A_lower_actual_count_replaces_progress_without_erasing_an_earlier_goal()
    {
        var first = Status(30, at: DateTimeOffset.UtcNow.AddDays(-2));
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal, new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id, Repeat = true,
            Daydreamer = new() { Streak = 29, FirstThirtyReceipt = first } });
        Menus(s);
        SubmitCount(s, 1, journal);
        await Run(c);
        Assert.Equal(1, c.Job.Daydreamer?.Streak);
        Assert.Equal(29, c.Job.Daydreamer?.PreviousCount);
        Assert.Equal(first, c.Job.Daydreamer?.FirstThirtyReceipt);
    }

    [Fact]
    public async Task Already_done_dialog_acknowledges_the_day_without_inventing_streak_credit()
    {
        var s = Ready();
        var oldSuccess = DateTimeOffset.UtcNow.AddDays(-2);
        var journal = new Journal();
        var c = Context(s, journal, new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id,
            Daydreamer = new() { Streak = 7, LastSuccessAt = oldSuccess } });
        Menus(s);
        s.OnCommand = command =>
        {
            if (command.Kind == "open") s.Set(s.Snapshot with { Dialog = Menu(s) });
            if (command.Kind == "choose") s.Set(s.Snapshot with { Dialog = Page(s, AlreadyDone, false) });
            if (command.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
        await Run(c);
        Assert.Equal(7, c.Job.Daydreamer?.Streak);
        Assert.Equal(oldSuccess, c.Job.Daydreamer?.LastSuccessAt);
        Assert.NotNull(c.Job.Daydreamer?.LastAcknowledgement?.Dialog);
        Assert.Null(c.Job.Daydreamer?.LastAcknowledgement?.Status);
        Assert.NotNull(c.Job.NextRun);
        Assert.Equal(0, s.Submissions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_previous_generation_registration_blocks_without_interaction(bool oldGeneration)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Registration = oldGeneration ? s.Snapshot.Registration! with { Generation = 0 } : null });
        var c = Context(s, new());
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.Empty(s.Commands);
        Assert.Equal(0, s.Submissions);
    }

    [Fact]
    public async Task Future_due_time_yields_without_opening_Celesta()
    {
        var s = Ready();
        var due = DateTimeOffset.UtcNow.AddHours(23);
        var c = Context(s, new(), new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id,
            Daydreamer = new() { NextCheckInAt = due } });
        var wait = await Assert.ThrowsAsync<QuestScheduledWaitException>(() => Run(c));
        Assert.Equal(due, wait.Next);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_unoffered_greeting_preserves_the_popup(bool missingNext)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, missingNext ? Greeting : "Unknown permanent choice", !missingNext) });
        var c = Context(s, new());
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.Equal(0, s.Submissions);
        Assert.DoesNotContain(s.Commands, x => x.Kind is "next" or "close");
    }

    [Fact]
    public async Task Timeout_after_preparing_submission_never_sends_it_again()
    {
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        s.Submit = page =>
        {
            s.Set(s.Snapshot with { DaydreamerStatus = Status(30, page.Generation, s.Snapshot.Revision + 1), Dialog = null });
            throw new TimeoutException("Ambiguous synthetic submission");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => Run(c));
        Assert.NotNull(c.Job.Pending);
        Assert.Null(c.Job.Daydreamer?.Submission);
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.Equal(1, s.Submissions);
        Assert.Null(c.Job.CompletedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Uncorrelated_count_cannot_complete_a_submitted_attempt(bool beforeSend)
    {
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        s.Submit = page =>
        {
            var baseline = s.Snapshot.Revision;
            var sentAt = DateTimeOffset.UtcNow;
            var count = Status(30, beforeSend ? page.Generation : page.Generation + 1,
                beforeSend ? baseline : baseline + 1, sentAt.AddSeconds(-1));
            s.Set(s.Snapshot with { DaydreamerStatus = count, Dialog = null });
            return Task.FromResult(new DaydreamerSubmission(page, baseline, sentAt));
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.NotNull(c.Job.Pending);
        Assert.False(DaydreamerEvidence.GoalVerified(c.Job));
        Assert.Null(c.Job.CompletedAt);
    }

    [Fact]
    public async Task Saved_submission_reconciles_a_late_count_without_another_Next()
    {
        var s = Ready();
        var page = Page(s);
        s.Set(s.Snapshot with { Dialog = page });
        var sentAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        var submission = new DaydreamerSubmission(page, s.Snapshot.Revision, sentAt);
        var count = Status(8, page.Generation, page.Revision + 1);
        s.Set(s.Snapshot with { DaydreamerStatus = count, Dialog = null });
        var c = Context(s, new(), new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id,
            Pending = new("daydreamer-submit", page.Generation, page.Revision - 1, 0, 0, 0, sentAt) { EntityId = page.EntityId, EntityType = page.EntityType },
            Daydreamer = new() { Source = page, Submission = submission } });
        await Run(c);
        Assert.Equal(8, c.Job.Daydreamer?.Streak);
        Assert.Null(c.Job.Pending);
        Assert.Equal(0, s.Submissions);
    }

    [Fact]
    public async Task Reward_receipt_survives_cleanup_timeout_and_resume_only_closes()
    {
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        SubmitCount(s, 30, journal);
        var menus = s.OnCommand;
        s.OnCommand = command =>
        {
            if (command.Kind == "close") throw new TimeoutException("Synthetic close timeout");
            menus!(command);
        };
        await Assert.ThrowsAnyAsync<Exception>(() => Run(c));
        Assert.True(DaydreamerEvidence.GoalVerified(c.Job));
        Assert.Null(c.Job.Pending);
        Assert.True(c.Job.Daydreamer!.CleanupPending);
        s.OnCommand = command => { if (command.Kind == "close") s.Set(s.Snapshot with { Dialog = null }); };
        await Run(c);
        Assert.False(c.Job.Daydreamer.CleanupPending);
        Assert.Equal(1, s.Submissions);
    }

    [Fact]
    public async Task Registration_denial_does_not_acknowledge_a_daily_attempt()
    {
        var s = Ready();
        s.OnCommand = command =>
        {
            if (command.Kind == "open") s.Set(s.Snapshot with { Dialog = Menu(s) });
            if (command.Kind == "choose") s.Set(s.Snapshot with { Dialog = Page(s, Denied, false) });
        };
        var c = Context(s, new());
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.Null(c.Job.CompletedAt);
        Assert.Null(c.Job.Daydreamer?.LastAcknowledgement);
        Assert.Equal(0, s.Submissions);
    }

    [Fact]
    public async Task A_count_before_terminal_dialog_arrival_waits_for_cleanup_without_another_submission()
    {
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        Task terminalArrival = Task.CompletedTask;
        s.Submit = page =>
        {
            var sent = new DaydreamerSubmission(page, s.Snapshot.Revision, DateTimeOffset.UtcNow);
            var count = Status(30, page.Generation, s.Snapshot.Revision + 1);
            s.Set(s.Snapshot with { DaydreamerStatus = count });
            terminalArrival = Task.Run(async () =>
            {
                await Task.Delay(25);
                s.Set(s.Snapshot with { Dialog = Page(s, "Your check-in is recorded.", false) });
            });
            return Task.FromResult(sent);
        };
        try { await Run(c); }
        finally { await terminalArrival; }
        Assert.True(DaydreamerEvidence.GoalVerified(c.Job));
        Assert.False(c.Job.Daydreamer!.CleanupPending);
        Assert.Equal(1, s.Submissions);
        Assert.Single(s.Commands, x => x.Kind == "close");
    }

    [Fact]
    public async Task A_repeated_count_refreshes_the_daily_wait_without_inventing_new_streak_credit()
    {
        var s = Ready();
        var journal = new Journal();
        var old = Status(7, at: DateTimeOffset.UtcNow.AddDays(-2));
        var c = Context(s, journal, new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id,
            Daydreamer = new() { Streak = 7, LastSuccessAt = old.ObservedAt, LastCountReceipt = old } });
        Menus(s);
        SubmitCount(s, 7, journal);
        await Run(c);
        Assert.Equal(7, c.Job.Daydreamer?.Streak);
        Assert.Equal(old.ObservedAt, c.Job.Daydreamer?.LastSuccessAt);
        Assert.True(c.Job.Daydreamer?.LastAcknowledgement?.At > old.ObservedAt);
        Assert.True(c.Job.NextRun >= c.Job.Daydreamer!.LastAcknowledgement!.At.AddHours(24));
    }

    [Fact]
    public async Task A_completed_goal_and_pending_cleanup_survive_journal_reload_and_reconnect()
    {
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString());
        var journal = new FileQuestJournal(path);
        var s = Ready();
        var c = new QuestContext(s, journal, new(), new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id });
        Menus(s);
        s.Submit = page =>
        {
            var sent = new DaydreamerSubmission(page, s.Snapshot.Revision, DateTimeOffset.UtcNow);
            var count = Status(30, page.Generation, s.Snapshot.Revision + 1);
            s.Set(s.Snapshot with { DaydreamerStatus = count, Dialog = Page(s, "Recorded.", false) });
            return Task.FromResult(sent);
        };
        var menus = s.OnCommand;
        s.OnCommand = command => { if (command.Kind == "close") throw new TimeoutException("Uncertain close"); menus!(command); };
        await Assert.ThrowsAsync<TimeoutException>(() => Run(c));
        var restored = new FileQuestJournal(path).Load("Lancelot", DaydreamerEvidence.Id);
        Assert.True(DaydreamerEvidence.GoalVerified(restored));
        Assert.True(restored.Daydreamer!.CleanupPending);
        s.Set(s.Snapshot with { Generation = 2, Dialog = null, Registration = null, DaydreamerStatus = null, ActiveStatus = null });
        var resumed = new QuestContext(s, journal, new(), restored);
        await Run(resumed);
        Assert.False(resumed.Job.Daydreamer!.CleanupPending);
        Assert.True(DaydreamerEvidence.GoalVerified(resumed.Job));
        Assert.Equal(1, s.Submissions);
    }

    [Fact]
    public async Task An_unrelated_popup_after_a_verified_result_is_preserved_on_resume()
    {
        var s = Ready();
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        s.Submit = page =>
        {
            var sent = new DaydreamerSubmission(page, s.Snapshot.Revision, DateTimeOffset.UtcNow);
            var count = Status(30, page.Generation, s.Snapshot.Revision + 1);
            s.Set(s.Snapshot with { DaydreamerStatus = count, Dialog = Page(s, "An unrelated choice", false) with
                { Name = "Other NPC", EntityId = 999, Choices = [new("Yes", 500, 1)] } });
            return Task.FromResult(sent);
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.True(DaydreamerEvidence.GoalVerified(c.Job));
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.Equal(1, s.Submissions);
        Assert.DoesNotContain(s.Commands, x => x.Kind == "close");
    }

    [Fact]
    public async Task Already_done_status_after_Next_preserves_the_last_successful_count()
    {
        var s = Ready();
        var oldSuccess = DateTimeOffset.UtcNow.AddDays(-2);
        var journal = new Journal();
        var c = Context(s, journal, new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id,
            Daydreamer = new() { Streak = 9, LastSuccessAt = oldSuccess } });
        Menus(s);
        s.Submit = page =>
        {
            var sent = new DaydreamerSubmission(page, s.Snapshot.Revision, DateTimeOffset.UtcNow);
            var ack = new ServerNotice(page.Generation, s.Snapshot.Revision + 1, 3, AlreadyDone, DateTimeOffset.UtcNow);
            s.Set(s.Snapshot with { DaydreamerStatus = ack, Dialog = null });
            return Task.FromResult(sent);
        };
        await Run(c);
        Assert.Equal(9, c.Job.Daydreamer?.Streak);
        Assert.Equal(oldSuccess, c.Job.Daydreamer?.LastSuccessAt);
        Assert.Null(c.Job.Daydreamer?.LastCountReceipt);
        Assert.True(DaydreamerEvidence.AttemptVerified(c.Job));
    }

    [Theory]
    [InlineData("Your expiration date is 9-11", true)]
    [InlineData("Your expiration date is 12-31.", true)]
    [InlineData("Your expiration date is 13-1", false)]
    [InlineData("Your expiration date is 1-32", false)]
    [InlineData("Your account is on auto renewal", true)]
    [InlineData("This account is on auto-renewal!", true)]
    public void Registration_recognizes_only_the_reported_month_day_or_auto_renewal(string text, bool expected) =>
        Assert.Equal(expected, DaydreamerEvidence.IsRegistration(new(1, 1, 3, text, DateTimeOffset.UtcNow)));

    [Theory]
    [InlineData("health")]
    [InlineData("map")]
    [InlineData("position")]
    public async Task Missing_initial_health_or_location_blocks_before_interaction(string missing)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Hp = missing == "health" ? null : 100, Map = missing == "map" ? null : DaydreamerEvidence.Map,
            Position = missing == "position" ? null : DaydreamerEvidence.Approach });
        var c = Context(s, new());
        Assert.Equal(EligibilityKind.Unknown, DaydreamerEvidence.Eligibility(s.Snapshot, c.Job).Kind);
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task An_active_trade_retains_control_before_a_new_check_in()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Exchange = new(1, s.Snapshot.Revision + 1, 80, "Other") });
        var c = Context(s, new());
        Assert.Equal(EligibilityKind.Blocked, DaydreamerEvidence.Eligibility(s.Snapshot, c.Job).Kind);
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task A_retained_cancelled_trade_allows_check_in_and_terminal_popup_cleanup()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Exchange = new(1, s.Snapshot.Revision + 1, 80, "Other") { Cancelled = true } });
        var journal = new Journal();
        var c = Context(s, journal);
        Menus(s);
        SubmitCount(s, 4, journal);
        await Run(c);
        Assert.Equal(4, c.Job.Daydreamer?.Streak);
        Assert.False(c.Job.Daydreamer!.CleanupPending);
        Assert.Equal(1, s.Submissions);
        Assert.Single(s.Commands, x => x.Kind == "close");
    }

    [Fact]
    public async Task A_fresh_registration_denial_in_Celestas_opening_is_saved_without_submission()
    {
        var s = Ready();
        s.OnCommand = command =>
        {
            if (command.Kind == "open") s.Set(s.Snapshot with { Dialog = Page(s, Denied, false) with { IsPursuit = false } });
        };
        var c = Context(s, new());
        await Assert.ThrowsAsync<QuestBlockedException>(() => Run(c));
        Assert.Equal(Denied, c.Job.Daydreamer?.LastRefusal?.Dialog?.Text);
        Assert.Equal(c.Job.Daydreamer?.LastRefusal?.At.AddHours(1), c.Job.NextRun);
        Assert.Null(c.Job.CompletedAt);
        Assert.Equal(0, s.Submissions);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task A_correlated_result_racing_cancellation_is_saved_before_reconnect(bool alreadyDone, bool reconnect)
    {
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString());
        var journal = new FileQuestJournal(path);
        var s = Ready();
        var c = new QuestContext(s, journal, new(), new() { Character = "Lancelot", Quest = DaydreamerEvidence.Id });
        using var stop = new CancellationTokenSource();
        Menus(s);
        s.Submit = page => Task.FromResult(new DaydreamerSubmission(page, s.Snapshot.Revision, DateTimeOffset.UtcNow));
        s.Waiting = (_, _, _, ct) =>
        {
            Assert.NotNull(c.Job.Daydreamer?.Submission);
            var receipt = Status(30, s.Snapshot.Generation, s.Snapshot.Revision + 1);
            if (alreadyDone) receipt = receipt with { Text = AlreadyDone };
            s.Set(s.Snapshot with { DaydreamerStatus = receipt, Dialog = Page(s, "Recorded.", false) });
            stop.Cancel();
            throw new OperationCanceledException(ct);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DaydreamerActions().ExecuteAsync(c,
            new("check-in", "daydreamer-check-in", "complete"), stop.Token));
        var restored = new FileQuestJournal(path).Load("Lancelot", DaydreamerEvidence.Id);
        Assert.Equal(!alreadyDone, DaydreamerEvidence.GoalVerified(restored));
        Assert.True(DaydreamerEvidence.AttemptVerified(restored));
        Assert.Null(restored.Pending);
        Assert.True(restored.Daydreamer!.CleanupPending);
        Assert.DoesNotContain(s.Commands, x => x.Kind == "close");
        s.Waiting = null;
        if (reconnect) s.Set(s.Snapshot with { Generation = 2, Dialog = null, Registration = null, DaydreamerStatus = null, ActiveStatus = null });
        var resumed = new QuestContext(s, journal, new(), restored);
        await Run(resumed);
        Assert.False(resumed.Job.Daydreamer!.CleanupPending);
        Assert.Equal(1, s.Submissions);
        Assert.Equal(reconnect ? 0 : 1, s.Commands.Count(x => x.Kind == "close"));
    }
}
