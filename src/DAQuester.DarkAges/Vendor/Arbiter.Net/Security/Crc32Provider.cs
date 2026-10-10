namespace Arbiter.Net.Security;

public class Crc32Provider : ICrcProvider<uint>
{
    public uint Polynomial { get; }

    public uint[] Table { get; } = new uint[256];

    public Crc32Provider(uint polynomial = 0xEDB88320)
    {
        Polynomial = polynomial;
        GenerateTable(polynomial, Table);
    }

    public uint Compute(ReadOnlySpan<byte> data)
        => Compute(data, uint.MaxValue, uint.MaxValue);

    public uint Compute(ReadOnlySpan<byte> data, uint initial) =>
        Compute(data, initial, uint.MaxValue);

    public uint Compute(ReadOnlySpan<byte> data, uint initial, uint finalXor)
    {
        var crc = initial;
        foreach (var value in data)
        {
            crc = ComputeNext(crc, value);
        }

        return crc ^ finalXor;
    }

    public uint ComputeNext(uint crc, byte value)
        => (crc >> 8) ^ Table[(crc ^ value) & 0xFF];

    private static void GenerateTable(uint polynomial, Span<uint> table)
    {
        for (var i = 0; i < 256; i++)
        {
            var crc = (uint)i;
            for (var bit = 0; bit < 8; bit++)
            {
                if ((crc & 1) != 0)
                {
                    crc = (crc >> 1) ^ polynomial;
                }
                else
                {
                    crc >>= 1;
                }
            }

            table[i] = crc;
        }
    }
}