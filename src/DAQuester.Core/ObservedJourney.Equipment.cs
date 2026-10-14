namespace DAQuester.Core;

static partial class ObservedJourney
{
    static async Task ArmamentsAsync(QuestContext c, ObservedQuestTask task, CancellationToken ct)
    {
        // Unarmed access excludes both weapon and shield. Reserve room for both
        // before removing either, then verify each individual removal receipt.
        if (c.Job.Pending is not null)
            await ReconcileWeaponAsync(c, task, true, ct);
        byte[] slots = [1, 3];
        var snapshot = c.Session.Snapshot;
        int needed = slots.Count(slot => snapshot.Equipment.ContainsKey(slot));
        if (!snapshot.InventoryKnown || snapshot.Inventory.Count + needed > 59)
            throw new QuestBlockedException("Reserve bag space for both the shrine weapon and shield before removal.");
        foreach (byte slot in slots)
        {
            var s = c.Session.Snapshot;
            if (!s.Equipment.TryGetValue(slot, out var name))
                continue;
            await c.SaveAsync(c.Job with
            {
                Route = c.Job.Route! with
                {
                    Baseline = Baseline(s),
                    Resource = name,
                    EquipmentSlot = slot
                }
            });
            await c.BeginAsync("observed-unequip:" + task.Id);
            await c.Session.ExecuteAsync(new("unequip", Text: name, Slot: slot, ExpectedGeneration: s.Generation), ct);
            await ReconcileWeaponAsync(c, task, true, ct);
        }
    }

    static async Task WeaponAsync(QuestContext c, ObservedQuestTask task, bool remove, CancellationToken ct)
    {
        // The armed shrine requires an equipped usable weapon. Merely carrying
        // one is insufficient, and this journey never buys an unknown substitute.
        var session = c.Session;
        if (c.Job.Pending is not null)
        {
            await ReconcileWeaponAsync(c, task, remove, ct);
            return;
        }
        var s = session.Snapshot;
        if (remove && !s.Equipment.ContainsKey(1) || !remove && s.Equipment.ContainsKey(1))
            return;
        if (!s.InventoryKnown)
            throw new QuestBlockedException("Known bag required before changing shrine weapon state.");
        if (remove && s.Inventory.Count >= 59)
            throw new QuestBlockedException("Free a bag slot before taking off the shrine weapon.");
        if (!remove && s.Class is null)
        {
            await session.ExecuteAsync(new("inspect-self"), ct);
            s = session.Snapshot;
        }
        Item? owned = remove ? null : WeaponRegistry.BestMelee(s);
        string name = remove ? s.Equipment[(byte)1] : owned?.Name ?? throw new ResourceUnavailableException("Carry a known usable melee weapon for the armed shrine; none is purchased or guessed automatically.");
        await c.SaveAsync(c.Job with
        {
            Route = c.Job.Route! with
            {
                Baseline = Baseline(s),
                Resource = name
            }
        });
        await c.BeginAsync((remove ? "observed-unequip:" : "observed-equip:") + task.Id);
        await session.ExecuteAsync(new(remove ? "unequip" : "equip", Text: name, Slot: remove ? (byte)1 : owned!.Slot, ExpectedGeneration: s.Generation), ct);
        await ReconcileWeaponAsync(c, task, remove, ct);
    }

    static async Task ReconcileWeaponAsync(QuestContext c, ObservedQuestTask task, bool remove, CancellationToken ct)
    {
        // Equipment and bag updates are separate packets. Require both sides of
        // the transfer in the original generation before clearing the pending action.
        var p = c.Job.Pending!;
        string name = c.Job.Route?.Resource ?? throw new QuestBlockedException("Weapon baseline missing.");
        byte slot = c.Job.Route?.EquipmentSlot ?? 1;
        if (p.Kind != (remove ? "observed-unequip:" : "observed-equip:") + task.Id || p.Generation != c.Session.Snapshot.Generation || !p.InventoryBaselineKnown)
            throw new QuestBlockedException("Weapon submission needs reconciliation in its own connection; do not repeat it.");
        bool Receipt(CharacterSnapshot s) => s.Generation == p.Generation && s.InventoryKnown &&
            (remove ? !s.Equipment.ContainsKey(slot) && s.Count(name) == p.InventoryCounts.GetValueOrDefault(name) + 1
                : s.Equipment.GetValueOrDefault((byte)1) == name && s.Count(name) == p.InventoryCounts.GetValueOrDefault(name) - 1);
        if (!Receipt(c.Session.Snapshot))
            await c.Session.WaitAsync(Receipt, p.Revision, ProofTimeout, ct);
        await c.SaveAsync(c.Job with
        {
            Pending = null,
            Reason = remove ? "Weapon removed and bag receipt verified" : "Equipped weapon and bag receipt verified"
        });
    }
}
