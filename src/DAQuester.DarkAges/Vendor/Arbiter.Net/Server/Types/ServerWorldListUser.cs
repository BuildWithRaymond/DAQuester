using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Types;

public class ServerWorldListUser
{
    public CharacterClass Class { get; set; }
    public byte Flags { get; set; }
    public WorldListColor Color { get; set; }
    public SocialStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsMaster { get; set; }
    public string Name { get; set; } = string.Empty;
}