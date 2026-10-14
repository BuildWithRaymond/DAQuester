namespace DAQuester.Core;

static partial class ObservedJourney
{
    static ObservedQuestTask[] JourneyFor(QuestContext c, QuestDefinition recipe) =>
        c.Job.JourneyVariant is not { } key ? recipe.Journey :
        recipe.JourneyVariants.TryGetValue(key, out var variant) ? variant.Journey :
        throw new QuestBlockedException("The saved quest path is unavailable. Preserve its checkpoint for reconciliation.");

    static async Task<ObservedQuestTask> SelectObservedVariantAsync(QuestContext c, QuestDefinition recipe,
        ObservedQuestTask task, Dialog submitted)
    {
        // Switch only on a fresh captured reply from the bound NPC. A variant
        // must preserve every submitted page so it cannot rewind a committed action.
        if (c.Job.JourneyVariant is not null || c.Job.Quest != DanaanEvidence.Id ||
            c.Session.Snapshot.Dialog is not { } reply || reply.Generation != submitted.Generation ||
            reply.EntityId != submitted.EntityId || reply.EntityType != 1 || reply.Revision <= submitted.Revision)
            return task;
        var route = c.Job.Route!;
        var candidates = recipe.JourneyVariants.Where(pair => pair.Value.TaskId == task.Id &&
            pair.Value.Page == route.Page + 1 && pair.Value.Speaker == reply.Name && pair.Value.Text == reply.Text &&
            pair.Value.Choices.All(choice => reply.Choices.Any(offered => offered.Text == choice))).ToArray();
        if (candidates.Length == 0)
            return task;
        if (candidates.Length != 1)
            throw new QuestBlockedException("The observed reply matches multiple quest paths; no choice sent.");
        var selected = candidates[0];
        var journey = selected.Value.Journey;
        if (journey.Length <= route.Task || journey[route.Task].Id != task.Id ||
            !journey[route.Task].Pages.Take(route.Page + 1).SequenceEqual(task.Pages.Take(route.Page + 1)) ||
            !journey.Take(route.Task).Select(t => t.Id).SequenceEqual(recipe.Journey.Take(route.Task).Select(t => t.Id)))
            throw new QuestBlockedException("The observed quest path does not preserve the submitted checkpoint.");
        await c.SaveAsync(c.Job with
        {
            JourneyVariant = selected.Key
        });
        return journey[route.Task];
    }
}
