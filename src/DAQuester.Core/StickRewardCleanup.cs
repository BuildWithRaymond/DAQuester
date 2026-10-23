namespace DAQuester.Core;

public sealed record StickCleanupDrop(long Generation, long Revision, byte Slot, string Item, uint Quantity, int TotalBefore);
public sealed record StickCleanupProgress(DateTimeOffset OutcomeAt, bool Finished = false, StickCleanupDrop? Pending = null);

public static class StickRewardCleanup
{
    public static bool Verified(QuestJob job) => job.Quest == "spare-a-stick" && job.Step == "complete" && job.CompletedAt is not null && job.Pending is null;
    public static bool Unfinished(QuestJob job) => job.Quest == "spare-a-stick" && job.StickCleanup is { Finished: false };
    static bool Leftover(string name) => name.Equals("Stick", StringComparison.OrdinalIgnoreCase) || name.Equals("Wooden Shield", StringComparison.OrdinalIgnoreCase);
    static bool Receipt(CharacterSnapshot s, StickCleanupDrop drop) => s.Connected && s.InventoryKnown && s.Generation == drop.Generation
        && s.Revision > drop.Revision && (long)s.Count(drop.Item) == (long)drop.TotalBefore - drop.Quantity;

    public static async Task RunAsync(QuestContext c, CancellationToken ct)
    {
        if (!Verified(c.Job)) return;
        var at = c.Job.CompletedAt!.Value;
        if (c.Job.StickCleanup is { Finished: true } finished && finished.OutcomeAt == at) return;
        if (c.Job.StickCleanup is { Pending: not null } older && older.OutcomeAt != at)
            throw new QuestBlockedException("An older leftover drop needs reconciliation before this outcome can be cleaned up.");
        void RequireOwnBag()
        {
            var current = c.Session.Snapshot;
            if (!current.Connected || !current.InventoryKnown || !string.Equals(current.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException("Fresh own inventory is required before clearing Stick and Wooden Shield leftovers.");
        }
        if (c.Job.StickCleanup is null || c.Job.StickCleanup.OutcomeAt != at)
            await c.SaveAsync(c.Job with { StickCleanup = new(at) });
        RequireOwnBag();
        for (int drops = 0; drops <= CharacterSnapshot.InventoryCapacity; drops++)
        {
            ct.ThrowIfCancellationRequested();
            RequireOwnBag();
            var progress = c.Job.StickCleanup!;
            if (progress.Pending is { } submitted)
            {
                if (!Receipt(c.Session.Snapshot, submitted))
                    throw new QuestBlockedException("Saved " + submitted.Item + " drop needs its inventory receipt. It will not be sent again.");
                await c.SaveAsync(c.Job with { StickCleanup = progress with { Pending = null } });
            }
            var s = c.Session.Snapshot;
            var item = s.Inventory.Values.OrderBy(i => i.Slot).FirstOrDefault(i => Leftover(i.Name));
            if (item is null)
            {
                await c.SaveAsync(c.Job with { StickCleanup = c.Job.StickCleanup! with { Finished = true },
                    Status = c.Job.PermanentCompletion is null ? "Completed" : "PermanentlyCompleted" });
                return;
            }
            if (drops == CharacterSnapshot.InventoryCapacity)
                throw new QuestBlockedException("Leftover cleanup reached its bag limit. Review inventory before resuming.");
            await QuestCompletionDialogs.DismissAsync(c, c.Job.PermanentCompletion?.Dialog ?? c.Job.CompletionDialog, ct);
            RequireOwnBag();
            s = c.Session.Snapshot;
            if (s.Dialog is not null || s.Map is null || s.Position is null)
                throw new QuestBlockedException("Close the completed quest popup and confirm the current tile before clearing leftovers.");
            // Resolve the slot again after closure, then persist before sending.
            item = s.Inventory.Values.OrderBy(i => i.Slot).FirstOrDefault(i => Leftover(i.Name));
            if (item is null) continue;
            var pending = new StickCleanupDrop(s.Generation, s.Revision, item.Slot, item.Name, item.Quantity, s.Count(item.Name));
            await c.SaveAsync(c.Job with { StickCleanup = c.Job.StickCleanup! with { Pending = pending } });
            await c.Session.ExecuteAsync(new("drop", Map: s.Map, Target: s.Position, Slot: item.Slot, Amount: item.Quantity,
                ExpectedGeneration: s.Generation, ExpectedResource: item.Name), ct);
            if (!Receipt(c.Session.Snapshot, pending))
            {
                try { await c.Session.WaitAsync(x => Receipt(x, pending), pending.Revision, TimeSpan.FromSeconds(5), ct); }
                catch (TimeoutException) { throw new QuestBlockedException("Leftover drop is unconfirmed. Its saved submission will not be resent."); }
            }
            // The next iteration reconciles this exact receipt before any further drop.
        }
    }
}
