using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AislingExchange.Broker;
using Xunit;

namespace AislingExchange.Broker.Tests;

public sealed class BrokerSecurityTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ae-broker-" + Guid.NewGuid().ToString("N"));
    private readonly string workerToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly ManualTime time = new();
    private BrokerEngine engine;
    private BrokerOptions Options => new()
    {
        GameServer = "synthetic-profile",
        WorkerTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workerToken))),
        CatalogLimits = new() { ["Mold"] = 3, ["Viper's Gland"] = 2 },
    };

    public BrokerSecurityTests() => engine = new BrokerEngine(directory, Options, time);

    [Fact]
    public void UnverifiedPairingCannotIssueCredential()
    {
        var pair = StartPairing();
        var reply = Call("POST", $"/pairings/{pair.Id}/complete", new { verifier = pair.Verifier });
        Assert.Equal(200, reply.Status);
        Assert.Equal("pending", reply.Json.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, reply.Json.GetProperty("credential").ValueKind);
    }

    [Fact]
    public void PublicCredentialCannotCallWorkerProofEndpoint()
    {
        var pair = StartPairing();
        var reply = Worker("POST", $"/pairings/{pair.Id}/proof", new
        {
            nonce = pair.Nonce, character = "Alice", gameServer = "synthetic-profile", observedAt = time.GetUtcNow(),
        }, "public-client-value");
        Assert.Equal(401, reply.Status);
        Assert.Equal("UNAUTHORIZED", Code(reply));
    }

    [Fact]
    public void PairingRejectsWrongSenderAndWrongVerifier()
    {
        var pair = StartPairing();
        ReadyPairing(pair);
        Assert.Equal(409, Proof(pair, "Mallory").Status);
        Assert.Equal(200, Proof(pair).Status);
        var workerStatus = Worker("POST", $"/pairings/{pair.Id}/status", new { });
        Assert.Equal(200, workerStatus.Status);
        Assert.Equal("observed", workerStatus.Json.GetProperty("state").GetString());
        Assert.Equal(401, Call("POST", $"/pairings/{pair.Id}/complete", new { verifier = Secret() }).Status);
        Assert.Equal(200, Complete(pair).Status);
    }

    [Fact]
    public void LostCredentialResponseRevokesIssuanceAndRequiresNewProof()
    {
        var pair = StartPairing();
        ReadyPairing(pair);
        Proof(pair);
        var token = Complete(pair).Json.GetProperty("credential").GetProperty("accessToken").GetString()!;
        var retry = Complete(pair);
        Assert.Equal(409, retry.Status);
        Assert.Equal("PAIRING_REPROOF_REQUIRED", Code(retry));
        Assert.Equal(401, Call("GET", "/capabilities", bearer: token).Status);
    }

    [Fact]
    public void CredentialCannotOutliveIndependentlyObservedProofLifetime()
    {
        var pair = StartPairing();
        ReadyPairing(pair);
        var proofAt = time.GetUtcNow();
        Proof(pair);
        time.Advance(TimeSpan.FromMinutes(4));
        var expires = Complete(pair).Json.GetProperty("credential").GetProperty("expiresAt").GetDateTimeOffset();
        Assert.Equal(proofAt.AddHours(1), expires);
    }

    [Fact]
    public void LostIssuanceCanBeRevokedWithVerifierWithoutResendingGameProof()
    {
        var alice = Pair();
        var revoked = Call("POST", $"/pairings/{alice.Pair.Id}/revoke", new { verifier = alice.Pair.Verifier });
        Assert.Equal(200, revoked.Status);
        Assert.Equal("revoked", revoked.Json.GetProperty("state").GetString());
        Assert.Equal(401, Call("GET", "/capabilities", bearer: alice.Token).Status);
        Assert.Equal(200, Call("POST", $"/pairings/{alice.Pair.Id}/revoke", new { verifier = alice.Pair.Verifier }).Status);
    }

    [Fact]
    public void ExpiredAndReplayedWhisperProofNeverIssuesAnotherToken()
    {
        var expired = StartPairing();
        ReadyPairing(expired);
        time.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(409, Proof(expired).Status);
        var fresh = StartPairing();
        ReadyPairing(fresh);
        Assert.Equal(200, Proof(fresh).Status);
        Assert.Equal(409, Proof(fresh).Status);
    }

    [Fact]
    public void StrictSchemaRejectsArbitraryWorkerCommandFields()
    {
        var reply = Call("POST", "/pairings", new
        {
            installationId = Guid.NewGuid(), clientOperationId = Guid.NewGuid(),
            character = "Alice", gameServer = "synthetic-profile", verifierChallenge = Challenge(Secret()),
            purpose = "quest-supplies", command = "arbitrary",
        });
        Assert.Equal(400, reply.Status);
        Assert.Equal("INVALID_REQUEST", Code(reply));
    }

    [Fact]
    public void SubmissionIsDurableAndDoesNotClaimReservation()
    {
        var identity = Pair();
        PublishStock();
        var quote = Quote(identity.Token);
        var operationId = Guid.NewGuid();
        var payload = SupplyPayload(quote, operationId);
        var key = Guid.NewGuid().ToString();
        var first = Call("POST", "/requests", payload, identity.Token, key);
        Assert.Equal(202, first.Status);
        Assert.Equal("submitted", first.Json.GetProperty("state").GetString());
        Assert.All(first.Json.GetProperty("units").EnumerateArray(), unit => Assert.False(unit.GetProperty("sourceAccepted").GetBoolean()));
        engine.Dispose();
        engine = new BrokerEngine(directory, Options, time);
        var replay = Call("POST", "/requests", payload, identity.Token, key);
        Assert.Equal(first.Json.GetProperty("id").GetGuid(), replay.Json.GetProperty("id").GetGuid());
        Assert.Equal(200, Call("POST", "/operations/find", new { clientOperationId = operationId }, identity.Token).Status);
    }

    [Fact]
    public void IdempotencyConflictAndOtherCharacterAccessAreRejected()
    {
        var alice = Pair();
        PublishStock();
        var quote = Quote(alice.Token);
        var id = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        var first = Call("POST", "/requests", SupplyPayload(quote, id), alice.Token, key);
        Assert.Equal(409, Call("POST", "/requests", SupplyPayload(quote, Guid.NewGuid()), alice.Token, key).Status);
        var bob = Pair("Bob");
        Assert.Equal(404, Call("GET", "/requests/" + first.Json.GetProperty("id").GetGuid(), bearer: bob.Token).Status);
        Assert.Equal(404, Call("POST", "/operations/find", new { clientOperationId = id }, bob.Token).Status);
    }

    [Fact]
    public void QuoteCannotBeAcceptedTwiceOrExceedMissingQuantity()
    {
        var alice = Pair();
        PublishStock();
        var quote = Quote(alice.Token, 1);
        Assert.Equal(422, Call("POST", "/requests", SupplyPayload(quote, Guid.NewGuid(), 2), alice.Token, Guid.NewGuid().ToString()).Status);
        Assert.Equal(202, Call("POST", "/requests", SupplyPayload(quote, Guid.NewGuid(), 1), alice.Token, Guid.NewGuid().ToString()).Status);
        Assert.Equal(409, Call("POST", "/requests", SupplyPayload(quote, Guid.NewGuid(), 1), alice.Token, Guid.NewGuid().ToString()).Status);
    }

    [Fact]
    public void DeficitAboveServiceAllowanceReceivesBoundedPartialOffer()
    {
        var alice = Pair();
        PublishStock();
        var quote = Quote(alice.Token, 5);
        Assert.Equal(3, quote.GetProperty("items")[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public void WorkerLeaseRedeliveryPreservesJobIdentityAndFencesOldResult()
    {
        var pair = StartPairing();
        var first = Claim();
        time.Advance(TimeSpan.FromSeconds(31));
        var second = Claim();
        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.NotEqual(first.GetProperty("leaseToken").GetString(), second.GetProperty("leaseToken").GetString());
        Assert.Equal(409, FinishJob(first, "ready").Status);
        Assert.Equal(200, FinishJob(second, "ready").Status);
        Assert.Equal("ready", Call("POST", $"/pairings/{pair.Id}/status", new { verifier = pair.Verifier }).Json.GetProperty("state").GetString());
    }

    [Fact]
    public void ExpiredPendingPairingCannotPoisonWorkerInbox()
    {
        StartPairing();
        time.Advance(TimeSpan.FromHours(2));
        var fresh = StartPairing();
        Assert.Equal(fresh.Id, Claim().GetProperty("targetId").GetGuid());
    }

    [Fact]
    public void StateFileContainsTokenHashButNoIssuedTokenOrVerifier()
    {
        var alice = Pair();
        var text = File.ReadAllText(Path.Combine(directory, "broker-state.json"));
        Assert.DoesNotContain(alice.Token, text, StringComparison.Ordinal);
        Assert.DoesNotContain(alice.Pair.Verifier, text, StringComparison.Ordinal);
        Assert.Contains("tokenHash", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondProcessCannotOpenSameStateDirectory()
    {
        Assert.Throws<IOException>(() => new BrokerEngine(directory, Options, time));
    }

    [Fact]
    public void CredentialExpiryCannotDeleteExistingOperationHistory()
    {
        var installation = Guid.NewGuid();
        var alice = Pair(installation: installation);
        PublishStock();
        var id = Guid.NewGuid();
        var first = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), id), alice.Token, Guid.NewGuid().ToString());
        time.Advance(TimeSpan.FromHours(2));
        Assert.Equal(401, Call("POST", "/operations/find", new { clientOperationId = id }, alice.Token).Status);
        var renewed = Pair(installation: installation);
        var recovered = Call("POST", "/operations/find", new { clientOperationId = id }, renewed.Token);
        Assert.Equal(first.Json.GetProperty("id").GetGuid(), recovered.Json.GetProperty("id").GetGuid());
    }

    [Fact]
    public void DuplicateJsonMembersAndUnboundedQuantitiesAreRejected()
    {
        using var duplicate = JsonDocument.Parse("{\"verifier\":\"first\",\"verifier\":\"second\"}");
        Assert.Equal(400, engine.Execute("POST", "/api/quest-supplies/v1/pairings/" + Guid.NewGuid() + "/complete", duplicate.RootElement).Status);
        var alice = Pair();
        PublishStock();
        Assert.Equal(422, Call("POST", "/quotes", new
        {
            character = "Alice", gameServer = "synthetic-profile", journeyId = Guid.NewGuid(),
            items = new[] { new { item = "Mold", quantity = int.MaxValue } },
        }, alice.Token).Status);
    }

    [Fact]
    public void ClientReceiptReportsCannotClaimDeliveryOrRestoreStock()
    {
        var alice = Pair();
        PublishStock();
        var submitted = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString());
        var id = submitted.Json.GetProperty("id").GetGuid();
        var reported = Call("POST", $"/requests/{id}/receipts", new
        {
            clientOperationId = Guid.NewGuid(), unitIds = submitted.Json.GetProperty("units").EnumerateArray().Select(u => u.GetProperty("id").GetGuid()).ToArray(),
        }, alice.Token, Guid.NewGuid().ToString());
        Assert.Equal(202, reported.Status);
        Assert.Equal("submitted", reported.Json.GetProperty("state").GetString());
        Assert.All(reported.Json.GetProperty("units").EnumerateArray(), u =>
        {
            Assert.True(u.GetProperty("clientReported").GetBoolean());
            Assert.False(u.GetProperty("sourceAccepted").GetBoolean());
            Assert.False(u.GetProperty("recipientObserved").GetBoolean());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettledDispatchAllowsNextRequestAndRetainsLateReceiptUpdates(bool cancelledRemainder)
    {
        var alice = Pair();
        PublishStock();
        var first = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString()).Json;
        var id = first.GetProperty("id").GetGuid();
        var units = first.GetProperty("units").EnumerateArray().Select((unit, index) => new
        {
            id = unit.GetProperty("id").GetGuid(), sourceAccepted = !cancelledRemainder || index == 0,
            recipientObserved = false, cancelled = cancelledRemainder && index > 0, collected = false, banked = false,
        }).ToArray();
        string state = cancelledRemainder ? "partial" : "sent";
        var update = new { sequence = 1L, state, errorCode = (string?)null, units };
        Assert.Equal(200, Worker("POST", $"/operations/{id}/status", update).Status);
        Assert.Equal(200, Worker("POST", $"/operations/{id}/status", update).Status);

        var next = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString());
        Assert.Equal(202, next.Status);
        Assert.NotEqual(id, next.Json.GetProperty("id").GetGuid());

        var reported = Call("POST", $"/requests/{id}/receipts", new
        {
            clientOperationId = Guid.NewGuid(), unitIds = units.Where(unit => unit.sourceAccepted).Select(unit => unit.id).ToArray(),
        }, alice.Token, Guid.NewGuid().ToString());
        Assert.Equal(202, reported.Status);
        Assert.Equal(state, reported.Json.GetProperty("state").GetString());
        Assert.All(reported.Json.GetProperty("units").EnumerateArray(), unit =>
        {
            Assert.False(unit.GetProperty("recipientObserved").GetBoolean());
            Assert.False(unit.GetProperty("collected").GetBoolean());
            Assert.False(unit.GetProperty("banked").GetBoolean());
        });
        Assert.Equal(200, Worker("POST", $"/operations/{id}/status", new
        {
            sequence = 2L, state = "received", errorCode = (string?)null,
            units = units.Select(unit => new { unit.id, unit.sourceAccepted, recipientObserved = unit.sourceAccepted, unit.cancelled, unit.collected, unit.banked }),
        }).Status);
    }

    [Fact]
    public void ForgedCollectionReportsCannotSettlePendingPartialDispatch()
    {
        var alice = Pair();
        PublishStock();
        var first = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString()).Json;
        var id = first.GetProperty("id").GetGuid();
        var units = first.GetProperty("units").EnumerateArray().Select((unit, index) => new
        {
            id = unit.GetProperty("id").GetGuid(), sourceAccepted = index == 0, recipientObserved = false,
            cancelled = false, collected = false, banked = false,
        }).ToArray();
        Assert.Equal(200, Worker("POST", $"/operations/{id}/status", new { sequence = 1L, state = "partial", errorCode = (string?)null, units }).Status);
        var report = Call("POST", $"/requests/{id}/receipts", new
        {
            clientOperationId = Guid.NewGuid(), unitIds = units.Select(unit => unit.id).ToArray(),
        }, alice.Token, Guid.NewGuid().ToString());
        Assert.Equal(202, report.Status);
        Assert.Equal("partial", report.Json.GetProperty("state").GetString());
        Assert.Equal(1, report.Json.GetProperty("units").EnumerateArray().Count(unit => unit.GetProperty("sourceAccepted").GetBoolean()));
        Assert.All(report.Json.GetProperty("units").EnumerateArray(), unit =>
        {
            Assert.True(unit.GetProperty("clientReported").GetBoolean());
            Assert.False(unit.GetProperty("recipientObserved").GetBoolean());
            Assert.False(unit.GetProperty("collected").GetBoolean());
            Assert.False(unit.GetProperty("banked").GetBoolean());
        });
        var nextQuote = Quote(alice.Token, 3);
        Assert.Equal(3, nextQuote.GetProperty("items")[0].GetProperty("quantity").GetInt32());
        var next = Call("POST", "/requests", SupplyPayload(nextQuote, Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString());
        Assert.Equal(409, next.Status);
        Assert.Equal("RECONCILIATION_REQUIRED", Code(next));
    }

    [Fact]
    public void FullManifestWorkerRejectionAllowsAnotherRequestWithoutDispatchReceipts()
    {
        var alice = Pair();
        PublishStock();
        var first = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString()).Json;
        var id = first.GetProperty("id").GetGuid();
        Assert.Equal(200, Worker("POST", $"/operations/{id}/status", new
        {
            sequence = 1L, state = "rejected", errorCode = "BUDGET_EXHAUSTED",
            units = first.GetProperty("units").EnumerateArray().Select(unit => new
            {
                id = unit.GetProperty("id").GetGuid(), sourceAccepted = false, recipientObserved = false,
                cancelled = false, collected = false, banked = false,
            }).ToArray(),
        }).Status);
        var next = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString());
        Assert.Equal(202, next.Status);
    }

    [Fact]
    public void LostCollectionReportAcknowledgementReplaysDurablyWithoutAnotherJob()
    {
        var alice = Pair();
        PublishStock();
        var first = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString()).Json;
        var id = first.GetProperty("id").GetGuid();
        var report = new { clientOperationId = Guid.NewGuid(), unitIds = first.GetProperty("units").EnumerateArray().Select(unit => unit.GetProperty("id").GetGuid()).ToArray() };
        string key = Guid.NewGuid().ToString();
        Assert.Equal(202, Call("POST", $"/requests/{id}/receipts", report, alice.Token, key).Status);
        engine.Dispose();
        engine = new(directory, Options, time);
        var repeated = Call("POST", $"/requests/{id}/receipts", report, alice.Token, key);
        Assert.Equal(202, repeated.Status);
        Assert.Equal(id, repeated.Json.GetProperty("id").GetGuid());
        Assert.Equal("submitted", repeated.Json.GetProperty("state").GetString());
        int reconciliations = 0;
        while (Claim() is { ValueKind: not JsonValueKind.Null } job)
        {
            if (job.GetProperty("kind").GetString() == "reconcile-operation") reconciliations++;
            Assert.Equal(200, FinishJob(job, "accepted").Status);
        }
        Assert.Equal(1, reconciliations);
        var conflict = Call("POST", $"/requests/{id}/receipts", report with { unitIds = report.unitIds.Take(1).ToArray() }, alice.Token, key);
        Assert.Equal(409, conflict.Status);
        Assert.Equal("IDEMPOTENCY_CONFLICT", Code(conflict));
    }

    [Fact]
    public void WorkerStatusRetriesAreIdempotentAndCannotReverseReceipts()
    {
        var alice = Pair();
        PublishStock();
        var operation = Call("POST", "/requests", SupplyPayload(Quote(alice.Token), Guid.NewGuid()), alice.Token, Guid.NewGuid().ToString()).Json;
        var id = operation.GetProperty("id").GetGuid();
        var units = operation.GetProperty("units").EnumerateArray().Select(u => new
        {
            id = u.GetProperty("id").GetGuid(), sourceAccepted = true, recipientObserved = false,
            cancelled = false, collected = false, banked = false,
        }).ToArray();
        var update = new { sequence = 1L, state = "sent", errorCode = (string?)null, units };
        Assert.Equal(200, Worker("POST", $"/operations/{id}/status", update).Status);
        Assert.Equal(200, Worker("POST", $"/operations/{id}/status", update).Status);
        Assert.Equal(409, Worker("POST", $"/operations/{id}/status", new
        {
            sequence = 2L, state = "reserved", errorCode = (string?)null,
            units = units.Select(u => new { u.id, sourceAccepted = false, u.recipientObserved, u.cancelled, u.collected, u.banked }),
        }).Status);
        var current = Call("GET", $"/requests/{id}", bearer: alice.Token);
        Assert.All(current.Json.GetProperty("units").EnumerateArray(), u => Assert.True(u.GetProperty("sourceAccepted").GetBoolean()));
    }

    [Fact]
    public void ReturnSubmissionIsNotWorkerRegistration()
    {
        var alice = Pair();
        PublishStock();
        var submitted = Call("POST", "/returns", new
        {
            clientOperationId = Guid.NewGuid(), journeyId = Guid.NewGuid(), character = "Alice", gameServer = "synthetic-profile",
            supplyRequestIds = Array.Empty<Guid>(), items = new[] { new { item = "Mold", quantity = 1, origin = "journey_surplus" } },
        }, alice.Token, Guid.NewGuid().ToString());
        Assert.Equal(202, submitted.Status);
        var report = Call("POST", "/returns/" + submitted.Json.GetProperty("id").GetGuid() + "/sent", new
        {
            clientOperationId = Guid.NewGuid(), unitIds = submitted.Json.GetProperty("units").EnumerateArray().Select(u => u.GetProperty("id").GetGuid()).ToArray(),
        }, alice.Token, Guid.NewGuid().ToString());
        Assert.Equal(409, report.Status);
        Assert.Equal("RETURN_NOT_REGISTERED", Code(report));
    }

    [Fact]
    public void FreshRecoveryProofRestoresOriginalOperationNamespaceWithoutNewShipment()
    {
        var originalInstallation = Guid.NewGuid();
        var original = Pair(installation: originalInstallation);
        PublishStock();
        var clientId = Guid.NewGuid();
        var payload = SupplyPayload(Quote(original.Token), clientId);
        var key = Guid.NewGuid().ToString();
        var first = Call("POST", "/requests", payload, original.Token, key);
        var replacementInstallation = Guid.NewGuid();
        var recovery = StartPairing(installation: replacementInstallation, purpose: "recovery");
        ReadyPairing(recovery);
        Proof(recovery);
        var grant = Complete(recovery).Json.GetProperty("credential").GetProperty("accessToken").GetString()!;
        Assert.Equal(401, Call("GET", "/capabilities", bearer: grant).Status);
        Assert.Equal(200, Call("POST", "/recoveries", new { installationIds = new[] { originalInstallation } }, grant).Status);
        Assert.Equal(401, Call("POST", "/recoveries", new { installationIds = new[] { originalInstallation } }, grant).Status);
        var replacement = Pair(installation: replacementInstallation);
        Assert.Equal(401, Call("GET", "/capabilities", bearer: original.Token).Status);
        Assert.Equal(first.Json.GetProperty("id").GetGuid(), Call("POST", "/requests", payload, replacement.Token, key).Json.GetProperty("id").GetGuid());
        Assert.Equal(first.Json.GetProperty("id").GetGuid(), Call("POST", "/operations/find", new { clientOperationId = clientId }, replacement.Token).Json.GetProperty("id").GetGuid());
    }

    private Pairing StartPairing(string character = "Alice", Guid? installation = null, string purpose = "quest-supplies")
    {
        var verifier = Secret();
        var response = Call("POST", "/pairings", new
        {
            installationId = installation ?? Guid.NewGuid(), clientOperationId = Guid.NewGuid(),
            character, gameServer = "synthetic-profile", verifierChallenge = Challenge(verifier), purpose,
        });
        Assert.Equal(202, response.Status);
        return new(response.Json.GetProperty("id").GetGuid(), response.Json.GetProperty("nonce").GetString()!, verifier, character);
    }

    private void ReadyPairing(Pairing pair)
    {
        JsonElement job;
        do
        {
            job = Claim();
            Assert.Equal(200, FinishJob(job, job.GetProperty("kind").GetString() == "register-pairing" ? "ready" : "accepted").Status);
        } while (job.GetProperty("payload").GetProperty("id").GetGuid() != pair.Id);
    }

    private BrokerReply Proof(Pairing pair, string? character = null) => Worker("POST", $"/pairings/{pair.Id}/proof", new
    {
        nonce = pair.Nonce, character = character ?? pair.Character, gameServer = "synthetic-profile", observedAt = time.GetUtcNow(),
    });
    private BrokerReply Complete(Pairing pair) => Call("POST", $"/pairings/{pair.Id}/complete", new { verifier = pair.Verifier });
    private (string Token, Pairing Pair) Pair(string character = "Alice", Guid? installation = null)
    {
        var pair = StartPairing(character, installation);
        ReadyPairing(pair);
        Assert.Equal(200, Proof(pair).Status);
        return (Complete(pair).Json.GetProperty("credential").GetProperty("accessToken").GetString()!, pair);
    }
    private JsonElement Claim() => Worker("POST", "/jobs/claim", new { }).Json.GetProperty("job").Clone();
    private BrokerReply FinishJob(JsonElement job, string state) => Worker("POST", "/jobs/" + job.GetProperty("id").GetGuid() + "/result", new
    {
        leaseToken = job.GetProperty("leaseToken").GetString(), fence = job.GetProperty("fence").GetInt64(), state,
    });
    private void PublishStock() => Assert.Equal(200, Worker("POST", "/catalog", new
    {
        gameServer = "synthetic-profile", ready = true, returnSupported = true, receiveModes = new[] { "single-unit" },
        items = new[] { new { item = "Mold", available = 3, maxPerRequest = 3 } },
    }).Status);
    private JsonElement Quote(string token, int quantity = 2)
    {
        var response = Call("POST", "/quotes", new
        {
            journeyId = Guid.NewGuid(), character = "Alice", gameServer = "synthetic-profile",
            items = new[] { new { item = "Mold", quantity } },
        }, token);
        Assert.Equal(200, response.Status);
        return response.Json;
    }
    private static object SupplyPayload(JsonElement quote, Guid operationId, int quantity = 2) => new
    {
        clientOperationId = operationId, journeyId = quote.GetProperty("journeyId").GetGuid(), character = "Alice", gameServer = "synthetic-profile",
        quoteId = quote.GetProperty("id").GetGuid(), items = new[] { new { item = "Mold", quantity } },
    };
    private BrokerReply Call(string method, string path, object? body = null, string? bearer = null, string? key = null) =>
        engine.Execute(method, "/api/quest-supplies/v1" + path, body is null ? default : JsonSerializer.SerializeToElement(body), bearer, key);
    private BrokerReply Worker(string method, string path, object body, string? bearer = null) =>
        engine.Execute(method, "/worker/v1" + path, JsonSerializer.SerializeToElement(body), bearer ?? workerToken);
    private static string Secret() => Base64Url(RandomNumberGenerator.GetBytes(32));
    private static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier)));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string? Code(BrokerReply reply) => reply.Json.GetProperty("code").GetString();
    private sealed record Pairing(Guid Id, string Nonce, string Verifier, string Character);
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
    public void Dispose()
    {
        engine.Dispose();
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
