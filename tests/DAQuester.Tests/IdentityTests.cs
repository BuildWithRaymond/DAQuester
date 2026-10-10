using Xunit;
using DAQuester.DarkAges.Protocol;
namespace DAQuester.Tests;
public class IdentityTests {
 [Fact] public void Datester_741_avatar_packet_sets_logged_in_identity() {
  var s=new StateTracker(1);s.Identity("Nuclear",0);
  s.Observe(0x05,[0,0,0,42,0,0,1,0,0]);
  Assert.Equal(42u,s.Snapshot.Id);Assert.True(s.Snapshot.Connected);
 }
}
