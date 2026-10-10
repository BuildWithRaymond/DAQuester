namespace Arbiter.Net;

public interface INetworkMessage
{
    public NetworkPacketSource Source { get; set; }
}