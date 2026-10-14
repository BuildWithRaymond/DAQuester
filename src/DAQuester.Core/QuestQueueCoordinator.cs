namespace DAQuester.Core;

public sealed class QuestQueueCoordinator(FileQuestQueueStore store, QuestQueueState initial, Func<CharacterSnapshot> snapshot, Func<string, bool, CancellationToken, Task<QuestJob>> execute, Func<string, bool>? shouldResume = null, Func<string, bool>? canRun = null, Func<DateTimeOffset, CancellationToken, Task>? waitForDue = null)
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
            await Save(State with
            {
                State = "Running",
                Reason = ""
            }, token.Token);
            while (State.Quests.Length > 0)
            {
                token.Token.ThrowIfCancellationRequested();
                CheckIdentity(character, generation);
                if (State.ActiveQuest is null && State.Deferred.Length > 0)
                {
                    // A deferred paid wait may yield only when no unresolved
                    // active checkpoint owns the character. Recheck live readiness.
                    var now = DateTimeOffset.UtcNow;
                    string? ready = State.Quests.FirstOrDefault(id => !State.Deferred.Any(d => d.Quest == id && d.Due > now) && (canRun?.Invoke(id) ?? true));
                    if (ready is null)
                    {
                        var due = State.Deferred.Where(d => d.Due > now).Select(d => (DateTimeOffset?)d.Due).Min();
                        if (due is null)
                            throw new QuestBlockedException("No queued quest is ready; saved bouquet wait remains intact.");
                        await Save(State with { State = "Running", Reason = "Bouquet check after " + due.Value.ToLocalTime().ToString("g") }, token.Token);
                        if (waitForDue is not null)
                            await waitForDue(due.Value, token.Token);
                        else
                            await Task.Delay(due.Value - DateTimeOffset.UtcNow is { } delay && delay > TimeSpan.Zero ? delay : TimeSpan.Zero, token.Token);
                        continue;
                    }
                    if (ready != State.Quests[0])
                        await Save(State with { Quests = [ready, .. State.Quests.Where(id => id != ready)] }, token.Token);
                }
                string quest = State.Quests[0];
                bool checkpoint = State.ActiveQuest == quest;
                if (State.ActiveQuest is not null && !checkpoint)
                    throw new QuestBlockedException("An unfinished quest checkpoint is waiting. Restore it to the front of the queue before resuming.");
                if (!checkpoint)
                    await Save(State with
                    {
                        ActiveQuest = quest,
                        ActiveStartedAt = DateTimeOffset.UtcNow,
                        ActiveResume = State.Deferred.Any(d => d.Quest == quest) || (shouldResume?.Invoke(quest) ?? false)
                    }, token.Token);
                bool resume = checkpoint || State.ActiveResume;
                token.Token.ThrowIfCancellationRequested();
                CheckIdentity(character, generation);
                var job = await execute(quest, resume, token.Token);
                token.Token.ThrowIfCancellationRequested();
                CheckIdentity(character, generation);
                if (!string.Equals(job.Character, character, StringComparison.OrdinalIgnoreCase) || job.Quest != quest)
                    throw new QuestBlockedException("Quest checkpoint identity does not match this character queue.");
                if (FiskEvidence.CanDefer(job))
                {
                    // Retain the quest and original acceptance instead of recording
                    // completion. The durable due time only schedules another check.
                    await Save(State with
                    {
                        ActiveQuest = null,
                        ActiveStartedAt = null,
                        ActiveResume = false,
                        Deferred = [.. State.Deferred.Where(d => d.Quest != quest), new(quest, job.NextRun!.Value)],
                        Reason = "Bouquet accepted; check after " + job.NextRun.Value.ToLocalTime().ToString("g")
                    }, token.Token);
                    continue;
                }
                var outcome = QueueAttemptOutcome.Evaluate(job) ?? throw new QuestBlockedException(QueueAttemptOutcome.BlockReason(job));
                if (!State.ActiveResume && State.ActiveStartedAt is { } started && outcome.At < started)
                    throw new QuestBlockedException("Saved outcome predates this queue attempt. Resume reconciles the fresh attempt checkpoint.");
                // Outcome and removal share an atomic checkpoint: either both survive a crash or neither does.
                // A crash before this write retains ActiveQuest, so the journal is reconciled on Resume.
                await Save(State with
                {
                    Quests = State.Quests[1..],
                    ActiveQuest = null,
                    ActiveStartedAt = null,
                    ActiveResume = false,
                    Deferred = State.Deferred.Where(d => d.Quest != quest).ToArray(),
                    History = State.History.Append(outcome).TakeLast(100).ToArray()
                }, token.Token);
            }

            token.Token.ThrowIfCancellationRequested();
            CheckIdentity(character, generation);
            await Save(State with
            {
                State = "Idle",
                Reason = "Queue completed"
            }, token.Token);
        }
        catch (OperationCanceledException)
        {
            await Save(State with
            {
                State = "Paused",
                Reason = pauseReason
            });
        }
        catch (Exception e)
        {
            await Save(State with
            {
                State = "Paused",
                Reason = e.Message
            });
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
            await Save(State with
            {
                State = stop ? "Idle" : "Paused",
                Reason = reason
            });
        }
        finally
        {
            owner.Release();
        }
    }
}
