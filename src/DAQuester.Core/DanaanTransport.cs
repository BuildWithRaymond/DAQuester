namespace DAQuester.Core;

public static class DanaanTransport
{
    // A server-directed connection handoff is distinct from an arbitrary disconnect.
    // Resume only the recorded read-only route after fresh own destination evidence.
    public const string RedirectReason = "Server redirect. Resume on replacement connection.";

    public static ObservedQuestTask[] Journey(QuestDefinition recipe, QuestJob job) => job.JourneyVariant is { } key
        ? recipe.JourneyVariants.GetValueOrDefault(key)?.Journey ?? [] : recipe.Journey;

    public static bool IsReturn(ObservedQuestTask task, Dialog page) => task.Id == "dreamer" &&
        task.Inputs.Length == 0 && task.Outputs.Length == 0 && task.Cost == 0 && task.Experience == 0 &&
        page is { Name: "Dreamer", Text: "Goodbye.", IsPursuit: true, EntityType: 1 } &&
        page.Choices.Any(choice => choice.Text == "Wait, I have more questions!");

    public static bool CanResume(QuestDefinition recipe, QuestJob job, CharacterSnapshot s)
    {
        int index = job.Route?.Task ?? -1;
        return index >= 0 && index < Journey(recipe, job).Length &&
            (CanResumeReturn(recipe, job, s) || CanResumeTravel(job, s));
    }

    public static bool CanResumeTravel(QuestJob job, CharacterSnapshot s) => job.Quest == DanaanEvidence.Id && job.Pending is null &&
        job.Route is { Baseline: null, TravelRedirect: { } receipt } && receipt.Generation > 0 && receipt.Map > 0 &&
        !string.IsNullOrEmpty(receipt.Npc) && s.Connected && s.Generation > receipt.Generation &&
        string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) && string.Equals(receipt.Character, job.Character, StringComparison.OrdinalIgnoreCase) &&
        s.Map == receipt.Map && s.Position is not null;

    public static bool CanResumeReturn(QuestDefinition recipe, QuestJob job, CharacterSnapshot s)
    {
        // Dreamer's captured return is the narrow cross-generation exception.
        // No payment or resource delta is reconstructed from the replacement session.
        var journey = Journey(recipe, job);
        if (job.Quest != DanaanEvidence.Id || job.Pending is not { Kind: "observed-page:dreamer" } pending ||
            job.Route is not { RedirectObservedAt: { } at, SubmittedPage: { } page } route ||
            at < pending.Started || route.Task >= journey.Length || route.Task < 0 ||
            !s.Connected || s.Generation <= pending.Generation || !string.Equals(s.Name, job.Character, StringComparison.OrdinalIgnoreCase) || s.Position is null)
            return false;
        var task = journey[route.Task];
        return page.Generation == pending.Generation && route.BoundGeneration == pending.Generation &&
            IsReturn(task, page) && (task.ArrivalMap == s.Map || task.ArrivalMaps.Contains(s.Map ?? -1));
    }
}
