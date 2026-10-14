namespace DAQuester.Core;

static partial class ObservedJourney
{
    static async Task BuyAsync(QuestContext c, ObservedQuestTask task, CancellationToken ct)
    {
        // Reconcile a prior submission first. Reopening stock after an uncertain
        // purchase could spend twice even if the earlier call timed out.
        var session = c.Session;
        string name = task.Item ?? throw new QuestBlockedException("Purchase item is missing.");
        if (c.Job.Pending is not null)
        {
            await ReconcilePurchaseAsync(c, task, ct);
            return;
        }
        if (!session.Snapshot.InventoryKnown)
            throw new QuestBlockedException("Fresh inventory required before purchasing.");
        // Preserve carried supplies and buy only the shortfall at today's observed
        // stock price, rather than the historical research price.
        int missing = task.Quantity - session.Snapshot.Count(name);
        if (missing <= 0)
            return;
        if (session.Snapshot.Inventory.Count >= 59 || session.Snapshot.Weight + task.WeightReserve > session.Snapshot.MaxWeight)
            throw new QuestBlockedException("Reserve carrying capacity for " + missing + " " + name + "; nothing is discarded automatically.");
        var before = session.Snapshot;
        if (before.Dialog is { IsPursuit: true })
            throw new QuestBlockedException("Close or reconcile the current quest popup before opening a shop.");
        await PrepareTravelAsync(c, task, ct);
        before = session.Snapshot;
        await session.ExecuteAsync(new("interact", task.Map, task.Approach, task.Npc, ExpectedGeneration: before.Generation), ct);
        var menu = session.Snapshot.Dialog;
        if (menu is not { IsPursuit: false } || menu.Name != task.Npc || !menu.Choices.Any(x => x.Text == "Buy"))
            throw new QuestBlockedException(task.Npc + "'s actual menu has no Buy option.");
        await session.ExecuteAsync(new("choose", Text: "Buy", Dialog: menu, ExpectedGeneration: menu.Generation, ExpectedDialogRevision: menu.Revision, ExpectedPageText: menu.Text), ct);
        if (session.Snapshot.Dialog is { } current && current.Revision <= menu.Revision)
            await session.WaitAsync(s => s.Generation != menu.Generation || s.Map != task.Map || s.Dialog is null || s.Dialog.Revision > menu.Revision,
                menu.Revision, ProofTimeout, ct);
        var shop = session.Snapshot.Dialog;
        if (shop is not { IsPursuit: false, Type: 4 } || shop.Name != task.Npc || shop.EntityId != menu.EntityId || shop.Generation != menu.Generation || shop.Revision <= menu.Revision)
            throw new QuestBlockedException("Fresh stock from " + task.Npc + " is required before purchase.");
        var offer = shop.Items.SingleOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new QuestBlockedException(task.Npc + " does not currently offer " + name + ".");
        uint cost = checked(offer.Price * (uint)missing);
        if (session.Snapshot.Gold < cost)
            throw new ResourceUnavailableException("Carry " + cost.ToString("N0") + " gold for " + missing + " " + name + " at the actual quoted price.");
        // Persist quantity and quote before sending. Both inventory gain and the
        // exact quoted debit must arrive before this purchase is considered settled.
        var route = c.Job.Route! with
        {
            Baseline = Baseline(session.Snapshot),
            Resource = name,
            Quantity = missing,
            QuotedGold = cost
        };
        await c.SaveAsync(c.Job with
        {
            Route = route,
            Reason = "Buying " + missing + " " + name + " from " + task.Npc
        });
        await c.BeginAsync("observed-buy:" + task.Id, shop.EntityId, observedDialog: shop);
        await session.ExecuteAsync(new("buy", Text: name, Dialog: shop, Quantity: missing, ExpectedGeneration: shop.Generation, ExpectedDialogRevision: shop.Revision), ct);
        await ReconcilePurchaseAsync(c, task, ct);
        if (session.Snapshot.Dialog is { IsPursuit: false } stock)
            await session.ExecuteAsync(new("close", Dialog: stock, ExpectedGeneration: stock.Generation, ExpectedDialogRevision: stock.Revision), ct);
    }

    static async Task ReconcilePurchaseAsync(QuestContext c, ObservedQuestTask task, CancellationToken ct)
    {
        var pending = c.Job.Pending ?? throw new QuestBlockedException("Purchase baseline missing.");
        var route = c.Job.Route!;
        string resource = route.Resource ?? throw new QuestBlockedException("Purchase item baseline is missing.");
        if (pending.Kind != "observed-buy:" + task.Id || route.Resource != task.Item || route.Quantity < 1 || !pending.InventoryBaselineKnown)
            throw new QuestBlockedException("A different item submission is pending; inspect it first.");
        if (c.Session.Snapshot.Generation != pending.Generation)
            throw new QuestBlockedException("Purchase belongs to an earlier connection. New-session balances cannot prove its old outcome; no second purchase sent.");
        bool Receipt(CharacterSnapshot s) => s.Generation == pending.Generation && s.InventoryKnown &&
            s.Count(resource) == pending.InventoryCounts.GetValueOrDefault(resource) + route.Quantity &&
            pending.Gold >= route.QuotedGold && s.Gold == pending.Gold - route.QuotedGold;
        if (!Receipt(c.Session.Snapshot))
            await c.Session.WaitAsync(Receipt, pending.Revision, ProofTimeout, ct);
        await c.SaveAsync(c.Job with
        {
            Pending = null,
            Reason = "Purchased " + route.Quantity + " " + route.Resource + "; inventory and quoted gold verified"
        });
    }
}
