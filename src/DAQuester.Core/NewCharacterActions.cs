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
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var gear in Due(s.Snapshot))
                await BuyAsync(c, gear, ct);
            await EquipOwned(c, ct);
            int level = s.Snapshot.Level ?? 0;
            if (level >= FirstStepsLevel && Due(s.Snapshot).Count == 0)
            {
                if (!Worn(s.Snapshot, LouresSaber))
                    throw new QuestBlockedException("The Loures Saber was bought but isn't equipped. Check the bag, then resume.");
                await Complete(c, name + " is level " + level + " with the Loures Saber");
                return;
            }

            await Level(c, name, NextStop(level), FirstStepsAvoid, ct);
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

    static async Task Level(QuestContext c, string name, int target, IReadOnlyDictionary<int, ushort[]> avoid, CancellationToken ct)
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
            Note = line => hunting.Log?.Invoke(name, line)
        };
        var leveling = new HuntingEngine(s, map, ground, options, hunting.Load(ground, name), p => hunting.Save(ground, name, p), Vestibule, new(), target, stats: new StatSpender(s, StatPaths.Strict, options.Note))
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
    static async Task BuyAsync(QuestContext c, StarterGear gear, CancellationToken ct)
    {
        var s = c.Session;
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

        var offer = shop.Items.FirstOrDefault(i => i.Name.Equals(gear.Name, StringComparison.OrdinalIgnoreCase)) ?? throw new QuestBlockedException(gear.Npc + " doesn't sell a " + gear.Name + ".");
        uint gold = s.Snapshot.Gold ?? 0;
        if (gold < offer.Price)
            throw new ResourceUnavailableException(s.Snapshot.Name + " needs " + offer.Price.ToString("N0") + " gold for a " + gear.Name + ". Send gold from another character, then resume.");
        int had = s.Snapshot.Count(gear.Name);
        long bought = s.Snapshot.Revision;
        await s.ExecuteAsync(new("buy", Text: gear.Name, Dialog: shop), ct);
        try
        {
            await s.WaitAsync(x => x.Count(gear.Name) > had && x.Gold == gold - offer.Price, bought, proof, ct);
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("The " + gear.Name + " purchase wasn't confirmed. Check gold and the bag, then resume.");
        }

        if (s.Snapshot.Dialog is { } list)
            await s.ExecuteAsync(new("close", Dialog: list), ct);
        await c.SaveAsync(c.Job with
        {
            Reason = "Bought a " + gear.Name + " for " + offer.Price.ToString("N0") + " gold"
        });
    }
}
