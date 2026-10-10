using Xunit;
using DAQuester.DarkAges.Protocol;
namespace DAQuester.Tests;
public class ItemPacketTests {
 [Fact] public void Give_preserves_excalibur_uint32_quantity_layout() {
  var packet=ItemPackets.Give(9,0x11223344,1000);
  Assert.Equal(new byte[]{9,0x11,0x22,0x33,0x44,0,0,3,0xE8},packet.Data);
 }
}
