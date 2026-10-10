using System.Collections;
using System.Net.Sockets;

namespace Arbiter.Net;

public abstract class NetworkPacket : IEnumerable<byte>
{
    public const byte Marker = 0xAA;
    public const int HeaderSize = 4;

    public byte Command { get; }
    public byte[] Data { get; }
    public NetworkPacketSource Source { get; init; } = NetworkPacketSource.Network;

    protected NetworkPacket(byte command, byte[] data)
    {
        Command = command;
        Data = data;
    }

    public async ValueTask WriteToAsync(NetworkStream stream, Memory<byte> headerBuffer,
        CancellationToken cancellationToken = default)
    {
        var size = Data.Length + 1;

        var header = headerBuffer.Span;
        header[0] = Marker;
        header[1] = (byte)((size >> 8) & 0xFF);
        header[2] = (byte)(size & 0xFF);
        header[3] = Command;

        await stream.WriteAsync(headerBuffer[..HeaderSize], cancellationToken)
            .ConfigureAwait(false);

        if (Data.Length > 0)
        {
            await stream.WriteAsync(Data, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public virtual IEnumerator<byte> GetEnumerator()
    {
        var size = Data.Length + 1;

        yield return Marker;
        yield return (byte)((size >> 8) & 0xFF);
        yield return (byte)(size & 0xFF);
        yield return Command;

        foreach (var dataByte in Data)
        {
            yield return dataByte;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => string.Join(' ', this.Select(x => x.ToString("X2")));
}