using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using System.Diagnostics;
using Xunit;
namespace DAQuester.Tests;

[CollectionDefinition("Walking cadence",DisableParallelization=true)]
public class WalkingCadenceCollection {}

[Collection("Walking cadence")]
public class FastWalkingTests {
 static byte[] Human(uint id,Tile tile,string name="OtherPlayer")=>Payload(new ServerDrawHumanObjectsMessage{EntityId=id,X=(ushort)tile.X,Y=(ushort)tile.Y,Name=name});
 static byte[] Payload(ServerMessage message){
  var builder=new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value);
  try{message.Serialize(ref builder);return ((ServerPacket)builder.ToPacket()).Data;}finally{builder.Dispose();}
 }
 static async Task<double> Interval(MovingObstacleTests.Fixture f,Action? first=null){
  long sent=0;double elapsed=0;
  await f.Walk(new(3,1),(n,_)=>{
   if(n==1){sent=Stopwatch.GetTimestamp();first?.Invoke();}
   else elapsed=Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
   return Task.FromResult(false);
  },new("navigate",f.Session.Snapshot.Map,new(3,1)));return elapsed;
 }
 [Fact] public async Task Checkbox_off_preserves_configured_cadence(){
  await using var f=await MovingObstacleTests.Fixture.Create(60);
  Assert.False(f.Session.FastMode);Assert.InRange(await Interval(f),40,350);
 }
 [Fact] public async Task Fast_mode_uses_excalibur_160ms_cadence_alone(){
  await using var f=await MovingObstacleTests.Fixture.Create(600);f.Session.FastMode=true;
  Assert.InRange(await Interval(f),140,500);
 }
 [Theory][InlineData("OtherPlayer")][InlineData("")]
 public async Task Other_player_including_unnamed_uses_excalibur_430ms_cadence(string name){
  await using var f=await MovingObstacleTests.Fixture.Create(600);f.Session.FastMode=true;
  f.Connection.Tracker.Observe(0x33,Human(99,new(1,4),name));
  Assert.InRange(await Interval(f),410,570);
 }
 [Fact] public async Task Npc_and_self_do_not_slow_fast_walking(){
  await using var f=await MovingObstacleTests.Fixture.Create(600);f.Session.FastMode=true;
  f.Connection.Tracker.Observe(0x33,Human(42,new(1,1),"Nuclear"));
  f.Connection.Tracker.Observe(7,Payload(new ServerDrawObjectsMessage{Entities=[new ServerCreatureEntity{Id=88,X=1,Y=4,Name="Mundane"}]}));
  Assert.InRange(await Interval(f),140,500);
 }
 [Fact] public async Task Player_appearing_during_fast_wait_extends_current_interval(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;Task arrival=Task.CompletedTask;
  try{Assert.True(await Interval(f,()=>arrival=Task.Run(async()=>{await Task.Delay(70);f.Connection.Tracker.Observe(0x33,Human(99,new(1,4)));}))>=410);}
  finally{await arrival;}
 }
 [Fact] public async Task Player_removed_during_normal_wait_restores_fast_interval(){
  await using var f=await MovingObstacleTests.Fixture.Create(600);f.Session.FastMode=true;Task departure=Task.CompletedTask;
  f.Connection.Tracker.Observe(0x33,Human(99,new(1,4)));
  try{Assert.InRange(await Interval(f,()=>departure=Task.Run(async()=>{await Task.Delay(70);f.Connection.Tracker.Observe(0x0E,[0,0,0,99]);})),140,350);}
  finally{await departure;}
 }
 [Fact] public async Task Disabling_fast_mode_during_wait_restores_configured_cadence(){
  await using var f=await MovingObstacleTests.Fixture.Create(600);f.Session.FastMode=true;Task change=Task.CompletedTask;
  try{Assert.True(await Interval(f,()=>change=Task.Run(async()=>{await Task.Delay(70);f.Session.FastMode=false;}))>=580);}
  finally{await change;}
 }
 [Fact] public async Task Enabling_fast_mode_during_wait_takes_effect_without_new_session(){
  await using var f=await MovingObstacleTests.Fixture.Create(600);Task change=Task.CompletedTask;
  try{Assert.InRange(await Interval(f,()=>change=Task.Run(async()=>{await Task.Delay(70);f.Session.FastMode=true;})),140,350);}
  finally{await change;}
 }
 [Fact] public async Task Cancellation_during_fast_wait_sends_no_second_step(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;using var stop=new CancellationTokenSource();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Walk(new(3,1),(n,_)=>{if(n==1)stop.CancelAfter(70);return Task.FromResult(false);},cancellation:stop.Token));
  Assert.Single(f.Commands,c=>c==6);
 }
 [Theory][InlineData(21,true)][InlineData(22,false)]
 public async Task Player_radius_matches_excalibur_20_tile_manhattan_boundary(int y,bool normal){
  await using var f=await MovingObstacleTests.Fixture.Create(600,record:new MapRecord(500,"Synthetic",40,40,[]));f.Session.FastMode=true;
  f.Connection.Tracker.Observe(0x33,Human(99,new(2,y)));
  double interval=await Interval(f);Assert.InRange(interval,normal?410:140,normal?570:350);
 }
 [Fact] public async Task Player_moving_into_radius_during_fast_wait_slows_next_step(){
  await using var f=await MovingObstacleTests.Fixture.Create(600,record:new MapRecord(500,"Synthetic",40,40,[]));f.Session.FastMode=true;Task approach=Task.CompletedTask;
  f.Connection.Tracker.Observe(0x33,Human(99,new(2,22)));
  try{Assert.True(await Interval(f,()=>approach=Task.Run(async()=>{await Task.Delay(70);f.Connection.Tracker.Observe(0x0C,[0,0,0,99,0,2,0,22,0,0]);}))>=410);}
  finally{await approach;}
 }
 [Theory][InlineData(10056)][InlineData(10180)]
 public async Task Excalibur_restricted_maps_keep_normal_speed_even_when_alone(int map){
  await using var f=await MovingObstacleTests.Fixture.Create(600,record:new MapRecord(map,"Synthetic",5,5,[]));f.Session.FastMode=true;
  Assert.InRange(await Interval(f),410,570);
 }
 [Fact] public void Player_classification_survives_movement_and_clears_on_removal_and_map_reload(){
  var tracker=new StateTracker(1);tracker.Identity("Nuclear",42);
  tracker.Observe(0x33,Human(99,new(2,1)));Assert.True(tracker.Snapshot.Entities[99].IsPlayer);
  tracker.Observe(0x0C,[0,0,0,99,0,2,0,1,2,0]);Assert.True(tracker.Snapshot.Entities[99].IsPlayer);Assert.Equal(new Tile(2,2),tracker.Snapshot.Entities[99].Position);
  tracker.Observe(0x0E,[0,0,0,99]);Assert.Empty(tracker.Snapshot.Entities);
  tracker.Observe(0x33,Human(99,new(2,1)));tracker.Observe(0x15,[1,244,5,5,0,0,0,0,0,0]);Assert.Empty(tracker.Snapshot.Entities);
  Assert.Empty(new StateTracker(2).Snapshot.Entities);
 }
 [Fact] public async Task Inventory_goal_arriving_during_fast_wait_stops_before_second_step(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;Task inventory=Task.CompletedTask;
  try{await f.Walk(new(3,1),(n,_)=>{
   if(n==1)inventory=Task.Run(async()=>{await Task.Delay(70);f.Connection.Tracker.Observe(15,Payload(new ServerAddInventoryMessage{Slot=1,Name="Branch",Quantity=6}));});
   return Task.FromResult(false);
  },new("navigate",500,new(3,1),StopWhen:new("Branch",6)));Assert.Single(f.Commands,c=>c==6);}
  finally{await inventory;}
 }
 [Fact] public async Task Normal_mode_waits_for_acknowledgment_before_next_step(){
  await using var f=await MovingObstacleTests.Fixture.Create(160);long sent=0;
  await f.Walk(new(3,1),async(n,_)=>{
   if(n==1){sent=Stopwatch.GetTimestamp();await Task.Delay(500);Assert.False(f.Server.GetStream().DataAvailable,"Normal mode sent another step before acknowledgment.");}
   else Assert.True(Stopwatch.GetElapsedTime(sent).TotalMilliseconds>=490);
   return false;
  });Assert.Equal(2,f.Commands.Count(c=>c==6));
 }
 [Fact] public async Task Fast_mode_keeps_160ms_cadence_with_500ms_replies_without_inventing_position(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(500,"Synthetic",10,5,[]));f.Session.FastMode=true;
  var times=new List<long>();var replies=new List<Task>();
  try{await f.Walk(new(5,1),(n,next)=>{
   times.Add(Stopwatch.GetTimestamp());if(n==2)Assert.Equal(new Tile(1,1),f.Session.Snapshot.Position);
   var origin=next with{X=next.X-1};replies.Add(Task.Run(async()=>{await Task.Delay(500);await f.Send(11,[1,0,(byte)origin.X,0,(byte)origin.Y,0,0,0,0,0]);}));return Task.FromResult(true);
  },projectedServer:true);}finally{await Task.WhenAll(replies);}
  Assert.Equal(new Tile(5,1),f.Session.Snapshot.Position);
  Assert.InRange(Stopwatch.GetElapsedTime(times[0],times[1]).TotalMilliseconds,140,300);
  Assert.InRange(Stopwatch.GetElapsedTime(times[1],times[2]).TotalMilliseconds,140,300);
 }
 [Fact] public async Task Fast_step_is_presented_before_reply_and_not_animated_twice(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;
  using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(3));
  var before=f.Session.Snapshot;var walking=f.Session.ExecuteAsync(new("step",Target:new(2,1)),limit.Token);
  try{
   var first=await ReadClientFrame(f,TimeSpan.FromMilliseconds(350));Assert.Equal((byte)12,first[0]);
   Assert.Equal(before.Revision,f.Session.Snapshot.Revision);Assert.Equal(before.Position,f.Session.Snapshot.Position);Assert.Equal(0,f.Connection.VisibleMoves);
   await f.Send(11,[1,0,1,0,1,0,0,0,0,0]);await walking;
   var acknowledgment=await ReadClientFrame(f,TimeSpan.FromSeconds(1));Assert.Equal((byte)11,acknowledgment[0]);Assert.Equal(unchecked((byte)(first[1]+1)),acknowledgment[1]);
   await f.Send(1,[0]);var following=await ReadClientFrame(f,TimeSpan.FromSeconds(1));Assert.Equal((byte)1,following[0]);Assert.Equal(unchecked((byte)(first[1]+2)),following[1]);
   Assert.Equal(1,f.Connection.VisibleMoves);Assert.Equal(new Tile(2,1),f.Session.Snapshot.Position);
  }finally{limit.Cancel();try{await walking;}catch(OperationCanceledException){}}
 }
 [Fact] public async Task Fast_queue_is_bounded_and_stop_leaves_observed_position_unchanged(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(500,"Synthetic",12,5,[]));f.Session.FastMode=true;
  using var stop=new CancellationTokenSource(650);
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Walk(new(10,1),(_,_)=>Task.FromResult(true),cancellation:stop.Token,projectedServer:true));
  Assert.Equal(3,f.Commands.Count(c=>c==6));Assert.Equal(3,f.Connection.Movement.Pending);Assert.True(f.Connection.Movement.Recovery);
  Assert.Equal(new Tile(1,1),f.Session.Snapshot.Position);Assert.Equal(0,f.Connection.VisibleMoves);
 }
 [Fact] public async Task Player_appearing_with_a_pending_fast_step_drains_then_uses_normal_cadence(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;Task arrival=Task.CompletedTask;var times=new List<long>();
  try{await f.Walk(new(4,1),(n,_)=>{
   times.Add(Stopwatch.GetTimestamp());if(n!=1)return Task.FromResult(false);
   arrival=Task.Run(async()=>{await Task.Delay(70);await f.Send(0x33,Human(99,new(1,4)));await Task.Delay(430);await f.Send(11,[1,0,1,0,1,0,0,0,0,0]);});return Task.FromResult(true);
  });}finally{await arrival;}
  Assert.InRange(Stopwatch.GetElapsedTime(times[0],times[1]).TotalMilliseconds,490,900);
  Assert.InRange(Stopwatch.GetElapsedTime(times[1],times[2]).TotalMilliseconds,410,750);
  Assert.Equal("normal pace near players",f.Session.FastModeDescription);
 }
 [Fact] public async Task Inventory_goal_drains_already_sent_steps_before_navigation_returns(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(500,"Synthetic",10,5,[]));f.Session.FastMode=true;var replies=new List<Task>();
  try{await f.Walk(new(8,1),(n,next)=>{
   if(n==1)replies.Add(Task.Run(async()=>{await Task.Delay(220);await f.Send(15,Payload(new ServerAddInventoryMessage{Slot=1,Name="Branch",Quantity=6}));}));
   var origin=next with{X=next.X-1};replies.Add(Task.Run(async()=>{await Task.Delay(500);await f.Send(11,[1,0,(byte)origin.X,0,(byte)origin.Y,0,0,0,0,0]);}));return Task.FromResult(true);
  },new("navigate",500,new(8,1),StopWhen:new("Branch",6)),projectedServer:true);
  Assert.Equal(2,f.Commands.Count(c=>c==6));Assert.Equal(0,f.Connection.Movement.Pending);Assert.Equal(new Tile(3,1),f.Session.Snapshot.Position);
  }finally{await Task.WhenAll(replies);}
 }
 [Fact] public async Task Fast_approach_drains_before_sending_doorway_step(){
  var record=new MapRecord(500,"Synthetic",7,5,[new(new(4,1),501,new(1,1))]);
  await using var f=await MovingObstacleTests.Fixture.Create(record:record,additional:[new(501,"Destination",5,5,[])]);f.Session.FastMode=true;var replies=new List<Task>();
  try{await f.Walk(new(1,1),async(n,next)=>{
   if(next==new Tile(4,1)){
    Assert.Equal(3,n);Assert.Equal(new Tile(3,1),f.Session.Snapshot.Position);Assert.Equal(1,f.Connection.Movement.Pending);
    await f.Map(501,5,5);await f.Position(new(1,1));return true;
   }
   var origin=next with{X=next.X-1};replies.Add(Task.Run(async()=>{await Task.Delay(500);await f.Send(11,[1,0,(byte)origin.X,0,(byte)origin.Y,0,0,0,0,0]);}));return true;
  },new("navigate",501,new(1,1)),projectedServer:true);
  Assert.Equal(501,f.Session.Snapshot.Map);Assert.Equal(0,f.Connection.Movement.Pending);
  }finally{await Task.WhenAll(replies);}
 }
 [Fact] public async Task Fast_rejection_reanchors_before_detouring_and_corrects_immediate_presentation(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;bool refreshing=false;Task reply=Task.CompletedTask;
  try{await f.Walk(new(4,1),async(n,next)=>{
   Assert.False(refreshing);if(n==1){await f.Send(11,[4,0,1,0,1,0,0,0,0,0]);return true;}
   Assert.NotEqual(new Tile(2,1),next);return false;
  },refresh:()=>{
   refreshing=true;reply=Task.Run(async()=>{await Task.Delay(150);refreshing=false;await f.Position(new(1,1));});return Task.CompletedTask;
  });Assert.Single(f.Commands,c=>c==0x38);Assert.Equal(new Tile(4,1),f.Session.Snapshot.Position);
  Assert.Equal((byte)12,(await ReadClientFrame(f,TimeSpan.FromSeconds(1)))[0]);
  Assert.Equal((byte)11,(await ReadClientFrame(f,TimeSpan.FromSeconds(1)))[0]);
  Assert.Equal((byte)4,(await ReadClientFrame(f,TimeSpan.FromSeconds(1)))[0]);
  }finally{await reply;}
 }
 [Fact] public async Task Fast_correction_drains_late_replies_through_a_fresh_position_barrier(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(500,"Synthetic",10,5,[]));f.Session.FastMode=true;
  Task rejection=Task.CompletedTask,refreshReply=Task.CompletedTask;bool refreshing=false;int queued=0;
  try{await f.Walk(new(6,1),(n,next)=>{
   Assert.False(refreshing,"Movement escaped the fresh position barrier.");
   if(n<=3){queued++;if(n==1)rejection=Task.Run(async()=>{await Task.Delay(400);await f.Send(11,[4,0,1,0,1,0,0,0,0,0]);});return Task.FromResult(true);}
   Assert.NotEqual(new Tile(2,1),next);return Task.FromResult(false);
  },refresh:()=>{
   Assert.Equal(3,queued);refreshing=true;
   refreshReply=Task.Run(async()=>{await Task.Delay(100);await f.Send(11,[4,0,1,0,1,0,0,0,0,0]);await Task.Delay(100);await f.Send(11,[4,0,1,0,1,0,0,0,0,0]);await Task.Delay(100);refreshing=false;await f.Position(new(1,1));});return Task.CompletedTask;
  });Assert.Single(f.Commands,c=>c==0x38);Assert.Equal(new Tile(6,1),f.Session.Snapshot.Position);Assert.False(f.Connection.Movement.Recovery);
  }finally{await Task.WhenAll(rejection,refreshReply);}
 }
 [Fact] public async Task Resuming_after_stop_refreshes_pending_movement_before_new_actions(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(500,"Synthetic",10,5,[]));f.Session.FastMode=true;
  using(var stop=new CancellationTokenSource(650))await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Walk(new(8,1),(_,_)=>Task.FromResult(true),cancellation:stop.Token,projectedServer:true));
  Assert.Equal(3,f.Commands.Count(c=>c==6));bool refreshed=false;
  await f.Walk(new(5,1),(_,_)=>{Assert.True(refreshed);return Task.FromResult(false);},refresh:async()=>{
   await f.Send(11,[1,0,1,0,1,0,0,0,0,0]);await f.Send(11,[1,0,2,0,1,0,0,0,0,0]);await f.Send(11,[1,0,3,0,1,0,0,0,0,0]);refreshed=true;await f.Position(new(4,1));
  });Assert.Equal(4,f.Commands.Count(c=>c==6));Assert.Single(f.Commands,c=>c==0x38);Assert.Equal(new Tile(5,1),f.Session.Snapshot.Position);
 }
 [Fact] public async Task Fast_same_map_reload_waits_for_actual_position_without_projecting_across_reload(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;
  await f.Walk(new(4,1),async(n,next)=>{
   if(n!=1)return false;
   await f.Map(500,5,5);await Task.Delay(60);Assert.Equal(0,f.Connection.Movement.Pending);Assert.Null(f.Session.Snapshot.Position);
   await f.Position(next);return true;
  },refresh:()=>{Assert.Fail("A valid map reload should supply its own position.");return Task.CompletedTask;});
  Assert.Equal(new Tile(4,1),f.Session.Snapshot.Position);Assert.DoesNotContain((byte)0x38,f.Commands);
 }
 [Fact] public async Task Fast_missing_replies_refresh_and_detour_after_the_bounded_queue_times_out(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(500,"Synthetic",10,5,[]));f.Session.FastMode=true;
  await f.Walk(new(6,1),(n,next)=>{if(n<=3)return Task.FromResult(true);Assert.NotEqual(new Tile(2,1),next);return Task.FromResult(false);});
  Assert.Single(f.Commands,c=>c==0x38);Assert.Equal(new Tile(6,1),f.Session.Snapshot.Position);
 }
 [Fact] public async Task Repeated_fast_rejections_stop_after_three_recovery_attempts(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;
  await Assert.ThrowsAsync<QuestBlockedException>(()=>f.Walk(new(4,1),async(_,_)=>{await f.Send(11,[4,0,1,0,1,0,0,0,0,0]);return true;}));
  Assert.Equal(3,f.Commands.Count(c=>c==6));Assert.Equal(2,f.Commands.Count(c=>c==0x38));Assert.Equal(new Tile(1,1),f.Session.Snapshot.Position);
 }
 [Fact] public async Task Repeated_fast_patrol_rejection_yields_to_the_next_waypoint(){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=true;
  await f.Walk(new(4,1),async(_,_)=>{await f.Send(11,[4,0,1,0,1,0,0,0,0,0]);return true;},new("navigate",500,new(4,1),StopWhen:new("Branch",6)));
  Assert.Equal(3,f.Commands.Count(c=>c==6));Assert.Equal(new Tile(1,1),f.Session.Snapshot.Position);
  await f.Walk(new(4,2),(_,_)=>Task.FromResult(false));Assert.Equal(new Tile(4,2),f.Session.Snapshot.Position);
 }
 [Fact] public async Task Manual_movement_cancels_a_pending_fast_walk_without_duplicate_animation(){
  await using var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(500,"Synthetic",10,5,[]));f.Session.FastMode=true;
  using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(3));var interrupted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  f.Session.Interrupted+=_=>{stop.Cancel();interrupted.TrySetResult();};
  var walking=f.Session.ExecuteAsync(new("navigate",500,new(8,1)),stop.Token);
  try{
   await ReadFrame(f.Server.GetStream(),TimeSpan.FromSeconds(1));Assert.Equal((byte)12,(await ReadClientFrame(f,TimeSpan.FromSeconds(1)))[0]);
   var crypto=new Arbiter.Net.Client.ClientPacketEncryptor();await f.Client.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Client.ClientPacket(6,[2,0]),0).ToArray());
   await interrupted.Task.WaitAsync(TimeSpan.FromSeconds(1));var manual=await ReadFrame(f.Server.GetStream(),TimeSpan.FromSeconds(1));
   var decoded=(Arbiter.Net.Client.ClientPacket)crypto.Decrypt(new Arbiter.Net.Client.ClientPacket(manual[0],manual[1..]));Assert.Equal(new byte[]{2,1},decoded.Data);
   await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>walking);Assert.True(f.Connection.Movement.Recovery);Assert.Equal(new Tile(1,1),f.Session.Snapshot.Position);
   await Task.Delay(220);Assert.False(f.Server.GetStream().DataAvailable);Assert.False(f.Client.GetStream().DataAvailable);
  }finally{stop.Cancel();try{await walking;}catch(OperationCanceledException){}}
 }
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Incomplete_reply_is_forwarded_but_cannot_confirm_movement(bool fast){
  await using var f=await MovingObstacleTests.Fixture.Create();f.Session.FastMode=fast;
  using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(3));f.Session.Interrupted+=_=>stop.Cancel();
  var before=f.Session.Snapshot;var walking=f.Session.ExecuteAsync(new("step",Target:new(2,1)),stop.Token);
  try{
   await ReadFrame(f.Server.GetStream(),TimeSpan.FromSeconds(1));
   if(fast)Assert.Equal((byte)12,(await ReadClientFrame(f,TimeSpan.FromSeconds(1)))[0]);
   await f.Send(11,[1,0,1,0,1]);Assert.Equal((byte)11,(await ReadClientFrame(f,TimeSpan.FromSeconds(1)))[0]);
   await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>walking);
   Assert.Equal(0,f.Connection.VisibleMoves);Assert.Equal(before.Revision,f.Session.Snapshot.Revision);Assert.Equal(before.Position,f.Session.Snapshot.Position);
   Assert.True(f.Connection.Movement.Recovery);Assert.False(f.Client.GetStream().DataAvailable);
  }finally{stop.Cancel();try{await walking;}catch(OperationCanceledException){}}
 }
 static async Task<byte[]> ReadClientFrame(MovingObstacleTests.Fixture f,TimeSpan timeout){
  return await ReadFrame(f.Client.GetStream(),timeout);
 }
 static async Task<byte[]> ReadFrame(System.Net.Sockets.NetworkStream stream,TimeSpan timeout){
  using var limit=new CancellationTokenSource(timeout);byte[] header=new byte[3];await stream.ReadExactlyAsync(header,limit.Token);byte[] data=new byte[(header[1]<<8)|header[2]];await stream.ReadExactlyAsync(data,limit.Token);return data;
 }
}
