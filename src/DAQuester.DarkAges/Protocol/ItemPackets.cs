using Arbiter.Net.Client;
using Arbiter.Net.Serialization;

namespace DAQuester.DarkAges.Protocol;

public static class ItemPackets
{
    public static ClientPacket Give(byte slot, uint entityId, uint quantity)
    {
        var b = new NetworkPacketBuilder(ClientCommand.Give);
        try
        {
            b.AppendByte(slot);
            b.AppendUInt32(entityId);
            b.AppendUInt32(quantity);
            return (ClientPacket)b.ToPacket();
        }
        finally
        {
            b.Dispose();
        }
    }
}
