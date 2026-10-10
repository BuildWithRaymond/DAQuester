using System.Text;
using System.Text.Json;
using Arbiter.Net.Server.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;
namespace DAQuester.Tests;

public class TravelPlannerTests {
 [Fact] public void Final_target_distance_can_outweigh_distance_to_first_door(){
  using var f=new Assets([new(1,"Start",20,5,[new(new(2,1),2,new(18,1)),new(new(8,1),2,new(1,1))]),new(2,"End",20,5,[])]);
  var plan=f.Maps.Plan(1,new(1,1),2,new(2,1));Assert.Equal(new Tile(8,1),Assert.Single(plan.Steps).From);Assert.Equal(10,plan.Cost);
 }
 [Fact] public void Equal_walking_cost_prefers_fewer_map_changes(){
  using var f=new Assets([new(1,"Start",8,5,[new(new(2,1),2,new(1,1)),new(new(4,2),3,new(1,1))]),new(2,"Middle",5,5,[new(new(4,1),3,new(1,1))]),new(3,"End",5,5,[])]);
  var plan=f.Maps.Plan(1,new(1,1),3,new(1,1));Assert.Single(plan.Steps);Assert.Equal(4,plan.Cost);
 }
 [Fact] public void Collision_distance_beats_nearest_door_by_straight_line(){
  using var f=new Assets([new(1,"Start",9,9,[new(new(3,1),2,new(1,1)),new(new(1,4),2,new(1,1))]),new(2,"End",5,5,[])],(m,x,y)=>m==1&&x==2&&y<8);
  Assert.Equal(new Tile(1,4),f.Maps.Plan(1,new(1,1),2,new(1,1)).Steps[0].From);
 }
 [Fact] public void Planner_does_not_walk_through_an_unintended_door(){
  using var f=new Assets([new(1,"Hall",5,1,[new(new(1,0),3,new(0,0)),new(new(4,0),2,new(0,0))]),new(2,"End",1,1,[]),new(3,"Dead end",1,1,[])]);
  Assert.Throws<InvalidOperationException>(()=>f.Maps.Plan(1,new(0,0),2));
 }
 [Fact] public void Only_recorded_portal_endpoint_may_override_collision_wall(){
  using var f=new Assets([new(1,"Start",5,5,[new(new(4,1),2,new(1,1))]),new(2,"End",5,5,[])],(m,x,_)=>m==1&&x==4);
  Assert.Equal(3,f.Maps.Plan(1,new(1,1),2,new(1,1)).Cost);
  Assert.Throws<InvalidOperationException>(()=>f.Maps.Plan(1,new(1,1),1,new(4,2)));
 }
 [Fact] public void Directed_transfer_chain_beats_valid_direct_walk(){
  using var f=new Assets([new MapRecord(1,"Shortcuts",20,5,[]){InternalTransfers=[new(new(2,1),new(8,1)),new(new(9,1),new(17,1))]}]);
  var forward=f.Maps.Plan(1,new(1,1),1,new(18,1));Assert.Equal(3,forward.Cost);Assert.Equal(2,forward.Steps.Count);
  var reverse=f.Maps.Plan(1,new(18,1),1,new(1,1));Assert.Empty(reverse.Steps);Assert.Equal(19,reverse.Cost);
 }
 [Fact] public void Standing_on_transfer_requires_leave_and_reenter_cost(){
  using var f=new Assets([new MapRecord(1,"Shortcut",10,5,[]){InternalTransfers=[new(new(2,1),new(8,1))]}]);
  Assert.Equal(3,f.Maps.Plan(1,new(2,1),1,new(9,1)).Cost);
 }
 [Fact] public void Occupied_near_exit_uses_alternative_without_polluting_static_cache(){
  using var f=new Assets([new(1,"Start",10,5,[new(new(2,1),2,new(1,1)),new(new(8,1),2,new(1,1))]),new(2,"End",5,5,[])]);
  Assert.Equal(new Tile(2,1),f.Maps.Plan(1,new(1,1),2).Steps[0].From);
  Assert.Equal(new Tile(8,1),f.Maps.Plan(1,new(1,1),2,obstacles:new HashSet<Tile>{new(2,1)}).Steps[0].From);
  Assert.Equal(new Tile(2,1),f.Maps.Plan(1,new(1,1),2).Steps[0].From);
 }
 [Fact] public void Missing_future_collision_map_cannot_offer_shortcut(){
  using var f=new Assets([new(1,"Start",10,5,[new(new(2,1),2,new(1,1)),new(new(8,1),3,new(1,1))]),new(2,"Missing",5,5,[new(new(2,1),3,new(1,1))]),new(3,"End",5,5,[])]);
  File.Delete(Path.Combine(f.Folder,"maps","lod2.map"));Assert.Equal(3,Assert.Single(f.Maps.Plan(1,new(1,1),3,new(1,1)).Steps).Map);
 }
 [Fact] public void Cancellation_is_checked_even_for_cached_route(){
  using var f=new Assets([new(1,"Map",5,5,[])]);f.Maps.Plan(1,new(1,1),1,new(3,1));
  using var stop=new CancellationTokenSource();stop.Cancel();Assert.Throws<OperationCanceledException>(()=>f.Maps.Plan(1,new(1,1),1,new(3,1),ct:stop.Token));
 }
 [Fact] public void World_nodes_rank_remaining_walk_from_recorded_landing(){
  using var f=new Assets([new(1,"Field",5,5,[new(new(2,1),2,new(18,1),true),new(new(2,1),3,new(1,1),true)]),new(2,"Near by hops",20,5,[new(new(2,1),5,new(1,1))]),new(3,"Shortcut",5,5,[new(new(2,1),4,new(1,1))]),new(4,"Middle",5,5,[new(new(2,1),5,new(1,1))]),new(5,"End",5,5,[])]);
  var quick=new ServerWorldMapNode{MapId=3,MapX=99,MapY=88,Checksum=9876};
  Assert.Same(quick,f.Maps.WorldDestination(5,[new(){MapId=2},quick],1,new(2,1),new(1,1)));
 }
 [Fact] public void Known_world_landing_with_no_walkable_route_does_not_fallback_to_hops(){
  using var f=new Assets([new(1,"Field",5,5,[new(new(2,1),2,new(1,1),true)]),new(2,"Blocked",5,5,[new(new(4,1),3,new(1,1))]),new(3,"End",5,5,[])],(m,x,_)=>m==2&&x==2);
  Assert.Throws<QuestBlockedException>(()=>f.Maps.WorldDestination(3,[new(){MapId=2}],1,new(2,1),new(1,1)));
 }
 internal sealed class Assets:IDisposable {
  public string Folder {get;}=Path.Combine(Path.GetTempPath(),"DAQuester-planner-"+Guid.NewGuid());
  public MapRepository Maps {get;}
  public Assets(MapRecord[] records,Func<int,int,int,bool>? wall=null){
   Directory.CreateDirectory(Path.Combine(Folder,"maps"));
   foreach(var r in records){var data=new byte[r.Width*r.Height*6];for(int y=0;y<r.Height;y++)for(int x=0;x<r.Width;x++)if(wall?.Invoke(r.Id,x,y)==true)data[(y*r.Width+x)*6+2]=1;File.WriteAllBytes(Path.Combine(Folder,"maps","lod"+r.Id+".map"),data);}
   using(var w=new BinaryWriter(File.Create(Path.Combine(Folder,"ia.dat")))){w.Write(2);w.Write(25);w.Write(Encoding.ASCII.GetBytes("sotp.dat".PadRight(13,'\0')));w.Write(26);w.Write((byte)15);}
   string routes=Path.Combine(Folder,"routes.json");File.WriteAllText(routes,JsonSerializer.Serialize(records));Maps=new(Folder,routes);
  }
  public void Dispose()=>Directory.Delete(Folder,true);
 }
}
