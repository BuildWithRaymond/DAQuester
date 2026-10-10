using System.Security.Cryptography;
using Arbiter.Net.Security;

namespace Arbiter.Net.Client;

public class ClientPacketEncryptor : INetworkPacketEncryptor
{
    private const int MaxStackAllocLimit = 1024;
    private readonly Crc16Provider _crc16Provider = new();

    public NetworkEncryptionParameters Parameters { get; set; }

    public ClientPacketEncryptor() :
        this(NetworkEncryptionParameters.Default)
    {
    }

    public ClientPacketEncryptor(NetworkEncryptionParameters parameters)
    {
        Parameters = parameters;
    }

    public static bool IsEncrypted(byte command) => command is not 0x00 and not 0x10 and not 0x48;

    private static bool UseStaticKey(byte command) => command is 0x02 or 0x03 or 0x04 or 0x0B or 0x26 or 0x2D or 0x3A
        or 0x42 or 0x43 or 0x4B or 0x57 or 0x62 or 0x68 or 0x71 or 0x73 or 0x7B;

    private static bool UseHashKey(byte command) => !UseStaticKey(command);
    private static bool IsDialog(byte command) => command is 0x39 or 0x3A;

    public bool ShouldEncrypt(byte command) => IsEncrypted(command);
    
    public NetworkPacket Decrypt(NetworkPacket packet)
    {
        if (!IsEncrypted(packet.Command))
        {
            return packet;
        }

        var saltTable = Parameters.SaltTable.Span;
        var keyLength = Parameters.PrivateKey.Length;
        Span<byte> privateKey = stackalloc byte[keyLength];

        var useHashKey = UseHashKey(packet.Command);

        // Determine if we need to generate a hash key or just use the static key
        if (useHashKey)
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

        // Get the sequence number which is used for decrypting
        var sequence = packet.Data[0];

        // Extract the packet MD5 "32-bit" checksum (md5[13] md5[3] md5[11] md5[7])
        var checksum = (uint)packet.Data[^7] << 24 | (uint)packet.Data[^6] << 16 | (uint)packet.Data[^5] << 8 |
                       packet.Data[^4];

        // [u8 Sequence] [u8... Dialog] [u8... Payload] [0x00] [u8? Command] [u8 bRand Lo] [u8 sRand] [u8 bRand Hi]
        // If the packet is a dialog packet, it will have a 6-byte header before the payload
        // If the packet uses the hash key, it will have the command byte duplicated after the payload and zero byte
        var payloadLength = packet.Data.Length - (useHashKey ? 10 : 9);

        var payload = new Span<byte>(packet.Data, 1, payloadLength);
        var decrypted = new byte[payloadLength].AsSpan();
        payload.CopyTo(decrypted);

        // Decrypt the payload using the standard encryption algorithm (dialog is done separately)
        for (var i = 0; i < payloadLength; i++)
        {
            decrypted[i] ^= privateKey[i % keyLength];
            decrypted[i] ^= saltTable[i / keyLength % saltTable.Length];

            if (i / keyLength % saltTable.Length != sequence)
            {
                decrypted[i] ^= saltTable[sequence];
            }
        }

        // If not a dialog packet, no further decryption is needed
        if (!IsDialog(packet.Command))
        {
            return new ClientPacket(packet.Command, decrypted, checksum) { Sequence = sequence };
        }

        // Handle dialog encryption from 6-byte dialog header
        // [u8 xPrime] [u8 x] [u16 Length] [u16 Checksum]
        var xPrime = (byte)(decrypted[0] - 0x2D);
        var x = (byte)(decrypted[1] ^ xPrime);
        var y = (byte)(x + 0x72);
        var z = (byte)(x + 0x28);

        // Decrypt the dialog length first
        // This will be the CRC-16 bytes plus the payload itself
        decrypted[2] ^= y;
        decrypted[3] ^= (byte)((y + 1) & 0xFF);
        var length = (decrypted[2] << 8) | decrypted[3];

        // Decrypt the dialog checksum and payload bytes
        for (var i = 0; i < length; i++)
        {
            decrypted[4 + i] ^= (byte)((z + i) & 0xFF);
        }

        // We can now discard the dialog header after decrypting the payload
        decrypted = decrypted[6..];
        return new ClientPacket(packet.Command, decrypted, checksum) { Sequence = sequence };
    }

    public NetworkPacket Encrypt(NetworkPacket packet, byte sequence)
    {
        // Generate the random values within their expected ranges
        var bRand = (ushort)Random.Shared.Next(256, ushort.MaxValue);
        var sRand = (byte)Random.Shared.Next(100, byte.MaxValue);

        return Encrypt(packet, sequence, bRand, sRand);
    }

    public NetworkPacket Encrypt(NetworkPacket packet, byte sequence, ushort bRand, byte sRand,
        ushort? dialogRand = null)
    {
        if (!IsEncrypted(packet.Command))
        {
            return packet;
        }

        var isDialog = IsDialog(packet.Command);
        var useHashKey = UseHashKey(packet.Command);

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

        // [u8 Command] [u8 Sequence] [u8... Dialog] [u8... Payload] [00] [u8? Command] [u32 Checksum] [u8 bRand Lo] [u8 sRand] [u8 bRand Hi]
        // We need to include the command byte in the checksum calculation, even if we discard it later
        // If the packet is a dialog packet, it will have a 6-byte header before the payload
        // If the packet uses the hash key, it will have the command byte duplicated after the payload and zero byte
        var totalLength = packet.Data.Length + 10 + (isDialog ? 6 : 0) + (useHashKey ? 1 : 0);

        // Allocate a buffer for the entire encrypted payload, including headers, checksum and trailing random values
        // Attempt to use a stackalloc buffer first, but fall back to a heap allocation if the stackalloc is too large
        // This is to avoid double allocation, as the ClientPacket constructor will copy the data into a new buffer
        var buffer = totalLength <= MaxStackAllocLimit ? stackalloc byte[totalLength] : new byte[totalLength];

        // Copy the command and sequence first, this is needed to generate the checksum
        buffer[0] = packet.Command;
        buffer[1] = sequence;

        var payloadLength = totalLength - 9;

        if (isDialog)
        {
            // Generate random values for dialog key if not provided (avoid generating zero or 0xFFFF)
            dialogRand ??= (ushort)Random.Shared.Next(1, ushort.MaxValue);

            // Generate the dialog header and write before the payload
            // This length should not include the trailing command byte
            var dataLengthPlusTwo = packet.Data.Length + 2;
            var dialogChecksum = _crc16Provider.Compute(packet.Data);

            buffer[2] = (byte)(dialogRand! >> 8);
            buffer[3] = (byte)(dialogRand! & 0xFF);
            buffer[4] = (byte)(dataLengthPlusTwo >> 8);
            buffer[5] = (byte)(dataLengthPlusTwo & 0xFF);
            buffer[6] = (byte)(dialogChecksum >> 8);
            buffer[7] = (byte)(dialogChecksum & 0xFF);

            // Copy the payload after the dialog header
            packet.Data.CopyTo(buffer[8..]);

            // Obfuscate the dialog header size and generate key values
            var xPrime = (byte)(buffer[2] - 0x2D);
            var x = (byte)(buffer[3] ^ xPrime);
            var y = (byte)(x + 0x72);
            var z = (byte)(x + 0x28);
            buffer[4] ^= y;
            buffer[5] ^= (byte)((y + 1) & 0xFF);

            // Perform the dialog encryption on the checksum and payload bytes
            for (var i = 0; i < dataLengthPlusTwo; i++)
            {
                buffer[6 + i] ^= (byte)((z + i) & 0xFF);
            }
        }
        else
        {
            // Copy the payload after the command and sequence bytes
            packet.Data.CopyTo(buffer[2..]);
        }

        // If the packet uses the hash key, it will have the command byte duplicated after the payload
        if (useHashKey)
        {
            buffer[^8] = packet.Command;
        }

        // Perform the standard encryption on the entire payload
        for (var i = 0; i < payloadLength; i++)
        {
            buffer[2 + i] ^= privateKey[i % keyLength];
            buffer[2 + i] ^= saltTable[i / keyLength % saltTable.Length];

            if (i / keyLength % saltTable.Length != sequence)
            {
                buffer[2 + i] ^= saltTable[sequence];
            }
        }

        // Determine the MD5 checksum of the entire payload as encrypted
        Span<byte> checksum = stackalloc byte[16];
        MD5.HashData(buffer[..^7], checksum);

        buffer[^7] = checksum[13];
        buffer[^6] = checksum[3];
        buffer[^5] = checksum[11];
        buffer[^4] = checksum[7];

        // Write the trailing bRand and sRand values in their encoded form
        WriteHashKeySalt(buffer, bRand, sRand);
        return new ClientPacket(packet.Command, buffer[1..]);
    }

    public ushort DecryptDialogKey(byte command, ReadOnlySpan<byte> data)
    {
        if (!IsDialog(command))
        {
            return 0;
        }

        var saltTable = Parameters.SaltTable.Span;
        var keyLength = Parameters.PrivateKey.Length;
        Span<byte> privateKey = stackalloc byte[keyLength];

        var useHashKey = UseHashKey(command);

        // Determine if we need to generate a hash key or just use the static key
        if (useHashKey)
        {
            // Read the trailing 3 bytes to get the bRand and sRand values from their encoded form
            var (bRand, sRand) = ReadHashKeySalt(data);
            Parameters.GenerateHashKey(bRand, sRand, privateKey);
        }
        else
        {
            // Default to the static 9-byte key
            Parameters.PrivateKey.Span.CopyTo(privateKey);
        }

        // Get the sequence number which is used for decrypting
        var sequence = data[0];

        var payload = data[1..7];
        Span<byte> dialogHeader = stackalloc byte[6];
        payload.CopyTo(dialogHeader);

        // Decrypt the dialog header using the standard encryption algorithm
        for (var i = 0; i < dialogHeader.Length; i++)
        {
            dialogHeader[i] ^= privateKey[i % keyLength];
            dialogHeader[i] ^= saltTable[i / keyLength % saltTable.Length];

            if (i / keyLength % saltTable.Length != sequence)
            {
                dialogHeader[i] ^= saltTable[sequence];
            }
        }

        return (ushort)((dialogHeader[0] << 8) | dialogHeader[1]);
    }

    public static (ushort bRand, byte sRand) ReadHashKeySalt(ReadOnlySpan<byte> buffer)
    {
        var sRand = (byte)(buffer[^2] ^ 0x23);
        var bRand = (ushort)((buffer[^1] << 8 | buffer[^3]) ^ 0x7470);
        return (bRand, sRand);
    }

    public static void WriteHashKeySalt(Span<byte> buffer, ushort bRand, byte sRand)
    {
        buffer[^3] = (byte)((bRand & 0xFF) ^ 0x70);
        buffer[^2] = (byte)(sRand ^ 0x23);
        buffer[^1] = (byte)(((bRand >> 8) & 0xFF) ^ 0x74);
    }
}