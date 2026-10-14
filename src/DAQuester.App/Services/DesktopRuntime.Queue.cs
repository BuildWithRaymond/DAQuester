using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    readonly FileQuestQueueStore queueStore = new(Path.Combine(AppSettings.DataPath, "queues"));
    readonly Dictionary<string, QuestQueueState> queueCache = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, QuestQueueCoordinator> queueCoordinators = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> queueSaving = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, CancellationTokenSource> queuePreparations = new(StringComparer.OrdinalIgnoreCase);
    public QuestQueueState Queue(string name)
    {
        if (name.Length == 0)
            return new();
        if (Preview && previewQueues.TryGetValue(name, out var previewState))
            return previewState;
        if (!queueCache.TryGetValue(name, out var state))
            queueCache[name] = state = queueStore.Load(name);
        return queueCoordinators.TryGetValue(name, out var owner) ? owner.State : state;
    }

    public bool QueueRunning(string name) => Preview && previewQueueRunning.Contains(name) || queueCoordinators.TryGetValue(name, out var queue) && queue.IsRunning;
    public bool QueueBusy(string name) => queueSaving.Contains(name) || queuePreparations.ContainsKey(name);
    public bool QueueReserves(string name) => queueSaving.Contains(name) || queuePreparations.ContainsKey(name) || QueueRunning(name) || Queue(name).Locked;
    static QueueOutcome? SavedQueueOutcome(QuestJob job) => QueueAttemptOutcome.Evaluate(job) ?? (job.Pending is null && (job.Step == "complete" || QueueAttemptOutcome.HasTerminalReceipt(job)) ? QueueAttemptOutcome.Evaluate(job with { Status = job.PermanentCompletion is null ? "Completed" : "PermanentlyCompleted" }) : null);
    bool HasUnfinishedCheckpoint(string name, string id)
    {
        var job = journal.Load(name, id);
        return job.Pending is not null || job.Step is not ("preflight" or "complete");
    }

    bool CanSettleQueueCheckpoint(string name, string id)
    {
        var outcome = SavedQueueOutcome(journal.Load(name, id));
        if (outcome is null)
            return false;
        var state = Queue(name);
        return state.ActiveQuest == id && (state.ActiveResume || state.ActiveStartedAt is null || outcome.At >= state.ActiveStartedAt);
    }

    public async Task EditQueueAsync(string name, Func<QuestQueueState, QuestQueueState> change)
    {
        RequireOpen();
        if (name.Length == 0 || queueSaving.Contains(name))
            return;
        var saved = Queue(name);
        if (saved.Locked || queuePreparations.ContainsKey(name))
            throw new QuestBlockedException("Stop this queue before editing it.");
        queueSaving.Add(name);
        Notify();
        try
        {
            var next = change(saved);
            if (!next.Character.Equals(name, StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException("Queue identity must remain with this character.");
            if (HasBothPaulinQuests(next))
                throw new QuestBlockedException("Choose either Cured the Sick Child or Aided the Pact of Anaman. Completing one permanently locks out the other.");
            if (next.ActiveQuest != next.Quests.FirstOrDefault())
                next = next with
                {
                    ActiveQuest = null,
                    ActiveStartedAt = null,
                    ActiveResume = false
                };
            if (next.Quests.Any(id => !recipes.ContainsKey(id)))
                throw new QuestBlockedException("Only quests with an automatic journey can be queued.");
            foreach (string id in next.Quests.Except(saved.Quests))
            {
                var availability = Availability(name, id);
                if (!availability.CanQueue)
                    throw new QuestBlockedException(QuestName(id) + ": " + availability.Reason);
            }

            await queueStore.SaveAsync(next);
            queueCoordinators.Remove(name);
            queueCache[name] = next;
            if (Preview && previewQueues.ContainsKey(name))
                previewQueues[name] = next;
        }
        catch (Exception e)
        {
            Log("Queue could not be saved: " + e.Message);
            throw;
        }
        finally
        {
            queueSaving.Remove(name);
            Notify();
        }
    }

    public Task MoveQueueAsync(string name, string id, int target) => EditQueueAsync(name, state => state.Reorder(id, target));
    static bool HasBothPaulinQuests(QuestQueueState queue) => queue.Quests.Contains(SickChildEvidence.Id) && queue.Quests.Contains(SickChildEvidence.AnamanId);
    bool HasDeferredBouquet(QuestQueueState queue) => queue.Quests.Contains(FiskEvidence.Id) &&
        journal.Load(queue.Character,FiskEvidence.Id).FiskWait is { Verified:true } wait &&
        queue.Deferred.Any(d => d.Quest == FiskEvidence.Id && d.Due >= wait.Due);
    public string QueueStartReason(string name, QuestQueueState queue)
    {
        if (SchedulerReserves(name))
            return "Scheduler owns this character. Pause its scheduled run first.";
        if (name.Length == 0)
            return "Add a character to plan your first journey";
        if (!Snapshot(name).Connected)
            return "Connect " + name + " to start";
        if (RunnerBusy(name) || controlActions.ContainsKey(name) || calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted)
            return "Another action owns this character";
        if (IsRunning(name) && !QueueRunning(name))
            return "Pause the current quest before starting a queue";
        if (queue.Quests.Length == 0)
            return "Select a quest to get started";
        if (HasBothPaulinQuests(queue))
            return "Choose either Cured the Sick Child or Aided the Pact of Anaman before running this queue.";
        if (queue.Quests.Any(id => !recipes.ContainsKey(id)))
            return "A queued quest has no automatic journey.";
        if (HasDeferredBouquet(queue))
        {
            // Paid waiting is independent of later requirements. An active
            // unresolved checkpoint still owns the character and cannot yield.
            if (queue.ActiveQuest is { } active)
                return Availability(name,active).Ready || CanSettleQueueCheckpoint(name,active) || journal.Load(name,active).Pending is not null
                    ? "" : QuestName(active) + ": " + Availability(name,active).Reason;
            if (queue.Quests.Any(id => Availability(name,id).Ready && !queue.Deferred.Any(d => d.Quest == id && d.Due > DateTimeOffset.UtcNow)) ||
                Availability(name,FiskEvidence.Id).Ready)
                return "";
            return QuestName(FiskEvidence.Id) + ": " + Availability(name,FiskEvidence.Id).Reason;
        }
        foreach (string id in queue.Quests)
        {
            if (!recipes.ContainsKey(id))
                return QuestName(id) + ": automatic journey unavailable";
            var availability = Availability(name, id);
            if (queue.ActiveQuest == id && (CanSettleQueueCheckpoint(name, id) || journal.Load(name, id).Pending is not null))
                continue;
            if (!availability.Ready)
                return QuestName(id) + ": " + availability.Reason;
        }

        return "";
    }

    public async Task StartQueueAsync(string name)
    {
        RequireOpen();
        if (Preview)
            throw new InvalidOperationException("Preview does not run game queues.");
        if (QueueRunning(name))
        {
            await PauseQueueAsync(name);
            return;
        }

        if (queueSaving.Contains(name) || queuePreparations.ContainsKey(name))
            throw new QuestBlockedException("Wait for this character's queue to finish preparing.");
        using var preparing = new CancellationTokenSource();
        queuePreparations.Add(name, preparing);
        Notify();
        QuestQueueCoordinator coordinator;
        try
        {
            var state = Queue(name);
            string blocker = QueueStartReason(name, state);
            if (blocker.Length > 0)
                throw new QuestBlockedException(blocker);
            foreach (string id in state.Quests)
            {
                if (HasDeferredBouquet(state) && state.ActiveQuest != id && !Availability(name,id).Ready)
                    continue;
                if (!await ConfigureQuestAsync(name, recipes[id], state.ActiveQuest == id))
                    return;
                preparing.Token.ThrowIfCancellationRequested();
            }

            preparing.Token.ThrowIfCancellationRequested();
            // Choice dialogs may have been open while a connection or owner changed.
            blocker = QueueStartReason(name, state);
            if (blocker.Length > 0)
                throw new QuestBlockedException(blocker);
            if (!queueCoordinators.TryGetValue(name, out coordinator!))
            {
                coordinator = new(queueStore, state, () => Snapshot(name), async (id, resume, cancellation) =>
                {
                    var availability = Availability(name, id);
                    if (!availability.Ready && !(resume && CanSettleQueueCheckpoint(name, id)))
                        throw new QuestBlockedException(availability.Reason);
                    // A crash between activation and runner preparation must not accept an old outcome.
                    var active = Queue(name);
                    var oldOutcome = SavedQueueOutcome(journal.Load(name, id));
                    if (resume && !active.ActiveResume && active.ActiveStartedAt is { } started && oldOutcome is not null && oldOutcome.At < started)
                        resume = false;
                    var runner = await PrepareRunnerAsync(name, recipes[id]);
                    await runner.RunAsync(RepeatEnabled(name, id), cancellation, startNewAttempt: !resume, singleAttempt: true, deferTimedWaits: true);
                    return runner.Job;
                }, id => HasUnfinishedCheckpoint(name, id), id => Availability(name,id).Ready,
                    (due, cancellation) => WaitForBouquetQueueAsync(name,due,cancellation));
                queueCoordinators[name] = coordinator;
                coordinator.Changed += value => Ui(() =>
                {
                    queueCache[name] = value;
                    Notify();
                });
            }
        }
        catch (OperationCanceledException) when (preparing.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            queuePreparations.Remove(name);
            Notify();
        }

        await RunQueueCoordinatorAsync(coordinator, sessions[name]);
    }

    internal async Task RunQueueCoordinatorAsync(QuestQueueCoordinator coordinator, IGameSession session)
    {
        using var cancellation = new CancellationTokenSource();
        Task? interrupted = null;
        bool interruptionRequested = false;
        var gate = new object();
        long generation = session.Snapshot.Generation;
        string character = coordinator.State.Character;
        void Interrupt(string reason)
        {
            lock (gate)
            {
                if (interruptionRequested)
                    return;
                interruptionRequested = true;
                cancellation.Cancel();
                interrupted = coordinator.PauseAsync(PlayerReason(reason));
            }
        }

        void ChangedSession(CharacterSnapshot snapshot)
        {
            if (!snapshot.Connected || snapshot.Generation != generation || !string.Equals(snapshot.Name, character, StringComparison.OrdinalIgnoreCase))
                Interrupt("Character connection changed. Resume after fresh state.");
        }

        session.Interrupted += Interrupt;
        session.Changed += ChangedSession;
        try
        {
            await coordinator.RunAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            session.Interrupted -= Interrupt;
            session.Changed -= ChangedSession;
            Task? settle;
            lock (gate)
                settle = interrupted;
            if (settle is not null)
                await settle;
            Notify();
        }
    }

    public async Task PauseQueueAsync(string name, bool stop = false)
    {
        if (queuePreparations.TryGetValue(name, out var preparing))
        {
            preparing.Cancel();
            if (runnerPreparations.TryGetValue(name, out var runnerPreparation))
                runnerPreparation.Cancel();
        }

        await WaitForQueueSavesAsync(name);
        if (queueCoordinators.TryGetValue(name, out var coordinator))
            await coordinator.PauseAsync(stop ? "Stopped; progress saved" : "Paused by player", stop);
        else
        {
            var state = Queue(name);
            if (state.Locked)
            {
                var next = state with
                {
                    State = stop ? "Idle" : "Paused",
                    Reason = stop ? "Stopped; progress saved" : "Paused by player"
                };
                await queueStore.SaveAsync(next);
                queueCache[name] = next;
                if (Preview && previewQueues.ContainsKey(name))
                    previewQueues[name] = next;
            }
        }

        if (Preview)
            previewQueueRunning.Remove(name);
        Notify();
    }

    async Task WaitForQueueSavesAsync(string? character)
    {
        while (queueSaving.Any(name => character is null || name.Equals(character, StringComparison.OrdinalIgnoreCase)))
            await Task.Delay(20);
    }

    public QueueOutcome[] RecentActivity(string name) => Queue(name).History.Concat(name.Length == 0 ? [] : recipes.Values.Select(quest => SavedQueueOutcome(journal.Load(name, quest.Id))).OfType<QueueOutcome>()).DistinctBy(outcome => (outcome.Quest, outcome.At)).OrderByDescending(outcome => outcome.At).ToArray();
}
