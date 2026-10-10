using System.Net;
using System.Text;

namespace Arbiter.Net.Serialization;

public ref struct SpanReader(Endianness? endianness = null, Encoding? encoding = null)
{
    private readonly Endianness _endianness = endianness switch
    {
        Endianness.LittleEndian => Endianness.LittleEndian,
        Endianness.BigEndian => Endianness.BigEndian,
        _ => BitConverter.IsLittleEndian ? Endianness.LittleEndian : Endianness.BigEndian,
    };
    private readonly Encoding _encoding = encoding ?? Encoding.UTF8;
    private int _position;
    
    public int Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _position = value;
        }
    }

    public bool ReadBoolean(ReadOnlySpan<byte> span) => ReadByte(span) != 0;
    
    public sbyte ReadSByte(ReadOnlySpan<byte> span) => (sbyte)ReadByte(span);

    public byte ReadByte(ReadOnlySpan<byte> span)
    {
        EnsureCanRead(span, 1);
        return span[_position++];
    }

    public short ReadInt16(ReadOnlySpan<byte> span) => (short)ReadUInt16(span);

    public ushort ReadUInt16(ReadOnlySpan<byte> span)
    {
        EnsureCanRead(span, 2);
        return _endianness switch
        {
            Endianness.LittleEndian => (ushort)(span[_position++] | (span[_position++] << 8)),
            Endianness.BigEndian => (ushort)(span[_position++] << 8 | span[_position++]),
            _ => throw new InvalidOperationException("Unknown endianness"),
        };
    }
    
    public int ReadInt32(ReadOnlySpan<byte> span) => (int)ReadUInt32(span);

    public uint ReadUInt32(ReadOnlySpan<byte> span)
    {
        EnsureCanRead(span, 4);
        return _endianness switch
        {
            Endianness.LittleEndian => (uint)(span[_position++] | (span[_position++] << 8) | (span[_position++] << 16) |
                                              (span[_position++] << 24)),
            Endianness.BigEndian => (uint)(span[_position++] << 24 | span[_position++] << 16 | span[_position++] << 8 |
                                           span[_position++]),
            _ => throw new InvalidOperationException("Unknown endianness"),
        };
    }
    
    public long ReadInt64(ReadOnlySpan<byte> span) => (long)ReadUInt64(span);

    public ulong ReadUInt64(ReadOnlySpan<byte> span)
    {
        EnsureCanRead(span, 8);
        return _endianness switch
        {
            Endianness.LittleEndian => (span[_position++] | ((ulong)span[_position++] << 8) |
                                        ((ulong)span[_position++] << 16) | ((ulong)span[_position++] << 24) |
                                        ((ulong)span[_position++] << 32) | ((ulong)span[_position++] << 40) |
                                        ((ulong)span[_position++] << 48) | ((ulong)span[_position++] << 56)),
            Endianness.BigEndian => ((ulong)span[_position++] << 56 | (ulong)span[_position++] << 48 |
                                     (ulong)span[_position++] << 40 | (ulong)span[_position++] << 32 |
                                     (ulong)span[_position++] << 24 | (ulong)span[_position++] << 16 |
                                     (ulong)span[_position++] << 8 | span[_position++]),
            _ => throw new InvalidOperationException("Unknown endianness"),
        };
    }
    
    public string ReadString8(ReadOnlySpan<byte> span) => ReadFixedString(span, ReadByte(span));
    public string ReadString16(ReadOnlySpan<byte> span) => ReadFixedString(span, ReadUInt16(span));

    public string ReadFixedString(ReadOnlySpan<byte> span, int length)
    {
        if (length == 0)
        {
            return string.Empty;
        }

        EnsureCanRead(span, length);
        var stringValue = _encoding.GetString(span.Slice(_position, length));
        _position += length;
        return stringValue;
    }

    public string ReadNullTerminatedString(ReadOnlySpan<byte> span)
    {
        var length = 0;
        while (span[_position + length] != 0)
        {
            EnsureCanRead(span, 1);
            length++;
        }
        
        var stringValue = _encoding.GetString(span.Slice(_position, length));
        _position += length + 1;
        return stringValue;
    }

    public string ReadLine(ReadOnlySpan<byte> span)
    {
        var length = 0;
        while (span[_position + length] != '\n' && span[_position + length] != '\r')
        {
            EnsureCanRead(span, 1);
            length++;
        }
        
        var stringValue = _encoding.GetString(span.Slice(_position, length));
        _position += length + 1;
        return stringValue;
    }

    public IPAddress ReadIPv4Address(ReadOnlySpan<byte> span)
    {
        EnsureCanRead(span, 4);

        if (_endianness == Endianness.LittleEndian)
        {
            var ipAddress = new IPAddress(span.Slice(_position, 4));
            _position += 4;
            return ipAddress;
        }

        Span<byte> ipBytes = stackalloc byte[4];
        ipBytes[3] = span[_position++];
        ipBytes[2] = span[_position++];
        ipBytes[1] = span[_position++];
        ipBytes[0] = span[_position++];
        
        return new IPAddress(ipBytes);
    }

    public byte[] ReadBytes(ReadOnlySpan<byte> span, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count is 0)
        {
            return [];
        }

        EnsureCanRead(span, count);
        var destination = new byte[count];
        span.Slice(_position, count).CopyTo(destination);
        _position += count;

        return destination;
    }

    public void ReadBytes(ReadOnlySpan<byte> span, Span<byte> destination, int? count = null)
    {
        if (count.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count.Value);
        }

        var effectiveCount = count ?? destination.Length;
        if (effectiveCount is 0)
        {
            return;
        }

        EnsureCanRead(span, effectiveCount);
        span.Slice(_position, effectiveCount).CopyTo(destination);
        _position += effectiveCount;
    }

    public void Skip(int length)
    {
        _position += length;
    }

    private void EnsureCanRead(ReadOnlySpan<byte> span, int length)
    {
        if (_position + length > span.Length)
        {
            throw new IndexOutOfRangeException("Cannot read past end of span");
        }
    }
}