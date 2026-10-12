using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using Arbiter.Net.Types;
namespace DAQuester.DarkAges.Protocol;
// Built by hand: the client's single-item add carries no quantity byte (decoded live 2026-10-11).
public static class ExchangePackets
{
    public static ClientPacket Start(uint target) => Build(ExchangeClientActionType.BeginExchange, target);
    public static ClientPacket AddItem(uint target, byte slot) => Build(ExchangeClientActionType.AddItem, target, slot);
    public static ClientPacket Accept(uint target) => Build(ExchangeClientActionType.Accept, target);
    public static ClientPacket Cancel(uint target) => Build(ExchangeClientActionType.Cancel, target);
    static ClientPacket Build(ExchangeClientActionType action, uint target, byte? slot = null)
    {
        var builder = new NetworkPacketBuilder(ClientCommand.Exchange);
        try
        {
            builder.AppendByte((byte)action);
            builder.AppendUInt32(target);
            if (slot is { } s)
                builder.AppendByte(s);
            return (ClientPacket)builder.ToPacket();
        }
        finally { builder.Dispose(); }
    }
}
