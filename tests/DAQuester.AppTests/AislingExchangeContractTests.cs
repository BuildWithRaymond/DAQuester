using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AislingExchange.Broker;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class AislingExchangeContractTests
{
    private const string Profile = "datester-7.41";
    private const string WorkerToken = "synthetic-worker-only-token-never-used-over-a-network";
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class EngineHandler(BrokerEngine broker) : HttpMessageHandler
    {
        public bool LoseNextShipmentResponse;
        public bool LoseNextCompletionResponse;
        public string? IssuedToken;
        public int Shipments;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string? key = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
            var reply = broker.Execute(request.Method.Method, request.RequestUri!.AbsolutePath, body.RootElement,
                request.Headers.Authorization?.Parameter, key);
            if (request.RequestUri.AbsolutePath.EndsWith("/complete", StringComparison.Ordinal) && reply.Status == 200
                && reply.Json.TryGetProperty("credential", out var issued) && issued.ValueKind == JsonValueKind.Object)
                IssuedToken = issued.GetProperty("accessToken").GetString();
            if (LoseNextCompletionResponse && request.RequestUri.AbsolutePath.EndsWith("/complete", StringComparison.Ordinal) && reply.Status == 200)
            {
                LoseNextCompletionResponse = false;
                throw new HttpRequestException("Synthetic lost credential issuance receipt.");
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/requests", StringComparison.Ordinal))
            {
                Shipments++;
                if (LoseNextShipmentResponse && reply.Status == 202)
                {
                    LoseNextShipmentResponse = false;
                    throw new HttpRequestException("Synthetic lost receipt after the broker committed.");
                }
            }
            return new((HttpStatusCode)reply.Status)
            {
                Content = new StringContent(reply.Json.GetRawText(), Encoding.UTF8, "application/json")
            };
        }
    }

    private static JsonElement Body(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    private static BrokerReply Worker(BrokerEngine broker, string path, object value) => broker.Execute("POST", path, Body(value), WorkerToken);

    [Fact]
    public async Task Actual_broker_pairs_quotes_and_finds_committed_shipment_after_lost_response()
    {
        var time = new Clock();
        string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        using var broker = new BrokerEngine(Path.Combine(directory, "broker"), new BrokerOptions
        {
            GameServer = Profile,
            WorkerTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WorkerToken))),
            CatalogLimits = new() { ["Mold"] = 3 }
        }, time);
        var credentials = new AislingExchangeCredentialStore(Path.Combine(directory, "desktop"), time);
        var handler = new EngineHandler(broker);
        using var client = new AislingExchangeClient(AislingExchangeApiRoutes.FromApiBase(new("https://aislingexchange.com/api/quest-supplies/v1/")), credentials, handler, time);
        using var proof = AislingExchangePairingProof.Create();
        var challenge = await client.StartPairingAsync("Elara", Profile, proof);
        Assert.Equal("registering", (await client.GetPairingStatusAsync(challenge, proof)).State);

        var claim = Worker(broker, "/worker/v1/jobs/claim", new { });
        Assert.Equal(200, claim.Status);
        var job = claim.Json.GetProperty("job");
        Assert.Equal(challenge.Nonce, job.GetProperty("payload").GetProperty("nonce").GetString());
        var ready = Worker(broker, "/worker/v1/jobs/" + job.GetProperty("id").GetString() + "/result",
            new { leaseToken = job.GetProperty("leaseToken").GetString(), fence = job.GetProperty("fence").GetInt64(), state = "ready" });
        Assert.Equal(200, ready.Status);
        Assert.Equal("ready", (await client.GetPairingStatusAsync(challenge, proof)).State);
        var observed = Worker(broker, "/worker/v1/pairings/" + challenge.Id + "/proof",
            new { nonce = challenge.Nonce, character = "Elara", gameServer = Profile, observedAt = time.GetUtcNow() });
        Assert.Equal(200, observed.Status);
        Assert.Equal("observed", (await client.GetPairingStatusAsync(challenge, proof)).State);
        Assert.Equal("paired", (await client.CompletePairingAsync(challenge, proof)).State);
        Assert.NotNull(await credentials.LoadAsync("Elara", Profile));

        Assert.Equal(200, Worker(broker, "/worker/v1/catalog", new
        {
            gameServer = Profile, ready = true, returnSupported = true, receiveModes = new[] { "single-unit" },
            items = new[] { new { item = "Mold", available = 3, maxPerRequest = 3 } }
        }).Status);
        Guid journey = Guid.NewGuid();
        var quote = await client.QuoteAsync(new("Elara", Profile, journey.ToString("D"), [new("Mold", 2)]));
        Guid operation = Guid.NewGuid();
        handler.LoseNextShipmentResponse = true;
        var error = await Assert.ThrowsAsync<AislingExchangeException>(() => client.SubmitAsync(new(operation, journey.ToString("D"), "Elara", Profile, quote.Id, quote.Items)));
        Assert.True(error.OutcomeUncertain);
        var reconciled = await client.FindAsync(operation, "Elara", Profile);
        Assert.NotNull(reconciled);
        Assert.Equal(operation, reconciled.ClientOperationId);
        Assert.Equal("submitted", reconciled.State);
        Assert.Equal(2, reconciled.Units.Count);
        Assert.All(reconciled.Units, unit => Assert.False(unit.SourceAccepted));
        Assert.Equal(1, handler.Shipments);
    }

    [Fact]
    public async Task Actual_broker_revokes_lost_credential_issuance_before_fresh_proof()
    {
        var time = new Clock();
        string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        using var broker = new BrokerEngine(Path.Combine(directory, "broker"), new BrokerOptions
        {
            GameServer = Profile,
            WorkerTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WorkerToken))),
            CatalogLimits = new() { ["Mold"] = 3 }
        }, time);
        var credentials = new AislingExchangeCredentialStore(Path.Combine(directory, "desktop"), time);
        var handler = new EngineHandler(broker);
        using var client = new AislingExchangeClient(AislingExchangeApiRoutes.FromApiBase(new("https://supplies.aislingexchange.com/api/quest-supplies/v1/")), credentials, handler, time);
        using var proof = AislingExchangePairingProof.Create();
        var challenge = await client.StartPairingAsync("Elara", Profile, proof);
        var job = Worker(broker, "/worker/v1/jobs/claim", new { }).Json.GetProperty("job");
        Assert.Equal(200, Worker(broker, "/worker/v1/jobs/" + job.GetProperty("id").GetString() + "/result",
            new { leaseToken = job.GetProperty("leaseToken").GetString(), fence = job.GetProperty("fence").GetInt64(), state = "ready" }).Status);
        Assert.Equal(200, Worker(broker, "/worker/v1/pairings/" + challenge.Id + "/proof",
            new { nonce = challenge.Nonce, character = "Elara", gameServer = Profile, observedAt = time.GetUtcNow() }).Status);
        handler.LoseNextCompletionResponse = true;
        var failure = await Assert.ThrowsAsync<AislingExchangeException>(() => client.CompletePairingAsync(challenge, proof));
        Assert.True(failure.OutcomeUncertain);
        Assert.Null(await credentials.LoadAsync("Elara", Profile));
        Assert.Equal(200, broker.Execute("GET", "/api/quest-supplies/v1/capabilities", bearer: handler.IssuedToken).Status);
        await client.RevokePairingAsync(challenge, proof);
        await client.RevokePairingAsync(challenge, proof);
        Assert.Equal(401, broker.Execute("GET", "/api/quest-supplies/v1/capabilities", bearer: handler.IssuedToken).Status);
        Assert.Null(await credentials.LoadAsync("Elara", Profile));
    }
}
