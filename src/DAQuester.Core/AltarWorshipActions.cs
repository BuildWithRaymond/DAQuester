using static DAQuester.Core.AltarWorshipEvidence;
namespace DAQuester.Core;

public sealed class AltarWorshipActions : IQuestAction
{
    static readonly TimeSpan reply = TimeSpan.FromSeconds(5), proof = TimeSpan.FromSeconds(10);
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        var s = c.Session;
        if (s.Snapshot.Name is not { } name || !name.Equals(c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Altar Worship character changed; resume on the saved character.");
        if (c.Job.Pending is not null)
            await Reconcile(c, ct);
        if (s.Snapshot.Class is null)
            await s.ExecuteAsync(new("inspect-self"), ct);
        if (NoMark(name, s.Snapshot.Class) is { } warning && !c.Job.AltarNoMarkAccepted)
            throw new QuestBlockedException(warning + " Start the quest again to confirm.");
        switch (step.Action)
        {
            case "altar-preflight":
                var gate = Eligibility(s.Snapshot, c.Job);
                if (gate.Kind != EligibilityKind.Eligible)
                    throw new QuestBlockedException(gate.Reason);
                return;
            case "altar-run":
                await Run(c, ct);
                return;
            default:
                throw new QuestBlockedException("Unsupported Altar Worship action: " + step.Action);
        }
    }

    static async Task Run(QuestContext c, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (Made(c.Job) >= Goal)
            {
                await VerifyReward(c, null, ct);
                return;
            }
            if (NextOffering(c.Job) is { } next && next > DateTimeOffset.UtcNow)
            {
                await Wait(c, next, ct);
                continue;
            }
            long dropped = await Offer(c, ct);
            if (Made(c.Job) >= Goal)
            {
                await VerifyReward(c, dropped, ct);
                return;
            }
        }
    }

    static async Task Wait(QuestContext c, DateTimeOffset next, CancellationToken ct)
    {
        if (c.YieldScheduledWaits)
            throw new QuestScheduledWaitException(next);
        await c.SaveAsync(c.Job with
        {
            Status = "Scheduled",
            Reason = Progress(c.Job)
        });
        var delay = next - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct);
        await c.SaveAsync(c.Job with
        {
            Status = "Running",
            Reason = ""
        });
    }

    static async Task<long> Offer(QuestContext c, CancellationToken ct)
    {
        var s = c.Session;
        uint paid = s.Snapshot.Count(Shield) == 0 ? await BuyShield(c, ct) : 0;
        await s.ExecuteAsync(new("navigate", Map, Stand, Tolerance: 1), ct);
        if (s.Snapshot.Map != Map || !InReach(s.Snapshot.Position))
            throw new QuestBlockedException("Couldn't stand close enough to the Mileth Altar to drop the shield.");
        var shield = s.Snapshot.Inventory.Values.Where(i => i.Name.Equals(Shield, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Slot).FirstOrDefault() ?? throw new QuestBlockedException("The Leather Shield is no longer in the bag.");
        int rings = RingCount(s.Snapshot);
        await c.BeginAsync("altar-offer", reason: "Offering " + (Made(c.Job) + 1) + " of 10 at the Mileth Altar");
        var t = c.Job.Pending!;
        try
        {
            await s.ExecuteAsync(new("drop", Target: Altar, Slot: shield.Slot, Amount: 1), ct);
        }
        catch (QuestBlockedException) when (s.Snapshot.Generation == t.Generation && s.Snapshot.Count(Shield) == Carried(t))
        {
            await c.SaveAsync(c.Job with
            {
                Pending = null
            });
            throw new QuestBlockedException("The altar didn't take the Leather Shield. Nothing was offered; resume to try again.");
        }
        await Prove(c, t, paid, rings, ct);
        return t.Revision;
    }

    static async Task<uint> BuyShield(QuestContext c, CancellationToken ct)
    {
        var s = c.Session;
        if (s.Snapshot.Gold is not { } carried || carried < ShieldPrice)
            throw new ResourceUnavailableException("Carry at least 750 gold for a Leather Shield from Torrance.");
        if (s.Snapshot.Inventory.Count >= 59)
            throw new QuestBlockedException("Free an inventory slot for a Leather Shield.");
        await c.SaveAsync(c.Job with
        {
            Reason = "Buying a Leather Shield from Torrance"
        });
        await s.ExecuteAsync(new("visit", ShopMap, Text: Torrance), ct);
        if (s.Snapshot.Dialog is { } open)
            await s.ExecuteAsync(new("close", Dialog: open), ct);
        await s.ExecuteAsync(new("open", Text: Torrance), ct);
        var menu = s.Snapshot.Dialog;
        if (menu is null || menu.IsPursuit || !menu.Name.Equals(Torrance, StringComparison.OrdinalIgnoreCase) || !menu.Choices.Any(x => x.Text.Equals(Buy, StringComparison.OrdinalIgnoreCase)))
            throw new QuestBlockedException("Torrance's menu changed; it has no \"Buy\" option.");
        long before = s.Snapshot.Revision;
        await s.ExecuteAsync(new("choose", Text: Buy, Dialog: menu), ct);
        Dialog shop;
        try
        {
            shop = (await s.WaitAsync(x => x.Dialog is { IsPursuit: false, Type: 4 } d && d.EntityId == menu.EntityId && d.Revision > before, before, reply, ct)).Dialog!;
        }
        catch (TimeoutException) { throw new QuestBlockedException("Torrance didn't show his shop list."); }
        var offer = shop.Items.FirstOrDefault(i => i.Name.Equals(Shield, StringComparison.OrdinalIgnoreCase)) ?? throw new QuestBlockedException("Torrance no longer sells a Leather Shield.");
        uint gold = s.Snapshot.Gold ?? 0;
        if (gold < offer.Price)
            throw new ResourceUnavailableException("Carry at least " + offer.Price.ToString("N0") + " gold for a Leather Shield.");
        int had = s.Snapshot.Count(Shield);
        long bought = s.Snapshot.Revision;
        await s.ExecuteAsync(new("buy", Text: Shield, Dialog: shop), ct);
        try
        {
            await s.WaitAsync(x => x.Count(Shield) > had && x.Gold == gold - offer.Price, bought, proof, ct);
        }
        catch (TimeoutException) { throw new QuestBlockedException("The Leather Shield purchase wasn't confirmed. Check gold and the bag, then resume."); }
        if (s.Snapshot.Dialog is { } list)
            await s.ExecuteAsync(new("close", Dialog: list), ct);
        return offer.Price;
    }

    static int Carried(Transaction t) => t.InventoryCounts.FirstOrDefault(p => p.Key.Equals(Shield, StringComparison.OrdinalIgnoreCase)).Value;

    static async Task Prove(QuestContext c, Transaction t, uint paid, int rings, CancellationToken ct)
    {
        var s = c.Session;
        bool Taken(CharacterSnapshot n) => n.Generation == t.Generation && n.InventoryKnown && n.Count(Shield) == Carried(t) - 1;
        bool Touched(CharacterSnapshot n) => n.Generation == t.Generation && n.Notice is { Text: Touch } no && no.Revision > t.Revision;
        try
        {
            await s.WaitAsync(Touched, t.Revision, proof, ct);
            await s.WaitAsync(Taken, t.Revision, proof, ct);
        }
        catch (TimeoutException)
        {
            if (s.Snapshot.Generation == t.Generation && s.Snapshot.Count(Shield) == Carried(t))
            {
                await c.SaveAsync(c.Job with
                {
                    Pending = null
                });
                throw new QuestBlockedException("The altar didn't take the Leather Shield. Nothing was offered; resume to try again.");
            }
            throw new QuestBlockedException("The offering needs checking: the shield left the bag but the altar's reply didn't arrive. It will not be resent.");
        }
        string experience = "";
        try
        {
            experience = (await s.WaitAsync(n => n.Generation == t.Generation && n.Notice is { } no && no.Revision > t.Revision && SickChildEvidence.Experience(no.Text) is not null, t.Revision, TimeSpan.FromSeconds(1), ct)).Notice!.Text;
        }
        catch (TimeoutException) { }
        await Record(c, new(DateTimeOffset.UtcNow, t.Generation, paid, experience, rings));
    }

    static Task Record(QuestContext c, AltarOffering offering) => c.SaveAsync(c.Job with
    {
        Pending = null,
        AltarOfferings = [.. c.Job.AltarOfferings, offering],
        Reason = "Offering " + (Made(c.Job) + 1) + " of 10 made"
    });

    static async Task Reconcile(QuestContext c, CancellationToken ct)
    {
        var s = c.Session;
        var t = c.Job.Pending!;
        if (t.Kind != "altar-offer")
            throw new QuestBlockedException("Unexpected saved Altar Worship step requires reconciliation.");
        var snap = s.Snapshot;
        if (snap.Generation == t.Generation)
        {
            await Prove(c, t, 0, int.MaxValue, ct);
            return;
        }
        if (!snap.Connected || !snap.InventoryKnown)
            throw new QuestBlockedException("Waiting for a fresh inventory to check the last offering.");
        if (snap.Count(Shield) == Carried(t) - 1)
        {
            await Record(c, new(DateTimeOffset.UtcNow, t.Generation, 0, "Not observed (reconnected)", int.MaxValue));
            return;
        }
        if (snap.Count(Shield) == Carried(t))
        {
            await c.SaveAsync(c.Job with
            {
                Pending = null
            });
            return;
        }
        throw new QuestBlockedException("The last offering can't be confirmed from the bag. Check the Leather Shields, then resume.");
    }

    static async Task VerifyReward(QuestContext c, long? dropped, CancellationToken ct)
    {
        var s = c.Session;
        var last = c.Job.AltarOfferings[^1];
        bool priest = IsPriest(s.Snapshot.Class);
        // The tenth reply has never been seen; wait briefly for a ring before reading the legend.
        if (dropped is { } after && last.RingsBefore != int.MaxValue)
            try
            {
                await s.WaitAsync(n => RingCount(n) > last.RingsBefore, after, proof, ct);
            }
            catch (TimeoutException) { }
        await s.ExecuteAsync(new("inspect-self"), ct);
        string? reward = priest ? Mark(s.Snapshot.Legends) : last.RingsBefore != int.MaxValue && RingCount(s.Snapshot) > last.RingsBefore ? "Red Jade Ring and 50,000 experience received" : null;
        if (reward is null)
            throw new QuestBlockedException("10 offerings made, no reward seen. " + (priest ? "The \"" + Legend + "\" mark isn't in the legend." : "No new ring arrived.") + " Check the replay before running it again.");
        int experience = c.Job.AltarOfferings.Select(o => SickChildEvidence.Experience(o.Experience) is { } text && int.TryParse(text.Split(' ')[0], out var xp) ? xp : 0).Sum();
        var rewards = new Dictionary<string, int>(c.Job.Rewards);
        if (experience > 0)
            rewards["experience"] = experience;
        await c.SaveAsync(c.Job with
        {
            Step = "complete",
            Pending = null,
            CompletedAt = DateTimeOffset.UtcNow,
            CompletionLegend = priest ? reward : null,
            CompletionText = reward,
            Repeat = false,
            NextRun = null,
            Rewards = rewards,
            Reason = "Reward verified after the 10th offering"
        });
    }
}
