using System.Text.Json.Serialization;

namespace DAQuester.Core;

public sealed record AislingExchangeSupplyLine(string Item, int Quantity);
public sealed record AislingExchangeQuoteRequest(string Character, [property: JsonPropertyName("gameServer")] string ServerProfile,
    string JourneyId, IReadOnlyList<AislingExchangeSupplyLine> Items);
public sealed record AislingExchangeQuote(string Id, string Character, [property: JsonPropertyName("gameServer")] string ServerProfile,
    DateTimeOffset ExpiresAt, IReadOnlyList<AislingExchangeSupplyLine> Items, string JourneyId = "");
public sealed record AislingExchangeSubmission(Guid ClientOperationId, string JourneyId, string Character,
    [property: JsonPropertyName("gameServer")] string ServerProfile, string QuoteId, IReadOnlyList<AislingExchangeSupplyLine> Items);
public sealed record AislingExchangeParcelUnit(string Id, string Item, bool SourceAccepted, bool Cancelled = false,
    bool RecipientObserved = false, bool Collected = false, bool Banked = false, bool ClientReported = false);
public sealed record AislingExchangeRequestStatus(string Id, Guid ClientOperationId, string Character,
    [property: JsonPropertyName("gameServer")] string ServerProfile, string State, IReadOnlyList<AislingExchangeParcelUnit> Units,
    string? ErrorCode = null, long Sequence = 0);
public sealed record AislingExchangeUnitReport(Guid ClientOperationId, Guid OperationClientId, string OperationId,
    string Character, string ServerProfile, IReadOnlyList<string> UnitIds);
public sealed record AislingExchangeReturnLine(string Item, int Quantity, string Origin);
public sealed record AislingExchangeReturnSubmission(Guid ClientOperationId, string JourneyId, string Character,
    [property: JsonPropertyName("gameServer")] string ServerProfile, IReadOnlyList<string> SupplyRequestIds,
    IReadOnlyList<AislingExchangeReturnLine> Items);

public sealed record AislingExchangePairingStart(Guid InstallationId, Guid ClientOperationId, string Character,
    [property: JsonPropertyName("gameServer")] string ServerProfile, string VerifierChallenge, string Purpose = "quest-supplies");

public sealed record AislingExchangePairingChallenge(string Id, string Nonce, string Receiver, string Character,
    [property: JsonPropertyName("gameServer")] string ServerProfile, DateTimeOffset ExpiresAt, string State = "registering")
{
    public override string ToString() => "AislingExchangePairingChallenge { Sensitive values redacted }";
}

public sealed record AislingExchangePairingStatus(string Id, string Character,
    [property: JsonPropertyName("gameServer")] string ServerProfile, string State, DateTimeOffset ExpiresAt,
    string Receiver = "Exchanger", string? Nonce = null)
{
    public override string ToString() => "AislingExchangePairingStatus { Sensitive values redacted }";
}

public sealed record AislingExchangeCredential(string Character, [property: JsonPropertyName("gameServer")] string ServerProfile,
    string AccessToken, string Scope, DateTimeOffset ExpiresAt)
{
    public override string ToString() => "AislingExchangeCredential { Sensitive values redacted }";
}

public sealed record AislingExchangePairingCompletion(string State, AislingExchangeCredential? Credential)
{
    public override string ToString() => "AislingExchangePairingCompletion { Sensitive values redacted }";
}

public interface IAislingExchangeCredentialStore
{
    Task<AislingExchangeCredential?> LoadAsync(string character, string serverProfile, CancellationToken cancellation = default);
    Task SaveAsync(AislingExchangeCredential credential, CancellationToken cancellation = default);
    Task ClearAsync(string character, string serverProfile, CancellationToken cancellation = default);
}
