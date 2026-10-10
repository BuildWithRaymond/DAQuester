using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;

namespace Arbiter.Net.Client.Messages;

[NetworkCommand(ClientCommand.RequestHomepage)]
public class ClientRequestHomepageMessage : ClientMessage
{
    public bool NeedsHomepage { get; set; }

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);
        
        NeedsHomepage = reader.ReadBoolean();
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);
        
        builder.AppendBoolean(NeedsHomepage);
    }
}