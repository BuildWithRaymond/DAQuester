using Arbiter.Net.Security;

namespace Arbiter.Net.Server;

public class ServerPacketEncryptor : INetworkPacketEncryptor
{
    private const int MaxStackAllocLimit = 1024;

    public NetworkEncryptionParameters Parameters { get; set; }

    public ServerPacketEncryptor()
        : this(NetworkEncryptionParameters.Default)
    {
    }

    public ServerPacketEncryptor(NetworkEncryptionParameters parameters)
    {
        Parameters = parameters;
    }

    public static bool IsEncrypted(byte command) => command is not 0x00 and not 0x03 and not 0x40 and not 0x7E;

    private static bool UseStaticKey(byte command) =>
        command is 0x01 or 0x02 or 0x0A or 0x56 or 0x60 or 0x62 or 0x66 or 0x6F;

    private static bool UseHashKey(byte command) => !UseStaticKey(command);

    public bool ShouldEncrypt(byte command) => IsEncrypted(command);
    
    public NetworkPacket Decrypt(NetworkPacket packet)
    {
        if (!IsEncrypted(packet.Command))
        {
            return packet;
        }

        var saltTable = Parameters.SaltTable.Span;
        var keyLength = Parameters.PrivateKey.Length;
        Span<byte> privateKey = stackalloc byte[Parameters.PrivateKey.Length];

        // Determine if we need to generate a hash key or just use the static key
        if (UseHashKey(packet.Command))
        {
            // Read the trailing 3 bytes to get the bRand and sRand values from their encoded form
            var (bRand, sRand) = ReadHashKeySalt(packet.Data);
            Parameters.GenerateHashKey(bRand, sRand, privateKey);
        }
        else
        {
            // Default to the static 9-byte key
            Parameters.PrivateKey.Span.CopyTo(privateKey);
        }

        // [u8 Sequence] [u8... Payload] [u8 bRand Lo] [u8 sRand] [u8 bRand Hi]
        var payloadLength = packet.Data.Length - 4;
        var payload = new Span<byte>(packet.Data, 1, payloadLength);

        // Get the sequence number which is used for decrypting
        var sequence = packet.Data[0];

        var decrypted = new byte[payloadLength].AsSpan();
        payload.CopyTo(decrypted);

        // Decrypt the payload
        for (var i = 0; i < payloadLength; i++)
        {
            decrypted[i] ^= privateKey[i % keyLength];
            decrypted[i] ^= saltTable[i / keyLength % saltTable.Length];

            if (i / keyLength % saltTable.Length != sequence)
            {
                decrypted[i] ^= saltTable[sequence];
            }
        }

        return new ServerPacket(packet.Command, decrypted) { Sequence = sequence };
    }

    public NetworkPacket Encrypt(NetworkPacket packet, byte sequence)
    {
        // Generate the random values within their expected ranges
        var bRand = (ushort)Random.Shared.Next(256, ushort.MaxValue);
        var sRand = (byte)Random.Shared.Next(100, byte.MaxValue);

        return Encrypt(packet, sequence, bRand, sRand);
    }

    public NetworkPacket Encrypt(NetworkPacket packet, byte sequence, ushort bRand, byte sRand)
    {
        if (!IsEncrypted(packet.Command))
        {
            return packet;
        }

        var saltTable = Parameters.SaltTable.Span;
        var keyLength = Parameters.PrivateKey.Length;
        Span<byte> privateKey = stackalloc byte[keyLength];

        // Determine if we need to generate a hash key or just use the static key
        if (UseHashKey(packet.Command))
        {
            // Use the provided bRand and sRand values
            Parameters.GenerateHashKey(bRand, sRand, privateKey);
        }
        else
        {
            // Default to the static 9-byte key
            Parameters.PrivateKey.Span.CopyTo(privateKey);
        }

        // [u8 Sequence] [u8... Payload] [u8 bRand Lo] [u8 sRand] [u8 bRand Hi]
        var totalLength = packet.Data.Length + 4;

        // Attempt to use a stackalloc buffer first, but fall back to a heap allocation if the stackalloc is too large
        // This is to avoid double allocation, as the ServerPacket constructor will copy the data into a new buffer
        var encrypted = totalLength <= MaxStackAllocLimit ? stackalloc byte[totalLength] : new byte[totalLength];

        // Copy the sequence and unencrypted payload
        encrypted[0] = sequence;
        packet.Data.CopyTo(encrypted[1..]);

        // Perform the standard encryption on the current payload
        for (var i = 0; i < packet.Data.Length; i++)
        {
            encrypted[i + 1] ^= privateKey[i % keyLength];
            encrypted[i + 1] ^= saltTable[i / keyLength % saltTable.Length];

            if (i / keyLength % saltTable.Length != sequence)
            {
                encrypted[i + 1] ^= saltTable[sequence];
            }
        }

        // Write the trailing bRand and sRand values in their encoded form
        WriteHashKeySalt(encrypted, bRand, sRand);
        return new ServerPacket(packet.Command, encrypted);
    }

    public static (ushort bRand, byte sRand) ReadHashKeySalt(ReadOnlySpan<byte> buffer)
    {
        var sRand = (byte)(buffer[^2] ^ 0x24);
        var bRand = (ushort)((buffer[^1] << 8 | buffer[^3]) ^ 0x6474);
        return (bRand, sRand);
    }

    public static void WriteHashKeySalt(Span<byte> buffer, ushort bRand, byte sRand)
    {
        buffer[^3] = (byte)((bRand & 0xFF) ^ 0x74);
        buffer[^2] = (byte)(sRand ^ 0x24);
        buffer[^1] = (byte)(((bRand >> 8) & 0xFF) ^ 0x64);
    }
}