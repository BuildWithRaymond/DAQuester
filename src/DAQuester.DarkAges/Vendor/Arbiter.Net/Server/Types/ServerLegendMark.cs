using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Types;

public class ServerLegendMark
{
    public LegendMarkIcon Icon { get; set; }
    public LegendMarkColor Color { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}