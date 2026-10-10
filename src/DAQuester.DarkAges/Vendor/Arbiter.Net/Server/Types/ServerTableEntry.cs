using System.Net;

namespace Arbiter.Net.Server.Types;

public class ServerTableEntry
{
    public byte Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public required IPAddress Address { get; set; }

    public ushort Port { get; set; }
}