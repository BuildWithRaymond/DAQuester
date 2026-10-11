using System.Text.Json;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Control;
using Xunit;
namespace DAQuester.Tests;

public class ShinewoodFaerieStateTests {
 [Fact] public void Nonpure_display_word_does_not_establish_pure_status(){
  var tracker=new StateTracker(55);tracker.Identity("Synthetic",42);var builder=new NetworkPacketBuilder(ServerCommand.SelfLook);
  try{new ServerSelfLookMessage{Class=CharacterClass.Priest,DisplayClass="Impure Master Priest",ShowMasterMetadata=true}.Serialize(ref builder);tracker.Observe((byte)ServerCommand.SelfLook,builder.ToPacket().Data);}finally{builder.Dispose();}
  using var state=JsonDocument.Parse(JsonSerializer.Serialize(tracker.Snapshot));Assert.NotEqual(JsonValueKind.True,state.RootElement.GetProperty("IsPure").ValueKind);
 }
 [Fact] public void Own_look_preserves_class_master_and_pure_evidence_and_disconnect_clears_readiness(){
  var tracker=new StateTracker(55);tracker.Identity("Synthetic",42);
  var builder=new NetworkPacketBuilder(ServerCommand.SelfLook);
  try{new ServerSelfLookMessage{Class=CharacterClass.Priest,DisplayClass="Pure Master Priest",ShowMasterMetadata=true}.Serialize(ref builder);tracker.Observe((byte)ServerCommand.SelfLook,builder.ToPacket().Data);}finally{builder.Dispose();}
  tracker.Observe((byte)ServerCommand.UserReady,[0]);
  using(var state=JsonDocument.Parse(JsonSerializer.Serialize(tracker.Snapshot))){
   Assert.True(state.RootElement.TryGetProperty("Class",out var cls));Assert.Equal("Priest",cls.GetString());
   Assert.True(state.RootElement.GetProperty("IsMaster").GetBoolean());Assert.True(state.RootElement.GetProperty("IsPure").GetBoolean());
   Assert.True(state.RootElement.GetProperty("SpellsKnown").GetBoolean());
  }
  tracker.Disconnect();using var cleared=JsonDocument.Parse(JsonSerializer.Serialize(tracker.Snapshot));
  Assert.False(cleared.RootElement.GetProperty("SpellsKnown").GetBoolean());Assert.Equal(JsonValueKind.Null,cleared.RootElement.GetProperty("Class").ValueKind);
 }
 [Fact] public async Task Control_start_advertises_faerie_and_explicit_unsafe_walking_acknowledgment(){
  var protocol=new McpProtocol(_=>Task.FromResult<object?>(null));
  using var response=JsonDocument.Parse((await protocol.HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"))!);
  var p=response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Single(t=>t.GetProperty("name").GetString()=="daquester_start").GetProperty("inputSchema").GetProperty("properties");
  Assert.Contains("shinewood-faerie",p.GetProperty("quest").GetProperty("enum").EnumerateArray().Select(x=>x.GetString()));
  Assert.False(p.GetProperty("allowUnsafeWalking").GetProperty("default").GetBoolean());
 }
 [Fact] public void Catalog_explicitly_includes_requested_nonlegend_pet_quest(){
  using var catalog=JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content/catalog.json"))));
  var quest=Assert.Single(catalog.RootElement.GetProperty("entries").EnumerateArray(),q=>q.GetProperty("id").GetString()=="shinewoodfaerie");
  Assert.False(quest.GetProperty("givesLegend").GetBoolean());Assert.Equal("requested-pet-and-spells",quest.GetProperty("catalogException").GetString());
 }
}
