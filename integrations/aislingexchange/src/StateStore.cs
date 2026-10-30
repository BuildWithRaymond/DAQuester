using System.Text.Json;
namespace AislingExchange.Broker;

internal sealed class StateStore : IDisposable
{
    private const int MaxStateBytes = 16 * 1024 * 1024;
    private readonly object gate = new();
    private readonly FileStream processLock;
    private readonly string file;
    private BrokerState state;
    internal StateStore(string directory)
    {
        Directory.CreateDirectory(directory);
        file = Path.Combine(directory, "broker-state.json");
        processLock = new FileStream(Path.Combine(directory, ".broker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (File.Exists(file) && new FileInfo(file).Length > MaxStateBytes)
                throw new InvalidDataException("AislingExchange state exceeds protected capacity.");
            state = File.Exists(file) ? JsonSerializer.Deserialize<BrokerState>(File.ReadAllBytes(file), Wire.Json)
                ?? throw new InvalidDataException("Invalid AislingExchange state.") : new();
            if (state.Version != 1) throw new InvalidDataException("Unsupported AislingExchange state.");
        }
        catch { processLock.Dispose(); throw; }
    }
    internal T Update<T>(Func<BrokerState, T> mutate)
    {
        lock (gate)
        {
            var next = JsonSerializer.Deserialize<BrokerState>(JsonSerializer.SerializeToUtf8Bytes(state, Wire.Json), Wire.Json)!;
            var result = mutate(next);
            var temporary = file + ".pending";
            var bytes = JsonSerializer.SerializeToUtf8Bytes(next, Wire.Json);
            if (bytes.Length > MaxStateBytes) throw new BrokerFault(429, "HISTORY_CAPACITY_REACHED");
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, file, true);
            state = next;
            return result;
        }
    }
    public void Dispose() => processLock.Dispose();
}
internal sealed class BrokerState
{
    public int Version { get; set; } = 1;
    public List<PairingRecord> Pairings { get; set; } = [];
    public List<CredentialRecord> Credentials { get; set; } = [];
    public List<QuoteRecord> Quotes { get; set; } = [];
    public List<OperationRecord> Operations { get; set; } = [];
    public List<JobRecord> Jobs { get; set; } = [];
    public List<MutationRecord> Mutations { get; set; } = [];
    public List<RecoveryRecord> Recoveries { get; set; } = [];
    public CatalogRecord Catalog { get; set; } = new();
}
internal sealed class PairingRecord
{
    public Guid Id { get; set; }
    public Guid InstallationId { get; set; }
    public Guid ClientOperationId { get; set; }
    public string Character { get; set; } = "";
    public string GameServer { get; set; } = "";
    public string Challenge { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string State { get; set; } = "registering";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ProofObservedAt { get; set; }
    public string? IssuedTokenHash { get; set; }
}
internal sealed class CredentialRecord
{
    public string TokenHash { get; set; } = "";
    public Guid PairingId { get; set; }
    public Guid InstallationId { get; set; }
    public string Subject { get; set; } = "";
    public string Character { get; set; } = "";
    public string GameServer { get; set; } = "";
    public string Scope { get; set; } = "quest-supplies";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
internal sealed class QuoteRecord
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = "";
    public Guid InstallationId { get; set; }
    public Guid JourneyId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public List<ItemQuantity> Items { get; set; } = [];
    public Guid? AcceptedOperationId { get; set; }
}
internal sealed class OperationRecord
{
    public Guid Id { get; set; }
    public Guid ClientOperationId { get; set; }
    public Guid JourneyId { get; set; }
    public Guid BindingId { get; set; }
    public Guid InstallationId { get; set; }
    public string Subject { get; set; } = "";
    public string Character { get; set; } = "";
    public string GameServer { get; set; } = "";
    public string Kind { get; set; } = "";
    public string State { get; set; } = "submitted";
    public string? ErrorCode { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset CredentialExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public bool CancellationRequested { get; set; }
    public long Sequence { get; set; }
    public JsonElement Manifest { get; set; }
    public List<UnitRecord> Units { get; set; } = [];
}
internal sealed class UnitRecord
{
    public Guid Id { get; set; }
    public string Item { get; set; } = "";
    public bool SourceAccepted { get; set; }
    public bool RecipientObserved { get; set; }
    public bool Cancelled { get; set; }
    public bool Collected { get; set; }
    public bool Banked { get; set; }
    public bool ClientReported { get; set; }
}
internal sealed class JobRecord
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public Guid TargetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LeaseExpiresAt { get; set; }
    public long Fence { get; set; }
    public string? LeaseTokenHash { get; set; }
    public bool Completed { get; set; }
}
internal sealed class MutationRecord
{
    public string Subject { get; set; } = "";
    public Guid InstallationId { get; set; }
    public string Route { get; set; } = "";
    public string Key { get; set; } = "";
    public string BodyHash { get; set; } = "";
    public Guid ClientOperationId { get; set; }
    public Guid TargetId { get; set; }
}
internal sealed class RecoveryRecord
{
    public string Subject { get; set; } = "";
    public Guid InstallationId { get; set; }
    public List<Guid> OriginalInstallations { get; set; } = [];
}
internal sealed class CatalogRecord
{
    public bool Ready { get; set; }
    public bool ReturnSupported { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<string> ReceiveModes { get; set; } = [];
    public List<CatalogItem> Items { get; set; } = [];
}
