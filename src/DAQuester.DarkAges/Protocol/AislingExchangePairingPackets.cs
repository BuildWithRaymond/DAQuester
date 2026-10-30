using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using DAQuester.Core;

namespace DAQuester.DarkAges.Protocol;

// The existing Tell wire form carries two string8 fields. This scoped operation
// accepts only a pairing nonce and never exposes a general remote chat command.
public static class AislingExchangePairingPackets
{
    public static void Validate(CharacterSnapshot snapshot, string character, long expectedGeneration, string nonce)
    {
        if (!snapshot.Connected || snapshot.Id is null or 0 || snapshot.Revision <= 0 || expectedGeneration <= 0 ||
            snapshot.Generation != expectedGeneration || !string.Equals(snapshot.Name, character, StringComparison.OrdinalIgnoreCase) ||
            snapshot.Dialog is not null || snapshot.Exchange is { Cancelled: false } ||
            nonce is not { Length: 47 } || !nonce.StartsWith("AEQ-", StringComparison.Ordinal) ||
            nonce.AsSpan(4).ContainsAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"))
            throw new QuestBlockedException("AislingExchange pairing needs the same ready character and a valid current challenge.");
    }

    public static ClientPacket Create(CharacterSnapshot snapshot, string character, long expectedGeneration, string nonce)
    {
        Validate(snapshot, character, expectedGeneration, nonce);
        var builder = new NetworkPacketBuilder(ClientCommand.Tell);
        try
        {
            builder.AppendString8("Exchanger");
            builder.AppendString8(nonce);
            return (ClientPacket)builder.ToPacket();
        }
        finally { builder.Dispose(); }
    }
}
