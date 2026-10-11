using Xunit;
using Xunit.Abstractions;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
namespace DAQuester.Tests;
public class NavigationTests(ITestOutputHelper output) {
 [Fact] public void Known_portal_can_be_wall_flagged_but_intermediate_walls_stay_blocked() {
  var grid=new Grid(5,5,(x,y)=>x==4||(x==2&&y<4));
  Assert.Throws<InvalidOperationException>(()=>grid.Path(new(0,0),new(4,0)));
  var path=grid.Path(new(0,0),new(4,0),allowBlockedGoal:true);
  Assert.Equal(new Tile(4,0),path.Last());Assert.DoesNotContain(new Tile(2,0),path);Assert.Contains(new Tile(2,4),path);
  Assert.Throws<InvalidOperationException>(()=>grid.Path(new(0,0),new(5,0),allowBlockedGoal:true));
 }
 [Fact] public void Portal_route_preserves_town_entrance() {
  var content=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/routes.json"));
  var r=new MapRepository("",content);var route=r.Route(500,132);
  Assert.Single(route);Assert.Equal(new Tile(13,68),route[0].From);Assert.Equal(132,route[0].Map);
 }
 [Fact] public void Missing_map_in_uncovered_profile_fails_before_movement() {
  var content=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/routes.json"));
  var r=new MapRepository(Path.Combine(Path.GetTempPath(),"DAQuester-empty-"+Guid.NewGuid()),content,"uncovered-profile");Assert.Throws<FileNotFoundException>(()=>r.Load(500));
 }
 [Fact] public void Packaged_collision_can_plan_Mileth_to_Mioas_without_installed_map_assets() {
  var content=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/routes.json"));
  var r=new MapRepository(Path.Combine(Path.GetTempPath(),"DAQuester-empty-"+Guid.NewGuid()),content);
  var plan=r.Plan(500,new(33,37),413,new(3,13));
  Assert.NotEmpty(plan.Steps);Assert.Equal(413,plan.Steps.Last().Map);Assert.True(plan.Cost>0);
  Assert.Same(r.Load(500),r.Load(500));
  output.WriteLine("Packaged Mileth (33,37) to Mioas (3,13): "+plan.Cost+" modeled actions; maps "+string.Join(" → ",new[]{500}.Concat(plan.Steps.Select(step=>step.Map))));
 }
 [Fact] public void Mileth_to_suomi_inn_uses_recorded_world_map_exit_and_walkable_inn_entrance() {
  var content=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/routes.json"));
  var r=new MapRepository("",content);var route=r.Route(500,950);
  Assert.Contains(route,p=>p.Map==3016);Assert.Equal(950,route.Last().Map);
  Assert.Equal(new Tile(32,54),route.Last().From);
  Assert.True(route.Count<=10);
 }
 [Fact] public void World_map_targets_gateway_to_interior_using_live_node_fields() {
  var content=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/routes.json"));var r=new MapRepository("",content);
  var node=new Arbiter.Net.Server.Types.ServerWorldMapNode{MapId=3016,Name="Suomi",MapX=17,MapY=1,Checksum=987};
  Assert.Same(node,r.WorldDestination(950,[new(){MapId=504,Name="Undine"},node]));
  Assert.Throws<QuestBlockedException>(()=>r.WorldDestination(950,[new(){MapId=65535}]));
 }
}
