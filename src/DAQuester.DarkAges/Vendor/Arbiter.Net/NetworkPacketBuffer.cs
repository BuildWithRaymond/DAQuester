namespace Arbiter.Net;

public delegate NetworkPacket NetworkPacketFactory(byte command, ReadOnlySpan<byte> data);

public class NetworkPacketBuffer
{
    private byte[] _queueBuffer = new byte[4096];
    private readonly byte[] _dataBuffer = new byte[ushort.MaxValue];
    private int _bufferIndex;
    private readonly NetworkPacketFactory _packetFactory;

    public NetworkPacketBuffer(NetworkPacketFactory packetFactory)
    {
        _packetFactory = packetFactory;
    }

    public void Append(byte value)
    {
        EnsureCapacity(1);
        _queueBuffer[_bufferIndex++] = value;
    }

    public void Append(byte[] buffer)
    {
        Append(buffer, 0, buffer.Length);
    }
    
    public void Append(byte[] buffer, int offset, int count)
    {
        EnsureCapacity(count);
        Buffer.BlockCopy(buffer, offset, _queueBuffer, _bufferIndex, count);
        _bufferIndex += count;
    }

    public bool TryTakePacket(out NetworkPacket packet)
    {
        packet = null!;

        // Check we have at least the packet header ready
        if (_bufferIndex < NetworkPacket.HeaderSize)
        {
            return false;
        }

        // Check that we have the entire packet ready, from header size
        var dataSize = (_queueBuffer[1] << 8 | _queueBuffer[2]) - 1;
        var packetSize = dataSize + NetworkPacket.HeaderSize;
        if (_bufferIndex < packetSize)
        {
            return false;
        }
        
        // Copy the command and data bytes
        var command = _queueBuffer[3];
        if (dataSize > 0)
        {
            Buffer.BlockCopy(_queueBuffer, NetworkPacket.HeaderSize, _dataBuffer, 0, dataSize);
        }

        packet = _packetFactory(command, new ReadOnlySpan<byte>(_dataBuffer, 0, dataSize));

        // Copy the rest of the data back to the front of the queue
        var remaining = _bufferIndex - packetSize;
        if (remaining > 0)
        {
            Buffer.BlockCopy(_queueBuffer, packetSize, _queueBuffer, 0, remaining);
        }

        _bufferIndex = remaining;
        return true;
    }
    
    private void EnsureCapacity(int additional)
    {
        var required = _bufferIndex + additional;
        if (required <= _queueBuffer.Length)
        {
            return;
        }

        var newSize = _queueBuffer.Length * 2;
        while (newSize < required)
        {
            newSize *= 2;
        }

        var newBuffer = new byte[newSize];
        Buffer.BlockCopy(_queueBuffer, 0, newBuffer, 0, _bufferIndex);
        _queueBuffer = newBuffer;
    }
}