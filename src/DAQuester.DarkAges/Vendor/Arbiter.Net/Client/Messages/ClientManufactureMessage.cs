using Arbiter.Net.Annotations;
using Arbiter.Net.Client.Types;
using Arbiter.Net.Serialization;

namespace Arbiter.Net.Client.Messages;

[NetworkCommand(ClientCommand.Manufacture)]
public class ClientManufactureMessage : ClientMessage
{
    public ushort ManufactureId { get; set; }
    public ClientManufactureType MessageType { get; set; }
    public byte? RecipeIndex { get; set; }
    public string? RecipeName { get; set; }

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);

        ManufactureId = reader.ReadUInt16();
        MessageType = (ClientManufactureType)reader.ReadByte();

        if (MessageType == ClientManufactureType.RequestRecipe)
        {
            RecipeIndex = reader.ReadByte();
        }
        else
        {
            RecipeName = reader.ReadString8();
        }
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);

        builder.AppendUInt16(ManufactureId);
        builder.AppendByte((byte)MessageType);

        if (MessageType == ClientManufactureType.RequestRecipe)
        {
            builder.AppendByte(RecipeIndex ?? 0);
        }
        else
        {
            builder.AppendString8(RecipeName ?? "");
        }

        builder.AppendByte(0x00);
    }
}