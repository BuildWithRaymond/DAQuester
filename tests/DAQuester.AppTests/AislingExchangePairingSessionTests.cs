using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class AislingExchangePairingSessionTests
{
    sealed class Session : IGameSession
    {
        public CharacterSnapshot Snapshot { get; set; } = new() { Connected = true, Name = "ExamplePlayer", Id = 7, Generation = 8, Revision = 12 };
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string> { "aislingexchange-pair" };
        public event Action<CharacterSnapshot>? Changed { add { } remove { } }
        public event Action<string>? Interrupted { add { } remove { } }
        public List<GameCommand> Commands = [];
        public Task ExecuteAsync(GameCommand command, CancellationToken cancellation) { Commands.Add(command); return Task.CompletedTask; }
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation) => throw new NotSupportedException();
    }

    [Fact]
    public async Task An_owned_connection_submits_one_guarded_nonce_without_claiming_server_acceptance()
    {
        var session = new Session();
        var port = new AislingExchangePairingSession(session, "ExamplePlayer", "datester-7.41", () => true);
        var outcome = await port.SendPairingWhisperAsync("Exchanger", "AEQ-" + new string('A', 43), 8, default);
        Assert.Equal(AislingExchangeWhisperOutcome.Uncertain, outcome);
        var command = Assert.Single(session.Commands);
        Assert.Equal("aislingexchange-pair", command.Kind);
        Assert.Equal("ExamplePlayer", command.ExpectedResource);
        Assert.Equal(8, command.ExpectedGeneration);
    }

    [Theory]
    [InlineData(false, 8, "Exchanger")]
    [InlineData(true, 7, "Exchanger")]
    [InlineData(true, 8, "AnotherRecipient")]
    public async Task Unowned_changed_or_redirected_pairing_never_submits(bool owned, long generation, string recipient)
    {
        var session = new Session();
        var port = new AislingExchangePairingSession(session, "ExamplePlayer", "datester-7.41", () => owned);
        Assert.Equal(AislingExchangeWhisperOutcome.Rejected,
            await port.SendPairingWhisperAsync(recipient, "AEQ-" + new string('A', 43), generation, default));
        Assert.Empty(session.Commands);
    }
}
