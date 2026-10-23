using System.Collections.Concurrent;

namespace DAQuester.Core;

// Only captured pages authorize choices; unknown replies retain their checkpoint.
public sealed class WolfQuestActions : IQuestAction
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    readonly ConcurrentQueue<ServerNotice> messages = new();
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        Own(c);
        if (c.Job.Wolf is null) await c.SaveAsync(c.Job with { Wolf = new() });
        if (await CompletedOutsideRunner(c)) return;
        void Observe(CharacterSnapshot s)
        {
            if ((c.Job.Wolf?.TurnIn ?? c.Job.Wolf?.Acceptance) is { } baseline && s.Connected
                && string.Equals(s.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase)
                && s.ActiveStatus is { Type: 3 } notice && notice.Generation == baseline.Generation
                && notice.Revision > baseline.Revision && notice.Revision <= s.Revision)
                messages.Enqueue(notice);
        }
        c.Session.Changed += Observe;
        try
        {
            switch (step.Action)
            {
                case "wolf-preflight":
                    Gate(c);
                    if (c.Job.Pending is not null) throw new QuestBlockedException("Saved wolf action needs reconciliation before starting again.");
                    break;
                case "wolf-accept": await Dialogue(c, false, ct); break;
                case "wolf-enter": await Enter(c, ct); break;
                case "wolf-hunt": await Hunt(c, ct); break;
                case "wolf-return":
                    if (c.Job.Wolf!.Defeat is null) throw new QuestBlockedException("An own Wolf kill receipt is required before returning for the reward.");
                    await Dialogue(c, true, ct);
                    break;
                case "wolf-confirm": await Confirm(c, ct); break;
                default: throw new QuestBlockedException("Unsupported wolf quest action.");
            }
        }
        finally
        {
            c.Session.Changed -= Observe;
            await ObserveMessages(c);
        }
    }

    static void Own(QuestContext c)
    {
        var s = c.Session.Snapshot;
        if (!s.Connected || s.Id is null || !string.Equals(s.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Connect the saved wolf quest character before continuing.");
    }
    static void Gate(QuestContext c)
    {
        var e = WolfQuestEvidence.Eligibility(c.Session.Snapshot);
        if (e.Kind != EligibilityKind.Eligible) throw new QuestBlockedException(e.Reason);
    }
    static async Task<bool> CompletedOutsideRunner(QuestContext c)
    {
        var s = c.Session.Snapshot;
        if (c.Job.CompletedAt is not null || c.Job.Wolf!.TurnIn is not null || s.Dialog is not null ||
            c.Job.Pending is { Kind: not "wolf-approach" } || !WolfQuestEvidence.FreshLegends(s) ||
            WolfQuestEvidence.Mark(s) is not { } mark)
            return false;
        // A fresh own mark proves the goal even after manual play. It does not
        // prove a potion/XP receipt or settle a previously submitted claim.
        await c.SaveAsync(c.Job with { Step = "complete", Status = "Completed", CompletedAt = DateTimeOffset.UtcNow,
            CompletionText = mark, CompletionLegend = mark, LegendReward = true, Pending = null,
            Repeat = false, NextRun = null, Reason = "Your legend confirms this wolf quest is already completed" });
        return true;
    }
    static bool Throttler(CharacterSnapshot s, Dialog d) => s.Map == WolfQuestEvidence.NpcMap
        && d.Name.Equals(WolfQuestEvidence.Npc, StringComparison.OrdinalIgnoreCase) && d.EntityType == 1
        && d.Generation == s.Generation && d.Revision > 0 && d.Revision <= s.Revision && d.Prompt is null;
    static bool SamePage(Dialog? a, Dialog? b) => a is not null && b is not null
        && a.Generation == b.Generation && a.Revision == b.Revision && a.EntityId == b.EntityId && a.EntityType == b.EntityType
        && a.Text == b.Text && a.Pursuit == b.Pursuit && a.Step == b.Step;

    async Task ObserveMessages(QuestContext c)
    {
        if ((c.Job.Wolf?.TurnIn ?? c.Job.Wolf?.Acceptance) is not { } baseline) return;
        if (c.Session.Snapshot.ActiveStatus is { Type: 3 } latest && latest.Generation == baseline.Generation
            && latest.Revision > baseline.Revision && latest.Revision <= c.Session.Snapshot.Revision)
            messages.Enqueue(latest);
        var progress = c.Job.Wolf!;
        var retained = progress.RewardMessages.ToList();
        var objective = progress.ObjectiveReceipt;
        while (messages.TryDequeue(out var notice))
            if (notice.Generation == baseline.Generation && notice.Revision > baseline.Revision
                && !retained.Any(n => n.Generation == notice.Generation && n.Revision == notice.Revision))
            {
                if (progress.TurnIn is not null) retained.Add(notice);
                else if (progress.Acceptance is { } accepted && WolfQuestEvidence.ObjectiveMessage(notice, accepted))
                    objective = notice;
            }
        if (retained.Count != progress.RewardMessages.Length || objective != progress.ObjectiveReceipt)
            await c.SaveAsync(c.Job with { Wolf = progress with { RewardMessages = retained.ToArray(), ObjectiveReceipt = objective } });
    }

    async Task RefreshRewardLegend(QuestContext c, CancellationToken ct)
    {
        var s = c.Session.Snapshot;
        if (c.Job.Wolf?.TurnIn is not { } baseline || s.Generation != baseline.Generation || s.Map != WolfQuestEvidence.NpcMap)
            return;
        if (!WolfQuestEvidence.FreshLegends(s) || s.LegendsRevision <= baseline.Revision
            || s.LegendsRevision <= c.Job.Wolf.LegendBaselineRevision || WolfQuestEvidence.Mark(s) is null)
            await c.Session.ExecuteAsync(new("inspect-self", ExpectedGeneration: baseline.Generation), ct);
        Own(c);
    }

    bool Reward(QuestContext c, CharacterSnapshot snapshot)
    {
        var progress = c.Job.Wolf!;
        return WolfQuestEvidence.Reward(snapshot, progress with
        {
            RewardMessages = progress.RewardMessages.Concat(messages).ToArray()
        });
    }

    bool Accepted(QuestContext c, CharacterSnapshot s)
    {
        if (c.Job.Wolf?.Acceptance is not { } accepted || !s.Connected || s.Generation != accepted.Generation
            || s.Map != WolfQuestEvidence.NpcMap || s.Revision <= accepted.Revision || s.Dialog is not null)
            return false;
        bool Objective(ServerNotice? notice) => notice is not null && notice.Revision <= s.Revision
            && WolfQuestEvidence.ObjectiveMessage(notice, accepted);
        return Objective(c.Job.Wolf.ObjectiveReceipt) || Objective(s.ActiveStatus) || messages.Any(Objective);
    }

    async Task Submit(QuestContext c, string kind, Dialog page, GameCommand command, CancellationToken ct)
    {
        Own(c);
        var before = c.Session.Snapshot;
        if (!SamePage(before.Dialog, page)) throw new QuestBlockedException("Wolf quest page changed before submission; inspect the current reply.");
        await c.BeginAsync(kind, page.EntityId, observedDialog: page);
        await c.SaveAsync(c.Job with { Pending = c.Job.Pending! with { Revision = before.Revision },
            Wolf = c.Job.Wolf! with { SubmittedPage = page } });
        if (kind == "wolf-accept-choice")
            await c.SaveAsync(c.Job with { Wolf = c.Job.Wolf! with { Acceptance = c.Job.Pending, ObjectiveReceipt = null } });
        await c.Session.ExecuteAsync(command with { Dialog = page, ExpectedGeneration = page.Generation,
            ExpectedDialogRevision = page.Revision, ExpectedPageText = page.Text }, ct);
    }

    async Task<Dialog?> Reply(QuestContext c, CancellationToken ct)
    {
        var t = c.Job.Pending ?? throw new QuestBlockedException("Missing wolf submission checkpoint.");
        bool acceptanceFinished = false;
        bool Receipt(CharacterSnapshot s) => s.Generation == t.Generation && s.Revision > t.Revision
            && (s.Dialog is { } d && Throttler(s, d) && d.Revision > t.Revision && (t.EntityId is null || d.EntityId == t.EntityId)
                || s.Dialog is null && (c.Job.Wolf?.TurnIn is not null || t.Kind == "wolf-accept-finish"));
        if (c.Session.Snapshot.Generation != t.Generation)
            throw new QuestBlockedException("Wolf reply belongs to an earlier connection. Reconcile it without repeating the submission.");
        await ObserveMessages(c);
        if (c.Session.Snapshot.Dialog is null && c.Job.Wolf?.TurnIn is not null)
            await RefreshRewardLegend(c, ct);
        if (!Receipt(c.Session.Snapshot)) await c.Session.WaitAsync(Receipt, t.Revision, Timeout, ct);
        Own(c);
        await ObserveMessages(c);
        if (c.Session.Snapshot.Dialog is null && c.Job.Wolf?.TurnIn is not null)
        {
            await RefreshRewardLegend(c, ct);
            if (!Reward(c, c.Session.Snapshot))
                await c.Session.WaitAsync(s => Reward(c, s), t.Revision, Timeout, ct);
            await ObserveMessages(c);
        }
        if (c.Session.Snapshot.Dialog is null && t.Kind == "wolf-accept-finish")
        {
            if (!Accepted(c, c.Session.Snapshot))
                await c.Session.WaitAsync(s => Accepted(c, s), t.Revision, Timeout, ct);
            await ObserveMessages(c);
            acceptanceFinished = true;
        }
        var page = c.Session.Snapshot.Dialog;
        if (page is not null && c.Job.Wolf!.SubmittedPage is { } submitted && page.Text == submitted.Text
            && page.Pursuit == submitted.Pursuit && page.Step == submitted.Step
            && page.Choices.SequenceEqual(submitted.Choices))
            throw new QuestBlockedException("Throttler repeated the submitted page without a proven outcome. The saved action will not be repeated.");
        await c.SaveAsync(c.Job with { Pending = null, Wolf = c.Job.Wolf! with
        {
            Briefed = acceptanceFinished || c.Job.Wolf.Briefed,
            LastPage = page,
            SubmittedPage = null
        } });
        return page;
    }

    async Task Dialogue(QuestContext c, bool returning, CancellationToken ct)
    {
        if (!returning && c.Job.Wolf!.Briefed && c.Job.Pending is null) return;
        Dialog? page;
        if (c.Job.Pending is { } pending)
        {
            if (pending.Kind is not ("wolf-open" or "wolf-next" or "wolf-choice" or "wolf-accept-choice" or "wolf-accept-finish"))
                throw new QuestBlockedException("Another saved wolf action requires reconciliation before talking to Throttler.");
            page = await Reply(c, ct);
        }
        else if (c.Job.Wolf!.LastPage is { } saved && SamePage(saved, c.Session.Snapshot.Dialog)) page = saved;
        else
        {
            if (returning && c.Job.Wolf!.TurnIn is not null)
                throw new QuestBlockedException("Throttler's submitted reward conversation is unresolved; it will not be reopened.");
            Gate(c);
            if (c.Session.Snapshot.Dialog is not null) throw new QuestBlockedException("Review the open popup before visiting Throttler.");
            await c.Session.ExecuteAsync(new("navigate", WolfQuestEvidence.NpcMap, WolfQuestEvidence.NpcApproach,
                PreservePopup: true, ExpectedGeneration: c.Session.Snapshot.Generation), ct);
            Own(c);
            var s = c.Session.Snapshot;
            if (s.Map != WolfQuestEvidence.NpcMap || s.Position != WolfQuestEvidence.NpcApproach)
                throw new QuestBlockedException("Throttler's public-side approach is unconfirmed.");
            if (s.Dialog is not null) throw new QuestBlockedException("A popup opened during travel; it is preserved for review.");
            if (returning)
            {
                if (!WolfQuestEvidence.FreshLegends(s) || !s.InventoryKnown || s.Experience is null || s.Gold is null)
                    throw new QuestBlockedException("Fresh own legend, inventory, gold and experience are required before returning to Throttler.");
            }
            await c.BeginAsync("wolf-open", reason: returning ? "Returning to Throttler after the wolf hunt" : "Asking Throttler about the woodland beast");
            if (returning)
            {
                await c.SaveAsync(c.Job with { Wolf = c.Job.Wolf! with { TurnIn = c.Job.Pending,
                    LegendBaselineRevision = s.LegendsRevision, LastPage = null, RewardMessages = [] } });
            }
            await c.Session.ExecuteAsync(new("open", Text: WolfQuestEvidence.Npc, ExpectedGeneration: s.Generation), ct);
            page = await Reply(c, ct);
        }

        for (int count = 0; count < 24; count++)
        {
            Own(c);
            await ObserveMessages(c);
            if (page is null)
            {
                if (returning && Reward(c, c.Session.Snapshot)) return;
                if (!returning && c.Job.Wolf!.Briefed) return;
                throw new QuestBlockedException("Wolf briefing closed without a verified quest outcome.");
            }
            if (!Throttler(c.Session.Snapshot, page) || !SamePage(c.Session.Snapshot.Dialog, page))
                throw new QuestBlockedException("Throttler's current page identity changed; inspect it before continuing.");
            if (page.Choices.Length > 0)
            {
                var choice = WolfQuestEvidence.DialogueChoice(page, returning);
                if (choice is null || choice == WolfQuestEvidence.Accept && c.Job.Wolf!.Acceptance is not null)
                    throw new QuestBlockedException("Unfamiliar or ambiguous Throttler choice. Popup preserved for review.");
                await Submit(c, choice == WolfQuestEvidence.Accept ? "wolf-accept-choice" : "wolf-choice",
                    page, new("choose", Text: choice), ct);
                page = await Reply(c, ct);
                continue;
            }
            if (WolfQuestEvidence.NextPage(page, returning, c.Job.Wolf!.Acceptance is not null))
            {
                string kind = !returning && page.Text == WolfQuestEvidence.AcceptedPages[^1] ? "wolf-accept-finish" : "wolf-next";
                await Submit(c, kind, page, new("next"), ct);
                page = await Reply(c, ct);
                continue;
            }
            if (returning && WolfQuestEvidence.RewardTerminal(page)) return;
            throw new QuestBlockedException("Unfamiliar Throttler page. Popup preserved for review.");
        }
        throw new QuestBlockedException("Throttler conversation exceeded its bounded page limit. Resume only after review.");
    }

    async Task Enter(QuestContext c, CancellationToken ct)
    {
        var p = c.Job.Wolf!;
        if (!p.Briefed) throw new QuestBlockedException("Read Throttler's wolf briefing before entering.");
        if (p.EntrySettled && c.Session.Snapshot.Map == WolfQuestEvidence.WolfMap) return;
        if (c.Job.Pending is null)
        {
            Gate(c);
            if (c.Session.Snapshot.Map == WolfQuestEvidence.WolfMap)
                throw new QuestBlockedException("Wolf-area arrival has no saved entry receipt; inspect the landing before continuing.");
            if (c.Session.Snapshot.Dialog is not null) throw new QuestBlockedException("Review the current popup before approaching the wolf entrance.");
            // Navigation may only use recorded routes. An absent route is an intentional blocker.
            await c.BeginAsync("wolf-approach", reason: "Approaching the woodland wolf warning");
            try
            {
                await c.Session.ExecuteAsync(new("navigate", WolfQuestEvidence.EntranceMap, WolfQuestEvidence.Entrance, PreservePopup: true,
                    ExpectedGeneration: c.Session.Snapshot.Generation), ct);
            }
            catch (QuestBlockedException) when (c.Job.Pending is { Kind: "wolf-approach" } approach &&
                c.Session.Snapshot.Generation == approach.Generation && c.Session.Snapshot.Dialog is { } warning &&
                warning.Revision > approach.Revision && WolfQuestEvidence.EntrancePage(c.Session.Snapshot, warning))
            {
                // The expected consent reactor interrupts travel before its target.
                // Its current page is handled below; no unknown popup is dismissed.
            }
        }
        var t = c.Job.Pending!;
        if (t.Generation != c.Session.Snapshot.Generation) throw new QuestBlockedException("Wolf entry changed connections; reconcile its actual landing before moving.");
        if (t.Kind == "wolf-approach")
        {
            var s = c.Session.Snapshot;
            bool Warning(CharacterSnapshot observed) => observed.Dialog is { } d && d.Revision > t.Revision && WolfQuestEvidence.EntrancePage(observed, d);
            if (!Warning(s)) await c.Session.WaitAsync(Warning, t.Revision, Timeout, ct);
            s = c.Session.Snapshot;
            await Submit(c, "wolf-entry", s.Dialog!, new("choose", Text: "Yes"), ct);
            t = c.Job.Pending!;
        }
        if (t.Kind is not ("wolf-entry" or "wolf-inward" or "wolf-interior")) throw new QuestBlockedException("Unexpected saved wolf entry action.");
        bool Arrived(CharacterSnapshot s) => s.Generation == t.Generation && s.Map == WolfQuestEvidence.WolfMap
            && s.Position is not null && s.Revision > t.Revision;
        if (t.Kind == "wolf-entry" && !Arrived(c.Session.Snapshot)) await c.Session.WaitAsync(Arrived, t.Revision, Timeout, ct);
        await SettleEntry(c, ct);
    }

    async Task SettleEntry(QuestContext c, CancellationToken ct)
    {
        var s = c.Session.Snapshot;
        if (s.Map != WolfQuestEvidence.WolfMap || s.Position is not { } landing || c.Session is not ICombatMap map || map.Size is not { } size)
            throw new QuestBlockedException("Actual wolf-map dimensions, collision and landing are required before stepping inward.");
        var p = c.Job.Wolf!;
        if (p.Away is null)
        {
            var sides = new[] { new Tile(landing.X + 1, landing.Y), new(landing.X - 1, landing.Y), new(landing.X, landing.Y + 1), new(landing.X, landing.Y - 1) };
            var away = sides.Where(t => t.X >= 0 && t.Y >= 0 && t.X < size.Width && t.Y < size.Height
                && map.IsWalkable(t) && !map.IsWarp(t) && !s.Entities.Values.Any(e => e.Blocks && e.Position == t))
                .OrderBy(t => Math.Abs(t.X - size.Width / 2) + Math.Abs(t.Y - size.Height / 2)).FirstOrDefault();
            if (away is null) throw new QuestBlockedException("No clear inward wolf-map tile. Arrival remains pending.");
            await c.BeginAsync("wolf-inward", reason: "Leaving the actual wolf-area landing");
            await c.SaveAsync(c.Job with { Wolf = p with { Landing = landing, Away = away, EntryGeneration = s.Generation } });
            await c.Session.ExecuteAsync(new("navigate", WolfQuestEvidence.WolfMap, away, PreservePopup: true, ExpectedGeneration: s.Generation), ct);
        }
        p = c.Job.Wolf!;
        bool Stable(CharacterSnapshot a, Tile? target) => a.Generation == p.EntryGeneration && a.Map == WolfQuestEvidence.WolfMap && a.Position == target;
        if (p.Interior is null)
        {
            if (!Stable(c.Session.Snapshot, p.Away))
                throw new QuestBlockedException("Wolf-area inward arrival is uncertain. Reconcile it before another move.");
            var interior = Enumerable.Range(0, size.Width).SelectMany(x => Enumerable.Range(0, size.Height).Select(y => new Tile(x, y)))
                .OrderBy(t => Math.Abs(t.X - size.Width / 2) + Math.Abs(t.Y - size.Height / 2))
                .FirstOrDefault(t => map.IsWalkable(t) && !map.IsWarp(t) && t != p.Landing
                    && Math.Abs(t.X - p.Landing!.X) + Math.Abs(t.Y - p.Landing.Y) >= 3 && map.PathLength(p.Away!, t) > 0);
            if (interior is null) throw new QuestBlockedException("No observed reachable interior approach. Entry remains pending.");
            await c.BeginAsync("wolf-interior", reason: "Continuing to the wolf clearing before pausing");
            await c.SaveAsync(c.Job with { Wolf = p with { Interior = interior } });
            await c.Session.ExecuteAsync(new("navigate", WolfQuestEvidence.WolfMap, interior, PreservePopup: true,
                ExpectedGeneration: p.EntryGeneration), ct);
            p = c.Job.Wolf!;
        }
        if (!Stable(c.Session.Snapshot, p.Interior))
            throw new QuestBlockedException("Wolf-clearing arrival is uncertain. Reconcile it before another move.");
        if (c.Session.Snapshot.Dialog is not null) throw new QuestBlockedException("An entry popup remains open; preserve it for review.");
        await c.SaveAsync(c.Job with { Pending = null, Wolf = p with { EntrySettled = true, LastPage = null, SubmittedPage = null } });
    }

    async Task Hunt(QuestContext c, CancellationToken ct)
    {
        var p = c.Job.Wolf!;
        if (p.Defeat is not null) return;
        if (p.Briefed && p.EntrySettled && c.Job.Pending is null && p.HuntGeneration is not null
            && WolfQuestEvidence.Defeat(c.Session.Snapshot, p) is { } observed)
        {
            await c.SaveAsync(c.Job with { Wolf = p with { Defeat = observed } });
            return;
        }
        if (!p.Briefed || !p.EntrySettled || c.Job.Pending is not null || c.Session.Snapshot.Map != WolfQuestEvidence.WolfMap)
            throw new QuestBlockedException("A reconciled wolf-map entry is required before hunting.");
        if (p.HuntGeneration is null)
        {
            p = p with { HuntGeneration = c.Session.Snapshot.Generation, HuntRevision = c.Session.Snapshot.Revision };
            await c.SaveAsync(c.Job with { Wolf = p });
        }
        else if (p.HuntGeneration != c.Session.Snapshot.Generation)
            throw new QuestBlockedException("Interrupted wolf hunt has no reconciled kill receipt. Do not infer a kill from reconnect state.");
        OwnMonsterDefeat? found = WolfQuestEvidence.Defeat(c.Session.Snapshot, p);
        if (found is null)
        {
            if (c.Session is not ICombatMap map || c.Session.Snapshot.MaxHp is not > 0 || c.Session.Snapshot.Dialog is not null)
                throw new QuestBlockedException("Actual combat map, known maximum health and no popup are required to hunt the wolf.");
            // Retain a receipt even if disconnect/cancellation follows the killing blow.
            void Observe(CharacterSnapshot s) => found ??= WolfQuestEvidence.Defeat(s, p);
            c.Session.Changed += Observe;
            Observe(c.Session.Snapshot);
            try
            {
                using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
                bounded.CancelAfter(TimeSpan.FromMinutes(5));
                await MeleeFight.RunAsync(c.Session, map, new()
                {
                    PriorityOnly = true, PrioritySprites = [WolfQuestEvidence.WolfSprite], AvoidStrangerMobs = true,
                    IsFriend = c.Hunting?.IsFriend,
                    Note = line => c.Hunting?.Log?.Invoke(c.Job.Character, line),
                    Activity = a => c.Hunting?.Activity?.Invoke(c.Job.Character, a)
                }, s => found is not null || s.Map != WolfQuestEvidence.WolfMap || s.Dialog is not null,
                    TimeSpan.FromSeconds(30), bounded.Token, roam: true);
            }
            finally
            {
                c.Session.Changed -= Observe;
                if (found is not null) await c.SaveAsync(c.Job with { Wolf = c.Job.Wolf! with { Defeat = found } });
            }
        }
        if (found is null) throw new QuestBlockedException("No own Wolf kill receipt. Hunt paused without claiming completion.");
        await c.SaveAsync(c.Job with { Wolf = c.Job.Wolf! with { Defeat = found } });
    }

    async Task Confirm(QuestContext c, CancellationToken ct)
    {
        if (c.Job.CompletedAt is null)
        {
            var p = c.Job.Wolf!;
            if (p.TurnIn is not { } baseline) throw new QuestBlockedException("Saved Throttler reward baseline is required.");
            if (c.Session.Snapshot.Generation != baseline.Generation)
                throw new QuestBlockedException("Unresolved wolf reward belongs to an earlier connection. New inventory cannot prove the old claim.");
            await ObserveMessages(c);
            await RefreshRewardLegend(c, ct);
            if (!Reward(c, c.Session.Snapshot))
                await c.Session.WaitAsync(s => Reward(c, s), baseline.Revision, Timeout, ct);
            Own(c);
            await ObserveMessages(c);
            var s = c.Session.Snapshot;
            var rewards = new Dictionary<string, int>
            {
                ["experience"] = s.Experience is { } value && value >= (ulong)baseline.Experience + WolfQuestEvidence.Experience && value - baseline.Experience <= int.MaxValue
                    ? checked((int)(value - baseline.Experience)) : (int)WolfQuestEvidence.Experience
            };
            foreach (var potion in WolfQuestEvidence.Potions)
            {
                int gain = s.Count(potion) - baseline.InventoryCounts.GetValueOrDefault(potion);
                if (gain > 0)
                    rewards["item:" + potion] = gain;
            }
            await c.SaveAsync(c.Job with { CompletedAt = DateTimeOffset.UtcNow, Repeat = false, NextRun = null, LegendReward = true,
                CompletionText = WolfQuestEvidence.Legend, CompletionDialog = c.Job.Wolf!.LastPage, Rewards = rewards, Pending = null,
                Route = new() { CleanupPending = s.Dialog is not null } });
        }
        if (c.Job.Route?.CleanupPending != true) return;
        var current = c.Session.Snapshot;
        if (current.Dialog is { } page)
        {
            var receipt = c.Job.CompletionDialog;
            if (!SamePage(page, receipt) || !Throttler(current, page) || !WolfQuestEvidence.RewardTerminal(page))
                throw new QuestBlockedException("Rewards verified. A different or nonterminal popup is preserved; no reward claim will be repeated.");
            var after = current.Revision;
            await c.Session.ExecuteAsync(new("close", Dialog: page, ExpectedGeneration: page.Generation,
                ExpectedDialogRevision: page.Revision, ExpectedPageText: page.Text), ct);
            if (c.Session.Snapshot.Dialog is not null)
                await c.Session.WaitAsync(s => s.Generation == page.Generation && s.Dialog is null, after, Timeout, ct);
        }
        await c.SaveAsync(c.Job with { Pending = null, Route = c.Job.Route! with { CleanupPending = false } });
    }
}
