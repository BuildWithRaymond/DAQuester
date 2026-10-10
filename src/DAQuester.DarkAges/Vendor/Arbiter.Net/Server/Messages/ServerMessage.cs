using Arbiter.Net.Serialization;

namespace Arbiter.Net.Server.Messages;

public abstract class ServerMessage : IServerMessage
{
    public ServerCommand Command { get; private set; }
    public byte? Sequence { get; private set; }
    public NetworkPacketSource Source { get; set; } = NetworkPacketSource.Network;

    public virtual void Deserialize(NetworkPacketReader reader)
    {
        Command = (ServerCommand)reader.Command;
        Sequence = reader.Sequence;
    }

    public virtual void Serialize(ref NetworkPacketBuilder builder)
    {

    }
}