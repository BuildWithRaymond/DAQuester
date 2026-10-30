using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class AislingExchangePairingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class Store : IAislingExchangePairingStore, IAislingExchangeCredentialStore
    {
        public AislingExchangePairingIntent? Intent;
        public AislingExchangeCredential? Credential;
        public Task<Guid> GetInstallationIdAsync(CancellationToken cancellation = default) => Task.FromResult(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        public Task<AislingExchangePairingIntent?> LoadAsync(string character, string serverProfile, CancellationToken cancellation = default) => Task.FromResult(Intent);
        Task<AislingExchangeCredential?> IAislingExchangeCredentialStore.LoadAsync(string character, string serverProfile, CancellationToken cancellation) => Task.FromResult(Credential);
        public Task SaveAsync(AislingExchangePairingIntent intent, CancellationToken cancellation = default) { Intent = intent; return Task.CompletedTask; }
        public Task SaveAsync(AislingExchangeCredential credential, CancellationToken cancellation = default) { Credential = credential; return Task.CompletedTask; }
        public Task ClearAsync(string character, string serverProfile, CancellationToken cancellation = default) { Intent = null; return Task.CompletedTask; }
    }

    private sealed class Client(Store store) : IAislingExchangePairingClient
    {
        public string State = "registering";
        public int Starts;
        public int Completes;
        public int Revocations;
        public bool FailStart;
        public bool FailComplete;
        public Task<AislingExchangePairingChallenge> StartPairingAsync(string character, string serverProfile, AislingExchangePairingProof proof, CancellationToken cancellation = default)
        {
            Starts++;
            Assert.Equal("Starting", store.Intent!.State);
            Assert.Equal(proof.Verifier, store.Intent.Verifier);
            if (FailStart) throw new AislingExchangeException("Synthetic unavailable", true);
            return Task.FromResult(new AislingExchangePairingChallenge("pair-1", "AEQ-" + new string('n', 43), "Exchanger", character, serverProfile, Now.AddMinutes(5)));
        }
        public Task<AislingExchangePairingStatus> GetPairingStatusAsync(AislingExchangePairingChallenge challenge, AislingExchangePairingProof proof, CancellationToken cancellation = default)
            => Task.FromResult(new AislingExchangePairingStatus(challenge.Id, challenge.Character, challenge.ServerProfile, State, challenge.ExpiresAt));
        public async Task<AislingExchangePairingCompletion> CompletePairingAsync(AislingExchangePairingChallenge challenge, AislingExchangePairingProof proof, CancellationToken cancellation = default)
        {
            Completes++;
            Assert.Equal("CompletionIntent", store.Intent!.State);
            if (FailComplete) throw new AislingExchangeException("Synthetic uncertain", true);
            var credential = new AislingExchangeCredential(challenge.Character, challenge.ServerProfile, new string('t', 48), "quest-supplies", Now.AddHours(1));
            await store.SaveAsync(credential, cancellation);
            return new("paired", credential);
        }
        public Task RevokePairingAsync(AislingExchangePairingChallenge challenge, AislingExchangePairingProof proof, CancellationToken cancellation = default)
        {
            Revocations++;
            store.Credential = null;
            return Task.CompletedTask;
        }
    }

    private sealed class Session(Store store) : IAislingExchangePairingSession
    {
        public AislingExchangePairingSessionState Snapshot { get; set; } = new("Elara", "test-server", 7, true, true);
        public int Sends;
        public bool InterruptSend;
        public Task<AislingExchangeWhisperOutcome> SendPairingWhisperAsync(string receiver, string nonce, long expectedGeneration, CancellationToken cancellation)
        {
            Assert.Equal("WhisperIntent", store.Intent!.State);
            Assert.Equal("Exchanger", receiver);
            Assert.Equal(47, nonce.Length);
            Assert.Equal(Snapshot.Generation, expectedGeneration);
            Sends++;
            if (InterruptSend) throw new OperationCanceledException();
            return Task.FromResult(AislingExchangeWhisperOutcome.SourceAccepted);
        }
    }

    [Fact]
    public async Task Waits_for_worker_readiness_then_sends_once_and_requires_observed_proof()
    {
        var store = new Store();
        var client = new Client(store);
        var session = new Session(store);
        var coordinator = new AislingExchangePairingCoordinator(client, store, store, new Clock());
        Assert.False((await coordinator.EnsurePairedAsync(session)).Paired);
        Assert.Equal(0, session.Sends);
        client.State = "ready";
        Assert.False((await coordinator.EnsurePairedAsync(session)).Paired);
        Assert.Equal(1, session.Sends);
        Assert.False((await coordinator.EnsurePairedAsync(session)).Paired);
        Assert.Equal(1, session.Sends);
        client.State = "observed";
        Assert.True((await coordinator.EnsurePairedAsync(session)).Paired);
        Assert.Equal(1, client.Starts);
        Assert.Equal(1, client.Completes);
        Assert.Null(store.Intent);
    }

    [Fact]
    public async Task Interrupted_whisper_is_not_sent_again_after_coordinator_recreation()
    {
        var store = new Store();
        var client = new Client(store) { State = "ready" };
        var session = new Session(store) { InterruptSend = true };
        var coordinator = new AislingExchangePairingCoordinator(client, store, store, new Clock());
        Assert.False((await coordinator.EnsurePairedAsync(session)).Paired);
        session.InterruptSend = false;
        session.Snapshot = session.Snapshot with { Generation = 8 };
        var resumed = new AislingExchangePairingCoordinator(client, store, store, new Clock());
        Assert.False((await resumed.EnsurePairedAsync(session)).Paired);
        Assert.Equal(1, session.Sends);
        client.State = "observed";
        Assert.True((await resumed.EnsurePairedAsync(session)).Paired);
    }

    [Fact]
    public async Task Uncertain_pairing_mutations_are_not_automatically_repeated()
    {
        var store = new Store();
        var client = new Client(store) { FailStart = true };
        var session = new Session(store);
        var coordinator = new AislingExchangePairingCoordinator(client, store, store, new Clock());
        await coordinator.EnsurePairedAsync(session);
        await coordinator.EnsurePairedAsync(session);
        Assert.Equal(1, client.Starts);
        Assert.Equal(0, session.Sends);

        var completedStore = new Store();
        var completedClient = new Client(completedStore) { State = "observed", FailComplete = true };
        var completedSession = new Session(completedStore);
        var completing = new AislingExchangePairingCoordinator(completedClient, completedStore, completedStore, new Clock());
        await completing.EnsurePairedAsync(completedSession);
        await completing.EnsurePairedAsync(completedSession);
        Assert.Equal(1, completedClient.Completes);
        Assert.Equal(1, completedClient.Revocations);
        Assert.Null(completedStore.Intent);
    }

    [Fact]
    public async Task Disconnected_or_unowned_session_cannot_start_pairing()
    {
        var store = new Store();
        var client = new Client(store);
        var session = new Session(store) { Snapshot = new("Elara", "test-server", 7, true, false) };
        var coordinator = new AislingExchangePairingCoordinator(client, store, store, new Clock());
        Assert.False((await coordinator.EnsurePairedAsync(session)).Paired);
        Assert.Equal(0, client.Starts);
        Assert.Equal(0, session.Sends);
    }

    [Fact]
    public async Task Pairing_store_preserves_installation_and_protects_verifier()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        var store = new AislingExchangePairingStore(directory);
        Guid installation = await store.GetInstallationIdAsync();
        Assert.Equal(installation, await new AislingExchangePairingStore(directory).GetInstallationIdAsync());
        using var proof = AislingExchangePairingProof.Create(installation);
        var intent = new AislingExchangePairingIntent("Elara", "test-server", 7, installation, proof.ClientOperationId, proof.Verifier, Now.AddMinutes(5), "Starting", null);
        await store.SaveAsync(intent);
        Assert.Equal(proof.Verifier, (await store.LoadAsync("Elara", "test-server"))!.Verifier);
        Assert.DoesNotContain(proof.Verifier, intent.ToString());
        foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            Assert.DoesNotContain(proof.Verifier, System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)));
        await store.ClearAsync("Elara", "test-server");
        Assert.Null(await store.LoadAsync("Elara", "test-server"));
    }

    [Fact]
    public async Task Unknown_saved_state_cannot_authorize_a_second_whisper()
    {
        var store = new Store();
        var client = new Client(store);
        var session = new Session(store);
        var coordinator = new AislingExchangePairingCoordinator(client, store, store, new Clock());
        await coordinator.EnsurePairedAsync(session);
        store.Intent = store.Intent! with { State = "UnrecognizedState" };
        client.State = "ready";
        await Assert.ThrowsAsync<AislingExchangeException>(() => coordinator.EnsurePairedAsync(session));
        Assert.Equal(0, session.Sends);
    }

    [Fact]
    public async Task Independent_store_instances_share_one_installation_identity()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Guid> CreateAsync()
        {
            var store = new AislingExchangePairingStore(directory);
            await start.Task;
            return await store.GetInstallationIdAsync();
        }
        Task<Guid>[] attempts = Enumerable.Range(0, 16).Select(_ => CreateAsync()).ToArray();
        start.SetResult();
        Guid[] identities = await Task.WhenAll(attempts);
        Assert.All(identities, id => Assert.Equal(identities[0], id));
        Assert.Equal(identities[0], await new AislingExchangePairingStore(directory).GetInstallationIdAsync());
    }
}
