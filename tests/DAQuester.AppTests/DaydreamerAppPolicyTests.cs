using System.Reflection;
using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class DaydreamerAppPolicyTests
{
    const string Id = "avid-daydreamer";

    static DesktopRuntime Runtime()
    {
        var runtime = DashboardTests.Workspace();
        var recipes = (Dictionary<string, QuestDefinition>)typeof(DesktopRuntime)
            .GetField("recipes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(runtime)!;
        recipes.TryAdd(Id, new(Id, 1, "Avid Temuairian Daydreamer", "Synthetic integration", [], [], 24));
        return runtime;
    }

    static QuestJob Receipt(int count, DateTimeOffset at, bool cleanup = false)
    {
        var status = new ServerNotice(1, 12, 3, $"This is your {count}th consecutive Login!", at);
        return JsonSerializer.Deserialize<QuestJob>(JsonSerializer.Serialize(new
        {
            Character = "Elara", Quest = Id, Step = "complete", Status = "Completed", CompletedAt = at,
            LastAttemptAt = at, Repeat = false,
            Daydreamer = new
            {
                Streak = count, LastCountReceipt = status, LastAcknowledgement = new { At = at, Status = status },
                TimeZoneId = "America/Chicago", FirstThirtyReceipt = count >= 30 ? status : null,
                CleanupPending = cleanup
            }
        }))!;
    }

    [Fact]
    public async Task Daily_receipt_is_a_cooldown_and_its_recurring_schedule_keeps_the_explicit_deadline()
    {
        var runtime = Runtime();
        try
        {
            var at = DateTimeOffset.UtcNow.AddHours(-1);
            var due = at.AddHours(24);
            var saved = Receipt(29, at);
            saved = saved with { NextRun = due, Daydreamer = saved.Daydreamer! with { NextCheckInAt = due } };
            await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(saved);

            Assert.Equal("Cooldown", runtime.Availability("Elara", Id).Status);
            Assert.Equal(due, runtime.SchedulerDue("Elara", Id, DateTimeOffset.UtcNow));
            await runtime.SaveScheduleAsync("Elara", Id, DateTimeOffset.UtcNow, true, true);
            var entry = Assert.Single(runtime.SchedulerState.Entries);
            Assert.True(entry.Recurring);
            Assert.Equal(due, entry.Due);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Completed_first_mark_can_explicitly_enable_more_daily_checkins_without_erasing_its_receipt()
    {
        var runtime = Runtime();
        try
        {
            var saved = Receipt(30, DateTimeOffset.UtcNow.AddHours(-1));
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(saved);
            Assert.Equal("Completed", runtime.Availability("Elara", Id).Status);
            await Assert.ThrowsAsync<QuestBlockedException>(() => runtime.SaveScheduleAsync("Elara", Id, DateTimeOffset.UtcNow, true, true));

            await runtime.SetRepeatAsync("Elara", Id, true);

            var continued = journal.Load("Elara", Id);
            Assert.True(continued.Repeat);
            Assert.Equal(saved.Daydreamer!.FirstThirtyReceipt, continued.Daydreamer!.FirstThirtyReceipt);
            Assert.Equal(DaydreamerEvidence.NextEligible(saved.CompletedAt!.Value, "America/Chicago"), QuestRunPolicy.NextAttempt(runtime.Recipes[Id], continued));
            Assert.Equal("Cooldown", runtime.Availability("Elara", Id).Status);
            await runtime.SaveScheduleAsync("Elara", Id, DateTimeOffset.UtcNow, true, true);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Terminal_popup_cleanup_stays_resumable_before_goal_completion_or_daily_cooldown()
    {
        var runtime = Runtime();
        try
        {
            var saved = Receipt(30, DateTimeOffset.UtcNow.AddHours(-1), cleanup: true) with { NextRun = DateTimeOffset.UtcNow.AddDays(1) };
            await new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")).SaveAsync(saved);

            Assert.Equal("Resume ready", runtime.Availability("Elara", Id).Status);
            Assert.Equal(DateTimeOffset.UnixEpoch, runtime.SchedulerDue("Elara", Id, DateTimeOffset.UnixEpoch));
            Assert.True((bool)typeof(DesktopRuntime).GetMethod("HasUnfinishedCheckpoint", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(runtime, ["Elara", Id])!);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Scheduled_readiness_accepts_late_registration_without_inventory_level_or_legend_requirements()
    {
        var at = DateTimeOffset.UtcNow;
        var initial = new CharacterSnapshot
        {
            Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 2,
            Map = 500, Position = new(3, 4), Hp = 100
        };
        var oldRegistration = initial with { Revision = 3, Registration = new(6, 1, 1, "This account is on auto-renewal", at) };
        var popup = oldRegistration with
        {
            Revision = 4, Registration = new(7, 4, 1, "This account is on auto-renewal", at),
            Dialog = new(7, 4, 55, 1, "Unrelated NPC", "Unrelated startup popup", true, 2, 1, 0, [])
        };
        var ready = popup with
        {
            Revision = 5, Dialog = null, Exchange = new(7, 5, 55, "Synthetic trader") { Cancelled = true }
        };
        var session = new ReadinessSession(initial, [oldRegistration, popup, ready]);

        await DesktopRuntime.WaitForDaydreamerReadyAsync(session, true, default);

        Assert.Equal(3, session.Observed);
        Assert.Null(session.Snapshot.Level);
        Assert.False(session.Snapshot.InventoryKnown);
        Assert.False(session.Snapshot.LegendsKnown);
        Assert.True(session.Snapshot.Exchange!.Cancelled);
    }

    [Fact]
    public async Task Scheduled_readiness_accepts_only_the_login_registration_binding_for_the_current_own_identity()
    {
        var initial = new CharacterSnapshot
        {
            Name = "Elara", Id = 7, Connected = true, Generation = 17, Revision = 2,
            Map = 500, Position = new(3, 4), Hp = 100
        };
        var receiptType = typeof(CharacterSnapshot).Assembly.GetType("DAQuester.Core.LoginRegistrationReceipt");
        Assert.NotNull(receiptType);
        var property = typeof(CharacterSnapshot).GetProperty("LoginRegistration");
        Assert.NotNull(property);
        var source = new ServerNotice(16, 5, 2, "Your expiration date is 10-29", DateTimeOffset.UtcNow);
        var wrong = initial with { Revision = 3 };
        property.SetValue(wrong, Activator.CreateInstance(receiptType, source, 17L, 3L, "Elara", 8u));
        var old = initial with { Revision = 4 };
        property.SetValue(old, Activator.CreateInstance(receiptType, source, 18L, 4L, "Elara", 7u));
        var ready = initial with { Revision = 5 };
        property.SetValue(ready, Activator.CreateInstance(receiptType, source, 17L, 5L, "Elara", 7u));
        var session = new ReadinessSession(initial, [wrong, old, ready]);

        await DesktopRuntime.WaitForDaydreamerReadyAsync(session, true, default);

        Assert.Equal(3, session.Observed);
        Assert.Null(session.Snapshot.Registration);
        Assert.Null(session.Snapshot.ActiveStatus);
        Assert.Null(session.Snapshot.DaydreamerStatus);
    }

    sealed class ReadinessSession(CharacterSnapshot initial, CharacterSnapshot[] observations) : IGameSession
    {
        public CharacterSnapshot Snapshot { get; private set; } = initial;
        public int Observed { get; private set; }
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
        public event Action<CharacterSnapshot>? Changed { add { } remove { } }
        public event Action<string>? Interrupted { add { } remove { } }
        public Task ExecuteAsync(GameCommand command, CancellationToken cancellation) => throw new InvalidOperationException("Readiness never sends a command.");
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation)
        {
            foreach (var observation in observations)
            {
                cancellation.ThrowIfCancellationRequested();
                Snapshot = observation;
                Observed++;
                if (observation.Revision > after && predicate(observation)) return Task.FromResult(observation);
            }
            throw new TimeoutException("No qualifying synthetic observation.");
        }
    }
}
