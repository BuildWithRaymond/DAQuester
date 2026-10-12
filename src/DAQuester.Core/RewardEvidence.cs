namespace DAQuester.Core;

public static class RewardEvidence
{
    public static Dictionary<string, int> Record(Transaction before, CharacterSnapshot after)
    {
        var rewards = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["experience"] = checked((int)((after.Experience ?? before.Experience) - before.Experience)),
            ["branchesConsumed"] = before.Branches - after.Count("Branch")
        };
        if (after.Gold is { } gold && gold > before.Gold)
            rewards["gold"] = checked((int)(gold - before.Gold));
        if (before.InventoryBaselineKnown)
            foreach (var name in after.Inventory.Values.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                int previous = before.InventoryCounts.FirstOrDefault(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
                int delta = after.Count(name) - previous;
                if (delta > 0)
                    rewards["item:" + name] = delta;
            }

        return rewards;
    }
}
