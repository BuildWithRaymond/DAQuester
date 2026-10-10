namespace Arbiter.Net.Security;

public interface ICrcProvider<TChecksum>
{
    TChecksum Polynomial { get; }
    
    TChecksum Compute(ReadOnlySpan<byte> data);
    TChecksum Compute(ReadOnlySpan<byte> data, TChecksum initial);
    TChecksum Compute(ReadOnlySpan<byte> data, TChecksum initial, TChecksum finalXor);

    TChecksum ComputeNext(TChecksum checksum, byte value);
}