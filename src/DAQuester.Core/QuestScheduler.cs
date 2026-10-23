using System.Text.Json;

namespace DAQuester.Core;

public sealed record QuestSchedule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Character { get; init; } = "";
    public string Profile { get; init; } = "";
    public string Connection { get; init; } = "";
    public string Quest { get; init; } = "";
    public int RecipeVersion { get; init; } = 1;
    public DateTimeOffset Due { get; init; }
    public bool Recurring { get; init; }
    public bool Resume { get; init; }
    // Consent applies only to this exact obtained Callough outcome, never to a later mark.
    public DateTimeOffset? CalloughRetryAfter { get; init; }
    public string Status { get; init; } = "Armed";
    public string Reason { get; init; } = "";
}

public sealed record SchedulerLoginBlock(string Character, string Reason);
public sealed record QuestSchedulerState
{
    public bool Enabled { get; init; }
    public QuestSchedule[] Entries { get; init; } = [];
    public SchedulerLoginBlock[] LoginBlocks { get; init; } = [];
    public string[] PendingLogins { get; init; } = [];
}

public sealed record ScheduledQuestResult(DateTimeOffset? NextDue = null, bool Resume = false);
public sealed class QuestScheduledWaitException(DateTimeOffset next) : Exception("Quest work is waiting for its next eligible time.")
{
    public DateTimeOffset Next { get; } = next;
}
public sealed class ScheduledLoginException(string character, string reason) : QuestBlockedException(reason)
{
    public string Character { get; } = character;
}

public sealed class FileQuestSchedulerStore(string path)
{
    public QuestSchedulerState Load()
    {
        if (!File.Exists(path))
            return new();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var state = JsonSerializer.Deserialize<QuestSchedulerState>(stream) ?? throw new InvalidDataException("Unreadable scheduler settings.");
        if (state.Entries is null || state.LoginBlocks is null || state.PendingLogins is null || state.LoginBlocks.Any(b => b is null || string.IsNullOrWhiteSpace(b.Character) || b.Reason is null) || state.PendingLogins.Any(string.IsNullOrWhiteSpace) || state.Entries.Any(e => e is null || e.Id == Guid.Empty || string.IsNullOrWhiteSpace(e.Character) || string.IsNullOrWhiteSpace(e.Profile) || string.IsNullOrWhiteSpace(e.Quest) || e.Reason is null || e.Status is not ("Armed" or "Running" or "Needs attention" or "Paused" or "Completed")) || state.Entries.Select(e => e.Id).Distinct().Count() != state.Entries.Length)
            throw new InvalidDataException("Invalid scheduler settings. Scheduling remains disabled.");
        var interrupted = state.PendingLogins.Concat(state.Entries.Where(e => e.Status == "Running").Select(e => e.Character)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var blocks = state.LoginBlocks.Concat(interrupted.Where(n => !state.LoginBlocks.Any(b => b.Character.Equals(n, StringComparison.OrdinalIgnoreCase))).Select(n => new SchedulerLoginBlock(n, "Interrupted scheduled run or login. Review before trying again."))).ToArray();
        return state with { PendingLogins = [], LoginBlocks = blocks, Entries = state.Entries.Select(e => blocks.Any(b => b.Character.Equals(e.Character, StringComparison.OrdinalIgnoreCase)) && e.Status != "Completed" ? e with { Status = "Needs attention", Reason = blocks.First(b => b.Character.Equals(e.Character, StringComparison.OrdinalIgnoreCase)).Reason } : e).ToArray() };
    }

    public Task SaveAsync(QuestSchedulerState state) => AtomicJsonFile.WriteAsync(path, state, "Scheduler checkpoint could not be saved. Scheduling has stopped; review saved progress.", CancellationToken.None);
}

// All timers share a single execution gate. State changes are saved before they
// become visible, and a durable Running checkpoint precedes every login.
public sealed class QuestScheduler
{
    readonly FileQuestSchedulerStore store;
    readonly Func<QuestSchedule, CancellationToken, Task<ScheduledQuestResult>> execute;
    readonly SemaphoreSlim writes = new(1, 1), execution = new(1, 1);
    CancellationTokenSource? active;
    Guid? activeId;
    bool suspended;
    public QuestSchedulerState State { get; private set; }
    public bool IsRunning => active is not null;
    public event Action? Changed;

    public QuestScheduler(FileQuestSchedulerStore store, Func<QuestSchedule, CancellationToken, Task<ScheduledQuestResult>> execute, QuestSchedulerState? initial = null)
    {
        this.store = store;
        this.execute = execute;
        State = initial ?? store.Load();
    }

    public string? LoginBlock(string character) => State.LoginBlocks.FirstOrDefault(b => b.Character.Equals(character, StringComparison.OrdinalIgnoreCase))?.Reason ?? (State.PendingLogins.Contains(character, StringComparer.OrdinalIgnoreCase) ? "Unconfirmed scheduled login. Review before trying again." : null);
    static void Acknowledge(bool acknowledged)
    {
        if (!acknowledged)
            throw new QuestBlockedException("Acknowledge that scheduled logins can boot your existing session before enabling scheduling.");
    }

    async Task ChangeAsync(Func<QuestSchedulerState, QuestSchedulerState> change)
    {
        await writes.WaitAsync();
        try
        {
            var next = change(State);
            if (ReferenceEquals(next, State))
                return;
            try { await store.SaveAsync(next); }
            catch
            {
                State = State with { Enabled = false };
                active?.Cancel();
                throw;
            }
            State = next;
        }
        finally
        {
            writes.Release();
            Changed?.Invoke();
        }
    }

    public async Task SetEnabledAsync(bool enabled, bool acknowledged)
    {
        if (enabled)
        {
            Acknowledge(acknowledged);
            suspended = false;
        }
        else
            active?.Cancel();
        await ChangeAsync(s => s.Enabled == enabled ? s : s with { Enabled = enabled });
    }

    public Task AddAsync(QuestSchedule entry, bool acknowledged)
    {
        Acknowledge(acknowledged);
        if (entry.Id == Guid.Empty || string.IsNullOrWhiteSpace(entry.Character) || string.IsNullOrWhiteSpace(entry.Profile) || string.IsNullOrWhiteSpace(entry.Quest))
            throw new QuestBlockedException("Choose a saved character and automatic quest.");
        return ChangeAsync(s =>
        {
            if (s.Entries.Any(e => e.Id == entry.Id || e.Character.Equals(entry.Character, StringComparison.OrdinalIgnoreCase) && e.Quest == entry.Quest && e.Profile == entry.Profile && e.Status != "Completed"))
                throw new QuestBlockedException("This character already has a schedule for that quest. Edit its saved schedule.");
            return s with { Entries = [.. s.Entries, entry with { Status = "Armed", Reason = "" }] };
        });
    }

    public Task PauseAsync(Guid id)
    {
        if (activeId == id)
            active?.Cancel();
        return ChangeAsync(s => s with { Entries = s.Entries.Select(e => e.Id == id && e.Status != "Running" ? e with { Status = "Paused", Reason = "Paused by player" } : e).ToArray() });
    }

    public Task RemoveAsync(Guid id) => ChangeAsync(s =>
    {
        if (activeId == id)
            throw new QuestBlockedException("Pause this run and wait for cleanup before removing it.");
        return s with { Entries = s.Entries.Where(e => e.Id != id).ToArray() };
    });

    public Task EditAsync(QuestSchedule entry, bool acknowledged)
    {
        Acknowledge(acknowledged);
        return ChangeAsync(s =>
        {
            var old = s.Entries.Single(e => e.Id == entry.Id);
            if (activeId == entry.Id || old.Character != entry.Character || old.Quest != entry.Quest)
                throw new QuestBlockedException("Pause the run first. A saved schedule keeps its character and quest.");
            return s with { Entries = s.Entries.Select(e => e.Id == entry.Id ? entry with { Status = old.Status, Reason = old.Reason, Resume = old.Resume, CalloughRetryAfter = old.CalloughRetryAfter } : e).ToArray() };
        });
    }

    public Task RearmAsync(Guid id, bool acknowledged, DateTimeOffset due)
    {
        Acknowledge(acknowledged);
        return ChangeAsync(s =>
        {
            var entry = s.Entries.Single(e => e.Id == id);
            if (activeId == id)
                throw new QuestBlockedException("Wait for this run to finish saving before editing it.");
            return s with
            {
                LoginBlocks = s.LoginBlocks.Where(b => !b.Character.Equals(entry.Character, StringComparison.OrdinalIgnoreCase)).ToArray(),
                PendingLogins = s.PendingLogins.Where(n => !n.Equals(entry.Character, StringComparison.OrdinalIgnoreCase)).ToArray(),
                Entries = s.Entries.Select(e => e.Id == id ? e with { Status = "Armed", Reason = "", Due = due } : e).ToArray()
            };
        });
    }

    public Task BlockLoginAsync(string character, string reason) => ChangeAsync(s => s with
    {
        PendingLogins = s.PendingLogins.Where(n => !n.Equals(character, StringComparison.OrdinalIgnoreCase)).ToArray(),
        LoginBlocks = [.. s.LoginBlocks.Where(b => !b.Character.Equals(character, StringComparison.OrdinalIgnoreCase)), new(character, reason)],
        Entries = s.Entries.Select(e => e.Character.Equals(character, StringComparison.OrdinalIgnoreCase) && e.Status != "Completed" ? e with { Status = "Needs attention", Reason = reason } : e).ToArray()
    });

    public Task CompleteGoalAsync(Guid id, string reason) => ChangeAsync(s =>
    {
        var entry = s.Entries.Single(e => e.Id == id);
        if (activeId == id || entry.Status == "Completed" && entry.Reason == reason) return s;
        return s with { Entries = s.Entries.Select(e => e.Id == id ? e with { Status = "Completed", Reason = reason, Resume = false } : e).ToArray() };
    });

    public Task ApproveCalloughRetryAsync(Guid id, DateTimeOffset obtainedAt, DateTimeOffset due, bool confirmed) => ChangeAsync(s =>
    {
        Acknowledge(confirmed);
        var entry = s.Entries.Single(e => e.Id == id);
        if (entry.Quest != "spare-a-stick" || activeId == id || due < obtainedAt.AddHours(8))
            throw new QuestBlockedException("Review the obtained Callough mark and its cooldown before resuming.");
        if (LoginBlock(entry.Character) is { } block)
            throw new ScheduledLoginException(entry.Character, block);
        return s with { Entries = s.Entries.Select(e => e.Id == id ? e with
        {
            CalloughRetryAfter = obtainedAt, Due = due, Recurring = true, Resume = false, Status = "Armed",
            Reason = "Previous Callough outcome retained; approved to try for more marks"
        } : e).ToArray() };
    });

    public Task BeginLoginAsync(string character) => ChangeAsync(s =>
    {
        if (LoginBlock(character) is { } block)
            throw new ScheduledLoginException(character, block);
        return s with { PendingLogins = [.. s.PendingLogins, character] };
    });
    public Task ConfirmLoginAsync(string character) => ChangeAsync(s => s with { PendingLogins = s.PendingLogins.Where(n => !n.Equals(character, StringComparison.OrdinalIgnoreCase)).ToArray() });
    public Task MarkQuestStartedAsync(Guid id) => ChangeAsync(s =>
    {
        if (activeId != id)
            throw new QuestBlockedException("The scheduled run no longer owns this quest.");
        return s with { Entries = s.Entries.Select(e => e.Id == id ? e with { Resume = true } : e).ToArray() };
    });
    public Task ReviewLoginAsync(string character, bool acknowledged)
    {
        Acknowledge(acknowledged);
        return ChangeAsync(s =>
        {
            if (IsRunning)
                throw new QuestBlockedException("Pause the scheduler and wait for the active run before reviewing login blocks.");
            return s with { LoginBlocks = s.LoginBlocks.Where(b => !b.Character.Equals(character, StringComparison.OrdinalIgnoreCase)).ToArray(), PendingLogins = s.PendingLogins.Where(n => !n.Equals(character, StringComparison.OrdinalIgnoreCase)).ToArray() };
        });
    }

    public async Task TickAsync(DateTimeOffset now)
    {
        if (suspended)
            return;
        if (!await execution.WaitAsync(0))
            return;
        QuestSchedule? claimed = null;
        using var cancellation = new CancellationTokenSource();
        try
        {
            await ChangeAsync(s =>
            {
                if (!s.Enabled || suspended)
                    return s;
                claimed = s.Entries.Where(e => e.Status == "Armed" && e.Due <= now && LoginBlock(e.Character) is null).OrderBy(e => e.Due).FirstOrDefault();
                if (claimed is null)
                    return s;
                active = cancellation;
                activeId = claimed.Id;
                return s with { Entries = s.Entries.Select(e => e.Id == claimed.Id ? e with { Status = "Running", Reason = "Starting scheduled run" } : e).ToArray() };
            });
            if (claimed is null)
                return;
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var result = await execute(claimed, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (result.NextDue is { } next && next <= now)
                    throw new QuestBlockedException("Next scheduled time is not in the future. Review the quest result.");
                await ChangeAsync(s => s with { Entries = s.Entries.Select(e => e.Id == claimed.Id ? e with { Due = result.NextDue ?? e.Due, Resume = result.Resume, Status = result.NextDue is null ? "Completed" : "Armed", Reason = result.NextDue is null ? "Verified work completed" : "Waiting while logged out" } : e).ToArray() });
            }
            catch (ScheduledLoginException error)
            {
                await BlockLoginAsync(error.Character, error.Message);
                await AttentionAsync(claimed.Id, error.Message);
            }
            catch (Exception error)
            {
                await AttentionAsync(claimed.Id, error is OperationCanceledException ? "Stopped. Review before resuming saved progress." : error.Message);
            }
        }
        finally
        {
            active = null;
            activeId = null;
            execution.Release();
            Changed?.Invoke();
        }
    }

    Task AttentionAsync(Guid id, string reason) => ChangeAsync(s => s with { Entries = s.Entries.Select(e => e.Id == id ? e with { Status = "Needs attention", Reason = reason } : e).ToArray() });
    public async Task WaitForIdleAsync() { await execution.WaitAsync(); execution.Release(); }
    public void Suspend()
    {
        suspended = true;
        active?.Cancel();
    }
}
