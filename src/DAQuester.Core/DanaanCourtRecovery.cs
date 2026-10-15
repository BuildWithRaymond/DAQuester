namespace DAQuester.Core;

// A submitted packet intention accompanies server state. A balance, menu or
// closed popup by itself never authorizes skipping a purchase or handoff.
public sealed record RecordedJourneyAction(string Character, long Generation, long Revision,
    string Kind, string? Selection, Dialog? Dialog);
public sealed record DanaanCourtReceipt(long Generation, long OpeningRevision, long ClosedRevision);

public static class DanaanCourtRecovery
{
    public static QuestJob Reconcile(QuestJob job, QuestDefinition recipe,
        IEnumerable<CharacterSnapshot> observed, IEnumerable<RecordedJourneyAction> recorded)
    {
        // This is one captured manual prefix, not general recovery of unjournaled
        // game progress. No reward, rite, later payment or elapsed wait is inferred.
        if (job.Quest != DanaanEvidence.Id || recipe.Id != job.Quest || job.JourneyVariant != "loures-orders" ||
            job.CompletedAt is not null || job.PermanentCompletion is not null ||
            job.DanaanOptions is not { Avatar: "cail", SuppliedMoldRiteAccepted: true } ||
            job.Pending is not { Kind: "observed-page:ase-history", EntityType: 1 } pending ||
            job.Route is not { Task: 4, Page: 0, CleanupPending: false, Baseline: { } saved, SubmittedPage: { } submitted } route ||
            saved.Generation != pending.Generation || route.BoundGeneration != pending.Generation ||
            route.BoundEntity != submitted.EntityId || pending.EntityId != submitted.EntityId ||
            submitted.Generation != pending.Generation || submitted.Revision < pending.Revision ||
            !saved.InventoryBaselineKnown || !recipe.JourneyVariants.TryGetValue("loures-court", out var variant) ||
            variant.ParentVariant != "loures-orders" || variant.Journey.Length <= 9 || variant.Journey[9].Id != "cousin")
            return job;

        string[] expected = ["ase-history", "courtney-request", "oren-brandy", "courtney-cap", "logan-cap"];
        var tasks = variant.Journey.Skip(4).Take(5).ToArray();
        if (!tasks.Select(t => t.Id).SequenceEqual(expected) || tasks.Any(t => t.Kind != "dialog" || t.Pages.Length == 0 || t.Experience != 0))
            return job;
        var states = observed.Where(s => s.Connected && s.Generation == pending.Generation &&
            string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) && s.Revision >= pending.Revision)
            .OrderBy(s => s.Revision).ToArray();
        var actions = recorded.Where(a => a.Generation == pending.Generation && a.Revision >= pending.Revision &&
            string.Equals(a.Character, job.Character, StringComparison.OrdinalIgnoreCase) && expected.Any(id =>
                tasks.Single(t => t.Id == id).Npc == a.Dialog?.Name)).OrderBy(a => a.Revision).ToArray();
        if (states.Length == 0 || actions.Length < 2 || !SameDialog(actions[0].Dialog, submitted) ||
            actions[1].Dialog is not { } selector || selector.Name != variant.Speaker || selector.Text != variant.Text ||
            selector.Choices.Length != variant.Choices.Length || !variant.Choices.All(c => selector.Choices.Any(o => o.Text == c)))
            return job;

        int actionIndex = 0;
        long through = pending.Revision - 1;
        uint gold = saved.Gold;
        uint xp = saved.Experience;
        int brandy = saved.InventoryCounts.GetValueOrDefault("Oren Brandy");
        foreach (var task in tasks)
        {
            if (actionIndex >= actions.Length)
                return job;
            var first = actions[actionIndex];
            var before = states.LastOrDefault(s => s.Revision <= first.Revision);
            if (before is null || before.Map != task.Map || before.Revision < through || !before.InventoryKnown ||
                before.Gold != gold || before.Experience != xp || before.Count("Oren Brandy") != brandy ||
                first.Dialog is not { } root || root.Generation != pending.Generation)
                return job;
            foreach (var page in task.Pages)
            {
                if (actionIndex >= actions.Length)
                    return job;
                var act = actions[actionIndex++];
                var source = states.LastOrDefault(s => s.Revision <= act.Revision);
                if (act.Revision <= through || act.Dialog is not { } dialog || dialog.Revision <= through || dialog.Revision > act.Revision ||
                    dialog.Generation != pending.Generation || dialog.EntityId != root.EntityId || dialog.EntityType != 1 ||
                    source?.Map != task.Map || !SameDialog(source.Dialog, dialog) || !Matches(act, page))
                    return job;
                through = act.Revision;
            }
            // The first changed reply after the final action must be an actual
            // close on the same map, before movement or another conversation.
            var final = actions[actionIndex - 1];
            var close = states.FirstOrDefault(s => s.Revision > through && !SameDialog(s.Dialog, final.Dialog));
            if (close is null || close.Map != task.Map || close.Dialog is not null || !close.InventoryKnown ||
                gold < task.Cost || close.Gold != gold - task.Cost || close.Experience != xp)
                return job;
            brandy += task.Outputs.Where(i => i.Item == "Oren Brandy").Sum(i => i.Count) -
                task.Inputs.Where(i => i.Item == "Oren Brandy").Sum(i => i.Count);
            if (close.Count("Oren Brandy") != brandy)
                return job;
            gold = close.Gold.Value;
            through = close.Revision;
        }
        return job with
        {
            JourneyVariant = "loures-court", Pending = null, Route = new() { Task = 9 },
            CourtRecovery = new(pending.Generation, submitted.Revision, through),
            Status = "Paused", Reason = "Recorded Courtney purchase, handoff and Logan referral reconciled; continue at the Cousin"
        };
    }

    static bool Matches(RecordedJourneyAction action, ObservedQuestPage page)
    {
        var d = action.Dialog!;
        if (d.Name != page.Speaker || d.Text != page.Text || action.Kind != page.Kind || d.Prompt is not null)
            return false;
        return page.Kind switch
        {
            "choose" => d.Choices.Count(c => c.Text == page.Choice || page.ChoiceAlternatives.Contains(c.Text)) == 1 &&
                d.Choices.Any(c => c.Text == action.Selection && (c.Text == page.Choice || page.ChoiceAlternatives.Contains(c.Text))),
            "next" => d.IsPursuit && d.HasNext && d.Choices.Length == 0,
            "close" => !d.HasNext && d.Choices.Length == 0,
            _ => false
        };
    }

    static bool SameDialog(Dialog? left, Dialog? right) => left is not null && right is not null &&
        left.Generation == right.Generation && left.Revision == right.Revision && left.EntityId == right.EntityId &&
        left.EntityType == right.EntityType && left.Name == right.Name && left.Text == right.Text &&
        left.IsPursuit == right.IsPursuit && left.Pursuit == right.Pursuit && left.Step == right.Step &&
        left.Type == right.Type && left.HasNext == right.HasNext && left.Prompt == right.Prompt && left.Choices.SequenceEqual(right.Choices);
}
