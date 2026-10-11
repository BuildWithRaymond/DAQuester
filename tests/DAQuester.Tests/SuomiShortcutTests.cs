using System.Buffers.Binary;
using Arbiter.Net.Client;
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

// Synthetic dialogues: Excalibur establishes the NPC/anchor/greetings, not these IDs or labels.
[Collection("Walking cadence")]
public class SuomiShortcutTests {
 static byte[] Payload(ServerMessage m){var b=new NetworkPacketBuilder(ServerMessageFactory.Default.GetMessageCommand(m.GetType())!.Value);try{m.Serialize(ref b);return b.ToPacket().Data;}finally{b.Dispose();}}
 static byte[] Menu(string greeting,bool available=true)=>Payload(new ServerPursuitMessage{DialogType=DialogType.Menu,EntityType=EntityTypeFlags.Creature,EntityId=77,PursuitId=654,StepId=9,Name="Keane",Content=greeting,MenuChoices=available?["About Suomi","Renounce Abel Citizenship","Take me to Abel","Other"]:["About Suomi","Renounce Abel Citizenship"]});
 static byte[] Next()=>Payload(new ServerPursuitMessage{DialogType=DialogType.Popup,EntityType=EntityTypeFlags.Creature,EntityId=77,PursuitId=654,StepId=10,Name="Keane",Content="Synthetic: I will take you to Abel.",HasNextButton=true});
 static async Task<MovingObstacleTests.Fixture> Create(bool npc=true){
  var f=await MovingObstacleTests.Fixture.Create(record:new MapRecord(503,"Suomi",100,100,[new(new(40,0),3014,new(1,1))]),origin:new(40,56),additional:[new(3014,"Abel Way",5,5,[new(new(2,1),502,new(1,1))]),new(502,"Abel",5,5,[])]);
  if(npc)f.Connection.Tracker.Observe(7,Payload(new ServerDrawObjectsMessage{Entities=[new ServerCreatureEntity{Id=77,X=40,Y=59,Sprite=1,CreatureType=CreatureType.Mundane,Name="Keane"}]}));return f;
 }
 static async Task Run(MovingObstacleTests.Fixture f,Func<ClientPacket,Task> respond,GameCommand? command=null){
  using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(15));var decrypt=new ClientPacketEncryptor();
  var response=Task.Run(async()=>{while(true){var h=new byte[3];await f.Server.GetStream().ReadExactlyAsync(h,stop.Token);var data=new byte[(h[1]<<8)|h[2]];await f.Server.GetStream().ReadExactlyAsync(data,stop.Token);var p=(ClientPacket)decrypt.Decrypt(new ClientPacket(data[0],data[1..]));f.Commands.Add((byte)p.Command);await respond(p);}});
  var travel=f.Session.ExecuteAsync(command??new("navigate",502,new(1,1)),stop.Token);
  try{await Task.WhenAny(travel,response);if(response.IsFaulted)await response;await travel;}finally{stop.Cancel();try{await response;}catch(OperationCanceledException){}}
 }
 static Task Ack(MovingObstacleTests.Fixture f,ClientPacket p){var origin=f.Session.Snapshot.Position!;var data=new byte[10];data[0]=p.Data[0];BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(1),(ushort)origin.X);BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(3),(ushort)origin.Y);return f.Send(11,data);}
 [Theory][InlineData("Hello.  What can I do for you?")][InlineData("Good day, Aisling. Can I be of some assistance to you?")]
 public async Task Departure_selects_observed_Abel_choice_then_next_and_replans_from_actual_landing(string greeting){
  await using var f=await Create();int choices=0,nexts=0;
  await Run(f,async p=>{
   if((byte)p.Command==0x43){var open=new ClientRequestObjectInfoMessage();open.Deserialize(new NetworkPacketReader(p));Assert.Equal(77u,open.TargetId);await f.Send(0x30,Menu(greeting));return;}
   if((byte)p.Command==0x3A){var d=new ClientPursuitMessage();d.Deserialize(new NetworkPacketReader(p));Assert.Equal(77u,d.EntityId);Assert.Equal(654,d.PursuitId);
    if(d.ArgsType==DialogArgsType.MenuChoice){Assert.Equal((byte?)3,d.MenuChoice);Assert.Equal(10,d.StepId);Assert.Equal(1,++choices);await f.Send(0x30,Next());}
    else{Assert.Equal(DialogArgsType.None,d.ArgsType);Assert.Equal(11,d.StepId);Assert.Equal(1,++nexts);await f.Map(502,5,5);await f.Position(new(3,1));}return;}
   Assert.Equal((byte)6,(byte)p.Command);Assert.Equal(502,f.Session.Snapshot.Map);await Ack(f,p);
  });
  Assert.Equal(1,choices);Assert.Equal(1,nexts);Assert.Equal(new Tile(1,1),f.Session.Snapshot.Position);
 }
 [Fact] public async Task Local_destination_does_not_open_transport_NPC(){
  await using var f=await Create();await Run(f,p=>{Assert.Equal((byte)6,(byte)p.Command);return Ack(f,p);},new("navigate",503,new(40,55)));Assert.Single(f.Commands);
 }
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Departure_selects_Suomi_Help_then_observed_escort_choice_and_Next(bool merchant){
  await using var f=await Create();int choices=0,nexts=0;
  byte[] HelpMenu()=>merchant?Payload(new ServerScreenMenuMessage{MenuType=DialogMenuType.Menu,EntityType=EntityTypeFlags.Creature,EntityId=77,Name="Keane",Content="Hello.  What can I do for you?",MenuChoices=[new(){Text="Other",PursuitId=125},new(){Text="Suomi Help",PursuitId=987}]}):
   Payload(new ServerPursuitMessage{DialogType=DialogType.Menu,EntityType=EntityTypeFlags.Creature,EntityId=77,PursuitId=654,StepId=9,Name="Keane",Content="Hello.  What can I do for you?",MenuChoices=["Other","Suomi Help"]});
  byte[] EscortMenu()=>Payload(new ServerPursuitMessage{DialogType=DialogType.Menu,EntityType=EntityTypeFlags.Creature,EntityId=77,PursuitId=765,StepId=20,Name="Keane",Content="Synthetic Suomi help menu",MenuChoices=["Renounce Abel Citizenship","Please escort me out of suomi","Other"]});
  await Run(f,async p=>{
   if((byte)p.Command==0x43){await f.Send(merchant?(byte)0x2F:(byte)0x30,HelpMenu());return;}
   if(merchant&&choices==0){Assert.Equal((byte)ClientCommand.Merchant,(byte)p.Command);var c=new ClientMerchantMessage();c.Deserialize(new NetworkPacketReader(p));Assert.Equal(987,c.PursuitId);Assert.Equal(77u,c.EntityId);choices++;await f.Send(0x30,EscortMenu());return;}
   Assert.Equal((byte)0x3A,(byte)p.Command);var d=new ClientPursuitMessage();d.Deserialize(new NetworkPacketReader(p));Assert.Equal(77u,d.EntityId);
   if(d.ArgsType==DialogArgsType.MenuChoice){Assert.Equal((byte?)2,d.MenuChoice);if(++choices==1){Assert.Equal(654,d.PursuitId);Assert.Equal(10,d.StepId);await f.Send(0x30,EscortMenu());}else{Assert.Equal(765,d.PursuitId);Assert.Equal(21,d.StepId);await f.Send(0x30,Next());}}
   else{Assert.Equal(DialogArgsType.None,d.ArgsType);Assert.Equal(1,++nexts);await f.Map(502,5,5);await f.Position(new(1,1));}
  });Assert.Equal(2,choices);Assert.Equal(1,nexts);Assert.Null(f.Session.Snapshot.Dialog);
 }
 [Fact] public async Task Unfamiliar_menu_is_preserved_without_selecting_citizenship(){
  await using var f=await Create();await Assert.ThrowsAsync<QuestBlockedException>(()=>Run(f,async p=>{Assert.Equal((byte)0x43,(byte)p.Command);await f.Send(0x30,Menu("Hello.  What can I do for you?",false));}));Assert.Single(f.Commands);Assert.NotNull(f.Session.Snapshot.Dialog);
 }
 [Fact] public async Task Missing_travel_reply_and_resume_do_not_repeat_submission_or_close_popup(){
  await using var f=await Create();int submissions=0;
  await Assert.ThrowsAsync<QuestBlockedException>(()=>Run(f,async p=>{if((byte)p.Command==0x43){await f.Send(0x30,Menu("Hello.  What can I do for you?"));return;}Assert.Equal((byte)0x3A,(byte)p.Command);Assert.Equal(1,++submissions);}));
  await Assert.ThrowsAsync<QuestBlockedException>(()=>f.Session.ExecuteAsync(new("navigate",502,new(1,1)),default));Assert.Equal(1,submissions);Assert.NotNull(f.Session.Snapshot.Dialog);Assert.False(f.Server.GetStream().DataAvailable);
 }
 [Fact] public async Task Missing_NPC_before_interaction_uses_recorded_walk(){
  await using var f=await Create(false);await Run(f,async p=>{Assert.Equal((byte)6,(byte)p.Command);if(f.Session.Snapshot.Map==503&&f.Session.Snapshot.Position==new Tile(40,1)){await f.Map(3014,5,5);await f.Position(new(1,1));}else if(f.Session.Snapshot.Map==3014){await f.Map(502,5,5);await f.Position(new(1,1));}else await Ack(f,p);});Assert.DoesNotContain((byte)0x43,f.Commands);
 }
 [Fact] public async Task Merchant_menu_uses_actual_pursuit_ID_and_direct_arrival_needs_no_extra_next(){
  await using var f=await Create();int choices=0;await Run(f,async p=>{
   if((byte)p.Command==0x43){await f.Send(0x2F,Payload(new ServerScreenMenuMessage{MenuType=DialogMenuType.Menu,EntityType=EntityTypeFlags.Creature,EntityId=77,Name="Keane",Content="Good day, Aisling. Can I be of some assistance to you?",MenuChoices=[new(){Text="Other",PursuitId=125},new(){Text="Travel to Abel",PursuitId=987}]}));return;}
   Assert.Equal((byte)ClientCommand.Merchant,(byte)p.Command);var c=new ClientMerchantMessage();c.Deserialize(new NetworkPacketReader(p));Assert.Equal(987,c.PursuitId);Assert.Equal(77u,c.EntityId);Assert.Equal(1,++choices);await f.Map(502,5,5);await f.Position(new(1,1));
  });Assert.Equal(1,choices);
 }
 [Fact] public async Task Late_Abel_arrival_after_missing_reply_reconciles_without_another_submission(){
  await using var f=await Create();await Assert.ThrowsAsync<QuestBlockedException>(()=>Run(f,async p=>{if((byte)p.Command==0x43){await f.Send(0x30,Menu("Hello.  What can I do for you?"));return;}Assert.Equal((byte)0x3A,(byte)p.Command);}));
  f.Connection.Tracker.Observe(21,[1,246,5,5,0,0,0,0,0,0]);f.Connection.Tracker.Observe(4,[0,1,0,1,0,0,0,0]);
  await f.Session.ExecuteAsync(new("navigate",502,new(1,1)),default);Assert.Equal(2,f.Commands.Count);Assert.False(f.Server.GetStream().DataAvailable);
 }
 [Fact] public async Task Unowned_open_Keane_dialogue_is_preserved_before_navigation(){
  await using var f=await Create();f.Connection.Tracker.Observe(0x30,Next());var popup=f.Session.Snapshot.Dialog;
  await Assert.ThrowsAsync<QuestBlockedException>(()=>f.Session.ExecuteAsync(new("navigate",502,new(1,1)),default));Assert.Same(popup,f.Session.Snapshot.Dialog);Assert.False(f.Server.GetStream().DataAvailable);
 }
}
