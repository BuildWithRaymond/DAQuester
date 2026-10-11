using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DAQuester.Core;
public record QueueHome(int Map, Tile Position, string Label);
public record QueueOutcome(string Quest, string Outcome, DateTimeOffset At, string Summary);
public record QuestQueueState
{
    public string Character { get; init; } = "";
    public string[] Quests { get; init; } = [];
    public string State { get; init; } = "Idle";
    public string Reason { get; init; } = "";
    public string? ActiveQuest { get; init; }
    public DateTimeOffset? ActiveStartedAt { get; init; }
    public bool ActiveResume { get; init; }
    public QueueHome? Home { get; init; }
    public bool ReturnHome { get; init; }
    public bool HomePending { get; init; }
    public QueueOutcome[] History { get; init; } = [];

    [JsonIgnore]
    public bool Locked => State is "Running" or "Returning home" || State == "Paused" && (ActiveQuest is not null || HomePending);

    public QuestQueueState Reorder(string quest, int index)
    {
        if (Locked)
            throw new InvalidOperationException("Stop the queue before changing its order.");
        var ordered = UniqueQuests().ToList();
        if (!ordered.Remove(quest))
            return this with
            {
                Quests = ordered.ToArray()
            };
        ordered.Insert(Math.Clamp(index, 0, ordered.Count), quest);
        return this with
        {
            Quests = ordered.ToArray()
        };
    }

    internal string[] UniqueQuests() => Quests.Where(q => !string.IsNullOrWhiteSpace(q)).Distinct(StringComparer.Ordinal).ToArray();
    internal QuestQueueState Normalize() => this with
    {
        Quests = UniqueQuests(),
        History = History.TakeLast(100).ToArray()
    };
}

public sealed class FileQuestQueueStore(string directory)
{
    readonly SemaphoreSlim gate = new(1, 1);
    string PathFor(string character)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(character);
        return Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(character.ToUpperInvariant()))) + ".json");
    }

    public QuestQueueState Load(string character)
    {
        var path = PathFor(character);
        if (!File.Exists(path))
            return new()
            {
                Character = character
            };
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var state = JsonSerializer.Deserialize<QuestQueueState>(stream) ?? throw new InvalidDataException("Unreadable character queue.");
        if (!string.Equals(state.Character, character, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Character queue identity does not match.");
        state = state.Normalize();
        return state.State is "Running" or "Returning home" ? state with
        {
            State = "Paused",
            Reason = "Restored. Resume after fresh character state."
        }

        : state;
    }

    public async Task SaveAsync(QuestQueueState state, CancellationToken cancellation = default)
    {
        var path = PathFor(state.Character);
        await gate.WaitAsync(cancellation);
        try
        {
            await AtomicJsonFile.WriteAsync(path, state.Normalize(), "Could not replace character queue checkpoint. Previous progress remains saved.", cancellation);
        }
        finally
        {
            gate.Release();
        }
    }
}

public static class QueueAttemptOutcome
{
    public static bool HasTerminalReceipt(QuestJob job)
    {
        if (job.Pending is not null)
            return false;
        if (job.Quest == "mundane-birth")
            return job.Step == "confirm" && job.BirthCompletion is { } birth && MundaneBirthEvidence.Matches(birth) && job.CompletedAt == birth.ObservedAt;
        if (job.Quest == "entered-muisir-village")
            return job.Step == "confirm" && job.CompletedAt is not null && job.CompletionLegend is { } legend && (legend.Equals(MuisirEvidence.Legend, StringComparison.OrdinalIgnoreCase) || legend.StartsWith(MuisirEvidence.Legend + " -", StringComparison.OrdinalIgnoreCase)) && job.Rewards.GetValueOrDefault("mushroomsConsumed") == 5 && job.Rewards.GetValueOrDefault("item:" + MuisirEvidence.Potion) == 4;
        if (job.Quest == ShinewoodFaerieEvidence.Id)
            return job.Step == "return" && job.CompletedAt is { } completed && job.FaerieReturn is { } returned && returned.VerifiedAt >= completed;
        if (job.Quest != "perfect-hairstyle" || job.Step != "confirm" || job.LastAttemptAt is not { } at || job.HairstyleCompletion is not { } reply || !reply.Name.Equals("Eeva", StringComparison.OrdinalIgnoreCase) || !reply.IsPursuit || reply.EntityType != 1 || reply.Type != 0 || reply.Choices.Length != 0 || reply.Text is not (PerfectHairstyleEvidence.Completion or PerfectHairstyleEvidence.TwoRingCompletion))
            return false;
        return job.HairstyleAttempts.Any(a => a.CompletedAt == at && a.Completion.Generation == reply.Generation && a.Completion.Revision == reply.Revision && a.Completion.EntityId == reply.EntityId && a.Completion.Text == reply.Text && a.Completion.ObservedAt == reply.ObservedAt);
    }

    public static QueueOutcome? Evaluate(QuestJob job)
    {
        if (job.Pending is not null || job.Status is not ("Completed" or "AttemptCompleted" or "PermanentlyCompleted"))
            return null;
        if (job.PermanentCompletion is { } permanent)
            return new(job.Quest, "PermanentlyCompleted", permanent.ObservedAt, job.CompletionText ?? job.Reason);
        if (job.Quest == "perfect-hairstyle" && job.LegendReward != true)
        {
            if (job.LastAttemptAt is not { } at || !job.HairstyleAttempts.Any(a => a.CompletedAt == at) || job.Step is not ("complete" or "preflight") && !HasTerminalReceipt(job))
                return null;
            return new(job.Quest, "AttemptCompleted", at, "Weekly attempt verified; Suomi legend still needed" + (job.NextRun is { } next ? ". Next attempt " + next.ToLocalTime().ToString("g") : ""));
        }

        if (job.Step != "complete" && !HasTerminalReceipt(job) || job.CompletedAt is not { } completed)
            return null;
        if (job.Quest == ShinewoodFaerieEvidence.Id)
        {
            if (job.FaerieReturn is null)
                return null;
            return new(job.Quest, job.FaeriePetWait is null ? "Completed" : "AttemptCompleted", completed, job.CompletionText ?? "Faerie visit and return verified");
        }

        return new(job.Quest, "Completed", completed, job.CompletionText ?? "Completion verified");
    }

    public static string BlockReason(QuestJob job) => job.Pending is not null ? "Saved submission requires reconciliation before the queue can continue." : !string.IsNullOrWhiteSpace(job.Reason) ? job.Reason : "Quest attempt has no verified outcome. Resume to reconcile its checkpoint.";
}

public sealed class QuestQueueCoordinator(FileQuestQueueStore store, QuestQueueState initial, Func<CharacterSnapshot> snapshot, Func<string, bool, CancellationToken, Task<QuestJob>> execute, Func<QueueHome, CancellationToken, Task> returnHome, Func<string, bool>? shouldResume = null)
{
    readonly SemaphoreSlim owner = new(1, 1);
    volatile CancellationTokenSource? active;
    string pauseReason = "Paused by player";
    public QuestQueueState State { get; private set; } = initial.Normalize();
    public bool IsRunning => active is not null;

    public event Action<QuestQueueState>? Changed;
    async Task Save(QuestQueueState state, CancellationToken cancellation = default)
    {
        await store.SaveAsync(state, cancellation);
        State = state;
        Changed?.Invoke(state);
    }

    void CheckIdentity(string character, long generation)
    {
        var observed = snapshot();
        if (!observed.Connected || !string.Equals(observed.Name, character, StringComparison.OrdinalIgnoreCase) || observed.Generation != generation)
            throw new QuestBlockedException("Character connection changed. Resume the saved queue after fresh character state.");
    }

    async Task Return(string character, long generation, CancellationToken cancellation)
    {
        CheckIdentity(character, generation);
        var home = State.Home ?? throw new QuestBlockedException("Choose a home location before returning.");
        await Save(State with { State = "Returning home", Reason = "Returning to " + home.Label, HomePending = true }, cancellation);
        cancellation.ThrowIfCancellationRequested();
        CheckIdentity(character, generation);
        await returnHome(home, cancellation);
        cancellation.ThrowIfCancellationRequested();
        CheckIdentity(character, generation);
        var observed = snapshot();
        if (observed.Map != home.Map || observed.Position != home.Position)
            throw new QuestBlockedException("Home arrival remains unconfirmed. Resume checks the observed location.");
        await Save(State with { State = "Idle", Reason = "Home arrival verified", HomePending = false }, cancellation);
    }

    public async Task RunAsync(CancellationToken cancellation = default)
    {
        if (!await owner.WaitAsync(0, cancellation))
            return;
        using var token = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        active = token;
        pauseReason = "Paused by player";
        var character = State.Character;
        var generation = snapshot().Generation;
        try
        {
            CheckIdentity(character, generation);
            if (State.HomePending)
            {
                await Return(character, generation, token.Token);
                return;
            }

            await Save(State with { State = "Running", Reason = "" }, token.Token);
            while (State.Quests.Length > 0)
            {
                token.Token.ThrowIfCancellationRequested();
                CheckIdentity(character, generation);
                string quest = State.Quests[0];
                bool checkpoint = State.ActiveQuest == quest;
                if (State.ActiveQuest is not null && !checkpoint)
                    throw new QuestBlockedException("An unfinished quest checkpoint is waiting. Restore it to the front of the queue before resuming.");
                if (!checkpoint)
                    await Save(State with { ActiveQuest = quest, ActiveStartedAt = DateTimeOffset.UtcNow, ActiveResume = shouldResume?.Invoke(quest) ?? false }, token.Token);
                bool resume = checkpoint || State.ActiveResume;
                token.Token.ThrowIfCancellationRequested();
                CheckIdentity(character, generation);
                var job = await execute(quest, resume, token.Token);
                token.Token.ThrowIfCancellationRequested();
                CheckIdentity(character, generation);
                if (!string.Equals(job.Character, character, StringComparison.OrdinalIgnoreCase) || job.Quest != quest)
                    throw new QuestBlockedException("Quest checkpoint identity does not match this character queue.");
                var outcome = QueueAttemptOutcome.Evaluate(job) ?? throw new QuestBlockedException(QueueAttemptOutcome.BlockReason(job));
                if (!State.ActiveResume && State.ActiveStartedAt is { } started && outcome.At < started)
                    throw new QuestBlockedException("Saved outcome predates this queue attempt. Resume reconciles the fresh attempt checkpoint.");
                // Outcome and removal share an atomic checkpoint: either both survive a crash or neither does.
                // A crash before this write retains ActiveQuest, so the journal is reconciled on Resume.
                await Save(State with { Quests = State.Quests[1..], ActiveQuest = null, ActiveStartedAt = null, ActiveResume = false, History = State.History.Append(outcome).TakeLast(100).ToArray() }, token.Token);
            }

            token.Token.ThrowIfCancellationRequested();
            CheckIdentity(character, generation);
            if (State.ReturnHome)
                await Return(character, generation, token.Token);
            else
                await Save(State with { State = "Idle", Reason = "Queue completed" }, token.Token);
        }
        catch (OperationCanceledException)
        {
            await Save(State with { State = "Paused", Reason = pauseReason });
        }
        catch (Exception e)
        {
            await Save(State with { State = "Paused", Reason = e.Message });
        }
        finally
        {
            active = null;
            owner.Release();
            Changed?.Invoke(State);
        }
    }

    public async Task PauseAsync(string reason = "Paused by player", bool stop = false)
    {
        pauseReason = reason;
        active?.Cancel();
        await owner.WaitAsync();
        try
        {
            await Save(State with { State = stop ? "Idle" : "Paused", Reason = reason });
        }
        finally
        {
            owner.Release();
        }
    }
}
