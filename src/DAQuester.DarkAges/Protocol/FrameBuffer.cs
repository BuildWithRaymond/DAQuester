namespace DAQuester.DarkAges.Protocol;

public class FrameBuffer
{
    private readonly PacketFrameBuffer data = new();
    public void Append(byte[] bytes)
    {
        if (data.Count + bytes.Length > 262144)
            throw new InvalidDataException("Packet framing overflow.");
        data.Append(bytes, 0, bytes.Length);
    }

    public byte[]? Read()
    {
        if (data.RecoveryRequired)
            throw new InvalidDataException("Repeated framing corruption.");
        return data.TryReadFrame(out var frame) ? frame : null;
    }
}
