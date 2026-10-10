using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;
using Arbiter.Net.Types;

namespace Arbiter.Net.Client.Messages;

[NetworkCommand(ClientCommand.Say)]
public class ClientSayMessage : ClientMessage
{
    public PublicMessageType MessageType { get; set; }
    public string Content { get; set; } = string.Empty;

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);
        
        MessageType = (PublicMessageType)reader.ReadByte();
        Content = reader.ReadString8();
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);

        builder.AppendByte((byte)MessageType);
        builder.AppendString8(Content);
    }
}