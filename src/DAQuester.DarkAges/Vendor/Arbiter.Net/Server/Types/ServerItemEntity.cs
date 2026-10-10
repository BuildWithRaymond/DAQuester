using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Types;

public class ServerItemEntity : ServerEntityObject
{
    public DyeColor Color { get; set; }

    public ushort Unknown { get; set; }
}