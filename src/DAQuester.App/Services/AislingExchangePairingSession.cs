using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed class AislingExchangePairingSession(IGameSession session, string character, string serverProfile, Func<bool> owns)
    : IAislingExchangePairingSession
{
    public AislingExchangePairingSessionState Snapshot
    {
        get
        {
            var current = session.Snapshot;
            bool identity = current.Id is > 0 && current.Name?.Equals(character, StringComparison.OrdinalIgnoreCase) == true;
            return new(character, serverProfile, current.Generation, current.Connected && identity, owns() && identity);
        }
    }

    public async Task<AislingExchangeWhisperOutcome> SendPairingWhisperAsync(string receiver, string nonce, long expectedGeneration, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var before = Snapshot;
        if (receiver != "Exchanger" || !before.Connected || !before.Owned || before.Generation != expectedGeneration ||
            !session.Capabilities.Contains("aislingexchange-pair"))
            return AislingExchangeWhisperOutcome.Rejected;
        try { DarkAges.Protocol.AislingExchangePairingPackets.Validate(session.Snapshot, character, expectedGeneration, nonce); }
        catch (QuestBlockedException) { return AislingExchangeWhisperOutcome.Rejected; }
        try
        {
            await session.ExecuteAsync(new("aislingexchange-pair", Text: nonce, ExpectedGeneration: expectedGeneration,
                ExpectedResource: character), cancellation);
        }
        catch (OperationCanceledException) { throw; }
        catch { return AislingExchangeWhisperOutcome.Uncertain; }
        // A completed socket write does not prove that the game server delivered it.
        // Only the provider worker's independent observation finishes pairing.
        return AislingExchangeWhisperOutcome.Uncertain;
    }
}
