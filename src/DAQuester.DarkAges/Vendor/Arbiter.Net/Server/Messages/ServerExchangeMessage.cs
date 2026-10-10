using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;
using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Messages;

[NetworkCommand(ServerCommand.Exchange)]
public class ServerExchangeMessage : ServerMessage
{
    public ExchangeServerEventType Event { get; set; }
    public uint? TargetId { get; set; }
    public string? TargetName { get; set; }
    public byte? Slot { get; set; }
    public ExchangeParty? Party { get; set; }
    public byte? ItemIndex { get; set; }
    public ushort? ItemSprite { get; set; }
    public DyeColor? ItemColor { get; set; }
    public string? ItemName { get; set; }
    public uint? GoldAmount { get; set; }
    public string? Message { get; set; }
    
    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);
        
        Event = (ExchangeServerEventType)reader.ReadByte();

        switch (Event)
        {
            case ExchangeServerEventType.Started:
                TargetId = reader.ReadUInt32();
                TargetName = reader.ReadString8();
                break;
            case ExchangeServerEventType.QuantityPrompt:
                Slot = reader.ReadByte();
                break;
            case ExchangeServerEventType.ItemAdded:
                Party = (ExchangeParty)reader.ReadByte();
                ItemIndex = reader.ReadByte();
                ItemSprite = SpriteFlags.ClearFlags(reader.ReadUInt16());
                ItemColor = (DyeColor)reader.ReadByte();
                ItemName = reader.ReadString8();
                break;
            case ExchangeServerEventType.GoldAdded:
                Party = (ExchangeParty)reader.ReadByte();
                GoldAmount = reader.ReadUInt32();
                break;
            case ExchangeServerEventType.Accepted or ExchangeServerEventType.Cancelled:
                Party = (ExchangeParty)reader.ReadByte();
                Message = reader.ReadString8();
                break;
        }
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);
        
        builder.AppendByte((byte)Event);

        switch (Event)
        {
            case ExchangeServerEventType.Started:
                builder.AppendUInt32(TargetId!.Value);
                builder.AppendString8(TargetName!);
                break;
            case ExchangeServerEventType.QuantityPrompt:
                builder.AppendByte(Slot!.Value);
                break;
            case ExchangeServerEventType.ItemAdded:
                builder.AppendByte((byte)Party!.Value);
                builder.AppendByte(ItemIndex!.Value);
                builder.AppendUInt16(SpriteFlags.SetItem(ItemSprite!.Value));
                builder.AppendByte((byte)(ItemColor ?? DyeColor.Default));
                builder.AppendString8(ItemName!);
                break;
            case ExchangeServerEventType.GoldAdded:
                builder.AppendByte((byte)Party!.Value);
                builder.AppendUInt32(GoldAmount!.Value);
                break;
            case ExchangeServerEventType.Accepted or ExchangeServerEventType.Cancelled:
                builder.AppendByte((byte)Party!.Value);
                builder.AppendString8(Message ?? string.Empty);
                break;
        }
    }
}