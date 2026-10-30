using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed record BagSortTarget(string Character, long Generation, uint? Id);
public enum BagSortStatus { Sorted, Unchanged, Skipped, Stopped }
public sealed record BagSortOutcome(string Character, BagSortStatus Status, string Reason);

public sealed partial class DesktopRuntime
{
    public BagSortTarget CaptureBagSortTarget(string name)
    {
        var snapshot = Snapshot(name);
        return new(name, snapshot.Generation, snapshot.Id);
    }

    public string? BagSortBlocker(string name)
    {
        if (closing || shuttingDown)
            return "DAQuester is closing.";
        if (string.IsNullOrWhiteSpace(name) || !Settings.Characters.Contains(name, StringComparer.OrdinalIgnoreCase))
            return "Choose a saved character first.";
        if (SchedulerReserves(name))
            return "Scheduler owns this character.";
        if (QueueReserves(name) || QueueBusy(name))
            return "This character's quest queue owns its bag.";
        if (IsRunning(name) || RunnerBusy(name) || controlActions.ContainsKey(name)
            || LaunchPending(name) || calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted)
            return "This character already has active work.";
        var snapshot = Snapshot(name);
        if (!snapshot.Connected)
            return "Connect this character before sorting its bag.";
        if (snapshot.Id is null || !string.Equals(snapshot.Name, name, StringComparison.OrdinalIgnoreCase))
            return "This character's connection identity is not confirmed.";
        if (!snapshot.InventoryKnown)
            return "This character's bag has not been observed yet.";
        if (snapshot.Dialog is not null || snapshot.Exchange is not null)
            return "Close this character's conversation or exchange before sorting.";
        return null;
    }

    public async Task<BagSortOutcome> SortBagAsync(BagSortTarget target, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Preview)
            throw new InvalidOperationException("Preview cannot execute bag sorting commands.");
        if (cancellation.IsCancellationRequested)
            return new(target.Character, BagSortStatus.Stopped, "Bag sorting was cancelled.");
        if (BagSortBlocker(target.Character) is { } blocker)
            return new(target.Character, BagSortStatus.Skipped, blocker);
        if (!sessions.TryGetValue(target.Character, out var session))
            return new(target.Character, BagSortStatus.Skipped, "This character's connection is no longer available.");
        var before = session.Snapshot;
        if (before.Generation != target.Generation || before.Id != target.Id)
            return new(target.Character, BagSortStatus.Skipped, "This character's connection changed during confirmation.");

        // Claim before the first await so other admission paths see the whole sort
        // as one local owner. Stop and shutdown cancel and drain this same lease.
        using var owner = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        controlActions[target.Character] = owner;
        void Interrupt(string _)
        {
            try { owner.Cancel(); }
            catch (ObjectDisposedException) { }
        }
        session.Interrupted += Interrupt;
        try
        {
            Notify();
            await session.ExecuteAsync(new("sort-bag", ExpectedGeneration: target.Generation), owner.Token);
            return BagSortPlanner.Matches(before.Inventory, session.Snapshot.Inventory)
                ? new(target.Character, BagSortStatus.Unchanged, "Bag is already sorted.")
                : new(target.Character, BagSortStatus.Sorted, "Bag sorted.");
        }
        catch (OperationCanceledException)
        {
            return new(target.Character, BagSortStatus.Stopped, "Bag sorting stopped.");
        }
        catch (Exception error) when (error is QuestBlockedException or IOException or TimeoutException or InvalidOperationException)
        {
            Log(target.Character + ": bag sorting stopped: " + error.Message);
            return new(target.Character, BagSortStatus.Stopped, error.Message);
        }
        finally
        {
            session.Interrupted -= Interrupt;
            if (controlActions.GetValueOrDefault(target.Character) == owner)
                controlActions.Remove(target.Character);
            Notify();
        }
    }

    public async Task<IReadOnlyList<BagSortOutcome>> SortBagsAsync(IReadOnlyList<BagSortTarget> targets, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var frozen = targets.ToArray();
        var outcomes = new List<BagSortOutcome>(frozen.Length);
        bool stopped = false;
        foreach (var target in frozen)
        {
            var outcome = stopped
                ? new BagSortOutcome(target.Character, BagSortStatus.Stopped, "Bag sorting stopped before this character.")
                : await SortBagAsync(target, cancellation);
            outcomes.Add(outcome);
            stopped |= outcome.Status == BagSortStatus.Stopped;
        }
        return outcomes;
    }
}
