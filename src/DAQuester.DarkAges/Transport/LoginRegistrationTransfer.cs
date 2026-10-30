using System.Security.Cryptography;
using System.Text;
using Arbiter.Net.Security;
using DAQuester.Core;

namespace DAQuester.DarkAges.Transport;

/// <summary>One redirect's registration proof, bound to its entire client handshake.</summary>
internal sealed class LoginRegistrationTransfer
{
    readonly object gate = new();
    readonly ServerNotice source;
    readonly string character;
    readonly byte[] metadataHash;
    readonly DateTimeOffset issued;
    int state; // 0 awaiting first handshake, 1 matched, 2 terminal.

    LoginRegistrationTransfer(ServerNotice source, string character, byte[] metadataHash, DateTimeOffset issued)
        => (this.source, this.character, this.metadataHash, this.issued) = (source, character, metadataHash, issued);

    internal static LoginRegistrationTransfer? TryCreate(ServerNotice source, string expectedCharacter,
        byte[] redirectPayload, DateTimeOffset now)
    {
        if (source is null || source.Generation <= 0 || source.Revision <= 0 || !DaydreamerEvidence.IsRegistration(source)
            || string.IsNullOrWhiteSpace(expectedCharacter) || expectedCharacter.Length > byte.MaxValue
            || expectedCharacter.Any(value => value is < ' ' or > '~') || redirectPayload is null || redirectPayload.Length < 7)
            return null;
        // IPv4 address4, port2 and semantic metadata byte count1 precede
        // seed1, key-length1, key, String8 name and opaque cookie4.
        var metadata = redirectPayload.AsSpan(7);
        if (metadata.Length < 3 || metadata[1] != NetworkEncryptionParameters.KeyLength) return null;
        int nameOffset = 2 + metadata[1];
        if (nameOffset >= metadata.Length) return null;
        int nameLength = metadata[nameOffset], semanticLength = nameOffset + 1 + nameLength + 4;
        if (nameLength == 0 || semanticLength != redirectPayload[6] || metadata.Length < semanticLength
            || metadata.Length > semanticLength + 1 || metadata.Length == semanticLength + 1 && metadata[^1] != 0)
            return null;
        var nameBytes = metadata.Slice(nameOffset + 1, nameLength);
        foreach (byte value in nameBytes) if (value is < 32 or > 126) return null;
        if (!Encoding.ASCII.GetString(nameBytes).Equals(expectedCharacter, StringComparison.OrdinalIgnoreCase)) return null;
        // The optional trailing NUL is part of the binding. Retain only its
        // digest, never the key, cookie or raw redirect/handshake bytes.
        return new(source, expectedCharacter, SHA256.HashData(metadata), now);
    }

    bool Current(DateTimeOffset now) => now >= issued && now - issued < TimeSpan.FromSeconds(45);

    internal bool ObserveHandshake(byte[] payload, DateTimeOffset now)
    {
        lock (gate)
        {
            if (state != 0 || !Current(now) || payload is null
                || !CryptographicOperations.FixedTimeEquals(metadataHash, SHA256.HashData(payload)))
            {
                state = 2;
                return false;
            }
            state = 1;
            return true;
        }
    }

    internal ServerNotice? Consume(string character, uint actualOwnId, DateTimeOffset now)
    {
        lock (gate)
        {
            bool accepted = state == 1 && Current(now) && actualOwnId > 0
                && string.Equals(character, this.character, StringComparison.OrdinalIgnoreCase);
            state = 2;
            return accepted ? source : null;
        }
    }

    internal void Invalidate()
    {
        lock (gate) state = 2;
    }
}
