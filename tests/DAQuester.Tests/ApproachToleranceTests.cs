using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;
namespace DAQuester.Tests;

public class ApproachToleranceTests {
 static Grid Open(Func<int,int,bool>? wall=null)=>new(5,5,(x,y)=>wall?.Invoke(x,y)==true);
 static int Manhattan(Tile a,Tile b)=>Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y);
 [Fact] public void Free_reachable_target_is_kept(){
  Assert.Equal(new Tile(3,3),NpcApproach.Resolve(Open(),new(1,1),new(3,3),new HashSet<Tile>(),2));
 }
 [Fact] public void Wall_target_yields_nearest_free_neighbour(){
  var tile=NpcApproach.Resolve(Open((x,y)=>x==3&&y==3),new(1,1),new(3,3),new HashSet<Tile>(),2);
  Assert.NotEqual(new Tile(3,3),tile);Assert.Equal(1,Manhattan(tile,new(3,3)));
 }
 [Fact] public void Occupied_target_yields_nearest_free_neighbour(){
  var tile=NpcApproach.Resolve(Open(),new(1,1),new(3,3),new HashSet<Tile>{new(3,3)},2);
  Assert.NotEqual(new Tile(3,3),tile);Assert.Equal(1,Manhattan(tile,new(3,3)));
 }
 [Fact] public void Nothing_free_within_tolerance_is_reported(){
  var sealed_=Open((x,y)=>x==2);
  Assert.Throws<QuestBlockedException>(()=>NpcApproach.Resolve(sealed_,new(1,1),new(4,4),new HashSet<Tile>(),1));
 }
}

[Collection("Walking cadence")]
public class ApproachToleranceWalkTests {
 static byte[] Human(uint id,Tile tile){
  var b=new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(typeof(ServerDrawHumanObjectsMessage))!.Value);
  try{new ServerDrawHumanObjectsMessage{EntityId=id,X=(ushort)tile.X,Y=(ushort)tile.Y,Name="Alt"}.Serialize(ref b);return ((ServerPacket)b.ToPacket()).Data;}finally{b.Dispose();}
 }
 [Fact] public async Task Exact_navigate_to_an_occupied_tile_still_fails(){
  await using var f=await MovingObstacleTests.Fixture.Create();
  f.Connection.Tracker.Observe(0x33,Human(99,new(3,1)));
  await Assert.ThrowsAsync<QuestBlockedException>(()=>f.Walk(new(3,1),(_,_)=>Task.FromResult(false),new("navigate",500,new(3,1))));
 }
 [Fact] public async Task Tolerant_navigate_stops_beside_an_occupied_tile(){
  await using var f=await MovingObstacleTests.Fixture.Create();
  f.Connection.Tracker.Observe(0x33,Human(99,new(3,1)));
  await f.Walk(new(3,1),(_,_)=>Task.FromResult(false),new("navigate",500,new(3,1),Tolerance:1));
  var at=f.Session.Snapshot.Position!;Assert.NotEqual(new Tile(3,1),at);Assert.Equal(1,Math.Abs(at.X-3)+Math.Abs(at.Y-1));
 }
 [Fact] public async Task Tolerant_navigate_stops_beside_a_wall_tile(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(500,"Synthetic",5,5,[]),wall:(x,y)=>x==3&&y==1);
  await f.Walk(new(3,1),(_,_)=>Task.FromResult(false),new("navigate",500,new(3,1),Tolerance:1));
  var at=f.Session.Snapshot.Position!;Assert.NotEqual(new Tile(3,1),at);Assert.Equal(1,Math.Abs(at.X-3)+Math.Abs(at.Y-1));
 }
}
