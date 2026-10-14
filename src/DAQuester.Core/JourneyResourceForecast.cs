namespace DAQuester.Core;

// Availability is supplied by the existing helper ownership rules. Offline login capability
// alone cannot verify inventory, health or a future flower; no credentials belong here.
public sealed record ForecastHelper(string Name, CharacterSnapshot Snapshot, Eligibility Availability);
public sealed record ForecastSupply(string Character, string Item, int Quantity);
public sealed record ForecastItem(string Item, int RemainingRequired, int? UsableOwned, int? AcquisitionShortfall);
public sealed record ForecastGold(long Budget, long MinimumStartingBalance, long? AllocatedOwned, long? Shortfall, string Basis);
public sealed record ForecastCapacity(int FreeSlotsRequiredForAcquisition, int? AdditionalFreeSlotsNeededNow, int? FreeSlotsOwned);
public sealed record ForecastTiming(DateTimeOffset? EarliestEligibility, DateTimeOffset? FirstActionNotBefore,
    DateTimeOffset? LastActionNotBefore, TimeSpan? MinimumWait, string Basis);
public sealed record QuestResourceForecast(string Quest, bool Covered, int? RecordedProgress, int? DeclaredProgress,
    ForecastItem[] Items, ForecastSupply[] Supplies, ForecastGold Gold, ForecastCapacity? Capacity,
    Eligibility Eligibility, ForecastTiming Timing, string[] Unknowns);
public sealed record JourneyForecast(DateTimeOffset CalculatedAt, QuestResourceForecast[] Entries,
    long KnownGoldBudget, long MinimumStartingBalanceLowerBound, long? UsableOwnedGold, long? KnownGoldShortfall,
    TimeSpan KnownWaitLowerBound, bool ResourcesComplete, bool TimingComplete, string[] Unknowns);

public sealed class JourneyResourceForecast(Func<DateTimeOffset> clock)
{
    public JourneyForecast Calculate(QuestQueueState queue, CharacterSnapshot observed, Func<string, QuestJob> loadJob,
        IReadOnlyList<ForecastHelper>? helpers = null, IReadOnlyList<SickChildPick>? sharedPicks = null)
    {
        var now = clock();
        var rows = new List<QuestResourceForecast>();
        var notes = new List<string>();
        var supplies = new Dictionary<(string Character, string Item), int>();
        var picks = (sharedPicks ?? []).ToList();
        var helperByName = (helpers ?? []).DistinctBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(h => h.Name, StringComparer.OrdinalIgnoreCase);
        bool fresh = Fresh(observed, queue.Character);
        bool inventoryKnown = fresh && observed.InventoryKnown;
        long? gold = fresh ? observed.Gold : null;
        long budget = 0, starting = 0;
        var cursor = now;
        bool resourcesComplete = true, timingComplete = true;
        var ids = queue.Quests.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Contains(SickChildEvidence.Id) && ids.Contains(SickChildEvidence.AnamanId))
            notes.Add("The two Paulin outcomes are mutually exclusive. This queue cannot be run as written.");

        int Reserve(CharacterSnapshot s, string character, string item, int wanted)
        {
            var key = (character.ToUpperInvariant(), item.ToUpperInvariant());
            int used = supplies.GetValueOrDefault(key);
            int count = Math.Min(wanted, Math.Max(0, s.Count(item) - used));
            supplies[key] = used + count;
            return count;
        }

        foreach (string id in ids)
        {
            var job = loadJob(id);
            var unknown = new List<string>();
            var used = new List<ForecastSupply>();
            bool child = id == SickChildEvidence.Id, altar = id == AltarWorshipEvidence.Id;
            if (!child && !altar)
            {
                long floor = id == NewCharacterPath.FirstStepsId ? NewCharacterPath.FirstStepsGold : 0;
                starting = Math.Max(starting, budget + floor);
                resourcesComplete = timingComplete = false;
                string detail = id == NewCharacterPath.FirstStepsId
                    ? "The starter starting balance is known; live purchases, repairs and resulting supplies are not forecast."
                    : "Resource and duration estimates are unavailable for this quest.";
                rows.Add(new(id, false, null, null, [], [], new(0, floor, null, null, "Starting floor only; consumption unavailable"), null,
                    new(EligibilityKind.Unknown, detail), new(null, null, null, null, "Estimate unavailable"), [detail]));
                // Any uncovered quest can spend or change supplies. Do not reuse the original
                // balance or inventory as if it were guaranteed to survive that quest.
                gold = null;
                inventoryKnown = false;
                continue;
            }

            bool matching = string.Equals(job.Character, queue.Character, StringComparison.OrdinalIgnoreCase) && job.Quest == id;
            if (!matching)
                unknown.Add("The journal identity does not match this queue; progress is unavailable.");
            int recorded = matching ? child ? job.SickChildTurnIns.Length : AltarWorshipEvidence.Made(job) : 0;
            bool validOptions = true;
            if (child && matching && job.SickChildOptions is { } selected)
            {
                try
                {
                    selected.Validate();
                }
                catch (QuestBlockedException e) { validOptions = false; unknown.Add(e.Message); }
            }
            int declared = matching && child && validOptions ? job.SickChildOptions?.PriorTurnIns ?? 0 : 0;
            int remaining = child ? SickChildEvidence.Remaining(matching && validOptions ? job : new()
            {
                SickChildTurnIns = matching ? job.SickChildTurnIns : []
            }) : Math.Max(0, AltarWorshipEvidence.Goal - recorded);
            bool legendFresh = fresh && observed.LegendsKnown && observed.LegendsRevision > 0 && observed.LegendsRevision <= observed.Revision;
            if (legendFresh && (child ? SickChildEvidence.Mark(observed, SickChildEvidence.Legend) is not null : AltarWorshipEvidence.Mark(observed.Legends) is not null))
                remaining = 0;
            if (matching && QueueAttemptOutcome.Evaluate(job)?.Outcome is "Completed" or "PermanentlyCompleted")
                remaining = 0;
            string item = child ? SickChildEvidence.Sevti.Item : AltarWorshipEvidence.Shield;
            int? owned = remaining == 0 ? 0 : inventoryKnown ? Reserve(observed, queue.Character, item, remaining) : null;
            if (owned > 0)
                used.Add(new(queue.Character, item, owned.Value));
            if (!inventoryKnown)
                unknown.Add("Fresh inventory is unavailable; owned supplies and item shortfalls are unknown.");
            if (!matching || job.Pending is not null)
                unknown.Add("Progress requires journal reconciliation; pending submissions are not subtracted as completed actions.");
            if (declared > 0)
                unknown.Add("Prior turn-ins include " + declared + " declared by the player, separately from " + recorded + " recorded receipts.");
            if (remaining == 0 && matching && job.CompletedAt is null)
                unknown.Add("No further materials are forecast; the final reward still needs verification.");
            unknown.Add("Item weights and travel/combat durations are unavailable. Future loot and rewards are not credited.");
            bool rowTimingKnown = (altar || inventoryKnown || remaining == 0) && matching && validOptions && job.Pending is null;
            var cursorBefore = cursor;
            var actionStart = cursor;
            var actionEnd = cursor;
            int own = owned ?? 0;
            int acquired = remaining - own;
            if (child && remaining > 0)
            {
                picks.AddRange(job.SickChildPicks);
                var producers = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
                producers[queue.Character] = Later(cursor, SickChildEvidence.NextPick(picks, queue.Character));
                var options = matching ? job.SickChildOptions : null;
                if (options is null)
                {
                    if (acquired > 0)
                        rowTimingKnown = false;
                    unknown.Add("Flower mode and helpers have not been chosen; save the required choices before execution.");
                }
                else
                {
                    if (options.Mode == SickChildOptions.WithHelpers)
                    {
                        foreach (string name in options.Helpers.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            if (acquired == 0)
                                break;
                            if (name.Equals(queue.Character, StringComparison.OrdinalIgnoreCase))
                            {
                                unknown.Add("The quester cannot also be a helper.");
                                continue;
                            }
                            if (!helperByName.TryGetValue(name, out var h) || h.Availability.Kind == EligibilityKind.Unknown)
                            {
                                rowTimingKnown = false;
                                unknown.Add(name + ": helper eligibility or fresh state is unavailable; no flower is credited.");
                                continue;
                            }
                            if (h.Availability.Kind == EligibilityKind.Blocked)
                            {
                                unknown.Add(name + ": " + h.Availability.Reason + "; excluded from supplies and picks.");
                                continue;
                            }
                            if (!Fresh(h.Snapshot, name) || !h.Snapshot.InventoryKnown)
                            {
                                rowTimingKnown = false;
                                unknown.Add(name + ": fresh helper inventory is unavailable; no flower is credited.");
                                continue;
                            }
                            // The existing helper trade carries one flower per visit. A saved pick
                            // is not proof that it is still owned; only fresh inventory is credited.
                            int held = Reserve(h.Snapshot, name, item, Math.Min(1, acquired));
                            if (held > 0)
                            {
                                used.Add(new(name, item, held));
                                own += held;
                                acquired -= held;
                            }
                            if (held == 0 && h.Snapshot.FreeInventorySlots == 0)
                            {
                                unknown.Add(name + ": no free slot for a flower; excluded from new picks.");
                                continue;
                            }
                            producers[name] = Later(cursor, SickChildEvidence.NextPick(picks, name));
                        }
                    }
                }
                if (rowTimingKnown)
                {
                    for (int pick = 0; pick < acquired; pick++)
                    {
                        var next = producers.OrderBy(p => p.Value).First();
                        if (pick == 0 && own == 0)
                            actionStart = next.Value;
                        actionEnd = next.Value;
                        producers[next.Key] = next.Value + SickChildEvidence.PickInterval;
                        picks.Add(new(next.Key, next.Value, false));
                    }
                    actionEnd = Later(cursor, actionEnd);
                }
            }
            else if (remaining > 0)
            {
                actionStart = Later(cursor, AltarWorshipEvidence.NextOffering(job));
                actionEnd = actionStart + TimeSpan.FromTicks(AltarWorshipEvidence.Interval.Ticks * (remaining - 1));
            }

            var rowGold = gold;
            long cost = child ? (long)remaining * SickChildEvidence.Sevti.Cost : (long)acquired * AltarWorshipEvidence.ShieldPrice;
            bool costKnown = matching && validOptions && job.Pending is null && (child || inventoryKnown || remaining == 0);
            long? allocated = costKnown && gold is { } balance ? Math.Min(balance, cost) : null;
            if (allocated is not null)
                gold -= allocated;
            else
                gold = null;
            budget += cost;
            starting = Math.Max(starting, budget);
            if (!child && !inventoryKnown)
                unknown.Add("Shield purchase budget is an upper bound assuming every remaining shield must be purchased.");
            if (gold is null)
                unknown.Add("Usable gold and known gold shortfall are unavailable.");
            int slots = remaining > (owned ?? 0) ? 1 : 0;
            int? free = inventoryKnown ? observed.FreeInventorySlots : null;
            int? neededNow = null;
            if (free is { } f)
                neededNow = owned > 0 ? 0 : Math.Max(0, slots - f);
            var facts = observed with
            {
                Connected = fresh,
                InventoryKnown = inventoryKnown,
                LegendsKnown = legendFresh,
                Gold = rowGold is { } carried ? checked((uint)carried) : null
            };
            var eligibility = child ? SickChildEvidence.Eligibility(facts, job) : AltarWorshipEvidence.Eligibility(facts, job);
            DateTimeOffset? eligibleAt = eligibility.Kind == EligibilityKind.Eligible && timingComplete && matching && job.Pending is null ? cursorBefore : null;
            if (eligibility.Kind != EligibilityKind.Eligible)
                unknown.Add("Earliest eligibility has no date: " + eligibility.Reason);
            if (child && (job.SickChildOptions is null || !validOptions) && remaining > 0)
                eligibleAt = null;
            if (rowTimingKnown)
                cursor = Later(cursor, actionEnd);
            else
                timingComplete = false;
            resourcesComplete &= inventoryKnown && gold is not null && matching && validOptions && job.Pending is null;
            long nextGold = remaining == 0 ? 0 : child ? SickChildEvidence.Sevti.Cost : owned > 0 ? 0 : AltarWorshipEvidence.ShieldPrice;
            string goldBasis = child ? "Gold consumed by remaining handoffs"
                : "Up to this purchase budget at the configured shield price; live shop price wins";
            if (child && job.Pending is not null)
                goldBasis = "Upper bound awaiting reconciliation of the pending handoff";
            rows.Add(new(id, true, matching ? recorded : null, matching ? declared : null,
                [new(item, remaining, inventoryKnown ? own : null, inventoryKnown ? acquired : null)], used.ToArray(),
                new(cost, nextGold, allocated, allocated is { } amount ? cost - amount : null, goldBasis),
                new(slots, neededNow, free), eligibility,
                new(eligibleAt, rowTimingKnown ? actionStart : null, rowTimingKnown ? actionEnd : null,
                    rowTimingKnown ? actionEnd - cursorBefore : null,
                    child ? "Lower bound from the shared per-character flower-pick interval; helper trades and successful picks are not guaranteed"
                        : "Lower bound from configured offering spacing; not proof of a server-enforced interval or guaranteed reward"), unknown.ToArray()));
        }
        long? usableGold = rows.All(r => r.Gold.AllocatedOwned is not null) ? rows.Sum(r => r.Gold.AllocatedOwned!.Value) : null;
        notes.Add("Known waits are scheduled against absolute dates in queue order so cooldowns can overlap. Unknown durations contribute no invented time.");
        notes.Add("All figures are read-only planning estimates. They do not authorize spending, establish completion or promise a completion time.");
        return new(now, rows.ToArray(), budget, starting, usableGold, usableGold is { } usable ? budget - usable : null,
            cursor - now, resourcesComplete, timingComplete, notes.ToArray());
    }

    static bool Fresh(CharacterSnapshot s, string character) => s.Connected && s.Id is not null && s.Generation > 0 && s.Revision > 0
        && string.Equals(s.Name, character, StringComparison.OrdinalIgnoreCase);
    static DateTimeOffset Later(DateTimeOffset start, DateTimeOffset? due) => due is { } at && at > start ? at : start;
}
