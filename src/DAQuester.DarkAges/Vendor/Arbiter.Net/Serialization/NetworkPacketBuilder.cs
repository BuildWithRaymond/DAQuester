using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Text;
using Arbiter.Net.Client;
using Arbiter.Net.Server;

namespace Arbiter.Net.Serialization;

public ref struct NetworkPacketBuilder : IDisposable
{
    private const int InitialCapacity = 1024;

    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private readonly Encoding _encoding;
    private byte[] _buffer;
    private int _position;

    private bool _isDisposed;

    public byte Command { get; }
    public bool IsClient { get; }
    public bool IsServer { get; }

    public int Length => _position;

    public NetworkPacketBuilder(ClientCommand command, Encoding? encoding = null)
    {
        Command = (byte)command;
        IsClient = true;

        _buffer = _arrayPool.Rent(InitialCapacity);
        _encoding = encoding ?? Encoding.ASCII;
    }

    public NetworkPacketBuilder(ServerCommand command, Encoding? encoding = null)
    {
        Command = (byte)command;
        IsServer = true;

        _buffer = _arrayPool.Rent(InitialCapacity);
        _encoding = encoding ?? Encoding.ASCII;
    }

    private void EnsureCapacity(int additional)
    {
        CheckIfDisposed();
        if (_position + additional <= _buffer.Length)
        {
            return;
        }

        var newSize = Math.Max(_buffer.Length * 2, _position + additional);
        var newBuffer = _arrayPool.Rent(newSize);
        _buffer.AsSpan(0, _position).CopyTo(newBuffer);
        _arrayPool.Return(_buffer);
        _buffer = newBuffer;
    }

    public void AppendBoolean(bool value) => AppendByte(value ? (byte)1 : (byte)0);

    public void AppendSByte(sbyte value) => AppendByte((byte)value);

    public void AppendByte(byte value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = value;
    }

    public void AppendInt16(short value) => AppendUInt16((ushort)value);

    public void AppendUInt16(ushort value)
    {
        EnsureCapacity(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(_buffer.AsSpan(_position), value);
        _position += sizeof(ushort);
    }

    public void AppendInt32(int value) => AppendUInt32((uint)value);

    public void AppendUInt32(uint value)
    {
        EnsureCapacity(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(_buffer.AsSpan(_position), value);
        _position += sizeof(uint);
    }

    public void AppendInt64(long value) => AppendUInt64((ulong)value);

    public void AppendUInt64(ulong value)
    {
        EnsureCapacity(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(_buffer.AsSpan(_position), value);
        _position += sizeof(ulong);
    }

    public void AppendString8(string value)
    {
        var length = Math.Min(value.Length, byte.MaxValue);
        EnsureCapacity(length + 1);

        var stringBuffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var actualLength = _encoding.GetBytes(value.AsSpan()[..length], stringBuffer);
            AppendByte((byte)actualLength);
            AppendBytes(stringBuffer, 0, actualLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(stringBuffer);
        }
    }

    public void AppendString16(string value)
    {
        var length = Math.Min(value.Length, ushort.MaxValue);
        EnsureCapacity(length + sizeof(ushort));

        var stringBuffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var actualLength = _encoding.GetBytes(value.AsSpan()[..length], stringBuffer);
            AppendUInt16((ushort)actualLength);
            AppendBytes(stringBuffer, 0, actualLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(stringBuffer);
        }
    }

    public void AppendNullTerminatedString(string value)
    {
        var length = Math.Min(value.Length, byte.MaxValue);
        EnsureCapacity(length + 1);

        var stringBuffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var actualLength = _encoding.GetBytes(value.AsSpan()[..length], stringBuffer);
            AppendBytes(stringBuffer, 0, actualLength);
            AppendByte(0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(stringBuffer);
        }
    }

    public void AppendLine(string value)
    {
        var length = Math.Min(value.Length, byte.MaxValue);
        EnsureCapacity(length + 1);

        var stringBuffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var actualLength = _encoding.GetBytes(value.AsSpan()[..length], stringBuffer);
            AppendBytes(stringBuffer, 0, actualLength);
            AppendByte((byte)'\n');
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(stringBuffer);
        }
    }

    public void AppendIPv4Address(IPAddress address)
    {
        Span<byte> ipBytes = stackalloc byte[4];
        address.TryWriteBytes(ipBytes, out _);

        AppendByte(ipBytes[3]);
        AppendByte(ipBytes[2]);
        AppendByte(ipBytes[1]);
        AppendByte(ipBytes[0]);
    }

    public void AppendBytes(byte[] buffer) => AppendBytes(buffer.AsSpan());

    public void AppendBytes(byte[] buffer, int start, int count) => AppendBytes(buffer.AsSpan(start, count));

    public void AppendBytes(Span<byte> buffer) => AppendBytes((ReadOnlySpan<byte>)buffer);

    public void AppendBytes(ReadOnlySpan<byte> buffer)
    {
        EnsureCapacity(buffer.Length);
        
        buffer.CopyTo(_buffer.AsSpan(_position));
        _position += buffer.Length;
    }

    public void AppendBytes(IEnumerable<byte> bytes)
    {
        foreach (var value in bytes)
        {
            AppendByte(value);
        }
    }

    public NetworkPacket ToPacket(NetworkPacketSource source = NetworkPacketSource.Network)
    {
        CheckIfDisposed();
        var packetData = _buffer.AsSpan(0, _position).ToArray();

        NetworkPacket result = IsClient
            ? new ClientPacket(Command, packetData) { Source = source }
            : new ServerPacket(Command, packetData) { Source = source };

        return result;
    }

    public override string ToString()
    {
        CheckIfDisposed();
        var sb = new StringBuilder(Command.ToString("X2"));

        if (_position <= 0)
        {
            return sb.ToString();
        }

        sb.Append(' ');
        for (var i = 0; i < _position; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(_buffer[i].ToString("X2"));
        }

        return sb.ToString();
    }

    public void Dispose() => Dispose(true);
    
    private void Dispose(bool isDisposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (isDisposing)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        _isDisposed = true;
    }

    private void CheckIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, typeof(NetworkPacketBuilder));
}