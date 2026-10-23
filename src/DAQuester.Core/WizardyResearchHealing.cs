namespace DAQuester.Core;

// Independent of Pending: damage can arrive before the research outcome does.
public sealed record ResearchHealing(long Generation, long Revision, int BeforeCount, uint BeforeHp, byte Slot);

public sealed partial class WizardyResearchActions
{
    static bool Wounded(CharacterSnapshot s) => s.Hp > 0 && s.MaxHp > 0 && s.Hp < s.MaxHp;

    static async Task CaptureResultStatus(QuestContext c)
    {
        var observed = c.ResearchResultObservation is { } saved
            ? WizardyResearchEvidence.ObserveResultStatus(c.Job, saved) : c.Job;
        observed = WizardyResearchEvidence.ObserveResultStatus(observed, c.Session.Snapshot);
        if (!ReferenceEquals(observed, c.Job)) await c.SaveAsync(observed);
    }

    static async Task ReconcileHealing(QuestContext c, CancellationToken ct)
    {
        if (Progress(c).Healing is not { } dose) return;
        Identity(c);
        if (c.Session.Snapshot.Generation != dose.Generation)
            throw new QuestBlockedException("Saved Exkuranum use belongs to the earlier connection. Verify its original item and health receipts before continuing; no dose resent.");
        bool Receipt(CharacterSnapshot s) => s.Connected && s.Generation == dose.Generation && s.Revision > dose.Revision &&
            string.Equals(s.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase) && s.InventoryKnown &&
            s.Count(Exkuranum) == dose.BeforeCount - 1 && s.MaxHp > 0 && s.Hp >= s.MaxHp;
        if (!Receipt(c.Session.Snapshot)) await c.Session.WaitAsync(Receipt, dose.Revision, Timeout, ct);
        if (!Receipt(c.Session.Snapshot)) throw new QuestBlockedException("Exkuranum requires one consumed potion and full HP before research continues.");
        await Save(c, Progress(c) with { Healing = null, HealthCheckRequired = false }, "Exkuranum consumed; full health verified");
    }

    static async Task Health(QuestContext c, CancellationToken ct)
    {
        await ReconcileHealing(c, ct);
        await ReadHealth(c, ct);
        var s = c.Session.Snapshot;
        if (s.Hp >= s.MaxHp)
        {
            if (Progress(c).HealthCheckRequired)
                await Save(c, Progress(c) with { HealthCheckRequired = false }, "Full health verified");
            return;
        }
        if (!s.InventoryKnown) throw new QuestBlockedException("Current inventory required before using Exkuranum.");
        var item = s.Inventory.Values.Where(i => i.Quantity > 0 && i.Name.Equals(Exkuranum, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Slot).FirstOrDefault() ?? throw new ResourceUnavailableException("Exkuranum is missing. Restore full health before more research.");
        await CaptureResultStatus(c);
        await Save(c, Progress(c) with { HealthCheckRequired = true,
            Healing = new(s.Generation, s.Revision, s.Count(Exkuranum), s.Hp!.Value, item.Slot) }, "Restoring health with one Exkuranum");
        await c.Session.ExecuteAsync(new("use-item", Amount: item.Slot, ExpectedResource: Exkuranum, ExpectedGeneration: s.Generation), ct);
        await ReconcileHealing(c, ct);
    }

    // A pending result must wake on damage instead of waiting out its reply timeout.
    static async Task WaitResearch(QuestContext c, Func<CharacterSnapshot, bool> predicate, long after, CancellationToken ct)
    {
        var end = DateTimeOffset.UtcNow + Timeout;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await CaptureResultStatus(c);
            if (Wounded(c.Session.Snapshot) || Progress(c).Healing is not null) await Health(c, ct);
            if (predicate(c.Session.Snapshot)) return;
            var remaining = end - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) throw new TimeoutException("Research reply has not arrived; submission saved.");
            await c.Session.WaitAsync(s => predicate(s) || Wounded(s), after, remaining, ct);
            after = c.Session.Snapshot.Revision;
        }
    }
}
