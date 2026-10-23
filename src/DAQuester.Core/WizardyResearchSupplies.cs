namespace DAQuester.Core;

// Independent of research submission: a late shop reply must never authorize another payment.
public sealed record ResearchPotionPurchase(long Generation, long Revision, int BeforeCount, uint BeforeGold,
    int Quantity, uint QuotedGold, Dialog Stock)
{
    public bool ReceiptVerified { get; init; }
    public Dialog? Cleanup { get; init; }
    public bool CloseSubmitted { get; init; }
    public bool ClosePresented { get; init; }
}

public sealed partial class WizardyResearchActions
{
    const string Exkuranum = "Exkuranum";
    const int PotionStock = 30;
    const int PotionShopMap = 3214;

    static void PotionCapacity(CharacterSnapshot snapshot)
    {
        if (!snapshot.InventoryKnown || snapshot.Weight is null || snapshot.MaxWeight is null ||
            snapshot.Weight >= snapshot.MaxWeight || snapshot.FreeInventorySlots == 0 && snapshot.Count(Exkuranum) == 0)
            throw new QuestBlockedException("Fresh bag space and carrying capacity required for Exkuranum supplies.");
    }

    static async Task PreparePotions(QuestContext c, CancellationToken ct)
    {
        Identity(c);
        await ReconcilePotionPurchase(c, ct);
        if (!c.Session.Snapshot.InventoryKnown)
            throw new QuestBlockedException("Fresh inventory required before checking Exkuranum supplies.");
        if (c.Session.Snapshot.Count(Exkuranum) >= PotionStock) return;
        if (c.Job.Pending is { Kind: "research-supply-close" }) await Reconcile(c, ct);
        if (c.Job.Pending is not null || Progress(c).Stage != 0)
            throw new QuestBlockedException("Reconcile the current research submission before replenishing Exkuranum.");
        PotionCapacity(c.Session.Snapshot);
        if (c.Session.Snapshot.Gold is null)
            throw new QuestBlockedException("Fresh gold required before replenishing Exkuranum.");

        var before = c.Session.Snapshot;
        long generation = before.Generation;
        if (before.Dialog is { } opening)
        {
            bool researchOpening = WizardyResearchEvidence.ResearchPage(before, opening) &&
                ResearchChoice(opening, WizardyResearchEvidence.Begin) is not null;
            bool darShop = before.Map == WizardyResearchEvidence.Map && !opening.IsPursuit && opening.Type is 0 or 4 &&
                opening.EntityType == 1 && opening.Generation == generation && opening.Revision > 0 && opening.Revision <= before.Revision &&
                opening.Name.Equals("Dar", StringComparison.OrdinalIgnoreCase) &&
                NpcVisibility.Find(before, WizardyResearchEvidence.Map, "Dar")?.Id == opening.EntityId;
            if (!researchOpening && !darShop)
                throw new QuestBlockedException("Resolve the current popup before replenishing Exkuranum; it was preserved.");
            await Save(c, Progress(c) with { Source = opening }, "Closing research opening for Exkuranum supplies");
            if (opening.IsPursuit) await c.BeginAsync("research-supply-close", opening.EntityId, observedDialog: opening);
            await c.Session.ExecuteAsync(new("close", Dialog: opening, ExpectedGeneration: generation,
                ExpectedDialogRevision: opening.Revision, ExpectedPageText: opening.Text), ct);
            if (opening.IsPursuit) await Reconcile(c, ct);
        }
        if (c.Session.Snapshot.Map == WizardyResearchEvidence.Map && c.Session.Snapshot.Position is { } position &&
            WizardyResearchEvidence.Triggers.Contains(position))
        {
            await c.Session.ExecuteAsync(new("research-depart", ExpectedGeneration: generation), ct);
            if (c.Session.Snapshot.Generation != generation || c.Session.Snapshot.Map != WizardyResearchEvidence.Map ||
                c.Session.Snapshot.Position is null || WizardyResearchEvidence.Triggers.Contains(c.Session.Snapshot.Position))
                throw new QuestBlockedException("Research doorway departure needs reconciliation before supply travel.");
        }

        await c.Session.ExecuteAsync(new("navigate", PotionShopMap, new(9, 7), Tolerance: 2, ExpectedGeneration: generation), ct);
        Identity(c);
        var arrival = c.Session.Snapshot;
        if (arrival.Generation != generation || arrival.Map != PotionShopMap || arrival.Position is not { } at ||
            Math.Abs(at.X - 9) + Math.Abs(at.Y - 7) > 2 || arrival.Dialog is not null)
            throw new QuestBlockedException("Blackstar Research arrival changed; inspect its actual position and popup.");
        var merchants = arrival.Entities.Values.Where(entity => !entity.IsPlayer && entity.Kind == 2 &&
            !string.IsNullOrWhiteSpace(entity.Name) && Math.Abs(entity.Position.X - at.X) + Math.Abs(entity.Position.Y - at.Y) <= NpcVisibility.ScreenRange).Take(2).ToArray();
        if (merchants.Length != 1 || NpcVisibility.Find(arrival, PotionShopMap, merchants[0].Name)?.Id != merchants[0].Id)
            throw new QuestBlockedException("One fresh named merchant must be visible in Blackstar Research; no seller was guessed.");
        var seller = merchants[0];
        await c.Session.ExecuteAsync(new("open", Text: seller.Name, ExpectedGeneration: generation), ct);
        var menu = c.Session.Snapshot.Dialog;
        if (c.Session.Snapshot.Generation != generation || c.Session.Snapshot.Map != PotionShopMap ||
            menu is not { IsPursuit: false, EntityType: 1 } || menu.Generation != generation || menu.EntityId != seller.Id ||
            !menu.Name.Equals(seller.Name, StringComparison.OrdinalIgnoreCase) || menu.Revision <= arrival.Revision ||
            menu.Choices.Count(choice => choice.Text == "Buy") != 1)
            throw new QuestBlockedException("The observed Blackstar Research merchant must offer one Buy option.");
        await c.Session.ExecuteAsync(new("choose", Text: "Buy", Dialog: menu, ExpectedGeneration: generation,
            ExpectedDialogRevision: menu.Revision, ExpectedPageText: menu.Text), ct);
        bool FreshStock(CharacterSnapshot snapshot) => snapshot.Generation != generation || snapshot.Map != PotionShopMap ||
            snapshot.Dialog is { } reply && reply.Revision > menu.Revision;
        if (!FreshStock(c.Session.Snapshot)) await c.Session.WaitAsync(FreshStock, menu.Revision, Timeout, ct);
        Identity(c);
        var snapshot = c.Session.Snapshot;
        var stock = snapshot.Dialog;
        if (snapshot.Generation != generation || snapshot.Map != PotionShopMap ||
            stock is not { IsPursuit: false, Type: 4 } || stock.Generation != generation || stock.EntityId != seller.Id ||
            stock.EntityType != menu.EntityType || !stock.Name.Equals(seller.Name, StringComparison.OrdinalIgnoreCase) ||
            stock.Revision <= menu.Revision || stock.Revision > snapshot.Revision)
            throw new QuestBlockedException("Fresh stock from the observed Blackstar Research merchant is required; no purchase sent.");
        var offers = stock.Items.Where(item => item.Name.Equals(Exkuranum, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (offers.Length != 1)
            throw new QuestBlockedException("Blackstar Research must offer exactly one Exkuranum quote; no purchase sent.");
        if (!snapshot.InventoryKnown)
            throw new QuestBlockedException("Fresh inventory required before confirming Exkuranum supplies.");
        int missing = PotionStock - snapshot.Count(Exkuranum);
        if (missing <= 0)
        {
            var complete = new ResearchPotionPurchase(generation, snapshot.Revision, snapshot.Count(Exkuranum), snapshot.Gold ?? 0,
                0, 0, stock) { ReceiptVerified = true, Cleanup = stock };
            await Save(c, Progress(c) with { PotionPurchase = complete }, "Exkuranum stock already complete; closing shop without payment");
            await ReconcilePotionPurchase(c, ct);
            return;
        }
        PotionCapacity(snapshot);
        ulong total = (ulong)offers[0].Price * (uint)missing;
        if (total > uint.MaxValue || snapshot.Gold is not { } gold || gold < total)
            throw new ResourceUnavailableException("Carry enough gold for " + missing + " Exkuranum at the current Blackstar Research quote.");
        var purchase = new ResearchPotionPurchase(generation, snapshot.Revision, snapshot.Count(Exkuranum), gold,
            missing, (uint)total, stock);
        await Save(c, Progress(c) with { PotionPurchase = purchase }, "Buying " + missing + " Exkuranum in Blackstar Research");
        await c.Session.ExecuteAsync(new("buy", Text: Exkuranum, Quantity: missing, Dialog: stock,
            ExpectedGeneration: generation, ExpectedDialogRevision: stock.Revision, ExpectedPageText: stock.Text), ct);
        await ReconcilePotionPurchase(c, ct);
    }

    static async Task ReconcilePotionPurchase(QuestContext c, CancellationToken ct)
    {
        var purchase = Progress(c).PotionPurchase;
        if (purchase is null) return;
        Identity(c);
        if (!purchase.ReceiptVerified)
        {
            if (c.Session.Snapshot.Generation != purchase.Generation)
                throw new QuestBlockedException("Exkuranum purchase belongs to an earlier connection; new balances cannot prove it. Nothing resent.");
            if (purchase.BeforeCount < 0 || purchase.Quantity < 1 || purchase.BeforeCount + purchase.Quantity != PotionStock ||
                purchase.BeforeGold < purchase.QuotedGold)
                throw new QuestBlockedException("Exkuranum purchase baseline is incomplete; no payment repeated.");
            bool Receipt(CharacterSnapshot snapshot) => snapshot.Generation == purchase.Generation && snapshot.Revision > purchase.Revision &&
                snapshot.Map == PotionShopMap && snapshot.InventoryKnown && snapshot.Count(Exkuranum) == purchase.BeforeCount + purchase.Quantity &&
                snapshot.Gold == purchase.BeforeGold - purchase.QuotedGold;
            if (!Receipt(c.Session.Snapshot)) await c.Session.WaitAsync(Receipt, purchase.Revision, Timeout, ct);
            if (!Receipt(c.Session.Snapshot)) throw new QuestBlockedException("Exkuranum purchase remains uncertain; no payment repeated.");
            var reply = c.Session.Snapshot.Dialog;
            purchase = purchase with { ReceiptVerified = true, Cleanup = reply is { IsPursuit: false } &&
                reply.Generation == purchase.Generation && reply.EntityId == purchase.Stock.EntityId &&
                reply.EntityType == purchase.Stock.EntityType && reply.Name.Equals(purchase.Stock.Name, StringComparison.OrdinalIgnoreCase)
                ? reply : purchase.Stock };
            await Save(c, Progress(c) with { PotionPurchase = purchase }, "Exkuranum count and quoted payment verified; closing shop");
        }
        var current = c.Session.Snapshot.Dialog;
        if (current is not null)
        {
            var cleanup = purchase.Cleanup;
            if (cleanup is null || current.Generation != cleanup.Generation || current.EntityId != cleanup.EntityId ||
                current.EntityType != cleanup.EntityType || current.Revision != cleanup.Revision || current.IsPursuit)
                throw new QuestBlockedException("Exkuranum purchase is verified; reconcile the current popup separately.");
            if (!purchase.CloseSubmitted)
            {
                purchase = purchase with { CloseSubmitted = true };
                await Save(c, Progress(c) with { PotionPurchase = purchase }, "Closing the verified Exkuranum shop");
                await c.Session.ExecuteAsync(new("close", Dialog: current, ExpectedGeneration: current.Generation,
                    ExpectedDialogRevision: current.Revision, ExpectedPageText: current.Text), ct);
                // Merchant dismissal is acknowledged by the local presentation write;
                // its authoritative server page deliberately remains in the snapshot.
                purchase = purchase with { ClosePresented = true };
                await Save(c, Progress(c) with { PotionPurchase = purchase }, "Verified Exkuranum shop dismissed locally");
            }
            if (!purchase.ClosePresented && c.Session.Snapshot.Dialog is not null)
                await c.Session.WaitAsync(snapshot => snapshot.Dialog is null, current.Revision, Timeout, ct);
        }
        await Save(c, Progress(c) with { PotionPurchase = null, PotionShopDeparture = purchase.Cleanup },
            "Full Exkuranum stock verified; returning to research");
    }

    static bool DismissedPotionShop(QuestContext c)
    {
        var snapshot = c.Session.Snapshot;
        return snapshot.Map == PotionShopMap && snapshot.Dialog is { IsPursuit: false } current &&
            Progress(c).PotionShopDeparture is { IsPursuit: false } dismissed &&
            current.Generation == snapshot.Generation && current.Generation == dismissed.Generation &&
            current.EntityId == dismissed.EntityId && current.EntityType == dismissed.EntityType &&
            current.Revision == dismissed.Revision && current.Name == dismissed.Name && current.Text == dismissed.Text;
    }

    static Task ReconcilePotionDeparture(QuestContext c) => Progress(c).PotionShopDeparture is not null &&
        c.Session.Snapshot.Map == WizardyResearchEvidence.Map && c.Session.Snapshot.Position is not null
        ? Save(c, Progress(c) with { PotionShopDeparture = null }, "Return to research verified")
        : Task.CompletedTask;
}
