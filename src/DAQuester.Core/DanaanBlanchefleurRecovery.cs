namespace DAQuester.Core;

public sealed record DanaanBlanchefleurReceipt(long Generation, long OpeningRevision, long ClosedRevision);

public static class DanaanBlanchefleurRecovery
{
    public const string Variant = "blanchefleur-fiosachd";

    public static QuestJob Reconcile(QuestJob job, QuestDefinition recipe,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded)
    {
        // This exact informational prefix was finished manually while the old
        // runner retained its Where-origin submission. New balances alone never
        // authorize recovery, and no later payment or handoff is inferred.
        if (job.Quest != DanaanEvidence.Id || recipe.Id != job.Quest || job.JourneyVariant is not null ||
            job.CompletedAt is not null || job.PermanentCompletion is not null ||
            job.DanaanOptions is not { Avatar: "cail", SuppliedMoldRiteAccepted: true } ||
            job.Pending is not { Kind: "observed-page:blanchefleur-origin", EntityType: 1 } pending ||
            job.Route is not { Task: 4, Page: 2, CleanupPending: false, Baseline: { InventoryBaselineKnown: true } baseline,
                SubmittedPage: { } submitted, LastReply: { } reply } route ||
            baseline.Generation != pending.Generation || route.BoundGeneration != pending.Generation ||
            route.BoundEntity != submitted.EntityId || pending.EntityId != submitted.EntityId ||
            submitted.Generation != pending.Generation || pending.Revision != submitted.Revision - 1 ||
            !recipe.JourneyVariants.TryGetValue(Variant, out var variant) || variant.ParentVariant is not null ||
            variant.TaskId != "blanchefleur-origin" || variant.Page != 3 || variant.Journey.Length <= 7 || variant.Journey[7].Id != "cousin" ||
            reply.Generation != pending.Generation || reply.EntityId != submitted.EntityId || reply.EntityType != 1 ||
            reply.Revision <= submitted.Revision || reply.Name != variant.Speaker || reply.Text != variant.Text ||
            reply.Choices.Length != 0 || variant.Choices.Length != 0)
            return job;

        var tasks = variant.Journey.Skip(4).Take(3).ToArray();
        if (!tasks.Select(t => t.Id).SequenceEqual(new[] { "blanchefleur-origin", "keefe-answers", "logan-cap" }) ||
            tasks.Any(t => t.Kind != "dialog" || t.Pages.Length == 0 || t.Cost != 0 || t.Experience != 0 || t.Inputs.Length != 0 || t.Outputs.Length != 0) ||
            recipe.Journey.Length <= 4 || !variant.Journey.Take(4).Select(t => t.Id).SequenceEqual(recipe.Journey.Take(4).Select(t => t.Id)) ||
            !tasks[0].Pages.Take(3).Select(p => System.Text.Json.JsonSerializer.Serialize(p))
                .SequenceEqual(recipe.Journey[4].Pages.Take(3).Select(p => System.Text.Json.JsonSerializer.Serialize(p))))
            return job;
        var states = observed.Where(s => s.Connected && s.Generation == pending.Generation && s.Revision >= submitted.Revision &&
            string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.Revision).ToArray();
        var actions = recorded.Where(a => a.Generation == pending.Generation && a.Revision >= submitted.Revision &&
            string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase) && tasks.Any(t => t.Npc == a.Dialog?.Name))
            .OrderBy(a => a.Revision).ToArray();
        if (actions.Length == 0 || !SameDialog(actions[0].Dialog, submitted)) return job;
        int index = 0;
        long through = submitted.Revision - 1;
        foreach (var task in tasks)
        {
            if (index >= actions.Length || actions[index].Dialog is not { } root) return job;
            foreach (var page in task.Pages.Skip(task.Id == "blanchefleur-origin" ? 2 : 0))
            {
                if (index >= actions.Length) return job;
                var action = actions[index++];
                var source = states.LastOrDefault(s => s.Revision <= action.Revision);
                if (action.Revision <= through || action.Dialog is not { } d || d.Generation != pending.Generation ||
                    d.EntityId != root.EntityId || d.EntityType != 1 || d.Revision <= through || d.Revision > action.Revision ||
                    source?.Map != task.Map || !SameDialog(source.Dialog, d) || !source.InventoryKnown ||
                    source.Gold != baseline.Gold || source.Experience != baseline.Experience || !Matches(action, page)) return job;
                through = action.Revision;
            }
            var final = actions[index - 1];
            var closed = states.FirstOrDefault(s => s.Revision > through && !SameDialog(s.Dialog, final.Dialog));
            if (closed is not { Dialog: null, InventoryKnown: true } || closed.Map != task.Map ||
                closed.Gold != baseline.Gold || closed.Experience != baseline.Experience) return job;
            through = closed.Revision;
        }
        if (index != actions.Length) return job;
        var proof = new DanaanBlanchefleurReceipt(pending.Generation, submitted.Revision, through);
        if (ConflictsWithLaterProgress(job, proof, observed, recorded)) return job;
        return job with
        {
            JourneyVariant = Variant, Pending = null, Route = new() { Task = 7 }, BlanchefleurRecovery = proof,
            Status = "Paused", Reason = "Recorded Blanchefleur, Keefe and Logan referrals reconciled; continue at the Cousin"
        };
    }

    public static bool ConflictsWithLaterProgress(QuestJob job, DanaanBlanchefleurReceipt proof,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded)
    {
        bool Later(long generation, long revision) => generation > proof.Generation || generation == proof.Generation && revision > proof.ClosedRevision;
        return recorded.Any(a => string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase) && Later(a.Generation, a.Revision) &&
                a.Dialog is { } d && !(a.Kind == "close" && d.Name == "Beggar" && d.Text == "I'm a busy man, how much is my time worth to you?")) ||
            observed.Any(s => s.Connected && string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) && Later(s.Generation, s.Revision) &&
                (s.Dialog?.Name is "Varuna" or "Greim" or "Dreamer" or "Donnan" or "Meaveen" or "Kyros" or "Erin" ||
                 s.Generation == proof.Generation && (s.Gold != job.Route!.Baseline!.Gold || s.Experience != job.Route.Baseline.Experience)));
    }

    static bool Matches(RecordedJourneyAction action, ObservedQuestPage page)
    {
        var d = action.Dialog!;
        if (d.Name != page.Speaker || d.Text != page.Text || action.Kind != page.Kind || d.Prompt is not null) return false;
        return page.Kind switch
        {
            "choose" => action.Selection == page.Choice && d.Choices.Count(c => c.Text == action.Selection) == 1,
            "next" => d.IsPursuit && d.HasNext && d.Choices.Length == 0,
            // The observed informational loop returns this menu. Closing it is
            // explicit, rather than choosing the Fiosachd clue a second time.
            "close" => d.Name == "Blanchefleur" && d.Text == "I paid little attention to him." &&
                d.Choices.Select(c => c.Text).SequenceEqual(new[] { "Something involving Fiosachd?", "Gifts from the outside world?" }),
            _ => false
        };
    }

    static bool SameDialog(Dialog? a, Dialog? b) => a is not null && b is not null && a.Generation == b.Generation && a.Revision == b.Revision &&
        a.EntityId == b.EntityId && a.EntityType == b.EntityType && a.Name == b.Name && a.Text == b.Text &&
        a.IsPursuit == b.IsPursuit && a.Pursuit == b.Pursuit && a.Step == b.Step && a.Type == b.Type &&
        a.HasNext == b.HasNext && a.Prompt == b.Prompt && a.Choices.SequenceEqual(b.Choices);
}
