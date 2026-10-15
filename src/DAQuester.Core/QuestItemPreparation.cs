namespace DAQuester.Core;

public sealed record QuestSupply(string Quest, ObservedQuestTask Task)
{
    public QuestStep? GatherStep { get; init; }
}
public sealed record QuestPreparationState(QuestSupply[] Supplies, int Index = 0, ObservedQuestProgress? Progress = null)
{
    public uint GoldReserve { get; init; }
}

public static class QuestItemPreparation
{
    public static QuestSupply[] Requirements(QuestDefinition recipe, QuestJob job, Calibration? calibration = null)
    {
        if (recipe.Id == "spare-a-stick" && job.Step != "confirm" && calibration?.Ready == true &&
            recipe.Steps.Any(s => s.Action == "gather"))
            return [new(recipe.Id, new("branches", "branches", 500) { Item = (calibration ?? new()).BranchName, Quantity = 6, WeightReserve = (calibration ?? new()).BranchWeight })];
        if (recipe.Id == "entered-muisir-village" && job.Step != "confirm" && recipe.Steps.FirstOrDefault(s => s.Action == "muisir-gather") is { } collect)
            return [new(recipe.Id, new("mushrooms", "muisir", 504) { Item = MuisirEvidence.Mushroom, Quantity = 5 }) { GatherStep = collect }];
        if (recipe.Id == AltarWorshipEvidence.Id && AltarWorshipEvidence.Made(job) < AltarWorshipEvidence.Goal)
            return [new(recipe.Id, new("altar-shield", "buy", AltarWorshipEvidence.ShopMap, AltarWorshipEvidence.Torrance)
                { Item = AltarWorshipEvidence.Shield, Quantity = 1, WeightReserve = 5, Cost = AltarWorshipEvidence.ShieldPrice })];
        var journey = DanaanTransport.Journey(recipe, job);
        if (journey.Length == 0 && (job.JourneyVariant is not null || job.DanaanReplies.Length > 0))
            throw new QuestBlockedException("Saved quest branch is unknown; no supplies acquired before reconciliation.");
        int start = job.Route?.Task ?? 0;
        var paths = new List<ObservedQuestTask[]> { journey };
        var parents = new Queue<(string? Key, ObservedQuestTask[] Journey)>();
        parents.Enqueue((job.JourneyVariant, journey));
        while (parents.TryDequeue(out var parent))
            foreach (var pair in recipe.JourneyVariants.Where(v => v.Value.ParentVariant == parent.Key))
            {
                int trigger = Array.FindIndex(parent.Journey, t => t.Id == pair.Value.TaskId);
                if (trigger < start) continue;
                paths.Add(pair.Value.Journey);
                parents.Enqueue((pair.Key, pair.Value.Journey));
            }
        Dictionary<string, int> Totals(ObservedQuestTask[] path)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in path.Where(t => t.Kind is "buy" or "gather" && t.Item is not null)
                .DistinctBy(t => t.Item!, StringComparer.OrdinalIgnoreCase))
            {
                int needed = 0;
                int produced = 0;
                bool hasHandoff = false;
                foreach (var stage in path.Skip(start))
                {
                    int consumed = stage.Inputs.Where(i => i.Item.Equals(source.Item, StringComparison.OrdinalIgnoreCase)).Sum(i => i.Count);
                    hasHandoff |= consumed > 0;
                    produced -= consumed;
                    if (produced < 0)
                    {
                        needed = checked(needed - produced);
                        produced = 0;
                    }
                    produced = checked(produced + stage.Outputs.Where(i => i.Item.Equals(source.Item, StringComparison.OrdinalIgnoreCase)).Sum(i => i.Count));
                }
                // Isolated supplier fixtures and acquisition-only stages retain
                // their declared target; real deliveries determine consumption.
                result[source.Item!] = hasHandoff ? needed : path.Skip(start).Where(t => t.Item == source.Item && t.Kind is "buy" or "gather").Sum(t => t.Quantity);
            }
            return result;
        }
        var totals = paths.Select(Totals).ToArray();
        return journey.Where(t => t.Kind is "buy" or "gather" && t.Item is not null)
            .DistinctBy(t => t.Item!, StringComparer.OrdinalIgnoreCase)
            .Select(t => new QuestSupply(recipe.Id, t with { Quantity = totals.Min(p => p.GetValueOrDefault(t.Item!)) }))
            .Where(s => s.Task.Quantity > 0).ToArray();
    }

    static bool Ready(QuestDefinition recipe, QuestJob job, CharacterSnapshot s)
    {
        if (!s.Connected || s.Hp is null or 0 || !s.InventoryKnown || s.Weight is null || s.MaxWeight is null ||
            !string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) || job.Pending is not null ||
            job.Route?.Baseline is not null || job.Route?.CleanupPending == true || job.FiskWait is { Verified: true } ||
            job.PermanentCompletion is not null || QuestRunPolicy.Finished(recipe, job) ||
            QuestRunPolicy.NextAttempt(recipe, job) is { } due && due > DateTimeOffset.UtcNow)
            return false;
        return recipe.Id switch
        {
            DanaanEvidence.Id => job.DanaanOptions is { Avatar: "cail", SuppliedMoldRiteAccepted: true } &&
                DanaanEvidence.Mark(s) is null && DanaanEvidence.Eligibility(s, job).Kind == EligibilityKind.Eligible,
            FiskEvidence.Id => FiskEvidence.Mark(s) is null && FiskEvidence.Eligibility(s, job).Kind == EligibilityKind.Eligible,
            "spare-a-stick" => job.Accepted && job.AcceptedGeneration == s.Generation &&
                StickEligibility.Check(s, job.CompletedAt, DateTimeOffset.UtcNow).Kind == EligibilityKind.Eligible,
            "entered-muisir-village" => s.LegendsKnown && !MuisirEvidence.HasLegend(s) && MuisirEvidence.Eligibility(s).Kind == EligibilityKind.Eligible,
            AltarWorshipEvidence.Id => AltarWorshipEvidence.Made(job) < AltarWorshipEvidence.Goal && s.Class is not null &&
                (AltarWorshipEvidence.IsPriest(s.Class) || job.AltarNoMarkAccepted) &&
                !(AltarWorshipEvidence.NextOffering(job) is { } next && next > DateTimeOffset.UtcNow) &&
                AltarWorshipEvidence.Eligibility(s, job).Kind == EligibilityKind.Eligible,
            _ => false
        };
    }

    static QuestSupply[] Combine(IEnumerable<QuestSupply> supplies) => supplies.GroupBy(x => x.Task.Item!, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First() with { Task = g.First().Task with { Quantity = checked(g.Sum(x => x.Task.Quantity)) } }).ToArray();

    static uint RequiredPayments(QuestDefinition recipe, QuestJob job) => checked((uint)DanaanTransport.Journey(recipe, job)
        .Skip(job.Route?.Task ?? 0).Where(t => t.Kind == "dialog").Sum(t => (long)t.Cost));

    // Recorded unit costs decide whether optional later work fits the budget.
    // Only the fresh shop quote can authorize an actual purchase.
    static long EstimatedPurchases(CharacterSnapshot s, QuestSupply[] supplies) => supplies.Sum(x =>
        (long)Math.Max(0, x.Task.Quantity - s.Count(x.Task.Item!)) * x.Task.Cost);

    static bool Fits(CharacterSnapshot s, QuestSupply[] supplies)
    {
        int slots = 1 + s.Equipment.Keys.Count(slot => slot is 1 or 3);
        long weight = 4;
        foreach (var supply in supplies)
        {
            int missing = Math.Max(0, supply.Task.Quantity - s.Count(supply.Task.Item!));
            slots = checked(slots + missing);
            weight += (long)missing * supply.Task.WeightReserve;
        }
        return s.Inventory.Count + slots <= 59 && s.Weight + weight <= s.MaxWeight;
    }

    public static async Task EnsureAsync(QuestContext c, QuestDefinition recipe, CancellationToken ct)
    {
        long generation = c.Session.Snapshot.Generation;
        void CheckIdentity()
        {
            var current = c.Session.Snapshot;
            if (!current.Connected || current.Generation != generation || !string.Equals(current.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException("Preparation connection changed; reconcile fresh own state before continuing.");
        }
        if (c.Job.Preparation is null)
        {
            if (c.Job.Pending is not null || c.Job.Route?.Baseline is not null || c.Job.Route?.CleanupPending == true ||
                c.Job.FiskWait is { Verified: true } || c.Session.Snapshot.Dialog is { IsPursuit: true }) return;
            var requested = new List<QuestSupply>();
            uint currentPayments = RequiredPayments(recipe, c.Job);
            long plannedPayments = currentPayments;
            var batch = new[] { (Recipe: recipe, Job: c.Job) }.Concat(c.PreparationBatch?.Invoke() ?? [])
                .DistinctBy(x => x.Recipe.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in batch)
            {
                if (!Ready(entry.Recipe, entry.Job, c.Session.Snapshot)) continue;
                var next = Requirements(entry.Recipe, entry.Job, c.Calibration);
                if (next.Length == 0) continue;
                var combined = Combine(requested.Concat(next));
                long candidatePayments = plannedPayments + (entry.Recipe.Id == recipe.Id ? 0 : RequiredPayments(entry.Recipe, entry.Job));
                if (entry.Recipe.Id != recipe.Id && c.Session.Snapshot.Gold is { } available &&
                    EstimatedPurchases(c.Session.Snapshot, combined) + candidatePayments > available)
                    break;
                if (!Fits(c.Session.Snapshot, combined))
                {
                    if (entry.Recipe.Id == recipe.Id)
                        throw new QuestBlockedException("Free bag space and weight for the quest supplies, equipment, and reward before starting.");
                    break;
                }
                requested.AddRange(next);
                plannedPayments = candidatePayments;
            }
            var missing = Combine(requested).Where(x => c.Session.Snapshot.Count(x.Task.Item!) < x.Task.Quantity).ToArray();
            if (missing.Length == 0) return;
            await c.SaveAsync(c.Job with { Preparation = new(missing) { GoldReserve = currentPayments }, Route = c.Job.Route ?? (recipe.Journey.Length > 0 ? new() : null) });
        }
        while (c.Job.Preparation is { } state && state.Index < state.Supplies.Length)
        {
            ct.ThrowIfCancellationRequested();
            CheckIdentity();
            var supply = state.Supplies[state.Index];
            QuestJob? branchOwner = null;
            if (supply.Task.Kind == "branches")
            {
                branchOwner = supply.Quest == c.Job.Quest ? c.Job : c.PreparationBatch?.Invoke()
                    .Where(entry => entry.Recipe.Id == supply.Quest).Select(entry => entry.Job).FirstOrDefault();
                // Branch drops require Callough's activation on this connection.
                // Older preparation checkpoints must yield to acceptance first.
                if (branchOwner is not { Accepted: true } || branchOwner.AcceptedGeneration != generation)
                {
                    if (c.Job.Pending is not null)
                        throw new QuestBlockedException("Saved branch preparation submission requires reconciliation before Callough activation.");
                    await c.SaveAsync(c.Job with { Preparation = state with { Index = state.Index + 1, Progress = null } });
                    continue;
                }
            }
            bool alreadyCompleted = supply.Quest switch
            {
                DanaanEvidence.Id => DanaanEvidence.Mark(c.Session.Snapshot) is not null,
                FiskEvidence.Id => FiskEvidence.Mark(c.Session.Snapshot) is not null,
                "entered-muisir-village" => MuisirEvidence.HasLegend(c.Session.Snapshot),
                _ => false
            };
            if (c.Job.Pending is null && alreadyCompleted)
            {
                await c.SaveAsync(c.Job with { Preparation = state with { Index = state.Index + 1, Progress = null } });
                continue;
            }
            if (state.Progress is null && c.Job.Pending is null)
            {
                supply = supply with { Task = QuestSupplySources.Select(c.Session, supply.Task, c.Calibration.ServerProfile) };
                var supplies = state.Supplies.ToArray();
                supplies[state.Index] = supply;
                await c.SaveAsync(c.Job with { Preparation = state with { Supplies = supplies } });
            }
            var task = supply.Task with { Id = "prepare:" + supply.Quest + ":" + supply.Task.Id };
            var child = new QuestContext(c.Session, new PreparationJournal(c), c.Calibration,
                new() { Character = c.Job.Character, Quest = supply.Quest, Step = "run", Route = state.Progress ?? new(), Pending = c.Job.Pending,
                    Accepted = branchOwner?.Accepted == true, AcceptedGeneration = branchOwner?.AcceptedGeneration }, hunting: c.Hunting)
            {
                SupplyGoldReserve = state.GoldReserve
            };
            if (task.Kind == "branches")
                await StickActions.GatherBranchesAsync(child, ct);
            else if (task.Kind == "muisir")
                await new MuisirActions().ExecuteAsync(child, supply.GatherStep!, ct);
            else
                await ObservedJourney.AcquireSupplyAsync(child, task, ct);
            CheckIdentity();
            if (c.Session.Snapshot.Count(task.Item!) < task.Quantity && child.Job.Step != "complete")
                continue;
            await c.SaveAsync(c.Job with { Preparation = c.Job.Preparation! with { Index = state.Index + 1, Progress = null } });
        }
        await c.SaveAsync(c.Job with { Preparation = null });
    }

    sealed class PreparationJournal(QuestContext owner) : IQuestJournal
    {
        public QuestJob Load(string character, string quest) => throw new NotSupportedException();
        public Task SaveAsync(QuestJob job, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            return owner.SaveAsync(owner.Job with { Pending = job.Pending, Reason = job.Reason,
                Preparation = owner.Job.Preparation! with { Progress = job.Route } });
        }
    }
}
