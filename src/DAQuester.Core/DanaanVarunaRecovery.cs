namespace DAQuester.Core;

public sealed record DanaanVarunaReceipt(long Generation, long BaselineRevision, long ClosedRevision);

public static class DanaanVarunaRecovery
{
    public static QuestJob Reconcile(QuestJob job, QuestDefinition recipe,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded)
    {
        // This manual cleanup follows a journaled handoff. Require its original
        // packets and the final Next/close; reconnect balances prove nothing.
        if (job.Quest != DanaanEvidence.Id || recipe.Id != job.Quest || job.JourneyVariant is not (null or "loures-orders" or "loures-court" or DanaanBlanchefleurRecovery.Variant) ||
            job.CompletedAt is not null || job.PermanentCompletion is not null ||
            job.DanaanOptions is not { Avatar: "cail", SuppliedMoldRiteAccepted: true } ||
            job.Pending is not { Kind: "observed-page:varuna-dreamer", EntityType: 1 } pending ||
            job.Route is not { Page: 9, CleanupPending: false, Baseline: { InventoryBaselineKnown: true } baseline, SubmittedPage: { } submitted } route ||
            baseline.Generation != pending.Generation || route.BoundGeneration != pending.Generation ||
            submitted.Generation != pending.Generation || submitted.Revision < pending.Revision ||
            submitted.EntityId != route.BoundEntity || pending.EntityId != route.BoundEntity)
            return job;
        var resolved = route.LastReply is { } reply ? DanaanReplyContinuation.Select(job, recipe, submitted, reply) : job;
        var journey = DanaanTransport.Journey(recipe, resolved);
        if (route.Task < 0 || route.Task + 1 >= journey.Length || journey[route.Task + 1].Id is not ("greim-warning" or "mold"))
            return job;
        string next = journey[route.Task + 1].Id;
        var task = journey[route.Task];
        if (task is not { Id: "varuna-dreamer", Kind: "dialog", Map: 3075, Npc: "Varuna", Cost: 0, Experience: 0 } ||
            task.Inputs.Length != 1 || task.Inputs[0] is not { Item: "Viper's Gland", Count: 1 } || task.Outputs.Length != 0 ||
            task.Pages.Length != 11 || task.Pages[9].Kind != "next" || task.Pages[10].Kind != "next" ||
            !Matches(new(job.Character, submitted.Generation, submitted.Revision, "next", null, submitted), task.Pages[9]))
            return job;
        var states = observed.Where(s => s.Connected && s.Generation == pending.Generation && s.Revision >= baseline.Revision &&
            string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.Revision).ToArray();
        var ownActions = recorded.Where(a => a.Generation == pending.Generation && a.Revision >= baseline.Revision &&
            string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase)).ToArray();
        var actions = ownActions.Where(a => a.Dialog?.Name == task.Npc).OrderBy(a => a.Revision).ToArray();
        if (actions.Length != task.Pages.Length || !SameDialog(actions[9].Dialog, submitted))
            return job;
        long through = baseline.Revision - 1;
        int gland = baseline.InventoryCounts.GetValueOrDefault("Viper's Gland");
        if (gland < 1)
            return job;
        for (int page = 0; page < task.Pages.Length; page++)
        {
            var action = actions[page];
            var source = states.LastOrDefault(s => s.Revision <= action.Revision);
            if (action.Revision <= through || action.Dialog is not { } d || d.Revision <= through || d.Revision > action.Revision ||
                d.Generation != pending.Generation || d.EntityId != route.BoundEntity || d.EntityType != 1 ||
                source is not { Map: 3075, InventoryKnown: true } || !SameDialog(source.Dialog, d) ||
                source.Gold != baseline.Gold || source.Experience != baseline.Experience ||
                source.Count("Viper's Gland") != gland - (page >= 2 ? 1 : 0) || !Matches(action, task.Pages[page]))
                return job;
            through = action.Revision;
        }
        var close = states.FirstOrDefault(s => s.Revision > through && !SameDialog(s.Dialog, actions[^1].Dialog));
        if (close is not { Map: 3075, Dialog: null, InventoryKnown: true } || close.Gold != baseline.Gold ||
            close.Experience != baseline.Experience || close.Count("Viper's Gland") != gland - 1)
            return job;
        var receipt = new DanaanVarunaReceipt(pending.Generation, baseline.Revision, close.Revision);
        if (ConflictsWithLaterProgress(job, receipt, states, ownActions))
            return job;
        return resolved with
        {
            Pending = null, Route = new() { Task = route.Task + 1 },
            VarunaRecovery = receipt,
            Status = "Paused", Reason = "Recorded Varuna handoff and final closure reconciled; continue with " +
                (next == "mold" ? "Mold" : "Greim's warning")
        };
    }

    public static bool ConflictsWithLaterProgress(QuestJob job, DanaanVarunaReceipt receipt,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded)
    {
        if (job.Route?.Baseline is not { } baseline)
            return true;
        int gland = baseline.InventoryCounts.GetValueOrDefault("Viper's Gland") - 1;
        return recorded.Any(a => a.Generation == receipt.Generation && a.Revision > receipt.ClosedRevision && a.Dialog is not null &&
                string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase)) ||
            observed.Any(s => s.Connected && s.Generation == receipt.Generation && s.Revision > receipt.ClosedRevision &&
                string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) &&
                (!s.InventoryKnown || s.Gold != baseline.Gold || s.Experience != baseline.Experience || s.Count("Viper's Gland") != gland));
    }

    static bool Matches(RecordedJourneyAction action, ObservedQuestPage page)
    {
        var d = action.Dialog;
        return d is { IsPursuit: true, Prompt: null } && d.Name == page.Speaker &&
            (d.Text == page.Text || page.TextAlternatives.Contains(d.Text, StringComparer.Ordinal)) && action.Kind == page.Kind &&
            (page.Kind == "next" && d.HasNext && d.Choices.Length == 0 ||
                page.Kind == "choose" && d.Choices.Count(c => c.Text == page.Choice) == 1 && action.Selection == page.Choice);
    }

    static bool SameDialog(Dialog? left, Dialog? right) => left is not null && right is not null &&
        left.Generation == right.Generation && left.Revision == right.Revision && left.EntityId == right.EntityId &&
        left.EntityType == right.EntityType && left.Name == right.Name && left.Text == right.Text &&
        left.IsPursuit == right.IsPursuit && left.Pursuit == right.Pursuit && left.Step == right.Step &&
        left.Type == right.Type && left.HasNext == right.HasNext && left.Prompt == right.Prompt && left.Choices.SequenceEqual(right.Choices);
}
