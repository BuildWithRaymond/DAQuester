using System.Text.RegularExpressions;

namespace DAQuester.Core;

public record DachaidhReceipt(long Generation, long Revision, uint GoldBefore, uint GoldAfter, int SkullsBefore, int SkullsAfter, DateTimeOffset VerifiedAt);
public record DachaidhPayment(Dialog Quote, uint Gold, int Skulls)
{
    public DachaidhReceipt? Receipt { get; init; }
}

public static class DachaidhPreparation
{
    public const string PaymentKind = "learn-dachaidh";
    const string Spell = "dachaidh", Skull = "Goblin's Skull";
    const string Root = "Hello.  What can I do for you?";
    const string Secrets = "You have unravelled your mind.  It is waiting now; is it not?  There are some secrets which you may learn. ";
    const string Consent = "dachaidh Return to the starting position, you swear you will this secret for just good causes?";
    static readonly Regex price = new(@"\ATo learn dachaidh, (?:Goblin's Skull \((?<skulls>[0-9]+)\) and )?(?<gold>[0-9]+) coins have to be presented to me\.  You want to do that\?\z", RegexOptions.CultureInvariant);

    static bool HasSpell(CharacterSnapshot s) => ShinewoodFaerieEvidence.HasSpell(s, Spell);
    static bool Ready(CharacterSnapshot s)
    {
        if (!s.Connected || s.Hp is null or 0 || !s.InventoryKnown || !s.SpellsKnown || HasSpell(s) ||
            s.Gold is null or < 5000 || s.Stats is not { } stats)
            return false;
        // Wizard thresholds are live Devlin evidence. Priest thresholds come
        // from the official guide; the actual trainer quote still gates payment.
        return s.Class switch
        {
            "Wizard" => stats.Str >= 5 && stats.Int >= 36 && stats.Wis >= 5 && stats.Con >= 5 && stats.Dex >= 5 && s.Count(Skull) >= 1,
            "Priest" => stats.Str >= 3 && stats.Int >= 10 && stats.Wis >= 9 && stats.Con >= 3 && stats.Dex >= 6,
            _ => false
        };
    }

    static Dialog Current(QuestContext c, long generation, uint? entity = null)
    {
        var s = c.Session.Snapshot;
        if (!s.Connected || s.Generation != generation || s.Map != 302 ||
            s.Dialog is not { Name: "Devlin", EntityType: 1, IsPursuit: false } d ||
            d.Generation != generation || d.Revision <= 0 || d.Revision > s.Revision || entity is { } bound && d.EntityId != bound)
            throw new QuestBlockedException("Devlin's learning menu changed; inspect it before continuing.");
        return d;
    }

    static bool Choices(Dialog d, params string[] labels) => d.Choices.Length == labels.Length &&
        labels.All(label => d.Choices.Count(choice => choice.Text == label) == 1);

    static async Task SelectAsync(QuestContext c, Dialog d, string label, CancellationToken ct)
    {
        await c.Session.ExecuteAsync(new("choose", Text: label, Dialog: d, ExpectedGeneration: d.Generation,
            ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
        if (c.Session.Snapshot.Dialog is not { } reply || reply.Revision <= d.Revision)
            await c.Session.WaitAsync(s => s.Dialog is { } fresh && fresh.Generation == d.Generation &&
                fresh.EntityId == d.EntityId && fresh.Revision > d.Revision, d.Revision, Timeout.InfiniteTimeSpan, ct);
    }

    public static async Task EnsureAsync(QuestContext c, CancellationToken ct)
    {
        if (c.Job.Pending is { Kind: PaymentKind })
        {
            await ReconcileAsync(c, ct);
            return;
        }
        if (c.Job.Pending is not null || c.Job.CompletedAt is not null || c.Calibration.ServerProfile != "datester-7.41" ||
            c.Job.Quest == FiskEvidence.Id && c.Job.FiskWait is { Verified: true } || !Ready(c.Session.Snapshot))
            return;
        var before = c.Session.Snapshot;
        if (before.Dialog is { } open && (open.Name != "Devlin" || open.IsPursuit))
            return;
        await c.SaveAsync(c.Job with { Reason = "Preparing dachaidh for travel" });
        await c.Session.ExecuteAsync(new("interact", 302, new(8, 2), Text: "Devlin", ExpectedGeneration: before.Generation), ct);
        var root = Current(c, before.Generation);
        if (root.Text != Root || root.Type != 0 || root.Choices.Count(choice => choice.Text == "Learn Secret") != 1)
            throw new QuestBlockedException("Devlin's Learn Secret menu is unrecognized; no learning selection sent.");
        await SelectAsync(c, root, "Learn Secret", ct);
        var secrets = Current(c, before.Generation, root.EntityId);
        if (secrets.Text != Secrets || secrets.Type != 6 || secrets.Choices.Count(choice => choice.Text == Spell) != 1)
            throw new QuestBlockedException("Devlin has no verified dachaidh option; no other secret selected.");
        await SelectAsync(c, secrets, Spell, ct);
        var consent = Current(c, before.Generation, root.EntityId);
        if (consent.Text != Consent || consent.Type != 1 || consent.Prompt != Spell || !Choices(consent, "Yes", "No"))
            throw new QuestBlockedException("Devlin's dachaidh consent changed; no choice sent.");
        await SelectAsync(c, consent, "Yes", ct);
        var quote = Current(c, before.Generation, root.EntityId);
        var match = price.Match(quote.Text);
        if (quote.Type != 1 || quote.Prompt != Spell || !Choices(quote, "Yes", "No") || !match.Success ||
            !uint.TryParse(match.Groups["gold"].Value, out uint gold) || gold == 0 ||
            match.Groups["skulls"].Success && match.Groups["skulls"].Value != "1")
            throw new QuestBlockedException("Devlin's actual dachaidh requirements changed; no payment sent.");
        int skulls = match.Groups["skulls"].Success ? 1 : 0;
        var s = c.Session.Snapshot;
        if (!Ready(s) || s.Gold < gold || s.Count(Skull) < skulls)
        {
            await SelectAsync(c, quote, "No", ct);
            return;
        }
        if (s.Dialog != quote)
            throw new QuestBlockedException("Devlin's quote changed before payment.");
        // This independent payment keeps the parent's completion dialog intact.
        await c.SaveAsync(c.Job with
        {
            DachaidhPayment = new(quote, gold, skulls),
            Pending = new(PaymentKind, s.Generation, quote.Revision - 1, s.Gold!.Value, 0, s.Experience ?? 0, DateTimeOffset.UtcNow)
            {
                EntityId = quote.EntityId, EntityType = quote.EntityType, InventoryBaselineKnown = s.InventoryKnown,
                InventoryCounts = s.Inventory.Values.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => checked((int)g.Sum(i => (long)i.Quantity)), StringComparer.OrdinalIgnoreCase),
                SpellsBaselineKnown = s.SpellsKnown, Spells = s.Spells.Values.ToArray()
            }
        });
        await c.Session.ExecuteAsync(new("choose", Text: "Yes", Dialog: quote, ExpectedGeneration: quote.Generation,
            ExpectedDialogRevision: quote.Revision, ExpectedPageText: quote.Text), ct);
        await ReconcileAsync(c, ct);
    }

    static async Task ReconcileAsync(QuestContext c, CancellationToken ct)
    {
        var pending = c.Job.Pending!;
        var payment = c.Job.DachaidhPayment ?? throw new QuestBlockedException("Dachaidh's saved payment quote is missing; no purchase repeated.");
        if (pending.Kind != PaymentKind || !pending.SpellsBaselineKnown ||
            pending.Spells.Any(spell => ShinewoodFaerieEvidence.SpellNameEquals(spell, Spell)) || !pending.InventoryBaselineKnown ||
            pending.Gold < payment.Gold || payment.Quote.Generation != pending.Generation || payment.Quote.EntityId != pending.EntityId)
            throw new QuestBlockedException("Dachaidh's original payment baseline is incomplete; no purchase repeated.");
        bool Receipt(CharacterSnapshot s) => s.Connected && s.Generation == pending.Generation &&
            string.Equals(s.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase) && HasSpell(s) && s.InventoryKnown &&
            s.Gold == pending.Gold - payment.Gold && s.Count(Skull) == pending.InventoryCounts.GetValueOrDefault(Skull) - payment.Skulls;
        if (c.Session.Snapshot.Generation != pending.Generation)
            throw new QuestBlockedException("Dachaidh payment belongs to an earlier connection; reconcile its original receipts before continuing.");
        if (!Receipt(c.Session.Snapshot))
            await c.Session.WaitAsync(Receipt, pending.Revision, TimeSpan.FromSeconds(10), ct);
        var after = c.Session.Snapshot;
        await c.SaveAsync(c.Job with
        {
            Pending = null,
            DachaidhPayment = payment with
            {
                Receipt = new(pending.Generation, after.Revision, pending.Gold, after.Gold!.Value,
                    pending.InventoryCounts.GetValueOrDefault(Skull), after.Count(Skull), DateTimeOffset.UtcNow)
            },
            Reason = "Dachaidh learned; spell and payment verified"
        });
    }
}
