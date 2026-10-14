using System.Collections.Concurrent;
namespace DAQuester.Core;

static partial class ObservedJourney
{
    static readonly TimeSpan ProofTimeout = TimeSpan.FromSeconds(10);
    // Capture pre-action resources in the same connection. Reconnect balances
    // cannot tell whether an earlier payment or delivery committed.
    static Transaction Baseline(CharacterSnapshot s) => new("observed-stage", s.Generation, s.Revision,
        s.Gold ?? throw new QuestBlockedException("Known gold required."), 0,
        s.Experience ?? throw new QuestBlockedException("Known XP required."), DateTimeOffset.UtcNow)
    {
        InventoryBaselineKnown = s.InventoryKnown,
        InventoryCounts = s.Inventory.Values.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => checked((int)g.Sum(i => (long)i.Quantity)), StringComparer.OrdinalIgnoreCase),
        Legends = s.Legends.ToArray()
    };

    static bool Matches(QuestContext c, Dialog d, ObservedQuestPage page, uint entity, long generation) =>
        d.Generation == generation && d.EntityId == entity && d.EntityType == 1 &&
        d.Name.Equals(page.Speaker == "$character" ? c.Job.Character : page.Speaker, StringComparison.OrdinalIgnoreCase) &&
        (page.Root && !d.IsPursuit || d.Text == page.Text) &&
        (page.Kind != "choose" || d.Choices.Any(x => x.Text == page.Choice)) &&
        (page.Kind != "next" || d.IsPursuit && d.HasNext && d.Choices.Length == 0 && d.Prompt is null);

    static bool ResourcesMatch(CharacterSnapshot s, Transaction baseline, ObservedQuestTask task)
    {
        // Require exact combined deltas: dialogue, XP, gold and items can arrive
        // in different packets, and none alone proves the full stage outcome.
        if (!s.Connected || s.Generation != baseline.Generation || !s.InventoryKnown || !baseline.InventoryBaselineKnown ||
            baseline.Gold < task.Cost || s.Gold != baseline.Gold - task.Cost ||
            (long)(s.Experience ?? 0) - baseline.Experience != task.Experience)
            return false;
        var outputs = RewardOutputs(s, baseline, task);
        if (outputs is null)
            return false;
        foreach (string name in task.Inputs.Select(x => x.Item).Concat(outputs.Select(x => x.Item)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            int expected = baseline.InventoryCounts.GetValueOrDefault(name) - task.Inputs.Where(x => x.Item.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count)
                + outputs.Where(x => x.Item.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count);
            if (s.Count(name) != expected)
                return false;
        }
        return true;
    }

    static InventoryGoal[]? RewardOutputs(CharacterSnapshot s, Transaction baseline, ObservedQuestTask task)
    {
        // The shared Danaan recipe accepts exactly one newly delivered family
        // item. VerifyRewardAsync also requires its matching fresh own legend.
        if (task.Id != "erin-reward")
            return task.Outputs;
        var delivered = new[] { "Cap of Danaan", "Band of Danaan" }.Where(n => s.Count(n) - baseline.InventoryCounts.GetValueOrDefault(n) == 1).ToArray();
        return delivered.Length == 1 ? [new(delivered[0], 1)] : null;
    }

    static async Task RequireResourcesAsync(QuestContext c, Transaction baseline, ObservedQuestTask task, CancellationToken ct)
    {
        if (!ResourcesMatch(c.Session.Snapshot, baseline, task))
            await c.Session.WaitAsync(s => ResourcesMatch(s, baseline, task), baseline.Revision, ProofTimeout, ct);
    }

    static bool IsRewardMessage(string text, uint expected) => text == expected + " experience!" ||
        text == "You gain " + expected.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " experience!";

    static async Task DialogueAsync(QuestContext c, QuestDefinition recipe, ObservedQuestTask task, CancellationToken ct)
    {
        if (task.Pages.Length == 0)
            throw new QuestBlockedException("Observed conversation pages missing: " + task.Id);
        if (DanaanTransport.CanResumeReturn(recipe, c.Job, c.Session.Snapshot))
        {
            // This is a recorded read-only transport, never a balance-based
            // inference about an earlier connection's payment or handoff.
            var arrived = c.Session.Snapshot;
            await AwayAsync(c, task.ArrivalAway.GetValueOrDefault(arrived.Map!.Value) ?? task.Away, arrived.Map.Value, ct);
            await c.SaveAsync(c.Job with
            {
                Pending = null,
                Route = c.Job.Route! with
                {
                    Page = task.Pages.Length,
                    SubmittedPage = null
                },
                Reason = "Recorded Dreamer redirect and own hometown arrival reconciled"
            });
            return;
        }
        if (c.Job.Route?.CleanupPending == true)
        {
            await FinishCleanupAsync(c, task, ct);
            return;
        }
        var route = c.Job.Route ?? new();
        if (c.Job.Pending?.Kind == "observed-wait-close")
        {
            if (c.Session.Snapshot.Dialog is not null)
                throw new QuestBlockedException("Wait refusal close is not confirmed; inspect it before another check.");
            await c.SaveAsync(c.Job with
            {
                Pending = null
            });
        }
        // Travel and opening a conversation may expose an unfamiliar variant.
        // Bind the first captured page before saving any spending baseline.
        if (route.Baseline is null)
        {
            if (c.Job.Pending is not null && !c.Job.Pending.Kind.StartsWith("observed-access:", StringComparison.Ordinal))
                throw new QuestBlockedException("Reconcile the saved submission before opening another conversation.");
            foreach (var item in task.Inputs)
                if (c.Session.Snapshot.Count(item.Item) < item.Count)
                    throw new QuestBlockedException("Carry " + item.Count + " " + item.Item + " before " + task.Npc + "'s handoff.");
            if (c.Session.Snapshot.Gold < task.Cost)
                throw new QuestBlockedException("Carry " + task.Cost.ToString("N0") + " gold for " + task.Npc + ".");
            int slots = task.Outputs.Count(g => c.Session.Snapshot.Count(g.Item) == 0);
            if (c.Session.Snapshot.Inventory.Count + slots > 59 || c.Session.Snapshot.Weight + task.WeightReserve > c.Session.Snapshot.MaxWeight && task.Outputs.Length > 0)
                throw new QuestBlockedException("Reserve carrying space for " + task.Npc + "'s observed reward.");
            var current = c.Session.Snapshot;
            if (current.Dialog is { IsPursuit: true } foreign && !foreign.Name.Equals(task.Npc, StringComparison.OrdinalIgnoreCase) && !IsAccessDialog(current, task))
                throw new QuestBlockedException("Another quest popup is open; it has been preserved.");
            await PrepareTravelAsync(c, task, ct);
            current = c.Session.Snapshot;
            if (task.Speech is not null)
            {
                await c.Session.ExecuteAsync(new("navigate", task.Map, task.Approach, ExpectedGeneration: current.Generation), ct);
                await c.Session.ExecuteAsync(new("say", Text: task.Speech, ReplyFrom: task.Npc, ExpectedGeneration: current.Generation), ct);
            }
            else if (current.Map != task.Map || current.Dialog is not { } existing || !existing.Name.Equals(task.Npc, StringComparison.OrdinalIgnoreCase) ||
                !Matches(c, existing, task.Pages[0], existing.EntityId, current.Generation))
                await c.Session.ExecuteAsync(new("interact", task.Map, task.Approach, task.Npc, ExpectedGeneration: current.Generation), ct);
            var opened = c.Session.Snapshot;
            var first = opened.Dialog ?? throw new QuestBlockedException("No fresh " + task.Npc + " quest page.");
            if (opened.Map != task.Map || !Matches(c, first, task.Pages[0], first.EntityId, opened.Generation))
                throw new QuestBlockedException("Unverified " + task.Npc + " route: " + first.Text);
            route = route with
            {
                Baseline = Baseline(opened),
                BoundGeneration = opened.Generation,
                BoundEntity = first.EntityId,
                LastReply = first
            };
            await c.SaveAsync(c.Job with
            {
                Route = route
            });
        }
        var baseline = route.Baseline!;
        if (route.BoundGeneration != c.Session.Snapshot.Generation && c.Job.Route?.CleanupPending != true)
            throw new QuestBlockedException("Conversation connection changed; reconcile its saved receipt before reopening or spending again.");
        if (c.Job.Pending is not null)
            await ReconcilePageAsync(c, recipe, task, ct);

        while (true)
        {
            task = JourneyFor(c, recipe)[c.Job.Route!.Task];
            if (c.Job.Route.Page >= task.Pages.Length)
                break;
            ct.ThrowIfCancellationRequested();
            route = c.Job.Route!;
            var page = task.Pages[route.Page];
            var s = c.Session.Snapshot;
            if (IsArrival(task, s))
            {
                await RequireResourcesAsync(c, baseline, task, ct);
                await AwayAsync(c, task.ArrivalAway.GetValueOrDefault(s.Map!.Value) ?? task.Away, s.Map.Value, ct);
                return;
            }
            var d = s.Dialog ?? throw new QuestBlockedException("The expected " + task.Npc + " page closed before its next captured action; no submission repeated.");
            if (!Matches(c, d, page, route.BoundEntity!.Value, route.BoundGeneration!.Value))
                throw new QuestBlockedException("Unverified " + task.Npc + " page or choices: " + d.Text);
            if (page.Kind == "close" && task.Experience > 0)
                await VerifyRewardAsync(c, task, baseline, d, ct);
            if (page.Kind == "close" && d.Text == FiskEvidence.BouquetAccepted)
            {
                // Save the paid wait before dismissing its acceptance. A crash
                // after this point must preserve the flowers and gold already spent.
                await RequireResourcesAsync(c, baseline, task, ct);
                var at = d.ObservedAt ?? throw new QuestBlockedException("Bouquet acceptance timestamp is unknown.");
                await c.SaveAsync(c.Job with
                {
                    FiskWait = new(at, d, baseline.Gold, c.Session.Snapshot.Gold!.Value, true),
                    NextRun = at.AddHours(1),
                    Accepted = true
                });
            }
            await SendPageAsync(c, recipe, task, page, d, ct);
        }
        await RequireResourcesAsync(c, baseline, task, ct);
        if (task.Experience > 0)
        {
            if (c.Job.CompletedAt is null)
                await VerifyRewardAsync(c, task, baseline, c.Job.Route?.LastReply, ct);
            if (c.Session.Snapshot.Dialog is not null)
                throw new QuestBlockedException("Final reward popup remains open; resume its cleanup without another claim.");
            await c.SaveAsync(c.Job with
            {
                Step = "complete",
                Status = "Completed",
                Pending = null,
                NextRun = null,
                Repeat = false,
                Route = c.Job.Route! with
                {
                    CleanupPending = false
                },
                Reason = "Rewards, own legend and final popup closure verified"
            });
        }
    }

    static async Task SendPageAsync(QuestContext c, QuestDefinition recipe, ObservedQuestTask task, ObservedQuestPage page, Dialog d, CancellationToken ct)
    {
        // Observe asynchronous replies during submission, including reward notices
        // that precede the final item packet. The pending action remains durable.
        var route = c.Job.Route!;
        var notices = new ConcurrentQueue<string>();
        DateTimeOffset? redirect = null;
        void Interrupted(string reason)
        {
            if (reason == DanaanTransport.RedirectReason && page.Kind == "choose" && page.Choice == "Wait, I have more questions!" && DanaanTransport.IsReturn(task, d))
                redirect = DateTimeOffset.UtcNow;
        }
        Dialog? reply = route.LastReply;
        void Observe(CharacterSnapshot s)
        {
            if (s.Generation != d.Generation)
                return;
            if (s.Dialog is { } seen && seen.EntityId == d.EntityId && seen.Revision > d.Revision)
                reply = seen;
            if (s.Notice is { Type: 3 } n && n.Generation == d.Generation && n.Revision > d.Revision && task.Experience > 0 && IsRewardMessage(n.Text, task.Experience))
                notices.Enqueue(n.Text);
        }
        c.Session.Changed += Observe;
        c.Session.Interrupted += Interrupted;
        try
        {
            await c.SaveAsync(c.Job with
            {
                Route = route with
                {
                    SubmittedPage = d
                }
            });
            await c.BeginAsync("observed-page:" + task.Id, d.EntityId, observedDialog: d);
            await c.Session.ExecuteAsync(new(page.Kind, Text: page.Choice, Dialog: d, ExpectedGeneration: d.Generation, ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
            Observe(c.Session.Snapshot);
            await c.SaveAsync(c.Job with
            {
                Route = c.Job.Route! with
                {
                    LastReply = reply,
                    RedirectObservedAt = redirect ?? c.Job.Route.RedirectObservedAt,
                    RewardMessages = c.Job.Route.RewardMessages.Concat(notices).Distinct().ToArray()
                }
            });
            await ReconcilePageAsync(c, recipe, task, ct);
        }
        catch
        {
            await c.SaveAsync(c.Job with
            {
                Route = c.Job.Route! with
                {
                    LastReply = reply,
                    RedirectObservedAt = redirect ?? c.Job.Route.RedirectObservedAt,
                    RewardMessages = c.Job.Route.RewardMessages.Concat(notices).Distinct().ToArray()
                }
            });
            throw;
        }
        finally { c.Session.Changed -= Observe; c.Session.Interrupted -= Interrupted; }
    }

    static async Task ReconcilePageAsync(QuestContext c, QuestDefinition recipe, ObservedQuestTask task, CancellationToken ct)
    {
        var pending = c.Job.Pending ?? throw new QuestBlockedException("No submitted page receipt to reconcile.");
        var route = c.Job.Route!;
        if (pending.Kind != "observed-page:" + task.Id || route.SubmittedPage is not { } submitted)
            throw new QuestBlockedException("A different submission is pending; inspect it before continuing.");
        var s = c.Session.Snapshot;
        bool cleanup = route.CleanupPending && task.Pages[route.Page].Kind == "close";
        if (!s.Connected || pending.Generation != s.Generation && !cleanup)
            throw new QuestBlockedException("Pending page belongs to an earlier connection; no item or payment is inferred from new-session balances.");
        // Plain choose sends once without awaiting the server. Wait for its
        // first actual reply while preserving the write-ahead transaction;
        // unrelated state updates cannot authorize the next page or a retry.
        if (s.Dialog is { } current && current.Revision <= submitted.Revision)
        {
            await c.Session.WaitAsync(observed => !observed.Connected || observed.Generation != pending.Generation ||
                observed.Dialog is null || observed.Dialog.Revision > submitted.Revision ||
                task.ArrivalMap is { } destination && observed.Map == destination && observed.Position is not null,
                pending.Revision, ProofTimeout, ct);
            await ReconcilePageAsync(c, recipe, task, ct);
            return;
        }
        task = await SelectObservedVariantAsync(c, recipe, task, submitted);
        if (task.Id == "bouquet-collection" && s.Dialog is { Name: "Viveka", Text: FiskEvidence.BouquetNotReady, Choices.Length: 0, HasNext: false } waiting &&
            waiting.Generation == pending.Generation && waiting.EntityId == submitted.EntityId && waiting.Revision > submitted.Revision &&
            c.Job.FiskWait is { Verified: true } accepted && s.Gold == pending.Gold && s.Experience == pending.Experience &&
            s.InventoryKnown && s.Count(FiskEvidence.Bouquet) == pending.InventoryCounts.GetValueOrDefault(FiskEvidence.Bouquet))
        {
            var due = DateTimeOffset.UtcNow.AddMinutes(1);
            if (due < accepted.Due)
                due = accepted.Due;
            await c.SaveAsync(c.Job with
            {
                Pending = null,
                NextRun = due,
                Route = new()
                {
                    Task = route.Task
                },
                Reason = "Viveka is still preparing the bouquet; another check scheduled"
            });
            await c.BeginAsync("observed-wait-close", waiting.EntityId, observedDialog: waiting);
            await c.Session.ExecuteAsync(new("close", Dialog: waiting, ExpectedGeneration: waiting.Generation, ExpectedDialogRevision: waiting.Revision), ct);
            if (c.Session.Snapshot.Dialog is not null)
                throw new QuestBlockedException("Wait refusal popup close is uncertain.");
            await c.SaveAsync(c.Job with
            {
                Pending = null
            });
            await WaitAsync(c, ct);
            throw new CollectionCheckAgainException();
        }
        bool transported = IsArrival(task, s);
        int next = route.Page + 1;
        bool receipt = cleanup && s.Dialog is null || transported ||
            next == task.Pages.Length && s.Dialog is null ||
            next < task.Pages.Length && s.Dialog is { } d && d.Revision > submitted.Revision && Matches(c, d, task.Pages[next], submitted.EntityId, submitted.Generation);
        if (!receipt)
            throw new QuestBlockedException("The submitted " + task.Npc + " page has no matching later receipt. Inspect it; never repeat its choice or handoff.");
        await c.SaveAsync(c.Job with
        {
            Pending = null,
            Route = route with
            {
                Page = transported ? task.Pages.Length : next,
                SubmittedPage = null,
                LastReply = s.Dialog ?? route.LastReply
            }
        });
        if (transported)
        {
            await RequireResourcesAsync(c, route.Baseline!, task, ct);
            await AwayAsync(c, task.ArrivalAway.GetValueOrDefault(s.Map!.Value) ?? task.Away, s.Map.Value, ct);
        }
    }

    static async Task VerifyRewardAsync(QuestContext c, ObservedQuestTask task, Transaction baseline, Dialog? final, CancellationToken ct)
    {
        // Numeric XP is insufficient: correlate the active notice, actual item
        // delta and a newly inspected own legend before saving completion proof.
        await RequireResourcesAsync(c, baseline, task, ct);
        if (!c.Job.Route!.RewardMessages.Any(m => IsRewardMessage(m, task.Experience)))
            throw new QuestBlockedException("Numeric XP changed but its correlated active reward message is missing; no claim repeated.");
        await c.Session.ExecuteAsync(new("inspect-self", ExpectedGeneration: baseline.Generation), ct);
        var s = c.Session.Snapshot;
        if (!s.LegendsKnown || s.LegendsRevision <= baseline.Revision || Mark(c) is not { } mark || baseline.Legends.Contains(mark))
            throw new QuestBlockedException("Fresh matching own completion legend is required; reward claim stays committed.");
        var outputs = RewardOutputs(s, baseline, task) ?? throw new QuestBlockedException("Actual Danaan item receipt is ambiguous.");
        if (task.Id == "erin-reward" && !mark.StartsWith("Worthy of the " + outputs[0].Item, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Own worthy legend does not match the actual Danaan item; no claim repeated.");
        var rewards = new Dictionary<string, int>(c.Job.Rewards) { ["experience"] = checked((int)task.Experience) };
        foreach (var output in outputs)
            rewards["item:" + output.Item] = output.Count;
        await c.SaveAsync(c.Job with
        {
            CompletedAt = final?.ObservedAt ?? DateTimeOffset.UtcNow,
            CompletionDialog = final,
            CompletionLegend = mark,
            CompletionText = mark,
            Rewards = rewards,
            Repeat = false,
            NextRun = null,
            Route = c.Job.Route! with
            {
                CleanupPending = true
            }
        });
    }

    static async Task AwayAsync(QuestContext c, Tile? away, int map, CancellationToken ct)
    {
        var s = c.Session.Snapshot;
        if (away is null || s.Position == away)
            return;
        if (s.Position is not { } at || Math.Abs(at.X - away.X) + Math.Abs(at.Y - away.Y) != 1)
            throw new QuestBlockedException("Transport landing differs from the observed inward step; inspect before walking.");
        await c.Session.ExecuteAsync(new("step", map, away, ExpectedGeneration: s.Generation), ct);
        var arrived = c.Session.Snapshot;
        if (!arrived.Connected || arrived.Generation != s.Generation || arrived.Map != map || arrived.Position != away)
            throw new QuestBlockedException("The inward step has no authoritative arrival; inspect before continuing.");
    }

    static async Task FinishCleanupAsync(QuestContext c, ObservedQuestTask task, CancellationToken ct)
    {
        // Rewards were verified and persisted before close. A new connection only
        // reconciles the own mark and presentation, never the old payment/XP delta.
        var route = c.Job.Route!;
        if (c.Job.CompletedAt is null || c.Job.CompletionLegend is null ||
            c.Job.Rewards.GetValueOrDefault("experience") != task.Experience ||
            !route.RewardMessages.Any(m => IsRewardMessage(m, task.Experience)))
            throw new QuestBlockedException("Saved final reward receipt is incomplete; no new claim will be sent.");
        if (!CharacterMemory.FreshLegends(c.Session.Snapshot))
            await c.Session.ExecuteAsync(new("inspect-self"), ct);
        if (Mark(c) != c.Job.CompletionLegend)
            throw new QuestBlockedException("Fresh own completion mark differs from the saved outcome.");
        if (c.Session.Snapshot.Dialog is { } d)
        {
            var terminal = task.Pages.LastOrDefault(p => p.Kind == "close");
            if (terminal is null || d.Name != task.Npc || d.Text != terminal.Text || !d.IsPursuit || d.HasNext || d.Choices.Length > 0 || d.Prompt is not null)
                throw new QuestBlockedException("Another popup is open after the verified reward; it is preserved.");
            if (c.Job.Pending is not null && c.Job.Pending.Generation == d.Generation)
                throw new QuestBlockedException("Final close still has no receipt. Await or inspect it; the reward will not be reclaimed.");
            await c.SaveAsync(c.Job with
            {
                Pending = null
            });
            await c.BeginAsync("observed-cleanup:" + task.Id, d.EntityId, observedDialog: d);
            await c.Session.ExecuteAsync(new("close", Dialog: d, ExpectedGeneration: d.Generation, ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
            if (c.Session.Snapshot.Dialog is not null)
                throw new QuestBlockedException("Final popup close is not confirmed.");
        }
        await c.SaveAsync(c.Job with
        {
            Step = "complete",
            Status = "Completed",
            Pending = null,
            NextRun = null,
            Repeat = false,
            Route = route with
            {
                CleanupPending = false,
                Page = task.Pages.Length,
                SubmittedPage = null
            },
            Reason = "Verified rewards retained; final popup closed"
        });
    }
}

sealed class CollectionCheckAgainException : Exception
{
}
