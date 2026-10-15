namespace DAQuester.Core;

public interface IQuestTravelCosts
{
    int? SupplyTravelCost(int map);
}

public static class QuestSupplySources
{
    // Guide source claims, not new datester captures. Both listed Mehadi Vipers
    // use sprite 49; only a named inventory increase proves the Gland itself.
    public const string GlandGuide = "https://vorlof.com/items.php?id=327_Viper%27s+Gland";
    public static readonly IReadOnlyList<int> MehadiGlandMaps = Array.AsReadOnly(new[] { 3072, 3073, 3076, 3077, 3078 });

    public static ObservedQuestTask Select(IGameSession session, ObservedQuestTask task, string profile = "datester-7.41")
    {
        if (profile != "datester-7.41" || task.Kind != "gather" || task.Item != "Viper's Gland") return task;
        var candidates = MehadiGlandMaps.Append(task.Map).Distinct().ToArray();
        int? selected = candidates.Where(map => map == session.Snapshot.Map).Select(map => (int?)map).FirstOrDefault();
        if (selected is null && session is IQuestTravelCosts costs)
            selected = candidates.Select(map => (Map: map, Cost: costs.SupplyTravelCost(map)))
                .Where(x => x.Cost is not null).OrderBy(x => x.Cost).ThenBy(x => x.Map == task.Map ? 1 : 0)
                .Select(x => (int?)x.Map).FirstOrDefault();
        if (selected is not { } destination) return task;
        return task with { Map = destination, Approach = destination == task.Map && destination != session.Snapshot.Map ? task.Approach : null,
            Monster = 49, Evidence = destination == task.Map ? task.Evidence : "Guide hypothesis: " + GlandGuide };
    }
}
