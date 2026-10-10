using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;

namespace Arbiter.Net.Client.Messages;

[NetworkCommand(ClientCommand.RequestUserId)]
public class ClientRequestUserIdMessage : ClientMessage
{
    public byte Nonce { get; set; }

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);

        if (!reader.IsEndOfPacket())
        {
            Nonce = reader.ReadByte();
        }
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);

        builder.AppendByte(Nonce);
    }
}