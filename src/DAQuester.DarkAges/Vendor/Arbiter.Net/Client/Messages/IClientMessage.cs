using Arbiter.Net.Serialization;

namespace Arbiter.Net.Client.Messages;

public interface IClientMessage : INetworkMessage, INetworkSerializable
{
    public ClientCommand Command { get; }
    public byte? Sequence { get; }
}