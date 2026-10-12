using System.Text.Json;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Diagnostics;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Xunit;
namespace DAQuester.Tests;
public class BirthStatusTests {
 static byte[] Packet(byte type,string text){var b=new NetworkPacketBuilder(ServerCommand.Message);try{b.AppendByte(type);b.AppendString16(text);return b.ToPacket().Data;}finally{b.Dispose();}}
 [Fact] public void Status_message_is_server_observed_with_generation_revision_and_time() {
  var tracker=new StateTracker(42);tracker.Identity("Awesome",1179711);string text="She winks, \"It's on your legend now\"";var start=DateTimeOffset.UtcNow;
  tracker.Observe(0x0A,Packet(3,text));using var json=JsonDocument.Parse(JsonSerializer.Serialize(tracker.Snapshot));var n=json.RootElement.GetProperty("Notice");
  Assert.Equal(42,n.GetProperty("Generation").GetInt64());Assert.Equal(tracker.Snapshot.Revision,n.GetProperty("Revision").GetInt64());Assert.Equal(text,n.GetProperty("Text").GetString());Assert.True(n.GetProperty("ObservedAt").GetDateTimeOffset()>=start);
 }
 [Fact] public void Recorder_exports_birth_status_without_arbitrary_whisper_payloads() {
  var recorder=new ReplayRecorder();var tracker=new StateTracker(42);tracker.Identity("Awesome",1179711);tracker.Observed+=(o,p)=>recorder.Record(o,p,tracker.Snapshot);
  tracker.Observe(0x0A,Packet(0,"private message"));Assert.Equal(0,recorder.Count);
  tracker.Observe(0x0A,Packet(3,"She winks, \"It's on your legend now\""));Assert.Equal(1,recorder.Count);
 }
 [Theory][InlineData(0,false)][InlineData(5,false)][InlineData(8,true)][InlineData(9,true)][InlineData(10,true)][InlineData(11,false)][InlineData(12,false)]
 public void Chat_and_popup_text_cannot_be_recorded_as_birth_status(byte type,bool serverMessage) {
  var recorder=new ReplayRecorder();var tracker=new StateTracker(42);tracker.Identity("Awesome",1179711);tracker.Observed+=(o,p)=>recorder.Record(o,p,tracker.Snapshot);
  tracker.Observe(0x0A,Packet(type,"She winks, \"It's on your legend now\""));
  // Server messages stay in the replay; player chat never does. Neither becomes birth status.
  Assert.Equal(serverMessage?1:0,recorder.Count);Assert.Null(tracker.Snapshot.Notice is {} n&&DAQuester.Core.MundaneBirthEvidence.Matches(n)?n:null);
 }
}
