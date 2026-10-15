namespace DAQuester.Core;

public sealed record DanaanDreamerReceipt(long Generation, long SubmittedRevision, long ArrivalRevision, int Map);

public static class DanaanDreamerReturn
{
    const string FinalPage = "Before you finish your sentence, you find yourself back in the inn. But something this absurdly long and complex could not possibly have been a dream. You should go seek \"the Avatar father or the son\", as the spirit told you to.";

    public static bool IsSubmitted(QuestDefinition recipe, QuestJob job)
    {
        var journey = DanaanTransport.Journey(recipe, job);
        return job.Route is { } route && route.Task >= 0 && route.Task < journey.Length && IsSubmitted(journey[route.Task], job);
    }

    public static bool IsSubmitted(ObservedQuestTask task, QuestJob job) => job.Quest == DanaanEvidence.Id &&
        task is { Id: "dreamer", Kind: "dialog", Map: 720, Npc: "Dreamer", Cost: 0, Experience: 0 } &&
        task.Inputs.Length == 0 && task.Outputs.Length == 0 && task.Pages.Length > 0 && task.Pages[^1] is { Kind: "next", Text: FinalPage } &&
        job.Pending is { Kind: "observed-page:dreamer", EntityType: 1 } pending &&
        job.Route is { CleanupPending: false, Baseline: { } baseline, SubmittedPage: { } submitted } route &&
        route.Page == task.Pages.Length - 1 && baseline.Generation == pending.Generation && route.BoundGeneration == pending.Generation &&
        route.BoundEntity == pending.EntityId && submitted.Generation == pending.Generation && submitted.EntityId == pending.EntityId &&
        pending.Revision == submitted.Revision - 1 && submitted is
            { EntityType: 1, Name: "Dreamer", Text: FinalPage, IsPursuit: true, HasNext: true, Choices.Length: 0, Prompt: null };

    public static bool Arrived(ObservedQuestTask task, QuestJob job, CharacterSnapshot s) => IsSubmitted(task, job) &&
        s.Connected && s.Generation == job.Pending!.Generation && string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) &&
        s.Map is { } map && map != task.Map && s.Position is not null;

    public static QuestJob Reconcile(QuestJob job, QuestDefinition recipe,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded)
    {
        if (!IsSubmitted(recipe, job) || job.CompletedAt is not null || job.PermanentCompletion is not null ||
            job.DanaanOptions is not { Avatar: "cail", SuppliedMoldRiteAccepted: true }) return job;
        var pending = job.Pending!;
        var route = job.Route!;
        var submitted = route.SubmittedPage!;
        var states = observed.Where(s => s.Connected && s.Generation == pending.Generation && s.Revision >= pending.Revision &&
            string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.Revision).ToArray();
        var actions = recorded.Where(a => a.Generation == pending.Generation && a.Revision >= submitted.Revision &&
            string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase) && a.Kind == "next" && a.Selection is null &&
            DanaanDonnanRecovery.Same(a.Dialog, submitted)).ToArray();
        if (actions.Length != 1) return job;
        var action = actions[0];
        var before = states.LastOrDefault(s => s.Revision <= action.Revision);
        var arrival = states.FirstOrDefault(s => s.Revision > action.Revision && s.Map is not null and not 720 && s.Position is not null && s.Dialog is null);
        var baseline = route.Baseline!;
        if (before is not { Map: 720 } || !DanaanDonnanRecovery.Same(before.Dialog, submitted) ||
            before.Gold != baseline.Gold || before.Experience != baseline.Experience || arrival is null ||
            arrival.Gold != baseline.Gold || arrival.Experience != baseline.Experience) return job;
        var proof = new DanaanDreamerReceipt(pending.Generation, action.Revision, arrival.Revision, arrival.Map!.Value);
        if (ConflictsWithLaterProgress(job, proof, observed, recorded)) return job;
        return job with { Pending = null, Route = new() { Task = route.Task + 1 }, DreamerReturnRecovery = proof,
            Status = "Paused", Reason = "Recorded Dreamer final Next and actual map change reconciled; continue to the Avatar" };
    }

    public static bool ConflictsWithLaterProgress(QuestJob job, DanaanDreamerReceipt proof,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded) =>
        recorded.Any(a => string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase) && a.Dialog is not null &&
            (a.Generation > proof.Generation || a.Generation == proof.Generation && a.Revision > proof.SubmittedRevision)) ||
        observed.Any(s => s.Connected && s.Generation == proof.Generation && s.Revision >= proof.ArrivalRevision &&
            string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) &&
            (s.Gold != job.Route!.Baseline!.Gold || s.Experience != job.Route.Baseline.Experience));
}
