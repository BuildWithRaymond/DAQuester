namespace DAQuester.Core;

public sealed record QuestSupplyLine(string Item, int Quantity);
public sealed record QuestSupplyReturnLine(string Item, int Quantity, string Origin);
public sealed record QuestSupplyDemand(string Character, string ServerProfile, Guid JourneyId, QuestSupplyLine[] Items);
public sealed record QuestSupplyQuote(string Id, string Character, string ServerProfile, DateTimeOffset ExpiresAt, QuestSupplyLine[] Items);
public sealed record QuestSupplySubmission(Guid ClientOperationId, Guid JourneyId, string Character, string ServerProfile, string QuoteId, QuestSupplyLine[] Items);
public sealed record QuestSupplyParcelUnit(string Id, string Item, bool SourceAccepted, bool Cancelled = false)
{
    public bool RecipientObserved { get; init; }
    public bool Collected { get; init; }
    public bool Banked { get; init; }
    public bool ClientReported { get; init; }
}
public sealed record QuestSupplyRequestStatus(string Id, Guid ClientOperationId, string Character, string ServerProfile, string State, QuestSupplyParcelUnit[] Units);
public sealed record QuestSupplyOffer(QuestSupplyQuote Quote, long Generation, bool MayCollectOtherMail = true);
public sealed record QuestSupplyUnitReport(Guid ClientOperationId, Guid OperationClientId, string OperationId,
    string Character, string ServerProfile, string[] UnitIds);
public sealed record QuestSupplyReturnSubmission(Guid ClientOperationId, Guid JourneyId, string Character,
    string ServerProfile, string[] SupplyRequestIds, QuestSupplyReturnLine[] Items);

public interface IQuestSupplyProvider
{
    bool Enabled { get; }
    Task<QuestSupplyQuote?> QuoteAsync(QuestSupplyDemand demand, CancellationToken cancellation);
    Task<QuestSupplyRequestStatus> SubmitAsync(QuestSupplySubmission submission, CancellationToken cancellation);
    Task<QuestSupplyRequestStatus?> FindAsync(Guid clientOperationId, CancellationToken cancellation);
    Task<QuestSupplyRequestStatus> ReportReceiptsAsync(QuestSupplyUnitReport report, CancellationToken cancellation)
        => throw new QuestBlockedException("AislingExchange collection reporting is unavailable; its saved receipt remains pending.");
}

public interface IQuestSupplyReturnProvider
{
    Task<bool> ReturnsAvailableAsync(CancellationToken cancellation);
    Task<QuestSupplyRequestStatus> RegisterReturnAsync(QuestSupplyReturnSubmission submission, CancellationToken cancellation);
    Task<QuestSupplyRequestStatus?> FindReturnAsync(Guid clientOperationId, CancellationToken cancellation);
    Task<QuestSupplyRequestStatus> ReportReturnSentAsync(QuestSupplyUnitReport report, CancellationToken cancellation);
}

public interface IQuestSupplyConsent
{
    // Null means that the exact offer remains saved for a visible player decision.
    Task<bool?> AcceptAsync(QuestSupplyOffer offer, CancellationToken cancellation);
}

public sealed record QuestSupplyReceiptIntent(Guid ClientOperationId, string Character, string ServerProfile,
    long Generation, long Revision, Dictionary<string, int> InventoryBaseline, QuestSupplyParcelUnit[] Units);
public sealed record QuestSupplyUnitReceipt(string UnitId, string Item, string Sender);
public enum QuestSupplyCollectionOutcome { Received, Partial, Empty, CapacityBlocked, Refused, Uncertain }
public sealed record QuestSupplyParcelReceipt(string Character, long Generation, QuestSupplyUnitReceipt[] Units, bool Ambiguous = false)
{
    public long? ObservingGeneration { get; init; }
    public QuestSupplyLine[] PersonalReceived { get; init; } = [];
    public QuestSupplyCollectionOutcome Outcome { get; init; } = QuestSupplyCollectionOutcome.Received;
}

public interface IQuestSupplyParcelOperations
{
    bool SupportsReceiveAll(string serverProfile);
    Task<QuestSupplyParcelReceipt> ReceiveAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation);
    Task<QuestSupplyParcelReceipt> ReconcileAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation);
}

public sealed class UnsupportedQuestSupplyParcelOperations : IQuestSupplyParcelOperations
{
    public static UnsupportedQuestSupplyParcelOperations Instance { get; } = new();
    public bool SupportsReceiveAll(string serverProfile) => false;
    public Task<QuestSupplyParcelReceipt> ReceiveAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation)
        => throw new QuestBlockedException("AislingExchange parcel collection needs protocol qualification.");
    public Task<QuestSupplyParcelReceipt> ReconcileAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation)
        => throw new QuestBlockedException("AislingExchange supplies await parcel reconciliation.");
}

public interface IQuestSupplyJourneyStore
{
    AislingExchangeSupplyJourney? Load(string character, Guid journeyId);
    AislingExchangeSupplyJourney[] PendingReturns(string character, string serverProfile) => [];
    Task SaveAsync(AislingExchangeSupplyJourney journey, CancellationToken cancellation = default);
}

public sealed record QuestSupplyReturnReserves(IReadOnlyDictionary<string, int> Items, uint Gold);

public sealed record AislingExchangeSupplyCheckpoint(QuestSupplySubmission Submission)
{
    public string State { get; init; } = "Submitted";
    public string? RequestId { get; init; }
    public QuestSupplyParcelUnit[] Units { get; init; } = [];
    public string[] ReceivedUnits { get; init; } = [];
    public QuestSupplyReceiptIntent? ReceiptIntent { get; init; }
    public QuestSupplyUnitReport[] ReceiptReports { get; init; } = [];
    public Guid[] ConfirmedReports { get; init; } = [];
    public bool Resolved { get; init; }
}

public sealed record QuestSupplyReturnSendCheckpoint(QuestSupplyReturnParcelIntent Intent,
    Dictionary<string, int> InventoryBaseline, uint GoldBefore);

public sealed record QuestSupplyReturnSourceReceipt(string UnitId, string Item, string AttemptId,
    long Generation, uint GoldBefore, uint GoldAfter, uint ActualFeeGold)
{
    public QuestSupplyReturnParcelIntent? Intent { get; init; }
    public QuestParcelState? SourceBefore { get; init; }
    public QuestParcelState? SourceAfter { get; init; }
    public long? SourceWriteRevision { get; init; }
    public byte? SelectedSlot { get; init; }
    public bool CleanupPending { get; init; }
}

public sealed record AislingExchangeReturnCheckpoint(QuestSupplyReturnSubmission Submission)
{
    public string? OperationId { get; init; }
    public string State { get; init; } = "unsubmitted";
    public QuestSupplyParcelUnit[] Units { get; init; } = [];
    public string[] SentUnits { get; init; } = [];
    public QuestSupplyReturnSourceReceipt[] SourceReceipts { get; init; } = [];
    public QuestSupplyReturnSendCheckpoint? PendingSend { get; init; }
    public QuestSupplyUnitReport[] SentReports { get; init; } = [];
    public Guid[] ConfirmedReports { get; init; } = [];
    public bool Completed { get; init; }
}

public sealed record AislingExchangeSupplyJourney(Guid Id, string Character, string ServerProfile, Dictionary<string, int> PersonalBaseline)
{
    public Dictionary<string, int> LastInventory { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ProviderReceived { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> JourneyAcquired { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Consumed { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Reserved { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Returned { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public AislingExchangeSupplyCheckpoint[] Operations { get; init; } = [];
    public QuestSupplyOffer? AwaitingConsent { get; init; }
    public Dictionary<string, int>? OfferedInventory { get; init; }
    public QuestSupplyLine[]? DeclinedDemand { get; init; }
    public string[] ConsideredPreparations { get; init; } = [];
    public long Generation { get; init; }
    public QuestSupplyReturnLine[] ReturnManifest { get; init; } = [];
    public AislingExchangeReturnCheckpoint[] ReturnOperations { get; init; } = [];
    public string ReturnStatus { get; init; } = "None";
    public bool AttributionUncertain { get; init; }
    public bool Ended { get; init; }
}
