using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;

namespace Arbiter.Net.Client.Messages;

[NetworkCommand(ClientCommand.GiveGold)]
public class ClientGiveGoldMessage : ClientMessage
{
    public uint Amount { get; set; }
    public uint EntityId { get; set; }

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);
        
        Amount = reader.ReadUInt32();
        EntityId = reader.ReadUInt32();
    }
    
    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);
        
        builder.AppendUInt32(Amount);
        builder.AppendUInt32(EntityId);
    }
}