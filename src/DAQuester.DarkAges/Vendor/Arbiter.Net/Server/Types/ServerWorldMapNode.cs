namespace Arbiter.Net.Server.Types;

public class ServerWorldMapNode
{
    public ushort ScreenX { get; set; }
    public ushort ScreenY { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public ushort MapId { get; set; }
    public ushort MapX { get; set; }
    public ushort MapY { get; set; }
    
    public ushort Checksum { get; set; }
}