namespace Arbiter.Net.Server;

public class ServerPacket : NetworkPacket
{
    public byte? Sequence { get; init; }
    
    public new ServerCommand Command => Enum.IsDefined(typeof(ServerCommand), base.Command)
        ? (ServerCommand)base.Command
        : ServerCommand.Unknown;

    public ServerPacket(byte command, ReadOnlySpan<byte> payload)
        : base(command, payload.ToArray())
    {

    }

    public ServerPacket(byte command, byte[] payload)
        : base(command, payload)
    {

    }
}