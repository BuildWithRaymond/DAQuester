using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed record AislingExchangeApiRoutes(Uri PairingStart, Uri PairingComplete, Uri Quote, Uri Submit, Uri Find)
{
    public Uri? PairingStatus { get; init; }
    public Uri? PairingRevoke { get; init; }
    public Uri? Receipts { get; init; }
    public Uri? Returns { get; init; }
    public Uri? Capabilities { get; init; }

    // The caller supplies the deployed public API location. No startup networking or
    // fallback host is implied by this contract.
    public static AislingExchangeApiRoutes FromApiBase(Uri apiBase)
    {
        AislingExchangeClient.ValidateEndpoint(apiBase);
        if (!apiBase.AbsolutePath.EndsWith("/api/quest-supplies/v1/", StringComparison.Ordinal))
            throw new AislingExchangeException("AislingExchange service configuration is invalid.");
        return new(new(apiBase, "pairings"), new(apiBase, "pairings"), new(apiBase, "quotes"),
            new(apiBase, "requests"), new(apiBase, "operations/find"))
        {
            PairingStatus = new(apiBase, "pairings"),
            PairingRevoke = new(apiBase, "pairings"),
            Receipts = new(apiBase, "requests"),
            Returns = new(apiBase, "returns"),
            Capabilities = new(apiBase, "capabilities")
        };
    }
}

public sealed class AislingExchangeException(string message, bool outcomeUncertain = false) : Exception(message)
{
    public bool OutcomeUncertain { get; } = outcomeUncertain;
}

public sealed class AislingExchangePairingProof : IDisposable
{
    private readonly byte[] verifier;
    private bool disposed;
    public Guid InstallationId { get; }
    public Guid ClientOperationId { get; }
    public string Verifier { get { ObjectDisposedException.ThrowIf(disposed, this); return Base64Url(verifier); } }
    public string VerifierHash { get { ObjectDisposedException.ThrowIf(disposed, this); return Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier))); } }

    private AislingExchangePairingProof(Guid installationId, Guid clientOperationId, byte[] verifier)
    {
        InstallationId = installationId;
        ClientOperationId = clientOperationId;
        this.verifier = verifier;
    }

    public static AislingExchangePairingProof Create(Guid? installationId = null, Guid? clientOperationId = null)
        => new(installationId ?? Guid.NewGuid(), clientOperationId ?? Guid.NewGuid(), RandomNumberGenerator.GetBytes(32));
    internal static AislingExchangePairingProof Restore(Guid installationId, Guid clientOperationId, string verifier)
    {
        if (installationId == Guid.Empty || clientOperationId == Guid.Empty || verifier is not { Length: 43 }
            || !verifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new AislingExchangeException("AislingExchange pairing checkpoint is invalid.");
        try
        {
            byte[] bytes = Convert.FromBase64String(verifier.Replace('-', '+').Replace('_', '/') + "=");
            if (bytes.Length != 32 || Base64Url(bytes) != verifier)
                throw new FormatException();
            return new(installationId, clientOperationId, bytes);
        }
        catch (FormatException) { throw new AislingExchangeException("AislingExchange pairing checkpoint is invalid."); }
    }
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public void Dispose() { CryptographicOperations.ZeroMemory(verifier); disposed = true; }
    public override string ToString() => "AislingExchangePairingProof { Sensitive values redacted }";
}

public sealed partial class AislingExchangeClient : IAislingExchangePairingClient, IDisposable
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };
    private const int MaxResponseBytes = 65_536;
    private readonly AislingExchangeApiRoutes routes;
    private readonly IAislingExchangeCredentialStore credentials;
    private readonly HttpClient http;
    private readonly TimeProvider time;

    public AislingExchangeClient(AislingExchangeApiRoutes routes, IAislingExchangeCredentialStore credentials,
        HttpMessageHandler? handler = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(credentials);
        foreach (Uri endpoint in new[] { routes.PairingStart, routes.PairingComplete, routes.Quote, routes.Submit, routes.Find })
            ValidateEndpoint(endpoint);
        if (routes.PairingStatus is not null)
            ValidateEndpoint(routes.PairingStatus);
        if (routes.PairingRevoke is not null)
            ValidateEndpoint(routes.PairingRevoke);
        foreach (var endpoint in new[] { routes.Receipts, routes.Returns, routes.Capabilities })
            if (endpoint is not null)
            {
                ValidateEndpoint(endpoint);
                if (endpoint.GetLeftPart(UriPartial.Authority) != routes.Submit.GetLeftPart(UriPartial.Authority))
                    throw new AislingExchangeException("AislingExchange service configuration is invalid.");
            }
        if (handler is HttpClientHandler clientHandler)
            clientHandler.AllowAutoRedirect = false;
        if (handler is SocketsHttpHandler socketsHandler)
            socketsHandler.AllowAutoRedirect = false;
        this.routes = routes;
        this.credentials = credentials;
        this.time = time ?? TimeProvider.System;
        http = new(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false, MaxResponseHeadersLength = 16 })
        {
            Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = MaxResponseBytes
        };
    }

    internal static void ValidateEndpoint(Uri endpoint)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps || !endpoint.IsDefaultPort
            || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0
            || endpoint.Host is not ("aislingexchange.com" or "api.aislingexchange.com" or "supplies.aislingexchange.com")
            || !endpoint.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal)
            || endpoint.AbsolutePath.Contains('%') || endpoint.OriginalString.Contains('\\'))
            throw new AislingExchangeException("AislingExchange service configuration is invalid.");
    }

    public async Task<AislingExchangePairingChallenge> StartPairingAsync(string character, string serverProfile,
        AislingExchangePairingProof proof, CancellationToken cancellation = default)
    {
        ValidateSubject(character, serverProfile);
        if (proof.InstallationId == Guid.Empty || proof.ClientOperationId == Guid.Empty)
            throw InvalidRequest();
        var request = new AislingExchangePairingStart(proof.InstallationId, proof.ClientOperationId, character, serverProfile, proof.VerifierHash);
        var challenge = (await SendAsync<AislingExchangePairingChallenge>(routes.PairingStart, request, null, true, false, cancellation))!;
        ValidateChallenge(challenge, true);
        if (challenge.Character != character || challenge.ServerProfile != serverProfile)
            throw InvalidResponse(true);
        return challenge;
    }

    public async Task<AislingExchangePairingStatus> GetPairingStatusAsync(AislingExchangePairingChallenge challenge,
        AislingExchangePairingProof proof, CancellationToken cancellation = default)
    {
        ValidateChallenge(challenge, false);
        if (routes.PairingStatus is null)
            throw new AislingExchangeException("AislingExchange service configuration is incomplete.");
        var status = (await SendAsync<AislingExchangePairingStatus>(PairingEndpoint(routes.PairingStatus, challenge.Id, "status"),
            new VerifierBody(proof.Verifier), null, false, false, cancellation))!;
        if (status.Id != challenge.Id || status.Character != challenge.Character || status.ServerProfile != challenge.ServerProfile
            || status.ExpiresAt != challenge.ExpiresAt || status.Receiver != "Exchanger" || status.Nonce is not null
            || status.State is not ("registering" or "ready" or "observed" or "expired"))
            throw InvalidResponse();
        return status;
    }

    public async Task<AislingExchangePairingCompletion> CompletePairingAsync(AislingExchangePairingChallenge challenge,
        AislingExchangePairingProof proof, CancellationToken cancellation = default)
    {
        ValidateChallenge(challenge, false);
        var result = (await SendAsync<AislingExchangePairingCompletion>(PairingEndpoint(routes.PairingComplete, challenge.Id, "complete"),
            new VerifierBody(proof.Verifier), null, true, false, cancellation))!;
        if (result.State == "pending" && result.Credential is null)
            return result;
        if (result.State != "paired" || !ValidCredential(result.Credential, challenge.Character, challenge.ServerProfile, time.GetUtcNow())
            || result.Credential!.ExpiresAt > time.GetUtcNow().AddHours(1))
            throw InvalidResponse(true);
        await credentials.SaveAsync(result.Credential, cancellation);
        return result;
    }

    public async Task<AislingExchangeQuote> QuoteAsync(AislingExchangeQuoteRequest request, CancellationToken cancellation = default)
    {
        ValidateSubject(request.Character, request.ServerProfile);
        ValidateLines(request.Items);
        if (!Identifier(request.JourneyId))
            throw InvalidRequest();
        var credential = await CredentialAsync(request.Character, request.ServerProfile, cancellation);
        var quote = (await SendAsync<AislingExchangeQuote>(routes.Quote, request, credential, false, false, cancellation))!;
        ValidateLines(quote.Items, true, true);
        if (!Identifier(quote.Id) || quote.Character != request.Character || quote.ServerProfile != request.ServerProfile
            || quote.JourneyId != request.JourneyId || !ValidExpiry(quote.ExpiresAt, TimeSpan.FromMinutes(5))
            || !OfferedLines(quote.Items, request.Items))
            throw InvalidResponse();
        return quote;
    }

    public async Task RevokePairingAsync(AislingExchangePairingChallenge challenge, AislingExchangePairingProof proof,
        CancellationToken cancellation = default)
    {
        ValidateChallenge(challenge, false, true);
        if (routes.PairingRevoke is null)
            throw new AislingExchangeException("AislingExchange service configuration is incomplete.");
        var result = (await SendAsync<RevokedBody>(PairingEndpoint(routes.PairingRevoke, challenge.Id, "revoke"),
            new VerifierBody(proof.Verifier), null, true, false, cancellation))!;
        if (result.State != "revoked")
            throw InvalidResponse(true);
        await credentials.ClearAsync(challenge.Character, challenge.ServerProfile, cancellation);
    }

    public async Task<AislingExchangeRequestStatus> SubmitAsync(AislingExchangeSubmission request, CancellationToken cancellation = default)
    {
        ValidateSubject(request.Character, request.ServerProfile);
        ValidateLines(request.Items);
        if (request.ClientOperationId == Guid.Empty || !Identifier(request.QuoteId) || !Identifier(request.JourneyId))
            throw InvalidRequest();
        var credential = await CredentialAsync(request.Character, request.ServerProfile, cancellation);
        var status = (await SendAsync<AislingExchangeRequestStatus>(routes.Submit, request, credential, true, false, cancellation))!;
        ValidateStatus(status, request.ClientOperationId, request.Character, request.ServerProfile, true);
        if (status.Units.Any(unit => !request.Items.Any(line => line.Item == unit.Item))
            || status.Units.GroupBy(unit => unit.Item).Any(group => group.Count() > request.Items.Single(line => line.Item == group.Key).Quantity))
            throw InvalidResponse(true);
        return status;
    }

    public async Task<AislingExchangeRequestStatus?> FindAsync(Guid clientOperationId, string character, string serverProfile,
        CancellationToken cancellation = default)
    {
        ValidateSubject(character, serverProfile);
        if (clientOperationId == Guid.Empty)
            throw InvalidRequest();
        var credential = await CredentialAsync(character, serverProfile, cancellation);
        var status = await SendAsync<AislingExchangeRequestStatus>(routes.Find, new FindBody(clientOperationId), credential, false, true, cancellation);
        if (status is not null)
            ValidateStatus(status, clientOperationId, character, serverProfile, false);
        return status;
    }

    private async Task<AislingExchangeCredential> CredentialAsync(string character, string serverProfile, CancellationToken cancellation)
    {
        var credential = await credentials.LoadAsync(character, serverProfile, cancellation);
        if (!ValidCredential(credential, character, serverProfile, time.GetUtcNow()))
            throw new AislingExchangeException("Connect this character to AislingExchange again.");
        return credential!;
    }

    internal static bool ValidCredential(AislingExchangeCredential? value, string character, string serverProfile, DateTimeOffset now)
        => value is not null && value.Character == character && value.ServerProfile == serverProfile
            && value.Scope == "quest-supplies" && value.ExpiresAt > now
            && value.AccessToken is { Length: >= 32 and <= 4096 }
            && value.AccessToken.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private async Task<T?> SendAsync<T>(Uri endpoint, object? body, AislingExchangeCredential? credential, bool mutation,
        bool allowNotFound, CancellationToken cancellation, Guid? idempotencyKey = null, HttpMethod? method = null) where T : class
    {
        byte[] bytes;
        try { bytes = body is null ? [] : JsonSerializer.SerializeToUtf8Bytes(body, Json); }
        catch (JsonException) { throw InvalidRequest(); }
        if (bytes.Length > 16_384)
            throw InvalidRequest();
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, endpoint);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new("application/json");
        }
        request.Headers.Accept.Add(new("application/json"));
        if (credential is not null)
            request.Headers.Authorization = new("Bearer", credential.AccessToken);
        var key = idempotencyKey ?? (body is AislingExchangeSubmission submission ? submission.ClientOperationId : (Guid?)null);
        if (key is { } mutationKey)
            request.Headers.Add("Idempotency-Key", mutationKey.ToString("D"));
        cancellation.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15), time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, deadline.Token);
        CancellationToken requestCancellation = linked.Token;
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCancellation);
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
                throw new AislingExchangeException("AislingExchange could not confirm the request. Check its existing status before trying again.", mutation);
            if (response.Content.Headers.ContentType?.MediaType != "application/json" || response.Content.Headers.ContentLength > MaxResponseBytes)
                throw InvalidResponse(mutation);
            using var stream = await response.Content.ReadAsStreamAsync(requestCancellation);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, requestCancellation)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes)
                    throw InvalidResponse(mutation);
                buffer.Write(chunk, 0, read);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 12 });
            RejectDuplicateMembers(document.RootElement);
            return document.RootElement.Deserialize<T>(Json) ?? throw InvalidResponse(mutation);
        }
        catch (AislingExchangeException) { throw; }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested && !mutation)
        {
            throw new OperationCanceledException("AislingExchange operation cancelled.", cancellation);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException or NotSupportedException)
        {
            // Never retain upstream exceptions: URLs, headers and payloads can
            // contain internal service details or credentials.
            throw new AislingExchangeException("AislingExchange could not confirm the request. Check its existing status before trying again.", mutation);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void RejectDuplicateMembers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new JsonException();
                RejectDuplicateMembers(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                RejectDuplicateMembers(item);
    }

    private void ValidateChallenge(AislingExchangePairingChallenge value, bool response, bool allowExpired = false)
    {
        if (value is null || !Identifier(value.Id) || value.Receiver != "Exchanger"
            || value.Nonce is not { Length: 47 } || !value.Nonce.StartsWith("AEQ-", StringComparison.Ordinal)
            || !value.Nonce.AsSpan(4).ToArray().All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            || (!allowExpired && !ValidExpiry(value.ExpiresAt, TimeSpan.FromMinutes(5))) || value.ExpiresAt == default
            || value.State is not ("registering" or "ready" or "observed")
            || !Subject(value.Character, value.ServerProfile))
            throw response ? InvalidResponse(true) : InvalidRequest();
    }

    private bool ValidExpiry(DateTimeOffset expiry, TimeSpan maximum) => expiry > time.GetUtcNow() && expiry <= time.GetUtcNow().Add(maximum);
    private static Uri PairingEndpoint(Uri root, string id, string operation)
        => new(root.AbsoluteUri.TrimEnd('/') + "/" + id + "/" + operation);
    private static bool Identifier(string? value) => value is { Length: > 0 and <= 128 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static bool Subject(string? character, string? server) => character is { Length: > 0 and <= 30 }
        && character.All(char.IsAsciiLetter) && server is { Length: > 0 and <= 128 }
        && server.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    private static void ValidateSubject(string character, string server) { if (!Subject(character, server)) throw InvalidRequest(); }
    private static bool Item(string? item) => item is { Length: > 0 and <= 80 } && item == item.Trim() && item.All(c => !char.IsControl(c));

    private static void ValidateLines(IReadOnlyList<AislingExchangeSupplyLine>? items, bool response = false, bool allowEmpty = false)
    {
        if (items is null || items.Count > 32 || (!allowEmpty && items.Count == 0) || items.Any(line => line is null || !Item(line.Item) || line.Quantity is < 1 or > 100)
            || items.Sum(line => line.Quantity) > 100 || items.Select(line => line.Item).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw response ? InvalidResponse() : InvalidRequest();
    }

    private static bool OfferedLines(IReadOnlyList<AislingExchangeSupplyLine> actual, IReadOnlyList<AislingExchangeSupplyLine> expected)
        => actual.Count <= expected.Count && actual.All(line => expected.Any(other => other.Item == line.Item && other.Quantity >= line.Quantity));

    private static void ValidateStatus(AislingExchangeRequestStatus value, Guid operation, string character, string serverProfile, bool mutation, bool returns = false)
    {
        string[] states = returns
            ? ["submitted", "registered", "awaiting_player_send", "awaiting_exchanger_receive", "collecting", "banking", "complete", "partial", "review", "cancelled", "rejected"]
            : ["submitted", "reserved", "processing", "sent", "received", "partial", "cancelled", "rejected", "review"];
        if (!Identifier(value.Id) || value.ClientOperationId != operation || value.Character != character || value.ServerProfile != serverProfile
            || !states.Contains(value.State, StringComparer.Ordinal)
            || value.Sequence < 0 || (value.ErrorCode is not null && !Identifier(value.ErrorCode))
            || value.Units is null || value.Units.Count > 100
            || value.Units.Any(unit => unit is null || !Identifier(unit.Id) || !Item(unit.Item))
            || value.Units.Select(unit => unit.Id).Distinct(StringComparer.Ordinal).Count() != value.Units.Count)
            throw InvalidResponse(mutation);
        if (value.Units.Any(unit => unit.Cancelled && (unit.SourceAccepted || unit.RecipientObserved || unit.Collected || unit.Banked)
            || unit.RecipientObserved && !unit.SourceAccepted || unit.Banked && !unit.Collected
            || !returns && (unit.Collected || unit.Banked))
            || value.State == "sent" && value.Units.Any(unit => !unit.SourceAccepted && !unit.Cancelled)
            || value.State == "received" && value.Units.Any(unit => !unit.RecipientObserved && !unit.Cancelled)
            || value.State == "complete" && value.Units.Any(unit => !unit.Banked && !unit.Cancelled)
            || value.State == "rejected" && value.Units.Any(unit => unit.SourceAccepted || unit.RecipientObserved || unit.Collected || unit.Banked))
            throw InvalidResponse(mutation);
    }

    private static AislingExchangeException InvalidRequest() => new("AislingExchange request is invalid.");
    private static AislingExchangeException InvalidResponse(bool uncertain = false) => new("AislingExchange returned an invalid response.", uncertain);
    private sealed record VerifierBody(string Verifier)
    {
        public override string ToString() => "VerifierBody { Sensitive values redacted }";
    }
    private sealed record FindBody(Guid ClientOperationId);
    private sealed record RevokedBody(string State);
    public void Dispose() => http.Dispose();
}
