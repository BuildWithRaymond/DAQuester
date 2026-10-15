using System.Text.Json;

namespace DAQuester.Core;

// A local NPC reply can select a different captured continuation without
// changing the already completed opening or repeating the submitted page.
public sealed record DanaanReplyReceipt(int Task, string? SourceVariant, int SourceTask, int Page, Dialog Submitted, Dialog Reply);

public static class DanaanReplyContinuation
{
    public static QuestJob Select(QuestJob job, QuestDefinition recipe, Dialog submitted, Dialog reply)
    {
        if (job.Quest != DanaanEvidence.Id || recipe.Id != job.Quest || job.Route is not { } route ||
            job.Pending is not { EntityType: 1 } pending || pending.Generation != submitted.Generation ||
            pending.Revision != submitted.Revision - 1 || route.BoundGeneration != submitted.Generation ||
            route.BoundEntity != submitted.EntityId || pending.EntityId != submitted.EntityId)
            return job;
        var journey = DanaanTransport.Journey(recipe, job);
        int next = route.Page + 1;
        if (route.Task < 0 || route.Task >= journey.Length) return job;
        var task = journey[route.Task];
        if (pending.Kind != "observed-page:" + task.Id || next >= task.Pages.Length ||
            !SameActor(submitted, reply) || !Information(submitted, task.Pages[route.Page]) ||
            Information(reply, task.Pages[next])) return job;

        var sources = new List<(string? Key, ObservedQuestTask[] Journey)> { (null, recipe.Journey) };
        sources.AddRange(recipe.JourneyVariants.Select(p => ((string?)p.Key, p.Value.Journey)));
        var candidates = sources.SelectMany(source => source.Journey.Select((t, i) => (source.Key, source.Journey, Task: t, Index: i)))
            .Where(c => Compatible(task, c.Task, next) && Information(reply, c.Task.Pages[next]))
            .GroupBy(c => JsonSerializer.Serialize(c.Journey.Skip(c.Index))).Select(g => g.First()).ToArray();
        // Even a known paragraph cannot choose between conflicting future paths.
        if (candidates.Length != 1) return job;
        var selected = candidates[0];
        return job with { DanaanReplies = [..job.DanaanReplies,
            new(route.Task, selected.Key, selected.Index, next, submitted, reply)] };
    }

    internal static ObservedQuestTask[] Apply(QuestDefinition recipe, QuestJob job, ObservedQuestTask[] journey)
    {
        int previous = -1;
        foreach (var receipt in job.DanaanReplies)
        {
            var source = receipt.SourceVariant is null ? recipe.Journey : recipe.JourneyVariants.GetValueOrDefault(receipt.SourceVariant)?.Journey ?? [];
            if (job.Quest != DanaanEvidence.Id || receipt.Task <= previous || receipt.Task >= journey.Length || receipt.Task < 0 ||
                receipt.SourceTask < 0 || receipt.SourceTask >= source.Length || receipt.Page <= 0 ||
                !SameActor(receipt.Submitted, receipt.Reply) ||
                !Compatible(journey[receipt.Task], source[receipt.SourceTask], receipt.Page) ||
                !Information(receipt.Submitted, journey[receipt.Task].Pages[receipt.Page - 1]) ||
                !Information(receipt.Reply, source[receipt.SourceTask].Pages[receipt.Page])) return [];
            journey = [..journey.Take(receipt.Task), ..source.Skip(receipt.SourceTask)];
            previous = receipt.Task;
        }
        return journey;
    }

    static bool SameActor(Dialog submitted, Dialog reply) => submitted.Generation > 0 && submitted.Revision > 0 &&
        reply.Generation == submitted.Generation && reply.EntityId == submitted.EntityId && reply.EntityType == 1 &&
        submitted.EntityType == 1 && reply.Name == submitted.Name && reply.Revision > submitted.Revision;

    static bool Information(Dialog d, ObservedQuestPage page) => page.Kind == "next" && d.Name == page.Speaker &&
        (d.Text == page.Text || page.TextAlternatives.Contains(d.Text, StringComparer.Ordinal)) &&
        d.IsPursuit && d.HasNext && d.Prompt is null && d.Choices.Length == 0;

    static bool Compatible(ObservedQuestTask current, ObservedQuestTask candidate, int page) =>
        current.Id == candidate.Id && current.Kind == "dialog" && candidate.Kind == "dialog" && current.Map == candidate.Map &&
        current.Npc == candidate.Npc && current.Speech == candidate.Speech && current.Cost == candidate.Cost &&
        current.Experience == candidate.Experience && current.Inputs.SequenceEqual(candidate.Inputs) && current.Outputs.SequenceEqual(candidate.Outputs) &&
        current.ArrivalMap == candidate.ArrivalMap && current.ArrivalMaps.SequenceEqual(candidate.ArrivalMaps) &&
        current.Pages.Length > page && candidate.Pages.Length > page &&
        current.Pages.Take(page).Select(p => JsonSerializer.Serialize(p)).SequenceEqual(candidate.Pages.Take(page).Select(p => JsonSerializer.Serialize(p)));
}
