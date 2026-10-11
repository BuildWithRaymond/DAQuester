using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using Xunit;
namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class GroundCollectionFastTests {
 static byte[] Payload(ServerMessage message){var b=new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);try{message.Serialize(ref b);return b.ToPacket().Data;}finally{b.Dispose();}}
 static void Living(MovingObstacleTests.Fixture f)=>f.Connection.Tracker.Observe(8,Payload(new ServerStatusMessage{Fields=Arbiter.Net.Types.StatsFieldFlags.Vitals,Health=100}));
 static byte[] Item()=>Payload(new ServerDrawObjectsMessage{Entities=[new ServerItemEntity{Id=77,X=6,Y=1,Sprite=70}]});
 static GameCommand Collect()=>new("collect-ground",542,new(8,1),Amount:70,StopWhen:new("Emerald Knight Insignia",1));
 static byte[] Popup()=>Payload(new ServerPursuitMessage{DialogType=DialogType.Popup,EntityType=EntityTypeFlags.Reactor,EntityId=88,PursuitId=731,StepId=1,Name="Forest",Content="Synthetic discovery popup",HasNextButton=true});
 [Fact] public async Task Collection_preserves_an_open_popup_without_sending_close_or_movement(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(542,"Synthetic Shinewood",12,5,[]));Living(f);f.Connection.Tracker.Observe(0x30,Popup());var popup=f.Session.Snapshot.Dialog;
  await Assert.ThrowsAnyAsync<QuestBlockedException>(()=>f.Session.ExecuteAsync(Collect(),default));
  Assert.Same(popup,f.Session.Snapshot.Dialog);Assert.False(f.Server.GetStream().DataAvailable);
 }
 [Fact] public async Task Fast_collection_stops_for_a_popup_and_drains_existing_movement_without_dismissal(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(542,"Synthetic Shinewood",12,5,[]));Living(f);f.Session.FastMode=true;var replies=new List<Task>();
  try{await Assert.ThrowsAnyAsync<QuestBlockedException>(()=>f.Walk(new(8,1),(n,next)=>{
   if(n==1)f.Connection.Tracker.Observe(0x30,Popup());
   var origin=next with{X=next.X-1};replies.Add(Task.Run(async()=>{await Task.Delay(500);await f.Send(11,[1,0,(byte)origin.X,0,(byte)origin.Y,0,0,0,0,0]);}));return Task.FromResult(true);
  },Collect(),projectedServer:true));
  Assert.Single(f.Commands,c=>c==6);Assert.Equal(0,f.Connection.Movement.Pending);Assert.True(f.Session.Snapshot.Dialog!.HasNext);
  }finally{await Task.WhenAll(replies);}
 }
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Fast_collection_patrol_and_pickup_approach_send_next_step_before_delayed_reply(bool visible){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(542,"Synthetic Shinewood",12,5,[]));Living(f);f.Session.FastMode=true;
  if(visible)f.Connection.Tracker.Observe(7,Item());
  var replies=new List<Task>();
  try{await f.Walk(new(8,1),(n,next)=>{
   if(n==2)Assert.Equal(new Tile(1,1),f.Session.Snapshot.Position);
   var origin=next with{X=next.X-1};replies.Add(Task.Run(async()=>{await Task.Delay(500);await f.Send(11,[1,0,(byte)origin.X,0,(byte)origin.Y,0,0,0,0,0]);}));return Task.FromResult(true);
  },Collect(),async packet=>{
   Assert.Equal(0,f.Connection.Movement.Pending);
   var get=new ClientGetMessage();get.Deserialize(new NetworkPacketReader(packet));Assert.Equal(6,get.X);
   await f.Send(15,Payload(new ServerAddInventoryMessage{Slot=get.Slot,Name="Emerald Knight Insignia",Quantity=1}));await f.Send(14,[0,0,0,77]);
  },projectedServer:true);
  Assert.Equal(visible?new Tile(6,1):new Tile(8,1),f.Session.Snapshot.Position);
  Assert.Equal(visible?1:0,f.Commands.Count(c=>c==7));Assert.Equal(0,f.Connection.Movement.Pending);
  }finally{await Task.WhenAll(replies);}
 }
 [Fact] public async Task Fast_patrol_replans_for_new_item_and_drains_before_pickup(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(542,"Synthetic Shinewood",12,5,[]));Living(f);f.Session.FastMode=true;
  var replies=new List<Task>();
  try{await f.Walk(new(8,1),(n,next)=>{
   if(n==1)f.Connection.Tracker.Observe(7,Item());
   var origin=next with{X=next.X-1};replies.Add(Task.Run(async()=>{await Task.Delay(500);await f.Send(11,[1,0,(byte)origin.X,0,(byte)origin.Y,0,0,0,0,0]);}));return Task.FromResult(true);
  },Collect(),async packet=>{
   Assert.Equal(0,f.Connection.Movement.Pending);
   var get=new ClientGetMessage();get.Deserialize(new NetworkPacketReader(packet));Assert.Equal(6,get.X);
   await f.Send(15,Payload(new ServerAddInventoryMessage{Slot=get.Slot,Name="Emerald Knight Insignia",Quantity=1}));await f.Send(14,[0,0,0,77]);
  },projectedServer:true);
  Assert.Equal(new Tile(6,1),f.Session.Snapshot.Position);Assert.Single(f.Commands,c=>c==7);
  }finally{await Task.WhenAll(replies);}
 }
}
