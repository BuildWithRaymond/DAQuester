using System.Text;
using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;

namespace Arbiter.Net.Client.Messages;

[NetworkCommand(ClientCommand.Exception)]
public class ClientExceptionMessage : ClientMessage
{
    public string Message { get; set; } = string.Empty;

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);

        var bytes = reader.ReadToEnd();
        Message = Encoding.ASCII.GetString(bytes);
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);

        var textBytes = Encoding.ASCII.GetBytes(Message);
        builder.AppendBytes(textBytes);
    }
}