namespace DAQuester.Core;

// The opening is captured; later dialogue and cooldown remain player-reported.
// Unknown outcomes remain pending.
public sealed class WizardyResearchActions : IQuestAction
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    static WizardyResearchProgress Progress(QuestContext c) => c.Job.Research ?? new();
    static Task Save(QuestContext c, WizardyResearchProgress p, string reason) => c.SaveAsync(c.Job with { Research = p, Reason = reason });
    static void Identity(QuestContext c)
    {
        var s = c.Session.Snapshot;
        if (!s.Connected || !string.Equals(s.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Research connection changed; resume with this character's fresh state.");
    }

    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        Identity(c);
        if (c.Job.Research is null) await Save(c, new(), "Preparing wizardy research");
        if (c.Job.Pending is not null) await Reconcile(c, ct);
        await Cleanup(c, ct);
        if (Progress(c).RestAt is { } rest)
        {
            var due = c.Job.NextRun ?? rest.AddHours(12);
            if (due > DateTimeOffset.UtcNow) throw new QuestScheduledWaitException(due);
            await Save(c, Progress(c) with { RestAt = null, Cooldown = null, Source = null, Stage = 0 }, "Checking research after the rest");
            await c.SaveAsync(c.Job with { NextRun = null });
        }
        if (await Rest(c, ct)) return;
        var preflight = WizardyResearchEvidence.Eligibility(c.Session.Snapshot);
        if (preflight.Kind == EligibilityKind.Blocked) throw new QuestBlockedException(preflight.Reason);
        if (c.Session.Snapshot.Class is null) await c.Session.ExecuteAsync(new("inspect-self"), ct);
        await Health(c, ct);
        var eligible = WizardyResearchEvidence.Eligibility(c.Session.Snapshot);
        if (eligible.Kind != EligibilityKind.Eligible) throw new QuestBlockedException(eligible.Reason);
        var options = c.Job.ResearchOptions ?? new();
        options.Validate(c.Job.Character);
        IQuestHelper? helper = null;
        long? helperGeneration = null;
        bool AssistantReady() => helper is null || !helper.Released.IsCancellationRequested && helper.Session.Snapshot.Generation == helperGeneration &&
            string.Equals(helper.Session.Snapshot.Name, options.Assistant, StringComparison.OrdinalIgnoreCase) && WizardyResearchEvidence.AssistantEligible(helper.Session.Snapshot) &&
            helper.Session.Snapshot.Map == WizardyResearchEvidence.Map && helper.Session.Snapshot.Position is { } hp && c.Session.Snapshot.Position is { } qp &&
            c.Session.Snapshot.Map == WizardyResearchEvidence.Map && Math.Abs(hp.X - qp.X) + Math.Abs(hp.Y - qp.Y) <= NpcVisibility.ScreenRange;
        try
        {
            if (options.Assistant is { } name)
            {
                helper = await (c.Helpers ?? throw new QuestBlockedException("Assistant control is unavailable.")).AcquireAsync(name, ct);
                helperGeneration = helper.Session.Snapshot.Generation;
                if (!helper.Session.Snapshot.LegendsKnown || helper.Session.Snapshot.Class is null)
                    await helper.Session.ExecuteAsync(new("inspect-self"), ct);
                if (!WizardyResearchEvidence.AssistantEligible(helper.Session.Snapshot))
                    throw new QuestBlockedException(name + " must be a registered, living wizard to assist.");
                await helper.Session.ExecuteAsync(new("navigate", WizardyResearchEvidence.Map, new(7, 8), ExpectedGeneration: helperGeneration, Tolerance: 1), ct);
                if (helper.Session.Snapshot.Map != WizardyResearchEvidence.Map || helper.Session.Snapshot.Position is not { } arrived ||
                    Math.Abs(arrived.X - 7) + Math.Abs(arrived.Y - 8) > 1 || WizardyResearchEvidence.Triggers.Contains(arrived))
                    throw new QuestBlockedException("Assistant arrival needs reconciliation before research.");
            }
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                Identity(c);
                if (helper is not null && (helper.Released.IsCancellationRequested || !WizardyResearchEvidence.AssistantEligible(helper.Session.Snapshot) ||
                    helper.Session.Snapshot.Map != WizardyResearchEvidence.Map))
                    throw new QuestBlockedException("Assistant is no longer ready nearby; research paused.");
                await Cleanup(c, ct);
                if (await Rest(c, ct)) return;
                await Health(c, ct);
                if (Progress(c).Stage == 0 && (c.Session.Snapshot.Dialog is null || c.Session.Snapshot.Dialog is { IsPursuit: false, Name: "Dar" }))
                {
                    await c.Session.ExecuteAsync(new("navigate", WizardyResearchEvidence.Map, new(7, 9), ExpectedGeneration: c.Session.Snapshot.Generation), ct);
                    if (c.Session.Snapshot.Map != WizardyResearchEvidence.Map || c.Session.Snapshot.Position != new Tile(7, 9))
                        throw new QuestBlockedException("Research room arrival needs reconciliation.");
                    await BankSongs(c, ct);
                    await Parchment(c, ct);
                    if (c.Session.Snapshot.Dialog is not { } arrivedPage || !WizardyResearchEvidence.ResearchPage(c.Session.Snapshot, arrivedPage))
                        await c.Session.ExecuteAsync(new("research-trigger", ExpectedGeneration: c.Session.Snapshot.Generation), ct);
                }
                await Menu(c, options, AssistantReady, ct);
                await Reconcile(c, ct);
            }
        }
        finally
        {
            if (helper is not null)
                try { if (helper.LoggedInForQuest) await helper.LogOutAsync(CancellationToken.None); }
                finally { await helper.DisposeAsync(); }
        }
    }

    static async Task Health(QuestContext c, CancellationToken ct)
    {
        if (c.Session.Snapshot.Hp == 0)
            await c.Session.WaitAsync(s => s.Hp > 0, c.Session.Snapshot.Revision, Timeout, ct);
        if (c.Session.Snapshot.Hp is null) throw new QuestBlockedException("Fresh health required before continuing research.");
    }
    static bool FreshReply(CharacterSnapshot s, WizardyResearchProgress p, Transaction t, out Dialog d)
    {
        d = s.Dialog!;
        return d is not null && s.Generation == t.Generation && d.Generation == t.Generation && d.EntityId == t.EntityId &&
            d.EntityType == t.EntityType && p.Source is { } source && d.Revision > source.Revision;
    }
    static DialogChoice? ResearchChoice(Dialog d, string label)
    {
        static string Normalize(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        string expected = Normalize(label);
        bool Matches(DialogChoice choice)
        {
            string offered = Normalize(choice.Text);
            return offered.Equals(expected, StringComparison.OrdinalIgnoreCase) || label == WizardyResearchEvidence.Begin &&
                offered.Equals(WizardyResearchEvidence.ObservedBegin, StringComparison.OrdinalIgnoreCase);
        }
        var matches = d.Choices.Where(Matches).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    static async Task Select(QuestContext c, Dialog d, string label, int next, bool submit, CancellationToken ct)
    {
        var choice = ResearchChoice(d, label) ?? throw new QuestBlockedException("Research choice changed; current popup was preserved.");
        await Save(c, Progress(c) with { Source = d, Stage = next }, "Research · " + label);
        await c.BeginAsync(submit ? "research-submit" : "research-menu", d.EntityId, observedDialog: d);
        await c.Session.ExecuteAsync(new("choose", Text: choice.Text, Dialog: d, ExpectedGeneration: d.Generation,
            ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
        if (!submit) await Reconcile(c, ct);
    }
    static async Task Menu(QuestContext c, WizardyResearchOptions options, Func<bool> assistantReady, CancellationToken ct)
    {
        for (int bounded = 0; bounded < 12; bounded++)
        {
            if (await Rest(c, ct)) return;
            var s = c.Session.Snapshot;
            var d = s.Dialog;
            if (d is null || !WizardyResearchEvidence.ResearchPage(s, d)) throw new QuestBlockedException("Fresh wizardy research popup required; unrelated popup preserved.");
            int stage = Progress(c).Stage;
            // The player authorized this named choice. Paragraph presentation is
            // not its identity; the submitted command still binds the exact live page.
            if (stage == 0 && d.Text != WizardyResearchEvidence.Opening && ResearchChoice(d, WizardyResearchEvidence.Begin) is null)
            {
                await Save(c, Progress(c) with { Source = d }, "Unrecognized research opening saved for review");
                throw new QuestBlockedException("Unknown research opening; popup preserved and saved for review.");
            }
            if (stage == 0 && ResearchChoice(d, WizardyResearchEvidence.Begin) is not null &&
                (s.Count(WizardyResearchEvidence.Parchment) == 0 || Progress(c).BankingItem is not null || WizardyResearchEvidence.DepositCandidate(s) is not null))
            {
                await Save(c, Progress(c) with { Source = d }, "Closing research opening to prepare supplies");
                await c.BeginAsync("research-supply-close", d.EntityId, observedDialog: d);
                await c.Session.ExecuteAsync(new("close", Dialog: d, ExpectedGeneration: d.Generation,
                    ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
                await Reconcile(c, ct);
                return;
            }
            if (stage == 4)
            {
                if (!assistantReady()) throw new QuestBlockedException("Assistant left or changed connection before name submission; research paused.");
                if (options.Assistant is null || d.Choices.Length != 0 || d.HasNext || d.Prompt is null && d.Type != 2)
                    throw new QuestBlockedException("Fresh assistant name prompt required; unknown choices were preserved.");
                if (s.Count(WizardyResearchEvidence.Parchment) < 1) throw new QuestBlockedException("Parchment is missing; no assistant name submitted.");
                await Save(c, Progress(c) with { Source = d, Stage = 5 }, "Submitting assistant name");
                await c.BeginAsync("research-submit", d.EntityId, observedDialog: d);
                await c.Session.ExecuteAsync(new("input", Text: options.Assistant, Dialog: d, ExpectedGeneration: d.Generation,
                    ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
                return;
            }
            string label = stage switch { 0 => WizardyResearchEvidence.Begin, 1 => "Specific", 2 => "sorcery", 3 => options.Assistant is null ? "Alone" : "Assistant", _ => throw new QuestBlockedException("Research checkpoint requires reconciliation.") };
            await Health(c, ct);
            if (!assistantReady()) throw new QuestBlockedException("Assistant is no longer ready nearby; research paused.");
            // A Next page is safe only when it is the sole offered action in this bound sequence.
            if (d.HasNext && d.Choices.Length == 0 && d.Prompt is null)
            {
                await Save(c, Progress(c) with { Source = d }, "Reading research Next page");
                await c.BeginAsync("research-next", d.EntityId, observedDialog: d);
                await c.Session.ExecuteAsync(new("next", Dialog: d, ExpectedGeneration: d.Generation, ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
                await Reconcile(c, ct);
                continue;
            }
            bool submit = stage == 3 && options.Assistant is null;
            if (submit && c.Session.Snapshot.Count(WizardyResearchEvidence.Parchment) < 1)
                throw new QuestBlockedException("Parchment is missing; no research submitted.");
            await Select(c, d, label, stage + 1, submit, ct);
            if (submit) return;
        }
        throw new QuestBlockedException("Research page limit reached; resume to inspect current popup.");
    }

    static async Task<bool> Rest(QuestContext c, CancellationToken ct)
    {
        var d = c.Session.Snapshot.Dialog;
        if (d is null || !WizardyResearchEvidence.ResearchPage(c.Session.Snapshot, d) || d.Text != WizardyResearchEvidence.Cooldown) return false;
        if (c.Job.Pending is not null) throw new QuestBlockedException("Submitted research requires its resource receipt before a cooldown can replace it.");
        var p = Progress(c);
        var at = p.Cooldown is { } old && old.Generation == d.Generation && old.Revision == d.Revision ? p.RestAt!.Value : d.ObservedAt ?? DateTimeOffset.UtcNow;
        var due = at.AddHours(12);
        await c.SaveAsync(c.Job with { Research = p with { RestAt = at, Cooldown = d, Cleanup = d, Stage = 0 }, NextRun = due,
            Reason = "Research rest verified; next check " + due.ToLocalTime().ToString("g") });
        await Cleanup(c, ct);
        throw new QuestScheduledWaitException(due);
    }

    static async Task Parchment(QuestContext c, CancellationToken ct)
    {
        if (c.Session.Snapshot.Count(WizardyResearchEvidence.Parchment) > 0) return;
        if (c.Session.Snapshot.Gold is not { } gold || gold < 500)
            throw new ResourceUnavailableException("Carry 500 gold to buy the next Parchment Scroll from Dar.");
        if (!c.Session.Snapshot.InventoryKnown || c.Session.Snapshot.FreeInventorySlots == 0 || c.Session.Snapshot.Weight is null || c.Session.Snapshot.MaxWeight is null ||
            c.Session.Snapshot.Weight >= c.Session.Snapshot.MaxWeight) throw new QuestBlockedException("Fresh carrying capacity required for one parchment.");
        if (c.Session.Snapshot.Dialog is { IsPursuit: true } || c.Session.Snapshot.Dialog is { Name: not "Dar" }) throw new QuestBlockedException("Resolve the current popup before buying parchment.");
        await c.Session.ExecuteAsync(new("open", Text: "Dar", ExpectedGeneration: c.Session.Snapshot.Generation), ct);
        var menu = c.Session.Snapshot.Dialog;
        if (menu is not { IsPursuit: false, Name: "Dar" }) throw new QuestBlockedException("Dar's fresh shop menu required.");
        await c.Session.ExecuteAsync(new("choose", Text: "Buy", Dialog: menu, ExpectedGeneration: menu.Generation, ExpectedDialogRevision: menu.Revision, ExpectedPageText: menu.Text), ct);
        // Selection only sends the request. Await the server's stock before
        // checking its quote, including when the old menu has already closed.
        bool StockReply(CharacterSnapshot s) => s.Generation != menu.Generation || s.Map != WizardyResearchEvidence.Map ||
            s.Dialog is { } reply && reply.Revision > menu.Revision;
        if (!StockReply(c.Session.Snapshot)) await c.Session.WaitAsync(StockReply, menu.Revision, Timeout, ct);
        var stock = c.Session.Snapshot.Dialog;
        if (c.Session.Snapshot.Generation != menu.Generation || c.Session.Snapshot.Map != WizardyResearchEvidence.Map ||
            stock is not { Name: "Dar", IsPursuit: false, Type: 4 } || stock.EntityId != menu.EntityId || stock.Generation != menu.Generation || stock.Revision <= menu.Revision)
            throw new QuestBlockedException("Dar's fresh stock is required before buying Parchment Scroll; no purchase sent.");
        if (stock.Items.Count(i => i.Name == WizardyResearchEvidence.Parchment && i.Price == 500) != 1)
            throw new QuestBlockedException("Dar's parchment quote differs from 500 gold; no purchase sent.");
        if (c.Session.Snapshot.Gold is not >= 500) throw new ResourceUnavailableException("Carry 500 gold for the next parchment.");
        await Save(c, Progress(c) with { Source = stock }, "Buying one parchment from Dar");
        await c.BeginAsync("research-buy", stock.EntityId, observedDialog: stock);
        await c.Session.ExecuteAsync(new("buy", Text: WizardyResearchEvidence.Parchment, Quantity: 1, Dialog: stock,
            ExpectedGeneration: stock.Generation, ExpectedDialogRevision: stock.Revision, ExpectedPageText: stock.Text), ct);
        await Reconcile(c, ct);
        if (c.Session.Snapshot.Dialog is { IsPursuit: false } shop) await c.Session.ExecuteAsync(new("close", Dialog: shop, ExpectedGeneration: shop.Generation, ExpectedDialogRevision: shop.Revision), ct);
        // Merchant dismissal is local; a subsequent trigger still needs a fresh pursuit.
    }

    static async Task BankSongs(QuestContext c, CancellationToken ct)
    {
        while (true)
        {
            var s = c.Session.Snapshot;
            string? name = Progress(c).BankingItem ?? WizardyResearchEvidence.DepositCandidate(s)?.Name;
            if (name is null) return;
            if (Progress(c).BankingItem is null) await Save(c, Progress(c) with { BankingItem = name }, "Banking all carried " + name);
            var item = s.Inventory.Values.OrderBy(i => i.Slot).FirstOrDefault(i => i.Name == name && i.Quantity > 0);
            if (item is null)
            {
                await Save(c, Progress(c) with { BankingItem = null }, "Song stacks banked");
                continue;
            }
            var dar = NpcVisibility.Find(s, WizardyResearchEvidence.Map, "Dar") ?? throw new QuestBlockedException("Dar must be visible before depositing Songs.");
            await Save(c, Progress(c) with { DepositItem = item.Name, DepositSlot = item.Slot, DepositQuantity = checked((int)item.Quantity), Source = null }, "Banking " + item.Name + " through Dar");
            await c.BeginAsync("research-deposit", dar.Id);
            await c.Session.ExecuteAsync(new("deposit-song", Slot: item.Slot, Amount: item.Quantity, EntityId: dar.Id,
                ExpectedResource: item.Name, ExpectedGeneration: s.Generation), ct);
            await Reconcile(c, ct);
            await Cleanup(c, ct);
        }
    }

    static async Task Reconcile(QuestContext c, CancellationToken ct)
    {
        var t = c.Job.Pending;
        if (t is null) return;
        Identity(c);
        if (c.Session.Snapshot.Generation != t.Generation) throw new QuestBlockedException("Research submission belongs to an earlier connection; inspect its receipt. Nothing resent.");
        if (t.Kind == "research-submit") await ReadResultPages(c, t, ct);
        var p = Progress(c);
        bool Refusal(CharacterSnapshot s) => t.Kind == "research-submit" && FreshReply(s, p, t, out var refused) && refused.Text == WizardyResearchEvidence.Cooldown &&
            s.InventoryKnown && s.Count(WizardyResearchEvidence.Parchment) == t.InventoryCounts.GetValueOrDefault(WizardyResearchEvidence.Parchment) &&
            s.Gold == t.Gold && WizardyResearchEvidence.NewSongs(s, t).Count == 0;
        bool Receipt(CharacterSnapshot s)
        {
            if (s.Generation != t.Generation || !s.InventoryKnown) return false;
            if (Refusal(s)) return true;
            if (t.Kind == "research-supply-close") return s.Dialog is null;
            if (t.Kind == "research-buy") return s.Count(WizardyResearchEvidence.Parchment) == t.InventoryCounts.GetValueOrDefault(WizardyResearchEvidence.Parchment) + 1 && t.Gold >= 500 && s.Gold == t.Gold - 500;
            if (t.Kind == "research-deposit") return p.DepositItem is { } name && s.Count(name) == t.InventoryCounts.GetValueOrDefault(name) - p.DepositQuantity &&
                s.Dialog is { Text: "It will be done." } bank && bank.Generation == t.Generation && bank.EntityId == t.EntityId && bank.Revision > t.Revision && bank.Name == "Dar";
            if (WizardyResearchEvidence.StatusResult(c.Job, s) is not null) return true;
            if (!FreshReply(s, p, t, out var d)) return false;
            if (t.Kind is "research-menu" or "research-next") return WizardyResearchEvidence.ResearchPage(s, d);
            return t.Kind == "research-submit" && WizardyResearchEvidence.ResearchPage(s, d) &&
                s.Count(WizardyResearchEvidence.Parchment) == t.InventoryCounts.GetValueOrDefault(WizardyResearchEvidence.Parchment) - 1 &&
                WizardyResearchEvidence.NewSongs(s, t).Count > 0;
        }
        if (!Receipt(c.Session.Snapshot)) await c.Session.WaitAsync(Receipt, t.Revision, Timeout, ct);
        var after = c.Session.Snapshot;
        if (!Receipt(after)) throw new QuestBlockedException("Research receipt remains uncertain; no action resent.");
        if (Refusal(after))
        {
            await c.SaveAsync(c.Job with { Pending = null, Reason = "Research refused without consuming parchment" });
            return;
        }
        if (t.Kind == "research-submit")
        {
            var receipt = WizardyResearchEvidence.StatusResult(c.Job, after) ??
                new(DateTimeOffset.UtcNow, after.Dialog, WizardyResearchEvidence.NewSongs(after, t));
            await c.SaveAsync(WizardyResearchEvidence.RecordAttempt(c.Job, receipt));
        }
        else if (t.Kind == "research-deposit")
            await c.SaveAsync(c.Job with { Pending = null, Research = p with { Cleanup = after.Dialog, DepositCleanup = true,
                Deposits = [.. p.Deposits, new(DateTimeOffset.UtcNow, p.DepositItem!, p.DepositQuantity, after.Dialog!)] }, Reason = "Song deposit verified" });
        else await c.SaveAsync(c.Job with { Pending = null, Reason = t.Kind switch
        {
            "research-buy" => "One parchment and 500 gold payment verified",
            "research-supply-close" => "Research opening closed; preparing supplies",
            _ => "Research page received"
        } });
    }

    static async Task ReadResultPages(QuestContext c, Transaction t, CancellationToken ct)
    {
        for (int pages = 0; pages < 12; pages++)
        {
            var p = Progress(c);
            if (p.SubmittedNext is { } sent)
            {
                if (!FreshReply(c.Session.Snapshot, p, t, out var next) || next.Revision <= sent.Revision)
                    await c.Session.WaitAsync(s => FreshReply(s, p, t, out var d) && d.Revision > sent.Revision, sent.Revision, Timeout, ct);
                await Save(c, p with { SubmittedNext = null }, "Research Next reply received");
                p = Progress(c);
            }
            if (!FreshReply(c.Session.Snapshot, p, t, out var current) || !current.HasNext || current.Choices.Length != 0 || current.Prompt is not null) return;
            await Save(c, p with { SubmittedNext = current }, "Reading research result");
            await c.Session.ExecuteAsync(new("next", Dialog: current, ExpectedGeneration: current.Generation,
                ExpectedDialogRevision: current.Revision, ExpectedPageText: current.Text), ct);
        }
        throw new QuestBlockedException("Research result page limit reached; progress and pending submission remain saved.");
    }

    static async Task Cleanup(QuestContext c, CancellationToken ct, int remaining = 12)
    {
        var p = Progress(c);
        if (p.Cleanup is not { } receipt) return;
        if (remaining == 0) throw new QuestBlockedException("Research result page limit reached; cleanup remains saved.");
        if (p.SubmittedNext is { } submitted)
        {
            bool Successor(CharacterSnapshot state) => state.Generation == submitted.Generation && state.Dialog is { } next &&
                next.EntityId == submitted.EntityId && next.EntityType == submitted.EntityType && next.Revision > submitted.Revision;
            if (!Successor(c.Session.Snapshot)) await c.Session.WaitAsync(Successor, submitted.Revision, Timeout, ct);
            await Save(c, p with { Cleanup = c.Session.Snapshot.Dialog, SubmittedNext = null }, "Final research Next reply received");
            await Cleanup(c, ct, remaining - 1);
            return;
        }
        var s = c.Session.Snapshot;
        if (s.Dialog is { } current)
        {
            if (current.Generation != receipt.Generation || current.Revision != receipt.Revision || current.EntityId != receipt.EntityId)
                throw new QuestBlockedException("Saved research reward is verified; reconcile the current popup separately.");
            if (current.HasNext && current.Choices.Length == 0)
            {
                await Save(c, p with { SubmittedNext = current }, "Reading final research page");
                await c.Session.ExecuteAsync(new("next", Dialog: current, ExpectedGeneration: current.Generation, ExpectedDialogRevision: current.Revision, ExpectedPageText: current.Text), ct);
                await Save(c, p with { Cleanup = c.Session.Snapshot.Dialog, SubmittedNext = null }, "Final research page received");
                await Cleanup(c, ct, remaining - 1);
                return;
            }
            if (current.Choices.Length > 0 || current.Prompt is not null) throw new QuestBlockedException("Research result offers an unknown choice; popup preserved.");
            await c.Session.ExecuteAsync(new(p.DepositCleanup ? "close-song-deposit" : "close", Dialog: current, ExpectedGeneration: current.Generation, ExpectedDialogRevision: current.Revision, ExpectedPageText: current.Text), ct);
            if (c.Session.Snapshot.Dialog is { } stillOpen && stillOpen.Revision == current.Revision)
                await c.Session.WaitAsync(s => s.Dialog is null, current.Revision, Timeout, ct);
        }
        await Save(c, p with { Cleanup = null, Source = null, DepositCleanup = false }, "Research popup closed; progress saved");
    }
}
