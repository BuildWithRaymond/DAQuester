using Xunit;
using System.Buffers.Binary;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Navigation;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server.Messages;
namespace DAQuester.Tests;
public class ClientPresentationTests {
 static byte[] Ack(byte direction,int x,int y) {var b=new byte[10];b[0]=direction;BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(1),(ushort)x);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(3),(ushort)y);return b;}
 [Fact] public void Automatic_ack_produces_real_player_walk_animation() {
  var s=FoundationTests.Ready();var p=PlayerPresentation.AcknowledgedMove(new(1,500,42,s.Position!,1),s,0x0B,Ack(1,20,20))!;
  Assert.Equal((byte)0x0C,(byte)p.Command);Assert.Equal(17,p.Data.Length);
  var m=new ServerMoveObjectMessage();m.Deserialize(new NetworkPacketReader(p));Assert.Equal(42u,m.EntityId);Assert.Equal((ushort)20,m.OriginX);Assert.Equal((ushort)20,m.OriginY);Assert.Equal((byte)1,(byte)m.Direction);
 }
 [Fact] public void Correction_uses_authoritative_position_and_old_map_never_animates() {
  var s=FoundationTests.Ready();var i=new MovementIntent(1,500,42,s.Position!,1);
  var p=PlayerPresentation.AcknowledgedMove(i,s,0x0B,Ack(2,21,20))!;Assert.Equal((byte)4,(byte)p.Command);Assert.Equal(11,p.Data.Length);
  var m=new ServerUserPositionMessage();m.Deserialize(new NetworkPacketReader(p));Assert.Equal((ushort)21,m.X);Assert.Equal((ushort)21,m.Y);
  Assert.Null(PlayerPresentation.AcknowledgedMove(i,s with{Map=132},0x0B,Ack(1,20,20)));Assert.Null(PlayerPresentation.AcknowledgedMove(i,s with{Generation=2},0x0B,Ack(1,20,20)));
 }
 [Fact] public void Counter_does_not_force_npc_target_behind_wall() {
  var grid=new Grid(15,15,(x,y)=>y==9||x==0||x==14);var goal=NpcApproach.Find(grid,new(6,13),new(6,7),new HashSet<Tile>{new(6,7)});
  Assert.Equal(new Tile(6,10),goal);Assert.Equal(3,grid.Path(new(6,13),goal).Count);
 }
}
