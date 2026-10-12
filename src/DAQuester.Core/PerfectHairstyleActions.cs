namespace DAQuester.Core;

public static class PerfectHairstyleEvidence
{
    public const string Legend = "Loved by Suomi Mundanes";
    public const string Completion = "Excellent! Thank you so much! I couldn't have done it without your help. Take this as my thanks!";
    public const string TwoRingCompletion = "Well done! Oh I'm sure this will look GREAT on me! What an eye for style you have. Here's a token of my appreciation!";
    public static bool HasLegend(CharacterSnapshot s) => s.Legends.Any(l => l.Contains(Legend, StringComparison.OrdinalIgnoreCase));
    public static Eligibility Eligibility(CharacterSnapshot s)
    {
        if (!s.Connected || s.Name is null || s.Id is null || s.Level is null || s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for connected identity, level and health.");
        if (s.Level < 1 || s.Hp == 0)
            return new(EligibilityKind.Blocked, "Requires level 1+ and a living character.");
        if (!s.LegendsKnown)
            return new(EligibilityKind.Unknown, "Waiting for own legend.");
        if (HasLegend(s))
            return new(EligibilityKind.Eligible, "Loved by Suomi Mundanes already recorded");
        if (!s.InventoryKnown || s.Experience is null || s.Weight is null || s.MaxWeight is null)
            return new(EligibilityKind.Unknown, "Waiting for inventory, experience and weight.");
        if (s.Inventory.Count > 57 || s.MaxWeight - s.Weight < 2)
            return new(EligibilityKind.Blocked, "Free two inventory slots and two weight for possible rings.");
        return new(EligibilityKind.Eligible, "Ponytail: no items required; legend is a chance reward");
    }

    static bool ValidReply(Dialog d, Transaction t) => t.Kind == "hairstyle-turn-in" && d.Generation == t.Generation && d.EntityId == t.EntityId && d.EntityType == 1 && d.Revision > t.Revision && d.Name.Equals("Eeva", StringComparison.OrdinalIgnoreCase) && d.IsPursuit && d.Type == 0 && d.Choices.Length == 0 && d.Text is Completion or TwoRingCompletion;
    public static bool FreshReply(CharacterSnapshot s, Transaction t) => s.Generation == t.Generation && s.Dialog is { } d && d.Revision <= s.Revision && ValidReply(d, t);
    public static bool SavedReply(QuestJob job) => job.Pending is { } t && t.TerminalSeen && job.HairstyleCompletion is { } d && t.TerminalRevision == d.Revision && ValidReply(d, t);
    public static bool ReconcilesAfterReconnect(CharacterSnapshot s, QuestJob job) => s.Connected && s.LegendsKnown && job.Pending is { } t && s.Generation != t.Generation && SavedReply(job) && RewardDeltas(s, t);
    public static bool Rewards(CharacterSnapshot s, Transaction t) => s.Generation == t.Generation && RewardDeltas(s, t);
    static bool RewardDeltas(CharacterSnapshot s, Transaction t)
    {
        if (s.Experience is not { } xp || xp < t.Experience || !s.InventoryKnown || !t.InventoryBaselineKnown)
            return false;
        long gain = (long)xp - t.Experience;
        bool Added(string name) => s.Count(name) > t.InventoryCounts.FirstOrDefault(x => x.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
        return gain == 15000 && Added("Emerald Ring") || gain == 25000 && Added("Emerald Ring") && Added("Jade Ring") || gain == 50000 && HasLegend(s) && !t.Legends.Any(l => l.Contains(Legend, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class PerfectHairstyleActions : IQuestAction
{
    static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        if (step.Action == "hair-confirm")
        {
            await Confirm(c, ct);
            return;
        }

        if (c.Job.Pending is not null)
        {
            if (c.Job.Pending.Kind != "hairstyle-turn-in")
                throw new QuestBlockedException("Unexpected pending hairstyle transaction; reconciliation required.");
            return;
        }

        if (!c.Session.Snapshot.LegendsKnown)
            await c.Session.ExecuteAsync(new("inspect-self"), ct);
        var gate = PerfectHairstyleEvidence.Eligibility(c.Session.Snapshot);
        if (gate.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(gate.Reason);
        if (PerfectHairstyleEvidence.HasLegend(c.Session.Snapshot))
        {
            await c.SaveAsync(c.Job with
            {
                Step = "complete",
                CompletedAt = DateTimeOffset.UtcNow,
                CompletionText = "Loved by Suomi Mundanes already recorded",
                LegendReward = true,
                Repeat = false,
                NextRun = null
            });
            return;
        }

        if (step.Action == "hair-preflight")
            return;
        if (step.Action != "hair-dialog")
            throw new QuestBlockedException("Unsupported hairstyle action: " + step.Action);
        await c.SaveAsync(c.Job with
        {
            Reason = "Walking to Eeva in Suomi Inn; Ponytail needs no items"
        });
        await c.Session.ExecuteAsync(new("visit", 950, Text: "Eeva"), ct);
        await Open(c, ct);
        await Choose(c, "The Perfect Hairstyle", ct);
        if (Current(c) is { Type: 0, Prompt: null, Choices.Length: 0 } already && already.Text == "Thanks again for the new hairstyle.")
        {
            await QuestCompletionDialogs.DismissAsync(c, already, ct);
            throw new QuestBlockedException("Eeva has already received this hairstyle. Wait for the seven-day cooldown before trying again; no new reward was recorded.");
        }

        if (Has(c, "It's a bit bland to be honest"))
        {
            await Choose(c, "It's a bit bland to be honest", ct);
            await Choose(c, "I'd love to help. what do you need?", ct);
            await Choose(c, "Ponytail", ct);
            await Advance(c, ct, allowClose: true);
            if (c.Session.Snapshot.Dialog is not null)
            {
                await Choose(c, "Done", ct, allowClose: true);
                await Advance(c, ct, allowClose: true);
            }

            await Open(c, ct);
            await Choose(c, "The Perfect Hairstyle", ct);
        }

        await Choose(c, "Ponytail", ct);
        var d = Current(c);
        if (!Has(c, "Yes I did"))
            throw Unexpected(d, "Yes I did");
        gate = PerfectHairstyleEvidence.Eligibility(c.Session.Snapshot);
        if (gate.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(gate.Reason);
        ct.ThrowIfCancellationRequested();
        await c.BeginAsync("hairstyle-turn-in", d.EntityId, "Submitting Ponytail once; waiting for server reward");
        await c.Session.ExecuteAsync(new("choose", Text: "Yes I did", Dialog: d), ct);
    }

    static bool Has(QuestContext c, string text) => Current(c).Choices.Any(x => x.Text.Trim().TrimEnd('.').Equals(text.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
    static QuestBlockedException Unexpected(Dialog d, string wanted) => new("Eeva option missing: " + wanted + ". Server reply: " + d.Text);
    static Dialog Current(QuestContext c)
    {
        var s = c.Session.Snapshot;
        var d = s.Dialog ?? throw new QuestBlockedException("Expected Eeva dialog; resume to reopen the unfinished conversation.");
        if (!s.Connected || d.Generation != s.Generation || d.Revision > s.Revision || d.EntityId == 0 || !d.Name.Equals("Eeva", StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Unexpected or stale Eeva dialog.");
        return d;
    }

    static async Task Open(QuestContext c, CancellationToken ct)
    {
        await c.Session.ExecuteAsync(new("open", Text: "Eeva"), ct);
        Current(c);
    }

    static async Task Advance(QuestContext c, CancellationToken ct, bool allowClose = false)
    {
        for (int page = 0; page < 4; page++)
        {
            if (c.Session.Snapshot.Dialog is null)
            {
                if (allowClose)
                    return;
                throw new QuestBlockedException("Eeva conversation closed before expected option.");
            }

            var d = Current(c);
            if (!d.HasNext || d.Choices.Length > 0)
                return;
            if (!d.IsPursuit || d.Type != 0 || !(d.Text == "Ah! An honest Aisling! I like you already." || d.Text == "Thank you so much! Now...which do you like best?" || d.Text.StartsWith("A small ponytail makes for a very modest hairstyle.", StringComparison.Ordinal) || d.Text.StartsWith("All done? Wonderful! You aislings are so creative", StringComparison.Ordinal)))
                throw new QuestBlockedException("Unexpected Eeva Next page: " + d.Text);
            await c.Session.ExecuteAsync(new("next", Dialog: d), ct);
        }

        throw new QuestBlockedException("Too many Eeva Next pages. Inspect conversation.");
    }

    static async Task Choose(QuestContext c, string text, CancellationToken ct, bool allowClose = false)
    {
        await Advance(c, ct);
        ct.ThrowIfCancellationRequested();
        var d = Current(c);
        if (!Has(c, text))
            throw Unexpected(d, text);
        await c.Session.ExecuteAsync(new("choose", Text: d.Choices.Single(x => x.Text.Trim().TrimEnd('.').Equals(text.TrimEnd('.'), StringComparison.OrdinalIgnoreCase)).Text, Dialog: d), ct);
        await c.Session.WaitAsync(s => s.Generation == d.Generation && (allowClose && s.Dialog is null || s.Dialog is { } n && n.Generation == d.Generation && n.EntityId == d.EntityId && n.Revision > d.Revision), d.Revision, timeout, ct);
    }

    static async Task Confirm(QuestContext c, CancellationToken ct)
    {
        var t = c.Job.Pending ?? throw new QuestBlockedException("No pending hairstyle submission.");
        bool reconnected = t.Generation != c.Session.Snapshot.Generation;
        if (t.Kind != "hairstyle-turn-in" || reconnected && !PerfectHairstyleEvidence.SavedReply(c.Job))
            throw new QuestBlockedException("Hairstyle submission needs reconciliation after reconnect. Inspect legend and rewards; final choice will not be resent.");
        try
        {
            if (!t.TerminalSeen)
            {
                var reply = await c.Session.WaitAsync(s => PerfectHairstyleEvidence.FreshReply(s, t), t.Revision, timeout, ct);
                await c.SaveAsync(c.Job with
                {
                    HairstyleCompletion = reply.Dialog,
                    Pending = t with
                    {
                        TerminalSeen = true,
                        TerminalRevision = reply.Dialog!.Revision
                    }
                });
                t = c.Job.Pending!;
            }

            await QuestCompletionDialogs.DismissAsync(c, c.Job.HairstyleCompletion, ct);
            long beforeInspect = c.Session.Snapshot.Revision;
            await c.Session.ExecuteAsync(new("inspect-self"), ct);
            var done = await c.Session.WaitAsync(s => reconnected ? s.LegendsRevision > beforeInspect && PerfectHairstyleEvidence.ReconcilesAfterReconnect(s, c.Job) : s.Revision >= t.TerminalRevision && PerfectHairstyleEvidence.Rewards(s, t), reconnected ? beforeInspect : t.Revision, timeout, ct);
            var rewards = RewardEvidence.Record(t, done);
            rewards.Remove("branchesConsumed");
            bool legend = PerfectHairstyleEvidence.HasLegend(c.Session.Snapshot);
            var completed = c.Job.HairstyleCompletion?.ObservedAt ?? DateTimeOffset.UtcNow;
            var next = completed.AddDays(7);
            var attempt = new HairstyleAttempt(completed, c.Job.HairstyleCompletion!, rewards, legend);
            await c.SaveAsync(c.Job with
            {
                Pending = null,
                LastAttemptAt = completed,
                HairstyleAttempts = [.. c.Job.HairstyleAttempts, attempt],
                CompletedAt = legend ? completed : null,
                NextRun = legend ? null : next,
                Repeat = !legend,
                Accepted = false,
                LegendReward = legend,
                Rewards = rewards,
                CompletionText = legend ? PerfectHairstyleEvidence.Legend : "Weekly attempt verified; legend not awarded. Next attempt " + next.ToLocalTime().ToString("g"),
                Reason = "Server reward verified"
            });
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("Hairstyle reward evidence missing. Inspect Eeva reply, inventory and legend before reconciliation; Yes I did will not be resent.");
        }
    }
}
