using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession : IQuestTravelCosts
{
    public int? SupplyTravelCost(int destination)
    {
        var s = Snapshot;
        if (!s.Connected || s.Map is not { } source || s.Position is not { } position) return null;
        try
        {
            var occupied = s.Entities.Values.Where(e => e.Blocks && e.Id != s.Id).Select(e => e.Position).ToHashSet();
            return maps.Plan(source, position, destination, obstacles: occupied).Cost;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or KeyNotFoundException)
        {
            return null;
        }
    }
}
