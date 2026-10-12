namespace DAQuester.Core;

public sealed class QuestQueueCoordinator(FileQuestQueueStore store, QuestQueueState initial, Func<CharacterSnapshot> snapshot, Func<string, bool, CancellationToken, Task<QuestJob>> execute, Func<string, bool>? shouldResume = null)
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
                string quest = State.Quests[0];
                bool checkpoint = State.ActiveQuest == quest;
                if (State.ActiveQuest is not null && !checkpoint)
                    throw new QuestBlockedException("An unfinished quest checkpoint is waiting. Restore it to the front of the queue before resuming.");
                if (!checkpoint)
                    await Save(State with
                    {
                        ActiveQuest = quest,
                        ActiveStartedAt = DateTimeOffset.UtcNow,
                        ActiveResume = shouldResume?.Invoke(quest) ?? false
                    }, token.Token);
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
                await Save(State with
                {
                    Quests = State.Quests[1..],
                    ActiveQuest = null,
                    ActiveStartedAt = null,
                    ActiveResume = false,
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
