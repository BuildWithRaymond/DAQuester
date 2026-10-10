using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Messages;

[NetworkCommand(ServerCommand.Manufacture)]
public class ServerManufactureMessage : ServerMessage
{
    public ushort ManufactureId { get; set; }

    public ServerManufactureType MessageType { get; set; }
    public byte? RecipeCount { get; set; }
    public byte? RecipeIndex { get; set; }
    public ushort? Sprite { get; set; }
    public string? RecipeName { get; set; }
    public string? RecipeDescription { get; set; }
    public string? Ingredients { get; set; }

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);

        ManufactureId = reader.ReadUInt16();
        MessageType = (ServerManufactureType)reader.ReadByte();

        if (MessageType == ServerManufactureType.RecipeCount)
        {
            RecipeCount = reader.ReadByte();
        }
        else
        {
            RecipeIndex = reader.ReadByte();
            Sprite = SpriteFlags.ClearFlags(reader.ReadUInt16());

            RecipeName = reader.ReadString8();
            RecipeDescription = reader.ReadString16();
            Ingredients = reader.ReadString16();
        }
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);

        builder.AppendUInt16(ManufactureId);
        builder.AppendByte((byte)MessageType);

        if (MessageType == ServerManufactureType.RecipeCount)
        {
            builder.AppendByte(RecipeCount ?? 0);
        }
        else
        {
            builder.AppendByte(RecipeIndex ?? 0);
            builder.AppendUInt16(SpriteFlags.SetItem(Sprite ?? 0));

            builder.AppendString8(RecipeName ?? "");
            builder.AppendString16(RecipeDescription ?? "");
            builder.AppendString16(Ingredients ?? string.Empty);
            builder.AppendByte(0x01);
        }

        builder.AppendByte(0x00);
    }
}