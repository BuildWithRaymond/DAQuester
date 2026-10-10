using Arbiter.Net.Serialization;

namespace Arbiter.Net.Server.Messages;

public interface IServerMessage : INetworkMessage, INetworkSerializable
{
    public ServerCommand Command { get; }
    public byte? Sequence { get; }
}