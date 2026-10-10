namespace Arbiter.Net.Server.Types;

public class ServerMessageBoardPost
{
    public short Id { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public byte Month { get; set; }
    public byte Day { get; set; }
    public bool IsHighlighted { get; set; }
}