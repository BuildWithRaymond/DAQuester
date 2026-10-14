namespace DAQuester.Core;

// QuestRunner owns lifecycle and persistence. These adapters apply each quest's
// eligibility and consent rules before handing captured stages to the shared reader.
public sealed class FiskActions(QuestDefinition recipe) : IQuestAction
{
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        if (await ObservedJourney.ExistingAsync(c, recipe, ct))
            return;
        var gate = FiskEvidence.Eligibility(c.Session.Snapshot, c.Job);
        if (gate.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(gate.Reason);
        if (step.Action == "fisk-preflight")
            return;
        await ObservedJourney.RunAsync(c, recipe, ct);
    }
}

public sealed class DanaanActions(QuestDefinition recipe) : IQuestAction
{
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        if (await ObservedJourney.ExistingAsync(c, recipe, ct))
            return;
        var gate = DanaanEvidence.Eligibility(c.Session.Snapshot, c.Job);
        if (gate.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(gate.Reason);
        (c.Job.DanaanOptions ?? throw new QuestBlockedException("Choose Donnan / Cail and accept the supplied-Mold rite warning before starting.")).Validate();
        if (step.Action == "danaan-preflight")
            return;
        await ObservedJourney.RunAsync(c, recipe, ct);
    }
}

static partial class ObservedJourney
{
    internal static string? Mark(QuestContext c) => c.Job.Quest == FiskEvidence.Id ? FiskEvidence.Mark(c.Session.Snapshot) : DanaanEvidence.Mark(c.Session.Snapshot);
    internal static async Task<bool> ExistingAsync(QuestContext c, QuestDefinition recipe, CancellationToken ct)
    {
        // An own completion mark prevents another reward claim. A pending handoff
        // or final popup cleanup still needs its specific reconciliation path.
        var s = c.Session.Snapshot;
        if (!s.Connected || !string.Equals(s.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Quest character changed; resume on the saved connected character.");
        if (!s.LegendsKnown)
            await c.Session.ExecuteAsync(new("inspect-self"), ct);
        if (Mark(c) is not { } mark || c.Job.Route?.CleanupPending == true)
            return false;
        if (c.Job.Pending is not null && !CompletedReadOnlyIntro(c, recipe))
            return false;
        await c.SaveAsync(c.Job with
        {
            Step = "complete",
            CompletedAt = c.Job.CompletedAt ?? DateTimeOffset.UtcNow,
            CompletionLegend = mark,
            CompletionText = mark,
            Repeat = false,
            NextRun = null,
            Pending = null,
            Route = c.Job.Route is { } route ? route with
            {
                SubmittedPage = null
            } : null,
            Reason = "Own completion legend verified"
        });
        return true;
    }

    static bool CompletedReadOnlyIntro(QuestContext c, QuestDefinition recipe)
    {
        // Only these captured introductory choices are safe to settle from an
        // existing mark. This exception must never clear a payment or item claim.
        if (c.Job.Quest != DanaanEvidence.Id || c.Job.Pending is not { Kind: "observed-page:blaise-records" } pending ||
            c.Job.Route is not { SubmittedPage: { } submitted } route || c.Session.Snapshot.Dialog is not null ||
            route.Page is < 0 or > 1)
            return false;
        var journey = JourneyFor(c, recipe);
        if (route.Task < 0 || route.Task >= journey.Length)
            return false;
        var task = journey[route.Task];
        return task.Id == "blaise-records" && task.Cost == 0 && task.Experience == 0 && task.Inputs.Length == 0 && task.Outputs.Length == 0 &&
            task.Pages.Length > route.Page && task.Pages[route.Page] is { Kind: "choose", Choice: "Gods and hats" or "Yes!" } page &&
            pending.Generation == submitted.Generation && Matches(c, submitted, page, route.BoundEntity ?? submitted.EntityId, pending.Generation);
    }

    internal static async Task RunAsync(QuestContext c, QuestDefinition recipe, CancellationToken ct)
    {
        if (recipe.Journey.Length == 0)
            throw new QuestBlockedException("Observed quest journey is missing.");
        while ((c.Job.Route?.Task ?? 0) < JourneyFor(c, recipe).Length)
        {
            // Persist the current stage before acting. Advance only after its
            // handler verifies the receipt; interruption resumes this same stage.
            ct.ThrowIfCancellationRequested();
            var route = c.Job.Route ?? new();
            var task = JourneyFor(c, recipe)[route.Task];
            await c.SaveAsync(c.Job with
            {
                Reason = task.Label.Length > 0 ? task.Label : task.Id.Replace('-', ' '),
                Route = route
            });
            try
            {
                switch (task.Kind)
                {
                    case "wait":
                        await WaitAsync(c, ct);
                        break;
                    case "dialog":
                        await DialogueAsync(c, recipe, task, ct);
                        break;
                    case "buy":
                        await BuyAsync(c, task, ct);
                        break;
                    case "equip-weapon":
                        await WeaponAsync(c, task, false, ct);
                        break;
                    case "unequip-weapon":
                        await WeaponAsync(c, task, true, ct);
                        break;
                    case "unequip-armaments":
                        await ArmamentsAsync(c, task, ct);
                        break;
                    case "gather":
                        await GatherAsync(c, task, ct);
                        break;
                    default:
                        throw new QuestBlockedException("Unsupported observed quest task: " + task.Kind);
                }
            }
            catch (CollectionCheckAgainException) { continue; }
            if (c.Job.Step == "complete")
                return;
            await c.SaveAsync(c.Job with
            {
                Route = new()
                {
                    Task = route.Task + 1
                },
                NextRun = null
            });
        }
        if (c.Job.CompletedAt is null)
            throw new QuestBlockedException("Journey ended without verified quest rewards and own legend.");
    }

    static async Task WaitAsync(QuestContext c, CancellationToken ct)
    {
        if (c.Job.FiskWait is not { Verified: true } wait)
            throw new QuestBlockedException("No verified paid bouquet acceptance; inspect the last transaction before returning to Viveka.");
        var due = c.Job.NextRun is { } retry && retry > wait.Due ? retry : wait.Due;
        // Due time authorizes a collection check, never a second payment or an
        // assumed Bouquet. Scheduler waits and online queue deferrals have distinct owners.
        await c.SaveAsync(c.Job with
        {
            NextRun = due,
            Reason = "Bouquet check after " + due.ToLocalTime().ToString("g")
        });
        if (due <= DateTimeOffset.UtcNow)
            return;
        if (c.YieldScheduledWaits)
            throw new QuestScheduledWaitException(due);
        if (c.HasReadyQueuedWork?.Invoke() != true)
        {
            if (c.Session.Snapshot.Map != FiskWaitingArea.Map)
                await MoveAsync(c, FiskWaitingArea.Map, new(10, 8), ct);
            var tile = await c.WaitingArea.ParkAsync(c.Session, ct);
            await c.SaveAsync(c.Job with
            {
                Reason = tile is null ? "Tavern waiting row is full; paid bouquet wait preserved" : "Waiting in Suomi Tavern; bouquet check after " + due.ToLocalTime().ToString("g")
            });
        }
        if (c.DeferTimedWaits)
            throw new QuestDeferredWaitException(due);
        var remaining = due - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, ct);
        c.WaitingArea.Release(c.Session);
    }
}
