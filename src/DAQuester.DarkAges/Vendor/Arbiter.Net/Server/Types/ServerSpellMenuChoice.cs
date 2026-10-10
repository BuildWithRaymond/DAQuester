using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Types;

public class ServerSpellMenuChoice
{
    public ushort Sprite { get; set; }
    public DyeColor Color { get; set; }
    public string Name { get; set; } = string.Empty;
}