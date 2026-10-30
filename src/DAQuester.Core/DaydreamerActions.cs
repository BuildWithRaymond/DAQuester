using static DAQuester.Core.DaydreamerEvidence;

namespace DAQuester.Core;

public sealed class DaydreamerActions : IQuestAction
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    static DaydreamerProgress State(QuestContext c) => c.Job.Daydreamer ?? new();
    static Task Save(QuestContext c, DaydreamerProgress progress, string reason) => c.SaveAsync(c.Job with { Daydreamer = progress, Reason = reason });

    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        if (step.Action is not ("daydreamer-preflight" or "daydreamer-check-in"))
            throw new QuestBlockedException("Unsupported Daydreamer action: " + step.Action);
        Own(c);
        var observed = new List<ServerNotice>();
        var gate = new object();
        void Observe(CharacterSnapshot snapshot)
        {
            if (!snapshot.Connected || snapshot.Map != Map || !string.Equals(snapshot.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase)) return;
            lock (gate)
                foreach (var status in new[] { snapshot.DaydreamerStatus, snapshot.ActiveStatus })
                    if (status is not null && status.Generation == snapshot.Generation && status.Revision <= snapshot.Revision && IsStatus(status) && !observed.Contains(status))
                        observed.Add(status);
        }
        ServerNotice[] Receipts()
        {
            Observe(c.Session.Snapshot);
            lock (gate) return observed.ToArray();
        }

        c.Session.Changed += Observe;
        try
        {
            Observe(c.Session.Snapshot);
            if (c.Job.Pending is not null)
            {
                if (c.Job.Pending.Kind == "daydreamer-select")
                {
                    if (await SelectionReply(c, Receipts, ct)) return;
                }
                else
                {
                    await Result(c, Receipts, ct);
                    return;
                }
            }
            if (State(c).CleanupPending)
            {
                await Cleanup(c, ct);
                return;
            }
            if (State(c).Submission is not null)
                throw new QuestBlockedException("The saved Celesta submission needs reconciliation before another check-in.");
            if (GoalVerified(c.Job) && !c.Job.Repeat) return;
            var progress = State(c);
            var next = progress.NextCheckInAt ?? (progress.LastAcknowledgement is { } ack ? NextEligible(ack.At, progress.TimeZoneId) : (DateTimeOffset?)null);
            if (next > DateTimeOffset.UtcNow) throw new QuestScheduledWaitException(next.Value);
            var eligible = Eligibility(c.Session.Snapshot, c.Job);
            if (eligible.Kind != EligibilityKind.Eligible) throw new QuestBlockedException(eligible.Reason);
            if (step.Action == "daydreamer-preflight") return;
            await CheckIn(c, Receipts, ct);
        }
        finally
        {
            c.Session.Changed -= Observe;
            // Cancellation can race a genuine result while WaitAsync unwinds.
            // Save only a receipt bound to the actual write, without doing any
            // further live action or converting a prepared Next into success.
            if (SavedSubmission(c) is { } sent && FindReply(c, sent, Receipts(), includeRefusal: false) is { } reply)
                await RecordProof(c, new(reply.ObservedAt, Status: reply));
        }
    }

    static void Own(QuestContext c)
    {
        var s = c.Session.Snapshot;
        if (!s.Connected || s.Id is null || !string.Equals(s.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Daydreamer connection changed; resume on the saved character.");
    }

    static async Task CheckIn(QuestContext c, Func<ServerNotice[]> receipts, CancellationToken ct)
    {
        var s = c.Session;
        if (s.Snapshot.Dialog is null)
        {
            long generation = s.Snapshot.Generation;
            if (s.Snapshot.Map != Map || s.Snapshot.Position is not { } position ||
                Math.Abs(position.X - Approach.X) + Math.Abs(position.Y - Approach.Y) > 2)
                await s.ExecuteAsync(new("navigate", Map, Approach, Tolerance: 1, ExpectedGeneration: generation), ct);
            Own(c);
            var arrival = s.Snapshot;
            if (arrival.Generation != generation || arrival.Map != Map || arrival.Position is not { } arrived ||
                Math.Abs(arrived.X - Approach.X) + Math.Abs(arrived.Y - Approach.Y) > 2)
                throw new QuestBlockedException("Celesta arrival needs reconciliation before opening her menu.");
            var npcs = arrival.Entities.Values.Where(e => !e.IsPlayer && e.Name.Equals(Npc, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (npcs.Length != 1) throw new QuestBlockedException("One current visible Celesta is required for the check-in.");
            await s.ExecuteAsync(new("open", Text: Npc, ExpectedGeneration: generation), ct);
            Own(c);
            if (s.Snapshot.Dialog is not { } opened || !IsPage(s.Snapshot, opened) || opened.EntityId != npcs[0].Id || opened.Revision <= arrival.Revision)
                throw new QuestBlockedException("Celesta's fresh menu did not arrive; no check-in was submitted.");
        }
        var page = s.Snapshot.Dialog!;
        if (!IsPage(s.Snapshot, page)) throw new QuestBlockedException("A different popup is open; it was preserved.");
        if (IsRegistrationDenial(page.Text)) await Refused(c, Acknowledge(page));
        if (!IsGreeting(page))
        {
            var choices = page.Choices.Where(x => x.Text.Trim().Equals(Option, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (page.IsPursuit || choices.Length != 1)
                throw new QuestBlockedException("Unrecognized Celesta page; the popup was preserved without submitting Next.");
            await Save(c, State(c) with { Source = page, Submission = null }, "Opening Celesta's Avid Daydreamer check-in");
            await c.BeginAsync("daydreamer-select", page.EntityId, observedDialog: page);
            await s.ExecuteAsync(new("choose", Text: choices[0].Text, Dialog: page, ExpectedGeneration: page.Generation,
                ExpectedDialogRevision: page.Revision, ExpectedPageText: page.Text), ct);
            if (await SelectionReply(c, receipts, ct)) return;
            page = s.Snapshot.Dialog!;
        }
        if (!IsPage(s.Snapshot, page) || !IsGreeting(page))
            throw new QuestBlockedException("Celesta did not offer the recognized check-in Next page.");
        if (s is not IDaydreamerSession submitter)
            throw new QuestBlockedException("This session cannot confirm when the Celesta Next is sent.");
        await Save(c, State(c) with { Source = page, Submission = null }, "Submitting Celesta's daily check-in");
        await c.BeginAsync("daydreamer-submit", page.EntityId, observedDialog: page);
        // A prepared journal entry is deliberately not a send receipt. If the
        // write is uncertain, a later resume must not submit this page again.
        var submission = await submitter.SubmitDaydreamerAsync(page, ct);
        await Save(c, State(c) with { Submission = submission }, "Awaiting Celesta's actual consecutive-login reply");
        await Result(c, receipts, ct);
    }

    static bool FreshPage(QuestContext c, Dialog source, out Dialog page)
    {
        var snapshot = c.Session.Snapshot;
        page = snapshot.Dialog!;
        return page is not null && IsPage(snapshot, page) && page.Generation == source.Generation && page.EntityId == source.EntityId &&
            page.EntityType == source.EntityType && page.Revision > source.Revision;
    }

    static async Task<bool> SelectionReply(QuestContext c, Func<ServerNotice[]> receipts, CancellationToken ct)
    {
        var pending = c.Job.Pending!;
        if (State(c).Source is not { } source || source.IsPursuit || source.Generation != pending.Generation ||
            c.Session.Snapshot.Generation != pending.Generation || pending.EntityId != source.EntityId || pending.EntityType != source.EntityType)
            throw new QuestBlockedException("The saved Celesta menu selection needs reconciliation; it will not be selected again.");
        ServerNotice? StatusReply() => receipts().Where(n => n.Generation == pending.Generation && n.Revision > source.Revision && n.ObservedAt >= pending.Started &&
            (IsAlreadyDone(n.Text) || IsRegistrationDenial(n.Text))).OrderByDescending(n => n.Revision).FirstOrDefault();
        if (!FreshPage(c, source, out _) && StatusReply() is null)
            try { await c.Session.WaitAsync(snapshot => FreshPage(c, source, out _) || StatusReply() is not null, source.Revision, Timeout, ct); }
            catch (TimeoutException) { throw new QuestBlockedException("Celesta's selected menu needs checking; it will not be selected again after a timeout."); }
        Own(c);
        if (StatusReply() is { } status)
        {
            var ack = new DaydreamerAcknowledgement(status.ObservedAt, Status: status);
            if (IsRegistrationDenial(status.Text)) await Refused(c, ack);
            await Record(c, ack, ct);
            return true;
        }
        if (!FreshPage(c, source, out var page)) throw new QuestBlockedException("Celesta's fresh reply is missing; the selection will not be repeated.");
        if (IsRegistrationDenial(page.Text)) await Refused(c, Acknowledge(page));
        if (IsAlreadyDone(page.Text))
        {
            await Record(c, Acknowledge(page), ct);
            return true;
        }
        if (!IsGreeting(page)) throw new QuestBlockedException("Unknown Celesta reply; the submitted menu and popup were preserved for reconciliation.");
        await c.SaveAsync(c.Job with { Pending = null, Daydreamer = State(c) with { Source = page } });
        return false;
    }

    static bool SamePage(Dialog a, Dialog b) => a.Generation == b.Generation && a.Revision == b.Revision && a.EntityId == b.EntityId &&
        a.EntityType == b.EntityType && a.Pursuit == b.Pursuit && a.Step == b.Step && a.Type == b.Type && a.Text == b.Text && a.Name == b.Name && a.HasNext == b.HasNext;
    static DaydreamerAcknowledgement Acknowledge(Dialog page) => new(page.ObservedAt ?? throw new QuestBlockedException("Celesta's reply has no observation time."), Dialog: page);

    static DaydreamerSubmission? SavedSubmission(QuestContext c)
    {
        var pending = c.Job.Pending;
        var progress = State(c);
        if (pending is not { Kind: "daydreamer-submit" } || progress.Source is not { } source || progress.Submission is not { } sent ||
            !IsGreeting(source) || !SamePage(source, sent.Page) || source.Generation != pending.Generation || pending.EntityId != source.EntityId ||
            pending.EntityType != source.EntityType || sent.BaselineRevision < source.Revision || sent.SentAt < pending.Started)
            return null;
        return sent;
    }

    static ServerNotice? FindReply(QuestContext c, DaydreamerSubmission sent, IEnumerable<ServerNotice> observed, bool includeRefusal = true)
    {
        var candidates = observed.Concat(State(c).LastCountReceipt is { } saved ? [saved] : []).Where(notice => IsStatus(notice) &&
            notice.Generation == sent.Page.Generation && notice.Revision > sent.BaselineRevision && notice.ObservedAt >= sent.SentAt &&
            (includeRefusal || Count(notice) is not null || IsAlreadyDone(notice.Text))).ToArray();
        return candidates.Where(n => Count(n) is not null).OrderByDescending(n => n.Revision).FirstOrDefault() ??
            candidates.OrderByDescending(n => n.Revision).FirstOrDefault();
    }

    static async Task Result(QuestContext c, Func<ServerNotice[]> receipts, CancellationToken ct)
    {
        var sent = SavedSubmission(c) ?? throw new QuestBlockedException("The saved Celesta Next may already have been submitted. Reconcile it before another check-in; Next will not be repeated.");
        ServerNotice? Reply() => FindReply(c, sent, receipts());
        if (Reply() is null && c.Session.Snapshot.Generation == sent.Page.Generation)
            try { await c.Session.WaitAsync(_ => Reply() is not null, sent.BaselineRevision, Timeout, ct); }
            catch (TimeoutException) { }
        if (Reply() is not { } reply)
            throw new QuestBlockedException("Celesta's check-in result is uncertain. The saved Next will not be sent again; reconcile the actual server reply.");
        var acknowledgement = new DaydreamerAcknowledgement(reply.ObservedAt, Status: reply);
        if (IsRegistrationDenial(reply.Text)) await Refused(c, acknowledgement);
        await Record(c, acknowledgement, ct);
    }

    static async Task Refused(QuestContext c, DaydreamerAcknowledgement refusal)
    {
        var due = refusal.At.AddHours(1);
        await c.SaveAsync(c.Job with { Pending = null, NextRun = due,
            Daydreamer = State(c) with { LastRefusal = refusal, Submission = null, Source = null, NextCheckInAt = due },
            Reason = "Celesta requires registration. Check this account's registration before trying again." });
        throw new QuestBlockedException(c.Job.Reason);
    }

    static async Task Record(QuestContext c, DaydreamerAcknowledgement ack, CancellationToken ct)
    {
        await RecordProof(c, ack);
        await Cleanup(c, ct);
    }

    static async Task RecordProof(QuestContext c, DaydreamerAcknowledgement ack)
    {
        var p = State(c);
        int? value = ack.Status is { } status ? Count(status) : null;
        var first = p.FirstThirtyReceipt ?? (value >= Goal ? ack.Status : null);
        var due = first is not null && !c.Job.Repeat ? (DateTimeOffset?)null : NextEligible(ack.At, p.TimeZoneId);
        var snapshot = c.Session.Snapshot;
        var current = snapshot.Dialog;
        var terminal = current is not null && Terminal(c, current) ? current : null;
        var text = value is { } actual ? "Celesta confirmed " + actual + " consecutive logins." : "Celesta says today's check-in is already recorded; the count is unchanged.";
        await c.SaveAsync(c.Job with { Step = "complete", CompletedAt = ack.At, LastAttemptAt = ack.At, Pending = null, NextRun = due, CompletionText = text,
            Daydreamer = p with { Streak = value ?? p.Streak, PreviousCount = value is not null ? p.Streak : p.PreviousCount,
                LastCountReceipt = value is not null ? ack.Status : p.LastCountReceipt, LastAcknowledgement = ack, LastRefusal = null,
                LastSuccessAt = value is { } actualCount && (p.Streak != actualCount || p.LastSuccessAt is null) ? ack.At : p.LastSuccessAt,
                NextCheckInAt = due, FirstThirtyReceipt = first,
                Submission = null, CleanupPending = current is not null || !snapshot.Connected || snapshot.Generation != p.Source?.Generation,
                CleanupPage = terminal }, Reason = text });
    }

    static bool Terminal(QuestContext c, Dialog page) => State(c).Source is { } source &&
        IsPage(c.Session.Snapshot, page) && page.Generation == source.Generation && page.EntityId == source.EntityId && page.EntityType == source.EntityType &&
        page.Revision > source.Revision && !page.HasNext && page.Prompt is null && page.Choices.Length == 0;

    static async Task Cleanup(QuestContext c, CancellationToken ct)
    {
        var p = State(c);
        if (!p.CleanupPending) return;
        Own(c);
        if (c.Session.Snapshot.Exchange is { Cancelled: false })
            throw new QuestBlockedException("Celesta's result is saved. Finish the active trade before closing her popup.");
        if (c.Session.Snapshot.Dialog is { } waiting && p.Source is { } source && SamePage(waiting, source) && IsGreeting(source))
            try
            {
                await c.Session.WaitAsync(s => s.Generation == source.Generation &&
                    (s.Dialog is null || s.Dialog is { } terminal && Terminal(c, terminal)), c.Session.Snapshot.Revision, Timeout, ct);
            }
            catch (TimeoutException) { }
        if (c.Session.Snapshot.Dialog is { } page)
        {
            if (!Terminal(c, page) || p.CleanupPage is { } expected && !SamePage(page, expected))
                throw new QuestBlockedException("Celesta's result is saved. A different or nonterminal popup was preserved; the check-in will not be submitted again.");
            if (p.CleanupPage is null) await Save(c, p with { CleanupPage = page }, "Closing Celesta's confirmed terminal popup");
            var before = c.Session.Snapshot.Revision;
            await c.Session.ExecuteAsync(new("close", Dialog: page, ExpectedGeneration: page.Generation, ExpectedDialogRevision: page.Revision, ExpectedPageText: page.Text), ct);
            if (c.Session.Snapshot.Dialog is not null)
                await c.Session.WaitAsync(s => s.Generation == page.Generation && s.Dialog is null, before, Timeout, ct);
        }
        await Save(c, State(c) with { CleanupPending = false, CleanupPage = null, Source = null }, c.Job.CompletionText ?? "Celesta's result is saved");
    }
}
