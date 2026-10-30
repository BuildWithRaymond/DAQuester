using System.Reflection;
using System.Text;
using Arbiter.Net.Client;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Diagnostics;
using Xunit;

namespace DAQuester.Tests;

public class AislingExchangePairingProtocolTests
{
    static CharacterSnapshot Own => new() { Connected = true, Name = "ExamplePlayer", Id = 42, Generation = 9, Revision = 10 };
    const string Nonce = "AEQ-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    static ClientPacket Create(CharacterSnapshot snapshot, string character, long generation, string nonce)
    {
        var type = typeof(GameSession).Assembly.GetType("DAQuester.DarkAges.Protocol.AislingExchangePairingPackets");
        Assert.NotNull(type);
        var method = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        try { return Assert.IsType<ClientPacket>(method.Invoke(null, [snapshot, character, generation, nonce])); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    [Fact]
    public void Pairing_is_a_single_tell_to_the_fixed_provider_with_no_extra_fields()
    {
        var packet = Create(Own, "ExamplePlayer", 9, Nonce);
        Assert.Equal(0x19, (byte)packet.Command);
        Assert.Equal(new byte[] { 9 }.Concat(Encoding.ASCII.GetBytes("Exchanger"))
            .Concat(new byte[] { 47 }).Concat(Encoding.ASCII.GetBytes(Nonce)), packet.Data);
    }

    [Theory]
    [InlineData("AnotherPlayer", 9, Nonce)]
    [InlineData("ExamplePlayer", 8, Nonce)]
    [InlineData("ExamplePlayer", 9, "ordinary chat")]
    [InlineData("ExamplePlayer", 9, "AEQ-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n")]
    public void Pairing_rejects_changed_identity_generation_and_nonchallenge_text(string character, long generation, string nonce)
        => Assert.Throws<QuestBlockedException>(() => Create(Own, character, generation, nonce));

    [Fact]
    public void Pairing_nonce_never_enters_replay_payloads()
    {
        var packet = Create(Own, "ExamplePlayer", 9, Nonce);
        var recorder = new ReplayRecorder();
        recorder.RecordClient((byte)packet.Command, packet.Data, true, Own);
        string directory = Path.Combine(Path.GetTempPath(), "aislingexchange-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "replay.json");
            recorder.Export(path);
            string json = File.ReadAllText(path);
            Assert.DoesNotContain(Nonce, json);
            Assert.DoesNotContain(Convert.ToBase64String(packet.Data), json);
        }
        finally { Directory.Delete(directory, true); }
    }
}
