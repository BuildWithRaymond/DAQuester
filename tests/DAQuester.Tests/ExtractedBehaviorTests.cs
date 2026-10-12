using Xunit;
using Arbiter.Net.Client;
using Arbiter.Net.Server;
using Arbiter.Net.Security;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges;
using DAQuester.Core;

namespace DAQuester.Tests;

public class ExtractedBehaviorTests
{
    [Fact]
    public void Excalibur_corrupt_prefix_recovery()
    {
        var b = new FrameBuffer();
        b.Append([0, 0xAA, 0, 1, 0x10]);
        Assert.Equal(new byte[] { 0xAA, 0, 1, 0x10 }, b.Read());
    }

    [Theory]
    [InlineData(0x03)]
    [InlineData(0x06)]
    [InlineData(0x39)]
    [InlineData(0x3A)]
    [InlineData(0x43)]
    public void Client_crypto_keeps_payload_and_sequence(int opcode)
    {
        var c = new ClientPacketEncryptor(new(1, "Urk-12345"u8, "Nuclear"));
        var p = new ClientPacket((byte)opcode, new byte[] { 1, 0, 0, 0, 44, 0, 8, 0, 2, 1, 1 });
        var wire = c.Encrypt(p, 17, 512, 123);
        var result = c.Decrypt(wire);
        Assert.Equal(p.Data, result.Data);
        Assert.Equal((byte)17, ((ClientPacket)result).Sequence!.Value);
    }

    [Fact]
    public void Server_crypto_matches_excalibur_seed_zero_xor_vector()
    {
        var c = new ServerPacketEncryptor(new(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, "Nuclear"));
        // Static opcode 0x02; sequence 7. Payload XOR private key XOR salt[0] XOR salt[7].
        var encrypted = c.Encrypt(new ServerPacket(2, new byte[] { 10, 20, 30 }), 7, 512, 123);
        Assert.Equal(new byte[] { 7, 12, 17, 26, 0x74, 0x5F, 0x66 }, encrypted.Data);
    }

    [Fact]
    public void Stale_dialog_is_rejected_before_send()
    {
        var d = new Dialog(1, 4, 77, 1, "Callough", "", false, 0, 0, 0, [new("Spare a Stick", 33, 1)]);
        var s = FoundationTests.Ready() with
        {
            Dialog = d with
            {
                Revision = 5
            }
        };
        Assert.Throws<QuestBlockedException>(() => GameSession.ResolveChoice(s, d, "Spare a Stick"));
        Assert.Throws<QuestBlockedException>(() => GameSession.ResolveChoice(s with { Generation = 2, Dialog = d }, d, "Spare a Stick"));
    }
}
