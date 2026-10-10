using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;

namespace Arbiter.Net.Server.Messages;

[NetworkCommand(ServerCommand.Hello)]
public class ServerHelloMessage : ServerMessage
{
    public string Message { get; set; } = string.Empty;

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);
        
        reader.Skip(1);
        Message = reader.ReadLine();
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);
        
        builder.AppendByte(0x00);
        builder.AppendLine(Message);
    }
}