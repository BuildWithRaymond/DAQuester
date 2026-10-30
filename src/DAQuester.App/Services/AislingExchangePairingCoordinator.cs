using DAQuester.Core;

namespace DAQuester.App.Services;

public interface IAislingExchangePairingClient
{
    Task<AislingExchangePairingChallenge> StartPairingAsync(string character, string serverProfile, AislingExchangePairingProof proof, CancellationToken cancellation = default);
    Task<AislingExchangePairingStatus> GetPairingStatusAsync(AislingExchangePairingChallenge challenge, AislingExchangePairingProof proof, CancellationToken cancellation = default);
    Task<AislingExchangePairingCompletion> CompletePairingAsync(AislingExchangePairingChallenge challenge, AislingExchangePairingProof proof, CancellationToken cancellation = default);
    Task RevokePairingAsync(AislingExchangePairingChallenge challenge, AislingExchangePairingProof proof, CancellationToken cancellation = default);
}

public sealed record AislingExchangePairingSessionState(string Character, string ServerProfile, long Generation, bool Connected, bool Owned);
public enum AislingExchangeWhisperOutcome { SourceAccepted, Rejected, Uncertain }
public interface IAislingExchangePairingSession
{
    AislingExchangePairingSessionState Snapshot { get; }
    Task<AislingExchangeWhisperOutcome> SendPairingWhisperAsync(string receiver, string nonce, long expectedGeneration, CancellationToken cancellation);
}

public sealed record AislingExchangePairingProgress(bool Paired, string State);
public sealed record AislingExchangePairingIntent(string Character, string ServerProfile, long Generation, Guid InstallationId,
    Guid ClientOperationId, string Verifier, DateTimeOffset ExpiresAt, string State, AislingExchangePairingChallenge? Challenge)
{
    public override string ToString() => "AislingExchangePairingIntent { Sensitive values redacted }";
}
public interface IAislingExchangePairingStore
{
    Task<Guid> GetInstallationIdAsync(CancellationToken cancellation = default);
    Task<AislingExchangePairingIntent?> LoadAsync(string character, string serverProfile, CancellationToken cancellation = default);
    Task SaveAsync(AislingExchangePairingIntent intent, CancellationToken cancellation = default);
    Task ClearAsync(string character, string serverProfile, CancellationToken cancellation = default);
}

public sealed class AislingExchangePairingCoordinator
{
    private readonly IAislingExchangePairingClient client;
    private readonly IAislingExchangeCredentialStore credentials;
    private readonly IAislingExchangePairingStore store;
    private readonly TimeProvider time;
    private readonly SemaphoreSlim gate = new(1, 1);

    public AislingExchangePairingCoordinator(IAislingExchangePairingClient client, IAislingExchangeCredentialStore credentials,
        IAislingExchangePairingStore store, TimeProvider? time = null)
    {
        this.client = client;
        this.credentials = credentials;
        this.store = store;
        this.time = time ?? TimeProvider.System;
    }

    public async Task<AislingExchangePairingProgress> EnsurePairedAsync(IAislingExchangePairingSession session, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            var snapshot = session.Snapshot;
            if (!snapshot.Connected || !snapshot.Owned || snapshot.Generation <= 0)
                return new(false, "SessionUnavailable");
            var credential = await credentials.LoadAsync(snapshot.Character, snapshot.ServerProfile, cancellation);
            if (AislingExchangeClient.ValidCredential(credential, snapshot.Character, snapshot.ServerProfile, time.GetUtcNow()))
            {
                await store.ClearAsync(snapshot.Character, snapshot.ServerProfile, cancellation);
                return new(true, "Paired");
            }

            var intent = await store.LoadAsync(snapshot.Character, snapshot.ServerProfile, cancellation);
            if (intent is not null && (intent.Character != snapshot.Character || intent.ServerProfile != snapshot.ServerProfile))
                throw new AislingExchangeException("AislingExchange pairing checkpoint identity is invalid.");
            if (intent is not null && (intent.State is not ("Starting" or "WaitingReady" or "WhisperIntent" or "WhisperSubmitted"
                    or "WhisperUncertain" or "WhisperRejected" or "WaitingObservation" or "CompletionIntent")
                || intent.State == "CompletionIntent" && intent.Challenge is null
                || intent.Challenge is not null && (intent.Challenge.Character != intent.Character
                    || intent.Challenge.ServerProfile != intent.ServerProfile || intent.Challenge.Receiver != "Exchanger")))
                throw new AislingExchangeException("AislingExchange pairing checkpoint is invalid.");
            if (intent is { State: "CompletionIntent", Challenge: not null })
            {
                using var lostProof = AislingExchangePairingProof.Restore(intent.InstallationId, intent.ClientOperationId, intent.Verifier);
                try { await client.RevokePairingAsync(intent.Challenge, lostProof, cancellation); }
                catch (AislingExchangeException) { return new(false, "AwaitingCompletionReconciliation"); }
                await store.ClearAsync(snapshot.Character, snapshot.ServerProfile, cancellation);
                return new(false, "ReproofRequired");
            }
            if (intent is not null && intent.ExpiresAt <= time.GetUtcNow())
            {
                await store.ClearAsync(snapshot.Character, snapshot.ServerProfile, cancellation);
                return new(false, "Expired");
            }

            if (intent is null)
            {
                using var created = AislingExchangePairingProof.Create(await store.GetInstallationIdAsync(cancellation));
                intent = new(snapshot.Character, snapshot.ServerProfile, snapshot.Generation, created.InstallationId,
                    created.ClientOperationId, created.Verifier, time.GetUtcNow().AddMinutes(5).AddSeconds(15), "Starting", null);
                await store.SaveAsync(intent, cancellation);
                AislingExchangePairingChallenge challenge;
                try { challenge = await client.StartPairingAsync(snapshot.Character, snapshot.ServerProfile, created, cancellation); }
                catch (AislingExchangeException) { return new(false, "AwaitingChallengeReconciliation"); }
                if (challenge.Character != snapshot.Character || challenge.ServerProfile != snapshot.ServerProfile || challenge.Receiver != "Exchanger")
                    throw new AislingExchangeException("AislingExchange pairing response is invalid.");
                intent = intent with { Challenge = challenge, ExpiresAt = challenge.ExpiresAt, State = "WaitingReady" };
                await store.SaveAsync(intent, cancellation);
            }

            // A lost start response cannot justify posting a second challenge. Wait
            // out its complete possible lifetime before a fresh attempt is created.
            if (intent.Challenge is null)
                return new(false, "AwaitingChallengeReconciliation");
            using var proof = AislingExchangePairingProof.Restore(intent.InstallationId, intent.ClientOperationId, intent.Verifier);
            AislingExchangePairingStatus status;
            try { status = await client.GetPairingStatusAsync(intent.Challenge, proof, cancellation); }
            catch (AislingExchangeException) { return new(false, "AwaitingStatus"); }
            if (status.State == "expired")
            {
                await store.ClearAsync(snapshot.Character, snapshot.ServerProfile, cancellation);
                return new(false, "Expired");
            }

            if (status.State == "observed")
            {
                intent = intent with { State = "CompletionIntent" };
                await store.SaveAsync(intent, cancellation);
                AislingExchangePairingCompletion completed;
                try { completed = await client.CompletePairingAsync(intent.Challenge, proof, cancellation); }
                catch (AislingExchangeException) { return new(false, "AwaitingCompletionReconciliation"); }
                if (completed.State == "paired")
                {
                    await store.ClearAsync(snapshot.Character, snapshot.ServerProfile, cancellation);
                    return new(true, "Paired");
                }
                if (completed.State != "pending")
                    throw new AislingExchangeException("AislingExchange pairing response is invalid.");
                await store.SaveAsync(intent with { State = "WaitingObservation" }, cancellation);
                return new(false, "WaitingObservation");
            }

            if (status.State != "ready")
                return new(false, "WaitingReady");
            if (intent.State is "WhisperIntent" or "WhisperSubmitted" or "WhisperUncertain" or "WhisperRejected" or "WaitingObservation")
                return new(false, "WaitingObservation");
            var beforeSend = session.Snapshot;
            if (beforeSend != snapshot || beforeSend.Generation != intent.Generation)
                return new(false, "SessionChanged");

            intent = intent with { State = "WhisperIntent" };
            await store.SaveAsync(intent, cancellation);
            AislingExchangeWhisperOutcome outcome;
            try
            {
                outcome = await session.SendPairingWhisperAsync("Exchanger", intent.Challenge.Nonce, intent.Generation, cancellation);
            }
            catch { outcome = AislingExchangeWhisperOutcome.Uncertain; }
            string state = outcome switch
            {
                AislingExchangeWhisperOutcome.SourceAccepted => "WhisperSubmitted",
                AislingExchangeWhisperOutcome.Rejected => "WhisperRejected",
                _ => "WhisperUncertain"
            };
            // The preceding WhisperIntent already protects interruption during this
            // write. It is never sent again after a missing or uncertain receipt.
            await store.SaveAsync(intent with { State = state }, CancellationToken.None);
            cancellation.ThrowIfCancellationRequested();
            return new(false, "WaitingObservation");
        }
        finally { gate.Release(); }
    }
}
