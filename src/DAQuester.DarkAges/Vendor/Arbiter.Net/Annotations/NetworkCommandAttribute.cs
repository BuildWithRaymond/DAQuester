using Arbiter.Net.Client;
using Arbiter.Net.Server;

namespace Arbiter.Net.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
internal class NetworkCommandAttribute : Attribute
{
    public byte Command { get; set; }

    public NetworkCommandAttribute(ClientCommand command)
        : this((byte)command)
    {
    }

    public NetworkCommandAttribute(ServerCommand command)
        : this((byte)command)
    {
    }

    public NetworkCommandAttribute(byte command)
    {
        Command = command;
    }
}