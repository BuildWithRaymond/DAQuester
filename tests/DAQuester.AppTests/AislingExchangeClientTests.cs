using System.Net;
using System.Text;
using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class AislingExchangeClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly AislingExchangeApiRoutes Routes = new(
        new("https://aislingexchange.com/api/test/pairing/start"),
        new("https://aislingexchange.com/api/test/pairing/complete"),
        new("https://aislingexchange.com/api/test/quote"),
        new("https://aislingexchange.com/api/test/submit"),
        new("https://aislingexchange.com/api/test/find"));

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class DeadlineClock : TimeProvider
    {
        private TimerCallback? callback;
        private object? state;
        public override DateTimeOffset GetUtcNow() => Now;
        public override ITimer CreateTimer(TimerCallback timerCallback, object? timerState, TimeSpan dueTime, TimeSpan period)
        {
            callback = timerCallback;
            state = timerState;
            return new Timer();
        }
        public void FireDeadline() => callback?.Invoke(state);
        private sealed class Timer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class WaitingStream : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class Credentials : IAislingExchangeCredentialStore
    {
        public AislingExchangeCredential? Value { get; set; } = Credential();
        public Task<AislingExchangeCredential?> LoadAsync(string character, string serverProfile, CancellationToken cancellation = default)
            => Task.FromResult(Value);
        public Task SaveAsync(AislingExchangeCredential credential, CancellationToken cancellation = default)
        {
            Value = credential;
            return Task.CompletedTask;
        }
        public Task ClearAsync(string character, string serverProfile, CancellationToken cancellation = default)
        {
            Value = null;
            return Task.CompletedTask;
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }
        public string? IdempotencyKey { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Authorization = request.Headers.Authorization?.ToString();
            IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return reply(request);
        }
    }

    private static AislingExchangeCredential Credential() => new("Elara", "test-server", new string('s', 48), "quest-supplies", Now.AddHours(1));
    private static AislingExchangeQuoteRequest Demand() => new("Elara", "test-server", "fisk-secret", [new("Apple", 2)]);
    private static AislingExchangeSubmission Submission() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "fisk-secret", "Elara", "test-server", "quote-1", [new("Apple", 2)]);
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), Encoding.UTF8, "application/json")
    };

    [Theory]
    [InlineData("http://aislingexchange.com/api/test/quote")]
    [InlineData("https://aislingexchange.com.evil.example/api/test/quote")]
    [InlineData("https://supplies.aislingexchange.com.evil.example/api/test/quote")]
    [InlineData("https://other.example/api/test/quote")]
    [InlineData("https://aislingexchange.com:444/api/test/quote")]
    [InlineData("https://user:password@aislingexchange.com/api/test/quote")]
    [InlineData("https://aislingexchange.com/admin")]
    [InlineData("https://aislingexchange.com/api/test/quote?secret=x")]
    public void Rejects_unapproved_endpoint_before_network(string endpoint)
    {
        var handler = new Handler(_ => throw new InvalidOperationException());
        Assert.Throws<AislingExchangeException>(() => new AislingExchangeClient(Routes with { Quote = new(endpoint) }, new Credentials(), handler, new Clock()));
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task Quote_uses_scoped_credential_and_exact_requested_items()
    {
        var handler = new Handler(_ => Json(new AislingExchangeQuote("quote-1", "Elara", "test-server", Now.AddMinutes(2), [new("Apple", 2)], "fisk-secret")));
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        var quote = await client.QuoteAsync(Demand());
        Assert.Equal("quote-1", quote.Id);
        Assert.Equal("Bearer " + Credential().AccessToken, handler.Authorization);
        Assert.Equal(1, handler.Requests);
        Assert.Contains("\"quantity\":2", handler.Body);
    }

    [Fact]
    public async Task Expired_or_wrong_character_credential_sends_nothing()
    {
        var credentials = new Credentials { Value = new("Other", "test-server", new string('s', 48), "quest-supplies", Now.AddHours(1)) };
        var handler = new Handler(_ => throw new InvalidOperationException());
        using var client = new AislingExchangeClient(Routes, credentials, handler, new Clock());
        await Assert.ThrowsAsync<AislingExchangeException>(() => client.QuoteAsync(Demand()));
        credentials.Value = new("Elara", "test-server", new string('s', 48), "quest-supplies", Now.AddSeconds(-1));
        await Assert.ThrowsAsync<AislingExchangeException>(() => client.QuoteAsync(Demand()));
        Assert.Equal(0, handler.Requests);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(307)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Mutation_failure_is_uncertain_without_retry_or_upstream_details(int code)
    {
        var handler = new Handler(_ => new((HttpStatusCode)code)
        {
            Content = new StringContent("private-host.example token-secret"),
            Headers = { Location = new("https://other.example/private") }
        });
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        var failure = await Assert.ThrowsAsync<AislingExchangeException>(() => client.SubmitAsync(Submission()));
        Assert.True(failure.OutcomeUncertain);
        Assert.Equal(1, handler.Requests);
        Assert.DoesNotContain("private-host", failure.ToString());
        Assert.DoesNotContain("token-secret", failure.ToString());
        Assert.DoesNotContain("other.example", failure.ToString());
    }

    [Fact]
    public async Task Transport_failure_exposes_no_raw_exception_and_does_not_retry()
    {
        var handler = new Handler(_ => throw new HttpRequestException("private-host.example token-secret"));
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        var failure = await Assert.ThrowsAsync<AislingExchangeException>(() => client.SubmitAsync(Submission()));
        Assert.True(failure.OutcomeUncertain);
        Assert.Null(failure.InnerException);
        Assert.DoesNotContain("private-host", failure.ToString());
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task Rejects_oversized_or_unknown_schema_response()
    {
        var handler = new Handler(_ => Json(new { padding = new string('x', 70_000) }));
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        await Assert.ThrowsAsync<AislingExchangeException>(() => client.QuoteAsync(Demand()));
        var unknown = new Handler(_ => Json(new { id = "q", character = "Elara", gameServer = "test-server", journeyId = "fisk-secret", expiresAt = Now.AddMinutes(1), items = new[] { new { item = "Apple", quantity = 2 } }, administrator = true }));
        using var other = new AislingExchangeClient(Routes, new Credentials(), unknown, new Clock());
        await Assert.ThrowsAsync<AislingExchangeException>(() => other.QuoteAsync(Demand()));
    }

    [Fact]
    public async Task Rejects_quote_with_substituted_items_or_character()
    {
        var handler = new Handler(_ => Json(new AislingExchangeQuote("quote-1", "Other", "test-server", Now.AddMinutes(2), [new("Apple", 2)], "fisk-secret")));
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        await Assert.ThrowsAsync<AislingExchangeException>(() => client.QuoteAsync(Demand()));
        var substituted = new Handler(_ => Json(new AislingExchangeQuote("quote-1", "Elara", "test-server", Now.AddMinutes(2), [new("Sword", 2)], "fisk-secret")));
        using var other = new AislingExchangeClient(Routes, new Credentials(), substituted, new Clock());
        await Assert.ThrowsAsync<AislingExchangeException>(() => other.QuoteAsync(Demand()));
    }

    [Fact]
    public async Task Pairing_requires_pinned_receiver_and_short_expiry()
    {
        var handler = new Handler(_ => Json(new AislingExchangePairingChallenge("pair-1", "AEQ-" + new string('n', 43), "WrongReceiver", "Elara", "test-server", Now.AddMinutes(2))));
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        using var proof = AislingExchangePairingProof.Create();
        await Assert.ThrowsAsync<AislingExchangeException>(() => client.StartPairingAsync("Elara", "test-server", proof));
        var late = new Handler(_ => Json(new AislingExchangePairingChallenge("pair-1", "AEQ-" + new string('n', 43), "Exchanger", "Elara", "test-server", Now.AddMinutes(6))));
        using var other = new AislingExchangeClient(Routes, new Credentials(), late, new Clock());
        await Assert.ThrowsAsync<AislingExchangeException>(() => other.StartPairingAsync("Elara", "test-server", proof));
        Assert.Null(handler.Authorization);
        Assert.Contains(proof.VerifierHash, handler.Body);
        Assert.DoesNotContain(proof.Verifier, handler.Body);
    }

    [Fact]
    public void Pairing_proof_is_256_bit_unique_and_secret_values_do_not_print()
    {
        using var first = AislingExchangePairingProof.Create();
        using var second = AislingExchangePairingProof.Create();
        Assert.Equal(43, first.Verifier.Length);
        Assert.Equal(43, first.VerifierHash.Length);
        Assert.NotEqual(first.Verifier, second.Verifier);
        Assert.DoesNotContain(first.Verifier, first.ToString());
        Assert.DoesNotContain(Credential().AccessToken, Credential().ToString());
        var challenge = new AislingExchangePairingChallenge("pair-1", "secret-nonce", "Exchanger", "Elara", "test-server", Now.AddMinutes(1));
        Assert.DoesNotContain(challenge.Nonce, challenge.ToString());
    }

    [Fact]
    public async Task Status_must_match_operation_and_bound_character()
    {
        var request = Submission();
        var handler = new Handler(_ => Json(new AislingExchangeRequestStatus("order-1", Guid.NewGuid(), "Elara", "test-server", "queued", [])));
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        await Assert.ThrowsAsync<AislingExchangeException>(() => client.FindAsync(request.ClientOperationId, "Elara", "test-server"));
    }

    [Fact]
    public async Task Pairing_status_read_waits_for_worker_and_completion_can_remain_pending()
    {
        var challenge = new AislingExchangePairingChallenge("pair-1", "AEQ-" + new string('n', 43), "Exchanger", "Elara", "test-server", Now.AddMinutes(2));
        var handler = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/status", StringComparison.Ordinal)
            ? Json(new AislingExchangePairingStatus("pair-1", "Elara", "test-server", "registering", challenge.ExpiresAt))
            : Json(new AislingExchangePairingCompletion("pending", null)));
        var credentials = new Credentials { Value = null };
        using var client = new AislingExchangeClient(Routes with { PairingStatus = Routes.PairingStart }, credentials, handler, new Clock());
        using var proof = AislingExchangePairingProof.Create();
        var status = await client.GetPairingStatusAsync(challenge, proof);
        Assert.Equal("registering", status.State);
        var completion = await client.CompletePairingAsync(challenge, proof);
        Assert.Equal("pending", completion.State);
        Assert.Null(credentials.Value);
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task Completed_pairing_saves_only_matching_scoped_credential()
    {
        var challenge = new AislingExchangePairingChallenge("pair-1", "AEQ-" + new string('n', 43), "Exchanger", "Elara", "test-server", Now.AddMinutes(2));
        var credentials = new Credentials { Value = null };
        var handler = new Handler(_ => Json(new AislingExchangePairingCompletion("paired", Credential())));
        using var client = new AislingExchangeClient(Routes, credentials, handler, new Clock());
        using var proof = AislingExchangePairingProof.Create();
        await client.CompletePairingAsync(challenge, proof);
        Assert.Equal(Credential().AccessToken, credentials.Value!.AccessToken);
        Assert.Contains(proof.Verifier, handler.Body);
        Assert.Null(handler.Authorization);
    }

    [Fact]
    public async Task Credential_store_keeps_protected_data_separate_and_rejects_tampering()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        var store = new AislingExchangeCredentialStore(directory, new Clock());
        Assert.Null(await store.LoadAsync("Elara", "test-server"));
        await store.SaveAsync(Credential());
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        string file = Assert.Single(files);
        Assert.Contains("aisling-exchange", file);
        Assert.DoesNotContain(Credential().AccessToken, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)));
        Assert.Equal(Credential().AccessToken, (await store.LoadAsync("Elara", "test-server"))!.AccessToken);
        Assert.Null(await store.LoadAsync("Other", "test-server"));
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        var error = await Assert.ThrowsAsync<AislingExchangeException>(() => store.LoadAsync("Elara", "test-server"));
        Assert.DoesNotContain(directory, error.ToString());
        await store.ClearAsync("Elara", "test-server");
        Assert.Null(await store.LoadAsync("Elara", "test-server"));
    }

    [Fact]
    public async Task Request_deadline_also_bounds_body_reads_after_successful_headers()
    {
        var clock = new DeadlineClock();
        var stream = new WaitingStream();
        var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            response.Content.Headers.ContentType = new("application/json");
            return response;
        });
        using var cancellation = new CancellationTokenSource();
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, clock);
        var submission = client.SubmitAsync(Submission(), cancellation.Token);
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.FireDeadline();
        try
        {
            var error = await Assert.ThrowsAsync<AislingExchangeException>(() => submission.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.True(error.OutcomeUncertain);
            Assert.Equal(1, handler.Requests);
        }
        finally { cancellation.Cancel(); }
    }

    [Fact]
    public async Task Shipment_uses_the_durable_client_operation_as_its_idempotency_key()
    {
        var submission = Submission();
        var handler = new Handler(_ => Json(new AislingExchangeRequestStatus("order-1", submission.ClientOperationId, "Elara", "test-server", "submitted", [])));
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        await client.SubmitAsync(submission);
        Assert.Equal(submission.ClientOperationId.ToString("D"), handler.IdempotencyKey);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task Quote_can_offer_a_smaller_quantity_without_substituting_unrequested_items()
    {
        var handler = new Handler(_ => Json(new AislingExchangeQuote("quote-1", "Elara", "test-server", Now.AddMinutes(2), [new("Apple", 1)], "fisk-secret")));
        using var client = new AislingExchangeClient(Routes, new Credentials(), handler, new Clock());
        var quote = await client.QuoteAsync(Demand());
        Assert.Equal(1, Assert.Single(quote.Items).Quantity);
    }

    [Fact]
    public void Pairing_verifier_uses_the_standard_S256_text_commitment()
    {
        using var proof = AislingExchangePairingProof.Restore(Guid.NewGuid(), Guid.NewGuid(), "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", proof.VerifierHash);
    }

    [Fact]
    public void Dedicated_public_supply_origin_is_allowed_without_network_on_construction()
    {
        var handler = new Handler(_ => throw new InvalidOperationException());
        using var client = new AislingExchangeClient(AislingExchangeApiRoutes.FromApiBase(new("https://supplies.aislingexchange.com/api/quest-supplies/v1/")), new Credentials(), handler);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task Default_server_profile_is_valid_for_both_protected_stores()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        var credentials = new AislingExchangeCredentialStore(directory, new Clock());
        var credential = Credential() with { ServerProfile = "datester-7.41" };
        await credentials.SaveAsync(credential);
        Assert.Equal(credential.AccessToken, (await credentials.LoadAsync("Elara", "datester-7.41"))!.AccessToken);
        var pairings = new AislingExchangePairingStore(directory);
        using var proof = AislingExchangePairingProof.Create();
        await pairings.SaveAsync(new("Elara", "datester-7.41", 7, proof.InstallationId, proof.ClientOperationId, proof.Verifier, Now.AddMinutes(5), "Starting", null));
        Assert.NotNull(await pairings.LoadAsync("Elara", "datester-7.41"));
    }

    [Fact]
    public async Task Lost_pairing_issuance_can_be_revoked_after_challenge_expiry()
    {
        var handler = new Handler(_ => Json(new { state = "revoked" }));
        using var client = new AislingExchangeClient(Routes with { PairingRevoke = Routes.PairingStart }, new Credentials(), handler, new Clock());
        using var proof = AislingExchangePairingProof.Create();
        var challenge = new AislingExchangePairingChallenge("pair-1", "AEQ-" + new string('n', 43), "Exchanger", "Elara", "test-server", Now.AddSeconds(-1));
        await client.RevokePairingAsync(challenge, proof);
        Assert.Equal(1, handler.Requests);
        Assert.Contains(proof.Verifier, handler.Body);
        Assert.Null(handler.Authorization);
    }
}
