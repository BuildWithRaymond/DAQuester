namespace DAQuester.Core;

static partial class ObservedJourney
{
    internal static Task AcquireSupplyAsync(QuestContext c, ObservedQuestTask task, CancellationToken ct) => task.Kind switch
    {
        "buy" => BuyAsync(c, task, ct),
        "gather" => GatherAsync(c, task, ct),
        _ => throw new QuestBlockedException("Unknown quest supply source; no acquisition submitted.")
    };
}
