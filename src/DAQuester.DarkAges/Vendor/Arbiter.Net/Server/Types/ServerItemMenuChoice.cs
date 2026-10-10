using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Types;

public class ServerItemMenuChoice
{
    public ushort Sprite { get; set; }
    public DyeColor Color { get; set; }
    public uint Price { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}