using static DAQuester.Core.SickChildEvidence;
namespace DAQuester.Core;

public sealed class SickChildActions(PaulinFlower? flower = null) : IQuestAction
{
    readonly PaulinFlower f = flower ?? Sevti;
    public static TimeSpan SpotPoll { get; set; } = TimeSpan.FromSeconds(10);
    public static TimeSpan RetryRest { get; set; } = TimeSpan.FromSeconds(15);
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        if (c.Session.Snapshot.Name is not { } name || !name.Equals(c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException(f.QuestName + " character changed; resume on the saved character.");
        var flow = new Flow(c, f);
        if (c.Job.Pending is not null)
            await flow.Reconcile(ct);
        if (!c.Session.Snapshot.LegendsKnown)
            await c.Session.ExecuteAsync(new("inspect-self"), ct);
        if (Mark(c.Session.Snapshot, f.Legend) is { } mark)
        {
            await flow.Complete(mark, "Own legend verified");
            return;
        }
        if (Lockout(f.Id, c.Session.Snapshot.Legends) is { } locked)
            throw new QuestBlockedException(locked);
        var options = c.Job.SickChildOptions ?? throw new QuestBlockedException("Choose how to get the flowers before starting.");
        options.Validate();
        switch (step.Action)
        {
            case "sick-preflight":
                var gate = Eligibility(c.Session.Snapshot, c.Job, f);
                if (gate.Kind != EligibilityKind.Eligible)
                    throw new QuestBlockedException(gate.Reason);
                return;
            case "sick-run":
                await flow.Run(options, ct);
                return;
            default:
                throw new QuestBlockedException("Unsupported " + f.QuestName + " action: " + step.Action);
        }
    }

    sealed class Flow(QuestContext c, PaulinFlower f)
    {
        static readonly TimeSpan reply = TimeSpan.FromSeconds(5), proof = TimeSpan.FromSeconds(10);
        // Helpers pick in parallel; every journal change goes through one gate.
        readonly SemaphoreSlim gate = new(1, 1);
        string Quester => c.Job.Character;
        // A pick recorded by either flower quest counts toward that character's one flower a day.
        IEnumerable<SickChildPick> Picks => c.Job.SickChildPicks.Concat(c.Helpers?.SharedPicks ?? []);
        async Task RecordPick(SickChildPick pick)
        {
            await Update(j => j with { SickChildPicks = [.. j.SickChildPicks, pick] });
            c.Helpers?.RecordPick(pick);
        }
        async Task Update(Func<QuestJob, QuestJob> change)
        {
            await gate.WaitAsync();
            try
            {
                await c.SaveAsync(change(c.Job));
            }
            finally { gate.Release(); }
        }
        async Task Begin(uint paulin)
        {
            await gate.WaitAsync();
            try
            {
                await c.BeginAsync("sick-turnin", paulin, "Giving Paulin a " + f.Name + " Blossom");
            }
            finally { gate.Release(); }
        }

        public async Task Run(SickChildOptions options, CancellationToken ct)
        {
            var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (Remaining(c.Job) == 0)
                {
                    await VerifyLegend(ct);
                    return;
                }
                if (c.Session.Snapshot.Count(f.Item) > 0)
                {
                    await TurnIn(null, ct);
                    continue;
                }
                var now = DateTimeOffset.UtcNow;
                bool own = CanPick(Picks, Quester, now);
                if (options.Mode == SickChildOptions.WithHelpers)
                {
                    // Helpers already holding a flower go first; they skip straight to the trade.
                    var pool = options.Helpers.Where(h => !tried.Contains(h) && (HoldsFlower(c.Job, h) || CanPick(Picks, h, now))).OrderBy(h => HoldsFlower(c.Job, h) ? 0 : 1).ToList();
                    if (pool.Count > 0)
                    {
                        await Round(pool, own, tried, ct);
                        continue;
                    }
                }
                if (own)
                {
                    if (await PickRetrying(c.Session, Quester, ct))
                        await TurnIn(null, ct);
                    continue;
                }
                var waits = new[] { Quester }.Concat(options.Mode == SickChildOptions.WithHelpers ? options.Helpers : []).Select(n => NextPick(Picks, n)).OfType<DateTimeOffset>().ToArray();
                await Wait(waits.Length > 0 ? waits.Min() : now + PickInterval, ct);
                tried.Clear();
            }
        }
        async Task Wait(DateTimeOffset next, CancellationToken ct)
        {
            // Wait out the day in Paulin's room, not at the flower or in the swamp.
            await Travel(c.Session, PaulinMap, QuesterTile, 1, ct);
            await Update(j => j with { Status = "Scheduled", NextRun = next, Reason = "Next flower after " + next.ToLocalTime().ToString("g") });
            var delay = next - DateTimeOffset.UtcNow;
            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException) { await Update(j => j with { NextRun = null }); throw; } // The plan ends with this run.
            await Update(j => j with { Status = "Running", NextRun = null, Reason = "" });
        }

        sealed record Fetched(string Name, IQuestHelper? Helper, bool LoginFailed);
        // Keeps one flower in flight per flower still needed; a flower that falls through is replaced at once.
        async Task Round(List<string> pool, bool own, HashSet<string> tried, CancellationToken ct)
        {
            var hub = c.Helpers ?? throw new QuestBlockedException("Helpers can't be used in this run. Start the quest from DAQuester's window.");
            using var round = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var queue = new Queue<string>(pool);
            var retried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var failedLogins = new List<string>();
            var fetches = new List<Task<Fetched>>();
            var ready = new Queue<IQuestHelper>();
            Task<bool>? ownPick = own ? PickRetrying(c.Session, Quester, round.Token) : null;
            // Without a pick of her own the quester waits in Paulin's room, never out in the swamp.
            Task? toPaulin = own ? null : Travel(c.Session, PaulinMap, QuesterTile, 1, round.Token);
            int InFlight() => fetches.Count + ready.Count + (ownPick is null ? 0 : 1);
            void Fill()
            {
                while (InFlight() < Remaining(c.Job) && queue.Count > 0)
                {
                    var name = queue.Dequeue();
                    tried.Add(name);
                    fetches.Add(Fetch(hub, name, round.Token));
                }
            }
            try
            {
                Fill();
                while (ownPick is not null || fetches.Count > 0 || ready.Count > 0)
                {
                    // The quester trades only after their own pick, so the two never pull them in different directions.
                    if (ownPick is null && toPaulin is null && ready.Count > 0)
                    {
                        var helper = ready.Dequeue();
                        bool given = false;
                        try
                        {
                            await Travel(c.Session, PaulinMap, QuesterTile, 1, round.Token);
                            await Trade(hub, helper, round.Token);
                            await TurnIn(helper.Name, round.Token);
                            hub.Report(helper.Name, "Flower given to Paulin");
                            given = true;
                        }
                        finally { await Release(hub, helper, given); }
                        Fill();
                        continue;
                    }
                    var waits = fetches.Cast<Task>().ToList();
                    if (ownPick is not null)
                        waits.Add(ownPick);
                    if (toPaulin is not null)
                        waits.Add(toPaulin);
                    var finished = await Task.WhenAny(waits);
                    if (finished == toPaulin)
                    {
                        await toPaulin;
                        toPaulin = null;
                    }
                    // A picked flower goes straight to Paulin; a refusal leaves the flower spot for his room at once.
                    else if (finished == ownPick)
                    {
                        bool picked = await ownPick;
                        ownPick = null;
                        if (picked)
                            await TurnIn(null, round.Token);
                        else
                            toPaulin = Travel(c.Session, PaulinMap, QuesterTile, 1, round.Token);
                        Fill();
                    }
                    else
                    {
                        var fetch = (Task<Fetched>)finished;
                        fetches.Remove(fetch);
                        var result = await fetch;
                        if (result.Helper is { } arrived)
                            ready.Enqueue(arrived);
                        else
                        {
                            if (result.LoginFailed && retried.Add(result.Name))
                                failedLogins.Add(result.Name);
                            Fill();
                        }
                    }
                    // A helper whose login failed gets one more try once nothing else is in flight; a hand launch is picked up.
                    if (InFlight() == 0 && failedLogins.Count > 0 && Remaining(c.Job) > 0)
                    {
                        foreach (var name in failedLogins)
                            queue.Enqueue(name);
                        failedLogins.Clear();
                        Fill();
                    }
                    if (toPaulin is not null && InFlight() == 0)
                    {
                        await toPaulin;
                        toPaulin = null;
                    }
                }
            }
            finally
            {
                round.Cancel();
                // Fetch reports its own failures and never throws; unused helpers stay online where they are.
                foreach (var pending in fetches)
                if (await pending is { Helper: { } left })
                    await Release(hub, left, false);
                while (ready.Count > 0)
                    await Release(hub, ready.Dequeue(), false);
                // Already reported by the failure that ended the round.
                if (ownPick is not null)
                    try
                    {
                        await ownPick;
                    }
                    catch (Exception) { }
                if (toPaulin is not null)
                    try
                    {
                        await toPaulin;
                    }
                    catch (Exception) { }
            }
        }
        async Task<Fetched> Fetch(IQuestHelpers hub, string name, CancellationToken ct)
        {
            IQuestHelper? helper = null;
            try
            {
                hub.Report(name, "Logging in");
                helper = await hub.AcquireAsync(name, ct);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, helper.Released);
                var s = helper.Session;
                if (s.Snapshot.Count(f.Item) == 0)
                {
                    hub.Report(name, "Walking to the flower");
                    if (!await PickRetrying(s, name, linked.Token))
                    {
                        hub.Report(name, name + " can't pick a flower right now.");
                        await Update(j => j with { Reason = name + " can't pick a flower right now." });
                        await Release(hub, helper, true);
                        return new(name, null, false);
                    }
                }
                hub.Report(name, "Bringing the flower to " + Quester);
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        await Travel(s, PaulinMap, StagingTile, 3, linked.Token);
                        break;
                    }
                    catch (Exception e) when (attempt < 3 && Transient(e) && !linked.IsCancellationRequested) { hub.Report(name, "Retrying in 15 seconds after: " + e.Message); await Task.Delay(RetryRest, linked.Token); }
                }
                return new(name, helper, false);
            }
            catch (HelperLoginException e)
            {
                hub.Report(name, "Login failed: " + e.Message);
                return new(name, null, true);
            }
            catch (Exception e)
            {
                hub.Report(name, helper is not null && helper.Released.IsCancellationRequested ? "Stopped: you took over " + name + ", or it disconnected. Its window is left alone." : ct.IsCancellationRequested ? "Stopped with the quest." : "Stopped: " + e.Message);
                if (helper is not null)
                    await Release(hub, helper, false);
                return new(name, null, false);
            }
        }
        // Logging out only follows a traded flower or a refusal; a helper the player took over keeps its window.
        static async Task Release(IQuestHelpers hub, IQuestHelper helper, bool logOut)
        {
            try
            {
                if (logOut && helper.LoggedInForQuest)
                {
                    hub.Report(helper.Name, "Logging out");
                    await helper.LogOutAsync(CancellationToken.None);
                }
            }
            catch (Exception e) { hub.Report(helper.Name, "Couldn't log out: " + e.Message); }
            finally { await helper.DisposeAsync(); }
        }

        async Task Note(string who, string status)
        {
            if (who.Equals(Quester, StringComparison.OrdinalIgnoreCase))
                await Update(j => j with { Reason = status });
            else
                c.Helpers?.Report(who, status);
        }
        static bool Transient(Exception e) => e is TimeoutException || e is QuestBlockedException and not HelperLoginException and not ResourceUnavailableException;
        // Walking hiccups near a crowded flower are retried after a short rest; refusals and stops never are.
        async Task<bool> PickRetrying(IGameSession s, string who, CancellationToken ct)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await Pick(s, who, ct);
                }
                catch (Exception e) when (attempt < 3 && Transient(e) && !ct.IsCancellationRequested) { await Note(who, "Retrying in 15 seconds after: " + e.Message); await Task.Delay(RetryRest, ct); }
            }
        }
        async Task<bool> Pick(IGameSession s, string who, CancellationToken ct)
        {
            if (s.Snapshot.Count(f.Item) > 0)
                return true;
            if (s.Snapshot.InventoryKnown && s.Snapshot.Inventory.Count >= 59)
                throw new QuestBlockedException(who + " needs a free inventory slot for the " + f.Item + ".");
            // A popup blocks movement, so close it before stepping off; stepping back on opens a fresh one.
            if (s.Snapshot.Dialog is { } open)
                await s.ExecuteAsync(new("close", Dialog: open), ct);
            await StepOff(s, ct);
            if (s.Snapshot.Map == FlowerMap && s.Snapshot.Position == f.Spot)
                throw new QuestBlockedException(who + " couldn't step off the flower spot.");
            var start = s.Snapshot;
            int had = start.Count(f.Item);
            await ReachFlower(s, who, ct);
            try
            {
                await s.WaitAsync(x => x.Map == FlowerMap && x.Dialog is { } d && IsFlowerPopup(d, f), start.Revision, reply, ct);
            }
            catch (TimeoutException) { throw new QuestBlockedException("The flower popup didn't open for " + who + " on the flower spot."); }
            for (int page = 0; page < 6; page++)
            {
                var snap = s.Snapshot;
                if (snap.Dialog is not { } d)
                    break;
                if (!IsFlowerPopup(d, f))
                    throw new QuestBlockedException("Unexpected popup at the flower for " + who + "; it is preserved: " + d.Text);
                if (d.Choices.Length > 0)
                {
                    if (!d.Choices.Any(x => x.Text == PickChoice))
                        throw new QuestBlockedException("The flower menu changed for " + who + "; nothing was picked.");
                    await s.ExecuteAsync(new("choose", Text: PickChoice, Dialog: d), ct);
                    try
                    {
                        await s.WaitAsync(x => x.InventoryKnown && x.Count(f.Item) > had, snap.Revision, reply, ct);
                    }
                    catch (TimeoutException) { throw new QuestBlockedException(who + " chose to pick the flower, but no " + f.Item + " arrived."); }
                    await RecordPick(new(who, DateTimeOffset.UtcNow, false));
                    if (s.Snapshot.Dialog is { } rest)
                        await s.ExecuteAsync(new("close", Dialog: rest), ct);
                    await StepOff(s, ct);
                    return true;
                }
                if (!d.HasNext)
                {
                    await s.ExecuteAsync(new("close", Dialog: d), ct);
                    break;
                }
                await s.ExecuteAsync(new("next", Dialog: d), ct);
            }
            if (s.Snapshot.Dialog is not null)
                throw new QuestBlockedException("The flower popup for " + who + " ran longer than expected; it is preserved.");
            try
            {
                await s.WaitAsync(x => IsPicked(x.Notice?.Text), start.Revision, reply, ct);
            }
            catch (TimeoutException) { throw new QuestBlockedException("The flower popup closed for " + who + " without a pick or a refusal message."); }
            await RecordPick(new(who, DateTimeOffset.UtcNow, true));
            await StepOff(s, ct);
            return false;
        }
        // The popup reopens on login while standing on the flower.
        async Task StepOff(IGameSession s, CancellationToken ct)
        {
            foreach (var tile in f.Sides)
            {
                if (s.Snapshot.Map != FlowerMap || s.Snapshot.Position != f.Spot)
                    return;
                if (s.Snapshot.Entities.Values.Any(e => e.Blocks && e.Id != s.Snapshot.Id && e.Position == tile))
                    continue;
                try
                {
                    await s.ExecuteAsync(new("step", Target: tile), ct);
                }
                catch (QuestBlockedException) { } // Try the next side; the next walk leaves anyway.
            }
        }
        // Onto the flower when it's free; beside it while someone stands there, checking every 10 seconds for 3 minutes.
        async Task ReachFlower(IGameSession s, string who, CancellationToken ct)
        {
            var giveUp = DateTimeOffset.UtcNow.AddMinutes(3);
            while (true)
            {
                await Travel(s, FlowerMap, f.Spot, 1, ct);
                var here = s.Snapshot;
                if (here.Map == FlowerMap && here.Position == f.Spot)
                    return;
                bool taken = here.Entities.Values.Any(e => e.Blocks && e.Id != here.Id && e.Position == f.Spot);
                if (!taken && here.Map == FlowerMap && here.Position is { } at && Math.Abs(at.X - f.Spot.X) + Math.Abs(at.Y - f.Spot.Y) == 1)
                {
                    try
                    {
                        await s.ExecuteAsync(new("step", Target: f.Spot), ct);
                    }
                    catch (QuestBlockedException) { }
                    if (s.Snapshot.Position == f.Spot)
                        return;
                }
                if (DateTimeOffset.UtcNow > giveUp)
                    throw new QuestBlockedException("The flower spot stayed occupied for 3 minutes, so " + who + " couldn't reach it.");
                await Note(who, "Waiting for the flower spot to clear");
                await Task.Delay(SpotPoll, ct);
            }
        }

        // The swamp can stun or blind for about 18 seconds; wait it out instead of failing the walk.
        static async Task Travel(IGameSession s, int map, Tile tile, int tolerance, CancellationToken ct)
        {
            for (int attempt = 0; ; attempt++)
            {
                long before = s.Snapshot.Revision;
                try
                {
                    await s.ExecuteAsync(new("navigate", map, tile, Tolerance: tolerance), ct);
                    return;
                }
                catch (QuestBlockedException) when (attempt < 4)
                {
                    if (!await Seen(s, x => IsHazard(x.Notice?.Text), before, TimeSpan.FromMilliseconds(50), ct))
                        throw;
                    await Seen(s, x => IsRecovery(x.Notice?.Text), before, TimeSpan.FromSeconds(30), ct);
                }
            }
        }
        static async Task<bool> Seen(IGameSession s, Func<CharacterSnapshot, bool> test, long after, TimeSpan wait, CancellationToken ct)
        {
            try
            {
                await s.WaitAsync(test, after, wait, ct);
                return true;
            }
            catch (TimeoutException) { return false; }
        }

        async Task Trade(IQuestHelpers hub, IQuestHelper helper, CancellationToken ct)
        {
            var q = c.Session;
            var h = helper.Session;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, helper.Released);
            var token = linked.Token;
            uint questerId = q.Snapshot.Id ?? throw new QuestBlockedException("Waiting for " + Quester + "'s identity before trading.");
            uint helperId = h.Snapshot.Id ?? throw new QuestBlockedException("Waiting for " + helper.Name + "'s identity before trading.");
            hub.Report(helper.Name, "Trading the flower to " + Quester);
            if (q.Snapshot.Map != PaulinMap)
                throw new QuestBlockedException(Quester + " must wait in Paulin's room before trading.");
            if (q.Snapshot.Dialog is { } qd)
                await q.ExecuteAsync(new("close", Dialog: qd), token);
            if (h.Snapshot.Dialog is { } hd)
                await h.ExecuteAsync(new("close", Dialog: hd), token);
            // Visit walks to a free tile beside the quester, the way it does beside an NPC.
            await h.ExecuteAsync(new("visit", PaulinMap, Text: Quester), token);
            var item = h.Snapshot.Inventory.Values.Where(i => i.Name.Equals(f.Item, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Slot).FirstOrDefault() ?? throw new QuestBlockedException(helper.Name + " no longer carries a " + f.Item + ".");
            int had = q.Snapshot.Count(f.Item);
            long qr = q.Snapshot.Revision, hr = h.Snapshot.Revision;
            bool questerAccepted = false;
            bool Clean(Exchange? e) => e is { Cancelled: false, OwnGold: 0, PartnerGold: 0 } && e.PartnerId == helperId && e.Items.Length == 1 && e.Items[0].Party == 1 && e.Items[0].Name.Equals(f.Item, StringComparison.OrdinalIgnoreCase);
            try
            {
                await h.ExecuteAsync(new("trade-start", EntityId: questerId), token);
                await Expect(h, x => x.Exchange is { Cancelled: false } e && e.Revision > hr && e.PartnerId == questerId, hr, "The trade window didn't open for " + helper.Name + ".", token);
                await h.ExecuteAsync(new("trade-item", EntityId: questerId, Slot: item.Slot), token);
                await Expect(h, x => x.Exchange is { Cancelled: false } e && e.PartnerId == questerId && e.Items.Any(i => i.Party == 0 && i.Name.Equals(f.Item, StringComparison.OrdinalIgnoreCase)), hr, helper.Name + "'s flower didn't reach the trade window.", token);
                await Expect(q, x => x.Exchange is { } e && e.Revision > qr && Clean(e), qr, Quester + " didn't see exactly one " + f.Item + " offered by " + helper.Name + ".", token);
                await h.ExecuteAsync(new("trade-accept", EntityId: questerId), token);
                await Expect(q, x => x.Exchange is { } e && e.Revision > qr && e.PartnerAccepted, qr, helper.Name + "'s OK didn't reach " + Quester + ".", token);
                if (!Clean(q.Snapshot.Exchange))
                    throw new QuestBlockedException("The trade changed before " + Quester + " accepted; it was cancelled.");
                questerAccepted = true;
                await q.ExecuteAsync(new("trade-accept", EntityId: helperId), token);
                // "You exchanged." also follows the first OK; only the flower arriving proves the trade.
                try
                {
                    await q.WaitAsync(x => x.InventoryKnown && x.Count(f.Item) > had, qr, proof, token);
                }
                catch (TimeoutException) { throw new QuestBlockedException("The trade with " + helper.Name + " wasn't confirmed. Check both inventories for the " + f.Item + " before resuming."); }
            }
            catch (Exception) when (!questerAccepted)
            {
                try
                {
                    await h.ExecuteAsync(new("trade-cancel", EntityId: questerId), CancellationToken.None);
                }
                catch (Exception) { }
                throw;
            }
        }
        static async Task Expect(IGameSession s, Func<CharacterSnapshot, bool> test, long after, string failure, CancellationToken ct)
        {
            try
            {
                await s.WaitAsync(test, after, reply, ct);
            }
            catch (TimeoutException) { throw new QuestBlockedException(failure); }
            if (s.Snapshot.Exchange is { Cancelled: true } e)
                throw new QuestBlockedException("The trade was cancelled" + (e.Message is { Length: > 0 } m ? ": " + m : "."));
        }

        async Task TurnIn(string? helper, CancellationToken ct)
        {
            var s = c.Session;
            if (s.Snapshot.Count(f.Item) < 1)
                throw new QuestBlockedException(Quester + " has no " + f.Item + " to give.");
            if (f.Cost > 0 && (s.Snapshot.Gold is not { } gold || gold < f.Cost))
                throw new ResourceUnavailableException("Carry at least 5,000 gold; Paulin takes 5,000 for each flower.");
            await Travel(s, PaulinMap, QuesterTile, 1, ct);
            if (s.Snapshot.Dialog is { } open)
                await s.ExecuteAsync(new("close", Dialog: open), ct);
            var npc = s.Snapshot.Entities.Values.SingleOrDefault(e => !e.IsPlayer && e.Name.Equals(Paulin, StringComparison.OrdinalIgnoreCase)) ?? throw new QuestBlockedException("Paulin is not visible.");
            await Begin(npc.Id);
            await s.ExecuteAsync(new("open", Text: Paulin), ct);
            await Advance(helper, ct);
        }
        async Task Advance(string? helper, CancellationToken ct)
        {
            var s = c.Session;
            for (int page = 0; page < 16; page++)
            {
                ct.ThrowIfCancellationRequested();
                var t = c.Job.Pending!;
                var snap = s.Snapshot;
                if (snap.Generation != t.Generation || snap.Map != PaulinMap)
                    throw new QuestBlockedException("Paulin's conversation changed connection or room; the turn-in will be reconciled, not resent.");
                if (snap.Dialog is not { } d)
                {
                    if (t.Kind == "sick-give")
                    {
                        await Prove(helper, ct);
                        return;
                    }
                    await Update(j => j with { Pending = null });
                    throw new QuestBlockedException("Paulin's conversation closed before the flower was offered. Nothing was given; resume to try again.");
                }
                if (d.EntityId != t.EntityId || !d.IsPursuit || d.Prompt is not null || !d.Name.Equals(Paulin, StringComparison.OrdinalIgnoreCase))
                    throw new QuestBlockedException("Unexpected page during Paulin's conversation; preserved: " + d.Text);
                if (d.Choices.Length > 0)
                {
                    string want = d.Choices.Any(x => x.Text == f.Give) ? f.Give : d.Choices.Any(x => x.Text == f.Offer) ? f.Offer : d.Choices.Any(x => x.Text == AskChild) ? AskChild : throw new QuestBlockedException("Paulin's menu changed; preserved: " + d.Text);
                    if (want == f.Give)
                        await Update(j => j with { Pending = t with { Kind = "sick-give", Revision = snap.Revision } });
                    await s.ExecuteAsync(new("choose", Text: want, Dialog: d), ct);
                    try
                    {
                        await s.WaitAsync(n => n.Dialog is null || n.Dialog.Revision > d.Revision || IsNoFlower(n.Notice?.Text), snap.Revision, reply, ct);
                    }
                    catch (TimeoutException) { throw new QuestBlockedException("Paulin didn't answer \"" + want + "\". The turn-in will be reconciled, not resent."); }
                    if (s.Snapshot.Notice is { } n && IsNoFlower(n.Text) && n.Revision > snap.Revision)
                        throw new QuestBlockedException("Paulin says " + Quester + " has no " + f.Name + " Blossom.");
                    continue;
                }
                if (d.Type != 0)
                    throw new QuestBlockedException("Unexpected input page from Paulin; preserved.");
                // After the give choice, Paulin's next page is the last one whatever it says; it is logged for review.
                if (t.Kind == "sick-give")
                {
                    await Note(Quester, "Paulin: " + d.Text);
                    await Update(j => j with { Pending = t with { TerminalSeen = true, TerminalRevision = d.Revision } });
                    await s.ExecuteAsync(new(d.HasNext ? "next" : "close", Dialog: d), ct);
                    await Prove(helper, ct);
                    return;
                }
                if (d.Text.StartsWith(Collect, StringComparison.Ordinal) && d.HasNext)
                {
                    await s.ExecuteAsync(new("next", Dialog: d), ct);
                    continue;
                }
                throw new QuestBlockedException("Unexpected Paulin page; preserved: " + d.Text);
            }
            throw new QuestBlockedException("Paulin's conversation ran longer than expected; preserved.");
        }
        int Carried(Transaction t) => t.InventoryCounts.FirstOrDefault(p => p.Key.Equals(f.Item, StringComparison.OrdinalIgnoreCase)).Value;
        async Task Prove(string? helper, CancellationToken ct)
        {
            var s = c.Session;
            var t = c.Job.Pending!;
            bool Paid(CharacterSnapshot n) => n.Generation == t.Generation && n.InventoryKnown && n.Gold == t.Gold - f.Cost && n.Count(f.Item) == Carried(t) - 1;
            string? experience = null;
            try
            {
                experience = (await s.WaitAsync(n => n.Generation == t.Generation && n.Notice is { } no && no.Revision > t.Revision && Experience(no.Text) is not null, t.Revision, proof, ct)).Notice!.Text;
            }
            catch (TimeoutException) { }
            CharacterSnapshot after;
            try
            {
                after = await s.WaitAsync(Paid, t.Revision, proof, ct);
            }
            catch (TimeoutException) { throw new QuestBlockedException("Paulin turn-in needs reconciliation: " + (f.Cost > 0 ? "5,000 gold and " : "") + "one " + f.Item + " should be gone" + (f.Cost > 0 ? "" : " with gold unchanged") + ". It will not be resent."); }
            if (experience is null)
                throw new QuestBlockedException("Paulin took the flower, but no experience message arrived. Check the experience, then resume.");
            await Record(t, after.Gold!.Value, experience, helper);
        }
        async Task Record(Transaction t, uint goldAfter, string experience, string? helper)
        {
            var receipt = new SickChildTurnIn(DateTimeOffset.UtcNow, t.Generation, t.Gold, goldAfter, experience, helper);
            await Update(j => j with { Pending = null, SickChildTurnIns = [.. j.SickChildTurnIns, receipt], Reason = "Flower " + (Given(j) + 1) + " of 5 given to Paulin" });
        }

        public async Task Reconcile(CancellationToken ct)
        {
            var s = c.Session;
            var t = c.Job.Pending!;
            var snap = s.Snapshot;
            if (t.Kind == "sick-turnin")
            {
                // The give choice was never sent; close any half-open page so the next offer starts clean.
                if (snap.Generation == t.Generation && snap.Dialog is { } d && d.EntityId == t.EntityId)
                    await s.ExecuteAsync(new("close", Dialog: d), ct);
                await Update(j => j with { Pending = null });
                return;
            }
            if (t.Kind != "sick-give")
                throw new QuestBlockedException("Unexpected saved " + f.QuestName + " step requires reconciliation.");
            if (snap.Generation == t.Generation)
            {
                if (snap.Dialog is { } d && d.EntityId == t.EntityId && d.Revision > t.Revision && snap.Map == PaulinMap)
                {
                    await Advance(null, ct);
                    return;
                }
                await Prove(null, ct);
                return;
            }
            if (!snap.Connected || !snap.InventoryKnown || snap.Gold is null)
                throw new QuestBlockedException("Waiting for fresh gold and inventory to check the last turn-in.");
            if (snap.Gold == t.Gold - f.Cost && snap.Count(f.Item) == Carried(t) - 1)
            {
                await Record(t, snap.Gold.Value, "Not observed (reconnected)", null);
                return;
            }
            if (snap.Gold == t.Gold && snap.Count(f.Item) == Carried(t))
            {
                await Update(j => j with { Pending = null });
                return;
            }
            throw new QuestBlockedException("The last Paulin turn-in can't be confirmed from gold and inventory. Check both, then resume.");
        }

        async Task VerifyLegend(CancellationToken ct)
        {
            var s = c.Session;
            await s.ExecuteAsync(new("inspect-self"), ct);
            if (Mark(s.Snapshot, f.Legend) is not { } mark)
                throw new QuestBlockedException("Five flowers are recorded, but the \"" + f.Legend + "\" mark isn't in your legend. Check the count in Change choices.");
            await Complete(mark, "Legend verified after the fifth flower");
        }
        public Task Complete(string mark, string reason)
        {
            var experience = c.Job.SickChildTurnIns.Select(r => Experience(r.Experience) is { } text && int.TryParse(text.Split(' ')[0], out var xp) ? xp : 0).Sum();
            var rewards = new Dictionary<string, int>(c.Job.Rewards);
            if (experience > 0)
                rewards["experience"] = experience;
            return Update(j => j with { Step = "complete", Pending = null, CompletedAt = DateTimeOffset.UtcNow, CompletionLegend = mark, CompletionText = mark, Repeat = false, NextRun = null, Rewards = rewards, Reason = reason });
        }
    }
}
