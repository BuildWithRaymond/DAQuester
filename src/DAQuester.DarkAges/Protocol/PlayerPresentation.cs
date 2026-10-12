using DAQuester.Core;
using Arbiter.Net.Server;
using System.Buffers.Binary;

namespace DAQuester.DarkAges.Protocol;

public record MovementIntent(long Generation, int Map, uint PlayerId, Tile Origin, byte Direction);
public static class PlayerPresentation
{
    // Excalibur Client.TavalyMovementSync.cs, commit 7d11dfe91e680de9a9b824137caa301f5429f03c.
    // Present server-confirmed auto movement without changing observed state twice.
    public static ServerPacket? AcknowledgedMove(MovementIntent intent, CharacterSnapshot before, byte opcode, byte[] payload)
    {
        if (opcode != 0x0B || payload.Length < 5 || payload[0] > 3 || !before.Connected || before.Generation != intent.Generation || before.Map != intent.Map)
            return null;
        byte dir = payload[0];
        var origin = new Tile(BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(1)), BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(3)));
        if (origin == intent.Origin && dir == intent.Direction)
        {
            return Move(intent);
        }

        return Position(StateTracker.GridStep(origin, dir));
    }

    public static ServerPacket Move(MovementIntent intent)
    {
        var data = new byte[17];
        BinaryPrimitives.WriteUInt32BigEndian(data, intent.PlayerId);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), (ushort)intent.Origin.X);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(6), (ushort)intent.Origin.Y);
        data[8] = intent.Direction;
        return new ServerPacket(0x0C, data);
    }

    public static ServerPacket Position(Tile position)
    {
        var data = new byte[11];
        BinaryPrimitives.WriteUInt16BigEndian(data, (ushort)position.X);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), (ushort)position.Y);
        return new ServerPacket(0x04, data);
    }
}
