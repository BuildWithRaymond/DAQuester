namespace DAQuester.Core;

// The referral assigns Kyros. It is not a repeatable request for the previous
// task's status. Keep that receipt when dialogue presentation is interrupted.
public sealed record DanaanDonnanAssignment(Dialog Confirmation, Dialog Referral, Dialog? Report = null);

public static class DanaanDonnanRecovery
{
    public const string NotFinished = "You have not finished your task";
    const string Completed = "You have completed it. Good! So, for the final task...";
    const string Son = "Are you the son I seek?";

    static bool Consent(QuestJob job) => job.Quest == DanaanEvidence.Id && job.CompletedAt is null && job.PermanentCompletion is null &&
        job.DanaanOptions is { Avatar: "cail", SuppliedMoldRiteAccepted: true };

    static bool TaskMatches(ObservedQuestTask task) => task is { Id: "donnan-next-task", Kind: "dialog", Map: 348, Npc: "Donnan", Cost: 0, Experience: 0 } &&
        task.Inputs.Length == 0 && task.Outputs.Length == 0 && task.ArrivalMap is null && task.ArrivalMaps.Length == 0 && task.Speech is null &&
        task.Pages.Length == 3 && task.Pages[0] is { Root: true, Kind: "choose", Choice: Son } &&
        task.Pages[1] is { Kind: "next", Text: Completed } && task.Pages[2].Kind == "next";

    static bool Page(Dialog d, ObservedQuestPage p) => d.EntityType == 1 && d.Name == p.Speaker && d.Revision > 0 &&
        (d.Text == p.Text || p.TextAlternatives.Contains(d.Text, StringComparer.Ordinal)) &&
        (p.Kind != "next" || d.IsPursuit && d.HasNext && d.Choices.Length == 0 && d.Prompt is null);

    static bool Root(Dialog d, ObservedQuestTask task) => d is { EntityType: 1, Name: "Donnan", IsPursuit: false, Prompt: null } &&
        d.Choices.Count(c => c.Text == Son) == 1 && d.Text == task.Pages[0].Text;

    internal static bool Same(Dialog? left, Dialog right) => left is not null && left.Generation == right.Generation && left.Revision == right.Revision &&
        left.EntityId == right.EntityId && left.EntityType == right.EntityType && left.Name == right.Name && left.Text == right.Text &&
        left.IsPursuit == right.IsPursuit && left.Pursuit == right.Pursuit && left.Step == right.Step && left.Type == right.Type &&
        left.HasNext == right.HasNext && left.Prompt == right.Prompt && left.Choices.SequenceEqual(right.Choices);

    public static bool Valid(DanaanDonnanAssignment receipt, ObservedQuestTask task) => TaskMatches(task) &&
        Page(receipt.Confirmation, task.Pages[1]) && Page(receipt.Referral, task.Pages[2]) &&
        receipt.Confirmation.Generation == receipt.Referral.Generation && receipt.Confirmation.EntityId == receipt.Referral.EntityId &&
        receipt.Referral.Revision > receipt.Confirmation.Revision;

    public static DanaanDonnanAssignment? FromCheckpoint(QuestJob job, ObservedQuestTask task)
    {
        if (!Consent(job) || !TaskMatches(task) || job.Pending is not { Kind: "observed-page:donnan-next-task", EntityType: 1 } pending ||
            job.Route is not { Page: 1, CleanupPending: false, Baseline: { } baseline, SubmittedPage: { } submitted, LastReply: { } reply } route ||
            pending.Generation != baseline.Generation || route.BoundGeneration != pending.Generation || route.BoundEntity != pending.EntityId ||
            submitted.Generation != pending.Generation || submitted.EntityId != pending.EntityId || submitted.Revision <= 0 ||
            pending.Revision != submitted.Revision - 1)
            return null;
        var receipt = new DanaanDonnanAssignment(submitted, reply);
        return Valid(receipt, task) ? receipt : null;
    }

    public static QuestJob Capture(QuestJob job, ObservedQuestTask task) => FromCheckpoint(job, task) is { } receipt && job.DonnanAssignment != receipt
        ? job with { DonnanAssignment = receipt } : job;

    internal static DanaanDonnanAssignment? Continuation(QuestJob job, ObservedQuestTask task)
    {
        if (FromCheckpoint(job, task) is { } captured) return captured;
        if (!Consent(job) || job.DonnanAssignment is not { Report: null } receipt || !Valid(receipt, task) ||
            job.Route is not { CleanupPending: false, Baseline: { } baseline } route || route.Page < 2 || route.Page > task.Pages.Length ||
            baseline.Generation != receipt.Referral.Generation || route.BoundGeneration != baseline.Generation ||
            route.BoundEntity != receipt.Referral.EntityId || !Same(route.LastReply, receipt.Referral)) return null;
        if (job.Pending is null && route.SubmittedPage is null) return receipt;
        return route.Page == 2 && job.Pending is { Kind: "observed-page:donnan-next-task", EntityType: 1 } pending &&
            pending.Generation == receipt.Referral.Generation && pending.EntityId == receipt.Referral.EntityId &&
            pending.Revision == receipt.Referral.Revision - 1 && Same(route.SubmittedPage, receipt.Referral) ? receipt : null;
    }

    internal static bool IsReport(Dialog reply) => reply is
        { EntityType: 1, Name: "Donnan", IsPursuit: true, HasNext: false, Prompt: null, Choices.Length: 0, Text: NotFinished };

    static bool CurrentReport(QuestJob job, ObservedQuestTask task)
    {
        if (!Consent(job) || !TaskMatches(task) || job.Pending is not { Kind: "observed-page:donnan-next-task", EntityType: 1 } p ||
            job.Route is not { Page: 0, CleanupPending: false, Baseline: { } b, SubmittedPage: { } root, LastReply: { } reply } r)
            return false;
        return b.Generation == p.Generation && r.BoundGeneration == p.Generation && r.BoundEntity == p.EntityId &&
            root.Generation == p.Generation && root.EntityId == p.EntityId && root.Revision == p.Revision + 1 && Root(root, task) &&
            IsReport(reply) &&
            reply.Generation == p.Generation && reply.EntityId == p.EntityId && reply.Revision > root.Revision;
    }

    public static QuestJob Reconcile(QuestJob job, QuestDefinition recipe, IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded)
    {
        if (recipe.Id != DanaanEvidence.Id || !Consent(job)) return job;
        var journey = DanaanTransport.Journey(recipe, job);
        if (job.Route is not { } route || route.Task < 0 || route.Task + 1 >= journey.Length ||
            journey[route.Task + 1].Id != "kyros-request" || !CurrentReport(job, journey[route.Task])) return job;
        var task = journey[route.Task];
        var own = observed.Where(s => s.Connected && string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase)).ToArray();
        var states = own.Where(s => s.Map == 348 &&
            s.Dialog is { Name: "Donnan" } d && d.Generation == s.Generation && d.Revision <= s.Revision).ToArray();
        var actions = recorded.Where(a => string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase)).ToArray();
        var proofs = new List<DanaanDonnanAssignment>();
        foreach (var next in actions.Where(a => a.Kind == "next" && a.Selection is null && a.Dialog is { } d && Page(d, task.Pages[1]) &&
                     d.Generation == a.Generation && d.Revision <= a.Revision))
        {
            var confirmation = next.Dialog!;
            if (!states.Any(s => s.Generation == next.Generation && s.Revision == next.Revision && Same(s.Dialog, confirmation))) continue;
            var opening = actions.Where(a => a.Generation == next.Generation && a.Revision < confirmation.Revision &&
                a.Kind == "choose" && a.Selection == Son && a.Dialog is { } d && d.Generation == next.Generation &&
                d.EntityId == confirmation.EntityId && Root(d, task) && d.Revision <= a.Revision &&
                states.Any(s => s.Generation == a.Generation && s.Revision == a.Revision && Same(s.Dialog, d))).OrderBy(a => a.Revision).LastOrDefault();
            if (opening is null || confirmation.Revision <= opening.Revision) continue;
            foreach (var s in states.Where(s => s.Generation == next.Generation && s.Revision > next.Revision && s.Dialog is { } d &&
                         d.Revision > next.Revision && d.EntityId == confirmation.EntityId && Page(d, task.Pages[2])))
            {
                var proof = new DanaanDonnanAssignment(confirmation, s.Dialog!);
                if (Valid(proof, task) && !actions.Any(a => (a.Generation != next.Generation || a.Revision > proof.Referral.Revision) &&
                        a.Dialog?.Name is "Kyros" or "Erin") &&
                    !own.Any(s => (s.Generation != next.Generation || s.Revision > proof.Referral.Revision) &&
                        s.Dialog?.Name is "Kyros" or "Erin")) proofs.Add(proof);
            }
        }
        var distinct = proofs.DistinctBy(p => (p.Referral.Generation, p.Referral.Revision, p.Referral.EntityId)).ToArray();
        if (distinct.Length != 1) return job;
        return job with { Pending = null, Route = new() { Task = route.Task + 1 },
            DonnanAssignment = distinct[0] with { Report = route.LastReply }, Status = "Paused",
            Reason = "Recorded Donnan assignment restored; continue with Kyros" };
    }
}
