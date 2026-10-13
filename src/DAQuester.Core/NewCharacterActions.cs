using static DAQuester.Core.NewCharacterPath;
namespace DAQuester.Core;

public sealed class NewCharacterActions : IQuestAction
{
    const string Buy = "Buy";
    static readonly TimeSpan reply = TimeSpan.FromSeconds(5), proof = TimeSpan.FromSeconds(10);

    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        var s = c.Session;
        if (s.Snapshot.Name is not { } name || !name.Equals(c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Character changed; resume on the saved character.");
        switch (step.Action)
        {
            case "first-steps-preflight":
                if (!c.Job.FirstStepsBriefed)
                    throw new QuestBlockedException("Start Peasant's First Steps from its page to read what it will do first.");
                Require(FirstStepsEligibility(s.Snapshot));
                return;
            case "first-steps-run":
                await FirstSteps(c, name, ct);
                return;
            case "climb-preflight":
                if (!s.Snapshot.LegendsKnown)
                    await s.ExecuteAsync(new("inspect-self"), ct);
                await EquipOwned(c, ct);
                Require(ClimbEligibility(s.Snapshot));
                if (TerrorWarning(name, s.Snapshot) is { } warning && !c.Job.TerrorWindowAccepted)
                    throw new QuestBlockedException(warning + " Start Crypt Climb again to confirm.");
                return;
            case "climb-run":
                await Climb(c, name, ct);
                return;
            default:
                throw new QuestBlockedException("Unsupported action: " + step.Action);
        }
    }

    static void Require(Eligibility e)
    {
        if (e.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(e.Reason);
    }

    static async Task FirstSteps(QuestContext c, string name, CancellationToken ct)
    {
        var s = c.Session;
        await ReconcilePurchase(c);
        var stats = new StatSpender(s, StatPaths.Strict, line => c.Hunting?.Log?.Invoke(name, line));
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (s.Snapshot.Stats is { Unspent: > 0 })
                Activity(c, "Stats");
            await stats.SpendAsync(ct);
            foreach (var shop in Due(s.Snapshot).GroupBy(g => (g.Map, g.Npc)))
                await BuyAsync(c, shop.ToArray(), ct);
            await EquipOwned(c, ct);
            int level = s.Snapshot.Level ?? 0;
            if (level >= FirstStepsLevel && Due(s.Snapshot).Count == 0)
            {
                if (!Worn(s.Snapshot, LouresSaber))
                    throw new QuestBlockedException("The Loures Saber was bought but isn't equipped. Check the bag, then resume.");
                await Complete(c, name + " is level " + level + " with the Loures Saber");
                return;
            }

            await Level(c, name, NextStop(level), FirstStepsAvoid, ct, stats);
        }
    }

    static async Task Climb(QuestContext c, string name, CancellationToken ct)
    {
        var s = c.Session;
        await EquipOwned(c, ct);
        Require(ClimbEligibility(s.Snapshot));
        await Level(c, name, ClimbLevel, new Dictionary<int, ushort[]>(), ct);
        if ((s.Snapshot.Level ?? 0) < ClimbLevel)
            throw new QuestBlockedException("Hunting ended before level 11. Resume to continue.");
        await Complete(c, name + " reached level " + s.Snapshot.Level);
    }

    static async Task Level(QuestContext c, string name, int target, IReadOnlyDictionary<int, ushort[]> avoid, CancellationToken ct, StatSpender? stats = null)
    {
        var s = c.Session;
        var hunting = c.Hunting ?? throw new QuestBlockedException("The hunting engine isn't available in this session.");
        var map = s as ICombatMap ?? throw new QuestBlockedException("The hunting engine needs the live game session.");
        var ground = HuntingGrounds.MilethCrypt;
        await c.SaveAsync(c.Job with
        {
            Reason = "Leveling to " + target + " in the Mileth crypt"
        });
        var options = new BashOptions
        {
            IsFriend = hunting.IsFriend,
            Note = line => hunting.Log?.Invoke(name, line),
            Activity = activity => hunting.Activity?.Invoke(name, activity)
        };
        var leveling = new HuntingEngine(s, map, ground, options, hunting.Load(ground, name), p => hunting.Save(ground, name, p), Vestibule, new(), target, stats: stats ?? new StatSpender(s, StatPaths.Strict, options.Note))
        {
            DeepestFloor = DeepestFloor,
            AlwaysAvoid = avoid
        };
        await leveling.RunAsync(ct);
    }

    static Task Complete(QuestContext c, string text) => c.SaveAsync(c.Job with
    {
        Step = "complete",
        Pending = null,
        CompletedAt = DateTimeOffset.UtcNow,
        CompletionText = text,
        Repeat = false,
        NextRun = null,
        Reason = text
    });

    static async Task EquipOwned(QuestContext c, CancellationToken ct)
    {
        var s = c.Session;
        foreach (var gear in ToEquip(s.Snapshot))
        {
            var item = s.Snapshot.Inventory.Values.Where(i => i.Name.Equals(gear.Name, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Slot).FirstOrDefault();
            if (item is null)
                continue;
            Activity(c, "Equipping", gear.Name);
            // Ring use fills an empty hand; explicitly free the chosen hand so an
            // upgrade of the right ring cannot accidentally replace the left one.
            if (gear.Slot is 7 or 8 && s.Snapshot.Equipment.ContainsKey(gear.Slot))
            {
                if (s.Snapshot.Inventory.Count >= 59)
                    throw new QuestBlockedException("Free an inventory slot before upgrading the " + gear.Name + ".");
                long removed = s.Snapshot.Revision;
                await s.ExecuteAsync(new("unequip", Slot: gear.Slot), ct);
                await s.WaitAsync(x => !x.Equipment.ContainsKey(gear.Slot), removed, reply, ct);
            }
            long before = s.Snapshot.Revision;
            await s.ExecuteAsync(new("equip", Slot: item.Slot), ct);
            try
            {
                await s.WaitAsync(x => Worn(x, gear), before, reply, ct);
            }
            catch (TimeoutException)
            {
                throw new QuestBlockedException("The " + gear.Name + " didn't go on. Check the level and the bag, then resume.");
            }
        }
    }

    // Same shop flow as Torrance's Leather Shield: open the NPC, choose Buy, buy from the list by name.
    static void Activity(QuestContext c, string kind, string? detail = null) => c.Hunting?.Activity?.Invoke(c.Job.Character, new(kind, Detail: detail));

    static async Task ReconcilePurchase(QuestContext c)
    {
        if (c.Job.Pending is not { } pending)
            return;
        var gear = Gear.FirstOrDefault(g => pending.Kind == "starter-buy:" + g.Name);
        if (gear is null || !c.Session.Snapshot.InventoryKnown || !Owned(c.Session.Snapshot, gear))
            throw new QuestBlockedException("The saved starter purchase is awaiting confirmation. Check gold and the bag before resuming; it will not be submitted again.");
        // Fresh ownership satisfies this shopping goal, including after a manual
        // reconciliation. It does not invent a receipt or attribute the purchase.
        await c.SaveAsync(c.Job with { Pending = null, Reason = gear.Name + " is owned; continuing starter journey" });
    }

    static async Task BuyAsync(QuestContext c, StarterGear[] purchases, CancellationToken ct)
    {
        var s = c.Session;
        var gear = purchases[0];
        Activity(c, "Buying", gear.Name + " from " + gear.Npc + " in " + gear.Town);
        if (s.Snapshot.Gold is not { } carried || carried < gear.Price)
            throw new ResourceUnavailableException(s.Snapshot.Name + " needs " + gear.Price.ToString("N0") + " gold for a " + gear.Name + ". Send gold from another character, then resume.");
        if (s.Snapshot.Inventory.Count >= 59)
            throw new QuestBlockedException("Free an inventory slot for the " + gear.Name + ".");
        await c.SaveAsync(c.Job with
        {
            Reason = "Buying a " + gear.Name + " from " + gear.Npc + " in " + gear.Town
        });
        await s.ExecuteAsync(new("visit", gear.Map, Text: gear.Npc), ct);
        if (s.Snapshot.Dialog is { } open)
            await s.ExecuteAsync(new("close", Dialog: open), ct);
        await s.ExecuteAsync(new("open", Text: gear.Npc), ct);
        var menu = s.Snapshot.Dialog;
        if (menu is null || menu.IsPursuit || !menu.Name.Equals(gear.Npc, StringComparison.OrdinalIgnoreCase) || !menu.Choices.Any(x => x.Text.Equals(Buy, StringComparison.OrdinalIgnoreCase)))
            throw new QuestBlockedException(gear.Npc + "'s menu has no \"Buy\" option.");
        long before = s.Snapshot.Revision;
        await s.ExecuteAsync(new("choose", Text: Buy, Dialog: menu), ct);
        Dialog shop;
        try
        {
            shop = (await s.WaitAsync(x => x.Dialog is { IsPursuit: false, Type: 4 } d && d.EntityId == menu.EntityId && d.Revision > before, before, reply, ct)).Dialog!;
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException(gear.Npc + " didn't show the shop list.");
        }

        foreach (var purchase in purchases)
        {
            ct.ThrowIfCancellationRequested();
            if (Owned(s.Snapshot, purchase))
                continue;
            var current = s.Snapshot.Dialog;
            if (current is { IsPursuit: false, Type: 0 } && current.EntityId == menu.EntityId && current.Choices.Any(x => x.Text == Buy))
            {
                long changed = s.Snapshot.Revision;
                await s.ExecuteAsync(new("choose", Text: Buy, Dialog: current), ct);
                current = (await s.WaitAsync(x => x.Dialog is { IsPursuit: false, Type: 4 } d && d.EntityId == menu.EntityId && d.Revision > changed, changed, reply, ct)).Dialog;
            }
            if (current is not { IsPursuit: false, Type: 4 } || current.EntityId != menu.EntityId || current.Generation != shop.Generation || !current.Name.Equals(gear.Npc, StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException(gear.Npc + "'s shop changed. No further purchase submitted; resume after checking the menu.");
            var offer = current.Items.FirstOrDefault(i => i.Name.Equals(purchase.Name, StringComparison.OrdinalIgnoreCase)) ?? throw new QuestBlockedException(gear.Npc + " doesn't sell a " + purchase.Name + ".");
            uint gold = s.Snapshot.Gold ?? 0;
            if (gold < offer.Price)
                throw new ResourceUnavailableException(s.Snapshot.Name + " needs " + offer.Price.ToString("N0") + " gold for a " + purchase.Name + ". Send gold from another character, then resume.");
            if (s.Snapshot.Inventory.Count >= 59)
                throw new QuestBlockedException("Free an inventory slot for the " + purchase.Name + ".");
            int had = s.Snapshot.Count(purchase.Name);
            Activity(c, "Buying", purchase.Name + " from " + purchase.Npc + " in " + purchase.Town);
            long bought = s.Snapshot.Revision, generation = s.Snapshot.Generation;
            await c.BeginAsync("starter-buy:" + purchase.Name, current.EntityId, "Buying a " + purchase.Name + " from " + purchase.Npc);
            await s.ExecuteAsync(new("buy", Text: purchase.Name, Dialog: current), ct);
            try
            {
                await s.WaitAsync(x => x.Generation == generation && string.Equals(x.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase) && x.Count(purchase.Name) > had && x.Gold == gold - offer.Price, bought, proof, ct);
            }
            catch (TimeoutException)
            {
                throw new QuestBlockedException("The " + purchase.Name + " purchase wasn't confirmed. Check gold and the bag, then resume. It will not be bought again automatically.");
            }
            await c.SaveAsync(c.Job with { Pending = null, Reason = "Bought a " + purchase.Name + " for " + offer.Price.ToString("N0") + " gold" });
        }

        if (s.Snapshot.Dialog is { } list)
            await s.ExecuteAsync(new("close", Dialog: list), ct);
    }
}
