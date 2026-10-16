namespace DAQuester.Core;

public sealed record DanaanRiteReceipt(long Generation, long BaselineRevision, long ArrivalRevision, string ConsumedItem);

public static class DanaanRiteEvidence
{
    const string Trance = "Your mind swirls and you enter a trance";
    static readonly string[] Decay = ["Mold", "Rotten Apple", "Rotten Grapes"];

    internal static bool IsTask(ObservedQuestTask task) =>
        task is { Id: "supplied-mold-rite", Kind: "dialog", Map: 3018, Npc: "Greim", Cost: 0, Experience: 0, ArrivalMap: 720 } &&
        task.Inputs is [{ Item: "Mold", Count: 1 }] && task.Outputs.Length == 0;

    static string? Consumed(CharacterSnapshot s, Transaction baseline)
    {
        int mold = baseline.InventoryCounts.GetValueOrDefault("Mold");
        int apple = baseline.InventoryCounts.GetValueOrDefault("Rotten Apple");
        if (mold < 1 || s.Count("Rotten Grapes") != baseline.InventoryCounts.GetValueOrDefault("Rotten Grapes")) return null;
        if (s.Count("Mold") == mold - 1 && s.Count("Rotten Apple") == apple) return "Mold";
        if (apple > 0 && s.Count("Mold") == mold && s.Count("Rotten Apple") == apple - 1) return "Rotten Apple";
        return null;
    }

    internal static bool ResourcesMatch(CharacterSnapshot s, Transaction baseline) =>
        s is { Connected: true, Map: 720, Position: not null, InventoryKnown: true } && baseline.InventoryBaselineKnown &&
        s.Generation == baseline.Generation && s.Revision > baseline.Revision && s.Gold == baseline.Gold && s.Experience == baseline.Experience &&
        Consumed(s, baseline) is { } item && (item == "Mold" ||
            s.ActiveStatus is { Type: 3, Text: Trance } notice && notice.Generation == baseline.Generation &&
            notice.Revision > baseline.Revision && notice.Revision <= s.Revision);

    public static bool IsCheckpoint(QuestDefinition recipe, QuestJob job)
    {
        var journey = DanaanTransport.Journey(recipe, job);
        return recipe.Id == DanaanEvidence.Id && job.Quest == recipe.Id && job.CompletedAt is null && job.PermanentCompletion is null &&
            job.DanaanOptions is { Avatar: "cail", SuppliedMoldRiteAccepted: true } && job.Pending is null &&
            job.Route is { CleanupPending: false, Baseline: { InventoryBaselineKnown: true } baseline, LastReply: { } reply } route &&
            route.Task >= 0 && route.Task + 1 < journey.Length && IsTask(journey[route.Task]) && journey[route.Task].Pages.Length == 9 &&
            journey[route.Task + 1].Id == "dreamer" && route.Page == 9 && route.SubmittedPage is null &&
            route.BoundGeneration == baseline.Generation && reply.Generation == baseline.Generation && reply.EntityId == route.BoundEntity;
    }

    public static QuestJob Reconcile(QuestJob job, QuestDefinition recipe,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded)
    {
        // A replacement connection cannot prove this handoff from its balances.
        // Recover only the full original stage, including its submitted final Next.
        if (!IsCheckpoint(recipe, job)) return job;
        var route = job.Route!;
        var baseline = route.Baseline!;
        var task = DanaanTransport.Journey(recipe, job)[route.Task];
        var states = observed.Where(s => s.Connected && s.Generation == baseline.Generation && s.Revision >= baseline.Revision &&
            string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.Revision).ToArray();
        var actions = recorded.Where(a => a.Generation == baseline.Generation && a.Revision >= baseline.Revision && a.Dialog?.Name == "Greim" &&
            string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase)).OrderBy(a => a.Revision).ToArray();
        if (actions.Length != task.Pages.Length || !DanaanDonnanRecovery.Same(route.LastReply, actions[^1].Dialog!)) return job;
        long through = baseline.Revision - 1;
        for (int index = 0; index < actions.Length; index++)
        {
            var action = actions[index];
            var source = states.LastOrDefault(s => s.Revision <= action.Revision);
            var page = task.Pages[index];
            if (action.Dialog is not { } d || action.Revision <= through || d.Revision <= through || d.Revision > action.Revision ||
                d.Generation != baseline.Generation || d.EntityId != route.BoundEntity || d.EntityType != 1 || d.Name != page.Speaker ||
                d.Text != page.Text || d.Prompt is not null || action.Kind != page.Kind ||
                (page.Root ? d.IsPursuit : !d.IsPursuit) ||
                page.Kind == "next" && (!d.HasNext || d.Choices.Length != 0 || action.Selection is not null) ||
                page.Kind == "choose" && (action.Selection != page.Choice || d.Choices.Count(c => c.Text == page.Choice) != 1) ||
                source is not { Map: 3018, InventoryKnown: true } || source.Gold != baseline.Gold || source.Experience != baseline.Experience ||
                !DanaanDonnanRecovery.Same(source.Dialog, d) ||
                index == 0 && source.Revision != baseline.Revision ||
                index < actions.Length - 1 && Decay.Any(n => source.Count(n) != baseline.InventoryCounts.GetValueOrDefault(n)) ||
                index == actions.Length - 1 && Consumed(source, baseline) is null) return job;
            through = action.Revision;
        }
        var arrival = states.FirstOrDefault(s => s.Revision > through && s.Dialog is null && ResourcesMatch(s, baseline));
        if (arrival is null) return job;
        var receipt = new DanaanRiteReceipt(baseline.Generation, baseline.Revision, arrival.Revision, Consumed(arrival, baseline)!);
        if (ConflictsWithLaterProgress(job, receipt, observed, recorded)) return job;
        return job with { Route = new() { Task = route.Task + 1 }, RiteRecovery = receipt,
            Status = "Paused", Reason = "Recorded Greim decay debit and Hall of Souls arrival reconciled; continue with Dreamer" };
    }

    public static bool ConflictsWithLaterProgress(QuestJob job, DanaanRiteReceipt receipt,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded) =>
        recorded.Any(a => a.Dialog is not null && string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase) &&
            (a.Generation > receipt.Generation || a.Generation == receipt.Generation && a.Revision > job.Route!.LastReply!.Revision)) ||
        observed.Any(s => s.Connected && s.Generation == receipt.Generation && s.Revision >= receipt.ArrivalRevision &&
            string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) &&
            (!s.InventoryKnown || s.Gold != job.Route!.Baseline!.Gold || s.Experience != job.Route.Baseline.Experience ||
                Consumed(s, job.Route.Baseline) != receipt.ConsumedItem));
}
