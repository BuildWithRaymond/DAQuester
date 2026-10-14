using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;

namespace DAQuester.Tests;

public class ContinentTravelTests
{
    static MapRepository PackagedMaps() => new("", Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/routes.json")));

    [Theory]
    [InlineData(10055, 43, 26)]
    [InlineData(10056, 4, 15)]
    [InlineData(10034, 46, 20)]
    public void Noam_to_Mileth_routes_through_Tagor_inn(int map, int x, int y)
    {
        var plan = PackagedMaps().Plan(map, new(x, y), 500, new(54, 69));
        Assert.Contains(plan.Steps, s => s.SourceMap == 10055 && s.Map == 1960);
        Assert.Equal("Habab", plan.Steps.First(s => s.NpcTransfer is not null).NpcTransfer!.Npc);
        Assert.Equal(500, plan.Steps.Last().Map);
    }

    [Fact]
    public void Mileth_to_Noam_routes_through_Quard_and_Asilon_caravan()
    {
        var plan = PackagedMaps().Plan(500, new(33, 37), 10055, new(43, 26));
        Assert.Contains(plan.Steps, s => s.SourceMap == 6926 && s.Map == 10028);
        Assert.Contains(plan.Steps, s => s.SourceMap == 10000 && s.Map == 10999);
        Assert.Equal(10055, plan.Steps.Last().Map);
    }

    [Fact]
    public void NPC_anchor_is_not_a_portal_or_a_collision_override()
    {
        var transfer = new NpcTransfer(new(4, 1), 2, new(1, 1), "Guide", "Travel", ["End"]);
        var record = new MapRecord(1, "Start", 8, 5, []) { NpcTransfers = [transfer] };
        using var f = new TravelPlannerTests.Assets([record, new(2, "End", 5, 5, [])], (map, x, y) => map == 1 && x == 4 && y == 1);
        var step = Assert.Single(f.Maps.Plan(1, new(1, 1), 2).Steps);
        Assert.Equal(new Tile(1, 1), step.From);
        Assert.Equal(transfer.Npc, step.NpcTransfer!.Npc);
        Assert.Equal(transfer.PlanningArrival, step.To);
        Assert.Empty(record.ExitTiles);
        Assert.True(f.Maps.Load(1).Blocked(transfer.Anchor));
        Assert.NotNull(Assert.Single(f.Maps.Route(1, 2)).NpcTransfer);
    }

    [Fact]
    public void Local_Noam_trip_keeps_walking_route()
    {
        var plan = PackagedMaps().Plan(10055, new(43, 26), 10061, new(8, 8));
        Assert.All(plan.Steps, step => Assert.Null(step.NpcTransfer));
    }

    [Fact]
    public void Medenia_world_map_can_choose_Noam_for_return_to_Mileth()
    {
        var noam = new Arbiter.Net.Server.Types.ServerWorldMapNode { MapId = 10055, Name = "Noam", Checksum = 7654 };
        Assert.Same(noam, PackagedMaps().WorldDestination(500, [noam]));
    }

    [Fact]
    public void Existing_walking_route_does_not_start_a_paid_NPC_detour()
    {
        var record = new MapRecord(1, "Start", 20, 5, [new(new(18, 1), 2, new(1, 1))])
        {
            NpcTransfers = [new(new(4, 1), 2, new(1, 1), "Guide", "Travel", ["End"])]
        };
        using var f = new TravelPlannerTests.Assets([record, new(2, "End", 5, 5, [])]);
        Assert.Null(Assert.Single(f.Maps.Plan(1, new(1, 1), 2).Steps).NpcTransfer);
    }

    [Fact]
    public void Noam_to_Asilon_uses_caravan_without_crossing_Temuair()
    {
        var plan = PackagedMaps().Plan(10055, new(43, 26), 10000, new(17, 5));
        Assert.Contains(plan.Steps, s => s.SourceMap == 10055 && s.NpcTransfer?.Service == "Caravan to Asilon or Hwarone");
        Assert.DoesNotContain(plan.Steps, s => s.Map is 1960 or 6926 or 10028);
    }
}
