using System.Text.Json;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Serialization;
using DAQuester.DarkAges.Protocol;
using Xunit;
namespace DAQuester.Tests;
public class LegendStateTests {
 [Fact] public void Self_look_sets_known_legend_revision_and_disconnect_invalidates_it() {
  var tracker=new StateTracker(88);tracker.Identity("Clock",42);
  Assert.False(tracker.Snapshot.LegendsKnown);
  var message=new ServerSelfLookMessage{LegendMarks=[new ServerLegendMark{Text="Loved by Suomi Mundanes"}]};
  var builder=new NetworkPacketBuilder(ServerCommand.SelfLook);try{message.Serialize(ref builder);tracker.Observe(0x39,builder.ToPacket().Data);}finally{builder.Dispose();}
  Assert.True(tracker.Snapshot.LegendsKnown);using var json=JsonDocument.Parse(JsonSerializer.Serialize(tracker.Snapshot));
  Assert.Equal(tracker.Snapshot.Revision,json.RootElement.GetProperty("LegendsRevision").GetInt64());tracker.Disconnect();Assert.False(tracker.Snapshot.LegendsKnown);
 }
}
