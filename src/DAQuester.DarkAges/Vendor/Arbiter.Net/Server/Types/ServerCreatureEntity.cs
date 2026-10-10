using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Types;

public class ServerCreatureEntity : ServerEntityObject
{
    public CreatureType CreatureType { get; set; }

    public string? Name { get; set; }

    public WorldDirection Direction { get; set; }

    public uint Unknown { get; set; }
}