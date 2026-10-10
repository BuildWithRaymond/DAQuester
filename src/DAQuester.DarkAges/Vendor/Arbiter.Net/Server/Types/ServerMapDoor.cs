using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Types;

public class ServerMapDoor
{
    public byte X { get; set; }

    public byte Y { get; set; }

    public DoorDirection Direction { get; set; }

    public DoorState State { get; set; }
}