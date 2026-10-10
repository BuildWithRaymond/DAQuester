using DAQuester.Core;
using Arbiter.Net.Server;
using System.Buffers.Binary;
namespace DAQuester.DarkAges.Protocol;
public record MovementIntent(long Generation,int Map,uint PlayerId,Tile Origin,byte Direction);
public static class PlayerPresentation {
 // Excalibur Client.TavalyMovementSync.cs, commit 7d11dfe91e680de9a9b824137caa301f5429f03c.
 // Present server-confirmed auto movement without changing observed state twice.
 public static ServerPacket? AcknowledgedMove(MovementIntent intent,CharacterSnapshot before,byte opcode,byte[] payload) {
  if(opcode!=0x0B||payload.Length<5||payload[0]>3||!before.Connected||before.Generation!=intent.Generation||before.Map!=intent.Map)return null;
  byte dir=payload[0];var origin=new Tile(BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(1)),BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(3)));
  if(origin==intent.Origin&&dir==intent.Direction) {
   var data=new byte[17];BinaryPrimitives.WriteUInt32BigEndian(data,intent.PlayerId);BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4),(ushort)origin.X);BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(6),(ushort)origin.Y);data[8]=dir;
   return new ServerPacket(0x0C,data);
  }
  var position=StateTracker.GridStep(origin,dir);var correction=new byte[11];BinaryPrimitives.WriteUInt16BigEndian(correction,(ushort)position.X);BinaryPrimitives.WriteUInt16BigEndian(correction.AsSpan(2),(ushort)position.Y);return new ServerPacket(0x04,correction);
 }
}
