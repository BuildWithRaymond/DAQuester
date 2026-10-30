using System.Text.Json;
using System.Text.Json.Serialization;

namespace AislingExchange.Broker;

public sealed record BrokerReply(int Status, JsonElement Json);
public sealed class BrokerOptions
{
    public string GameServer { get; set; } = "";
    public string WorkerTokenHash { get; set; } = "";
    public Dictionary<string, int> CatalogLimits { get; set; } = new(StringComparer.Ordinal);
    public int MaxPairings { get; set; } = 1000;
    public int MaxOperations { get; set; } = 100000;
    public int MaxQueuedJobs { get; set; } = 1000;
    public int MaxQuotes { get; set; } = 1000;
    public int MaxUnitsPerOperation { get; set; } = 100;
}
internal sealed record PairingStart(Guid InstallationId, Guid ClientOperationId, string GameServer, string Character, string VerifierChallenge, string Purpose);
internal sealed record PairingAccess(string Verifier);
internal sealed record PairingProof(string Nonce, string Character, string GameServer, DateTimeOffset ObservedAt);
internal sealed record ItemQuantity(string Item, int Quantity);
internal sealed record QuoteInput(string Character, string GameServer, Guid JourneyId, List<ItemQuantity> Items);
internal sealed record SupplyInput(Guid ClientOperationId, Guid JourneyId, string Character, string GameServer, Guid QuoteId, List<ItemQuantity> Items);
internal sealed record ReturnInput(Guid ClientOperationId, Guid JourneyId, string Character, string GameServer, List<Guid> SupplyRequestIds, List<ReturnItem> Items);
internal sealed record ReturnItem(string Item, int Quantity, string Origin);
internal sealed record FindInput(Guid ClientOperationId);
internal sealed record ReportInput(Guid ClientOperationId, List<Guid> UnitIds);
internal sealed record RecoveryInput(List<Guid> InstallationIds);
internal sealed record CatalogInput(string GameServer, bool Ready, bool ReturnSupported, List<string> ReceiveModes, List<CatalogItem> Items);
internal sealed record CatalogItem(string Item, int Available, int MaxPerRequest);
internal sealed record JobResult(string LeaseToken, long Fence, string State);
internal sealed record OperationUpdate(long Sequence, string State, string? ErrorCode, List<UnitUpdate> Units);
internal sealed record UnitUpdate(Guid Id, bool SourceAccepted, bool RecipientObserved, bool Cancelled, bool Collected, bool Banked);
internal sealed class BrokerFault(int status, string code) : Exception
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
internal static class Wire
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 12,
    };
    internal static T Read<T>(JsonElement value)
    {
        RejectDuplicateMembers(value);
        return value.Deserialize<T>(Json) ?? throw new BrokerFault(400, "INVALID_REQUEST");
    }
    internal static void RejectDuplicateMembers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new BrokerFault(400, "INVALID_REQUEST");
                RejectDuplicateMembers(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) RejectDuplicateMembers(child);
    }
}
