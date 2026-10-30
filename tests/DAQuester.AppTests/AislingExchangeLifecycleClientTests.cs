using System.Net;
using System.Text;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public sealed class AislingExchangeLifecycleClientTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid Parent = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Order = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly string Unit = "33333333-3333-3333-3333-333333333333";
    static AislingExchangeApiRoutes Routes => AislingExchangeApiRoutes.FromApiBase(new("https://supplies.aislingexchange.com/api/quest-supplies/v1/"));
    static AislingExchangeUnitReport Report() => new(Guid.NewGuid(), Parent, Order.ToString("D"), "Elara", "test-server", [Unit]);
    static AislingExchangeReturnSubmission Return() => new(Parent, Guid.NewGuid().ToString("D"), "Elara", "test-server", [], [new("Mold", 1, "journey_surplus")]);
    static AislingExchangeRequestStatus Status(string state = "sent", bool source = true, bool clientReported = true)
        => new(Order.ToString("D"), Parent, "Elara", "test-server", state, [new(Unit, "Mold", source, ClientReported: clientReported)], Sequence: 2);
    static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), Encoding.UTF8, "application/json")
    };
    static object Capabilities(bool ready = true, bool returns = true) => new
    {
        gameServer = "test-server", receiver = "Exchanger", ready, returnSupported = returns,
        receiveModes = new[] { "single-unit" }, items = new[] { new { item = "Mold", maxPerRequest = 3 } }
    };

    [Fact]
    public void ReturnLifecycleIsExposedByTheScopedProvider()
    {
        Assert.Contains(typeof(IQuestSupplyReturnProvider), typeof(AislingExchangeQuestSupplyProvider).GetInterfaces());
        Assert.NotNull(typeof(AislingExchangeClient).GetMethod("ReportReceiptsAsync"));
        Assert.NotNull(typeof(AislingExchangeClient).GetMethod("RegisterReturnAsync"));
        Assert.NotNull(typeof(AislingExchangeClient).GetMethod("FindReturnAsync"));
        Assert.NotNull(typeof(AislingExchangeClient).GetMethod("ReportReturnSentAsync"));
        Assert.NotNull(typeof(AislingExchangeClient).GetMethod("ReturnsAvailableAsync"));
    }

    [Fact]
    public async Task CollectionReportUsesExactBodyParentIdentityAndDurableKey()
    {
        var handler = new Handler(_ => Json(Status()));
        using var client = Client(handler);
        var report = Report();
        var result = await client.ReportReceiptsAsync(report);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("/api/quest-supplies/v1/requests/" + Order + "/receipts", sent.Path);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(report.ClientOperationId.ToString("D"), sent.Key);
        Assert.Equal("Bearer " + new string('s', 48), sent.Authorization);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal(new[] { "clientOperationId", "unitIds" }, body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(report.ClientOperationId, body.RootElement.GetProperty("clientOperationId").GetGuid());
        Assert.Equal(Unit, body.RootElement.GetProperty("unitIds")[0].GetString());
        Assert.Equal(Parent, result.ClientOperationId);
        Assert.False(Assert.Single(result.Units).RecipientObserved);
    }

    [Theory]
    [InlineData("parent")]
    [InlineData("operation")]
    [InlineData("character")]
    [InlineData("profile")]
    [InlineData("unreported")]
    [InlineData("absent-unit")]
    [InlineData("supply-banked")]
    [InlineData("cancelled-accepted")]
    [InlineData("supply-return-state")]
    public async Task CollectionReportRejectsUnrelatedOrContradictoryResponse(string change)
    {
        var status = change switch
        {
            "parent" => Status() with { ClientOperationId = Guid.NewGuid() },
            "operation" => Status() with { Id = Guid.NewGuid().ToString("D") },
            "character" => Status() with { Character = "Other" },
            "profile" => Status() with { ServerProfile = "other-server" },
            "unreported" => Status(clientReported: false),
            "absent-unit" => Status() with { Units = [] },
            "supply-banked" => Status() with { Units = [new(Unit, "Mold", true, Collected: true, Banked: true, ClientReported: true)] },
            "cancelled-accepted" => Status() with { Units = [new(Unit, "Mold", true, Cancelled: true, ClientReported: true)] },
            _ => Status("banking"),
        };
        var handler = new Handler(_ => Json(status));
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<AislingExchangeException>(() => client.ReportReceiptsAsync(Report()));
        Assert.True(error.OutcomeUncertain);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task LostReportAcknowledgementNeedsExplicitIdenticalRetryAndLeaksNoUpstreamDetails()
    {
        var handler = new Handler(index => index == 0 ? throw new HttpRequestException("private-host.invalid secret-token") : Json(Status()));
        using var client = Client(handler);
        var report = Report();
        var error = await Assert.ThrowsAsync<AislingExchangeException>(() => client.ReportReceiptsAsync(report));
        Assert.True(error.OutcomeUncertain);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("private-host", error.ToString());
        Assert.DoesNotContain("secret-token", error.ToString());
        Assert.Single(handler.Requests);
        await client.ReportReceiptsAsync(report);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests[0], handler.Requests[1]);
    }

    [Theory]
    [InlineData("unit-path")]
    [InlineData("operation-path")]
    [InlineData("duplicate")]
    [InlineData("empty")]
    [InlineData("too-many")]
    [InlineData("no-parent")]
    public async Task InvalidReportIsRejectedBeforeTransport(string change)
    {
        var original = Report();
        var report = change switch
        {
            "unit-path" => original with { UnitIds = ["../admin"] },
            "operation-path" => original with { OperationId = "../admin" },
            "duplicate" => original with { UnitIds = [Unit, Unit] },
            "empty" => original with { UnitIds = [] },
            "too-many" => original with { UnitIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid().ToString("D")).ToArray() },
            _ => original with { OperationClientId = Guid.Empty },
        };
        var handler = new Handler(_ => throw new InvalidOperationException());
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<AislingExchangeException>(() => client.ReportReceiptsAsync(report));
        Assert.False(error.OutcomeUncertain);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task ReturnAvailabilityUsesAuthenticatedBoundedCapabilities(bool ready, bool returns, bool expected)
    {
        var handler = new Handler(_ => Json(Capabilities(ready, returns)));
        using var client = Client(handler);
        Assert.Equal(expected, await client.ReturnsAvailableAsync("Elara", "test-server"));
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Null(sent.Body);
        Assert.Null(sent.Key);
        Assert.NotNull(sent.Authorization);
        Assert.Equal("/api/quest-supplies/v1/capabilities", sent.Path);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("recipient")]
    [InlineData("mode")]
    [InlineData("duplicate-items")]
    public async Task MalformedCapabilitiesCannotAdvertiseReturns(string condition)
    {
        var reply = new
        {
            gameServer = condition == "profile" ? "other-server" : "test-server",
            receiver = condition == "recipient" ? "Other" : "Exchanger", ready = true, returnSupported = true,
            receiveModes = new[] { condition == "mode" ? "arbitrary" : "single-unit" },
            items = Enumerable.Range(0, condition == "duplicate-items" ? 2 : 1).Select(_ => new { item = "Mold", maxPerRequest = 3 }).ToArray()
        };
        var handler = new Handler(_ => Json(reply));
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<AislingExchangeException>(() => client.ReturnsAvailableAsync("Elara", "test-server"));
        Assert.False(error.OutcomeUncertain);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("https://evil.invalid/api/quest-supplies/v1/returns")]
    [InlineData("https://aislingexchange.com/api/quest-supplies/v1/returns")]
    [InlineData("http://supplies.aislingexchange.com/api/quest-supplies/v1/returns")]
    public void NewRoutesRejectUnapprovedOrDifferentOriginsWithoutNetwork(string uri)
    {
        var handler = new Handler(_ => throw new InvalidOperationException());
        Assert.Throws<AislingExchangeException>(() => new AislingExchangeClient(Routes with { Returns = new(uri) }, new Credentials(), handler, new Clock()));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReturnRegistrationAndSentReportStaySeparateFromCollectionAndBanking()
    {
        var handler = new Handler(index => Json(index == 0 ? Status("submitted", source: false, clientReported: false) : Status("awaiting_player_send", source: false)));
        using var client = Client(handler);
        var submission = Return();
        var submitted = await client.RegisterReturnAsync(submission);
        Assert.Equal("submitted", submitted.State);
        var report = Report();
        var reported = await client.ReportReturnSentAsync(report);
        Assert.Equal("awaiting_player_send", reported.State);
        Assert.False(Assert.Single(reported.Units).Collected);
        Assert.False(Assert.Single(reported.Units).Banked);
        Assert.Equal("/api/quest-supplies/v1/returns", handler.Requests[0].Path);
        Assert.Equal(Parent.ToString("D"), handler.Requests[0].Key);
        Assert.Equal("/api/quest-supplies/v1/returns/" + Order + "/sent", handler.Requests[1].Path);
        Assert.Equal(report.ClientOperationId.ToString("D"), handler.Requests[1].Key);
        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.Equal("journey_surplus", body.RootElement.GetProperty("items")[0].GetProperty("origin").GetString());
        Assert.Equal(submission.JourneyId, body.RootElement.GetProperty("journeyId").GetString());
    }

    [Fact]
    public async Task LostReturnRegistrationResponseUsesExistingOperationLookupWithoutAnotherRegistration()
    {
        var handler = new Handler(index => index == 0 ? throw new HttpRequestException("Synthetic lost registration response")
            : Json(Status("submitted", source: false, clientReported: false)));
        using var client = Client(handler);
        Assert.True((await Assert.ThrowsAsync<AislingExchangeException>(() => client.RegisterReturnAsync(Return()))).OutcomeUncertain);
        Assert.Single(handler.Requests);
        Assert.Equal(Parent, (await client.FindReturnAsync(Parent, "Elara", "test-server"))!.ClientOperationId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/api/quest-supplies/v1/operations/find", handler.Requests[1].Path);
        Assert.Null(handler.Requests[1].Key);
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("quantity")]
    [InlineData("duplicate-items")]
    [InlineData("journey")]
    [InlineData("reference")]
    [InlineData("missing-reference")]
    public async Task InvalidReturnManifestCannotReachTransport(string condition)
    {
        var submission = condition switch
        {
            "origin" => Return() with { Items = [new("Mold", 1, "personal")] },
            "quantity" => Return() with { Items = [new("Mold", 101, "journey_surplus")] },
            "duplicate-items" => Return() with { Items = [new("Mold", 1, "journey_surplus"), new("Mold", 1, "journey_surplus")] },
            "journey" => Return() with { JourneyId = "../other" },
            "reference" => Return() with { SupplyRequestIds = ["../other"] },
            _ => Return() with { Items = [new("Mold", 1, "exchanger_supply")] }
        };
        var handler = new Handler(_ => throw new InvalidOperationException());
        using var client = Client(handler);
        Assert.False((await Assert.ThrowsAsync<AislingExchangeException>(() => client.RegisterReturnAsync(submission))).OutcomeUncertain);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReturnLookupAcceptsProvenBankingAndProviderPreservesEveryReceiptFlag()
    {
        var status = Status("complete", source: false) with { Units = [new(Unit, "Mold", false, Collected: true, Banked: true, ClientReported: true)] };
        var handler = new Handler(_ => Json(status));
        using var client = Client(handler);
        var provider = new AislingExchangeQuestSupplyProvider(client, "Elara", "test-server", () => false);
        var found = await provider.FindReturnAsync(Parent, default);
        Assert.NotNull(found);
        var unit = Assert.Single(found.Units);
        Assert.True(unit.Collected);
        Assert.True(unit.Banked);
        Assert.True(unit.ClientReported);
        Assert.False(unit.RecipientObserved);
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("received")]
    [InlineData("submitted")]
    [InlineData("bad-bank")]
    [InlineData("unreported")]
    public async Task ReturnReportRejectsSupplyStatesAndContradictoryReceiptFlags(string condition)
    {
        var status = condition switch
        {
            "bad-bank" => Status("banking", source: false) with { Units = [new(Unit, "Mold", false, Banked: true, ClientReported: true)] },
            "unreported" => Status("awaiting_player_send", source: false, clientReported: false),
            _ => Status(condition),
        };
        var handler = new Handler(_ => Json(status));
        using var client = Client(handler);
        Assert.True((await Assert.ThrowsAsync<AislingExchangeException>(() => client.ReportReturnSentAsync(Report()))).OutcomeUncertain);
    }

    [Fact]
    public async Task OptOutBlocksNewReturnRegistrationButPreservesBothReportRecoveries()
    {
        var handler = new Handler(index => Json(index == 0 ? Status() : Status("awaiting_player_send", source: false)));
        using var client = Client(handler);
        int pairings = 0;
        var provider = new AislingExchangeQuestSupplyProvider(client, "Elara", "test-server", () => false, _ => { pairings++; return Task.CompletedTask; });
        Assert.False(await provider.ReturnsAvailableAsync(default));
        await Assert.ThrowsAsync<QuestBlockedException>(() => provider.RegisterReturnAsync(new(Parent, Guid.NewGuid(), "Elara", "test-server", [], [new("Mold", 1, "journey_surplus")]), default));
        Assert.Empty(handler.Requests);
        var report = new QuestSupplyUnitReport(Guid.NewGuid(), Parent, Order.ToString("D"), "Elara", "test-server", [Unit]);
        Assert.True(Assert.Single((await provider.ReportReceiptsAsync(report, default)).Units).ClientReported);
        Assert.True(Assert.Single((await provider.ReportReturnSentAsync(report with { ClientOperationId = Guid.NewGuid() }, default)).Units).ClientReported);
        Assert.Equal(2, pairings);
    }

    [Fact]
    public async Task EnabledProviderChecksActualReturnReadinessAndPreservesTrustedRecipientFlag()
    {
        var received = Status("received") with { Units = [new(Unit, "Mold", true, RecipientObserved: true, ClientReported: true)] };
        var handler = new Handler(index => index == 0 ? Json(Capabilities(returns: false)) : Json(received));
        using var client = Client(handler);
        int pairings = 0;
        var provider = new AislingExchangeQuestSupplyProvider(client, "Elara", "test-server", () => true, _ => { pairings++; return Task.CompletedTask; });
        Assert.False(await provider.ReturnsAvailableAsync(default));
        Assert.True(Assert.Single((await provider.FindAsync(Parent, default))!.Units).RecipientObserved);
        Assert.Equal(2, pairings);
    }

    [Fact]
    public async Task UnknownReturnLookupIsAuthoritativeAbsenceAndHasNoRetry()
    {
        var handler = new Handler(_ => new(HttpStatusCode.NotFound));
        using var client = Client(handler);
        Assert.Null(await client.FindReturnAsync(Parent, "Elara", "test-server"));
        Assert.Single(handler.Requests);
    }

    static AislingExchangeClient Client(Handler handler) => new(Routes, new Credentials(), handler, new Clock());
    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    sealed class Credentials : IAislingExchangeCredentialStore
    {
        public Task<AislingExchangeCredential?> LoadAsync(string character, string serverProfile, CancellationToken cancellation = default)
            => Task.FromResult<AislingExchangeCredential?>(new(character, serverProfile, new string('s', 48), "quest-supplies", Now.AddMinutes(30)));
        public Task SaveAsync(AislingExchangeCredential credential, CancellationToken cancellation = default) => Task.CompletedTask;
        public Task ClearAsync(string character, string serverProfile, CancellationToken cancellation = default) => Task.CompletedTask;
    }
    sealed record ObservedRequest(string Path, HttpMethod Method, string? Body, string? Key, string? Authorization);
    sealed class Handler(Func<int, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public List<ObservedRequest> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.RequestUri!.AbsolutePath, request.Method,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null, request.Headers.Authorization?.ToString()));
            return reply(Requests.Count - 1);
        }
    }
}
