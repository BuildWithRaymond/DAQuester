using System.Collections.Immutable;

namespace DAQuester.Core;

public enum QuestParcelDirection { Receive, Send }
// Zero remains the legacy fixed quote, including genuinely free persisted intents.
public enum QuestParcelFeeMode { LegacyQuoted, ServerCalculated }
public enum QuestParcelOutcome { Pending, Received, Empty, InventoryFull, TooHeavy, Rejected, SourceAccepted, Unknown, NotSubmitted }
public enum QuestParcelReplySource { NpcDialog, ActiveStatus }

public sealed record QuestParcelRoute(int Map, string Npc, Tile Approach);
public readonly record struct QuestParcelInputStamp(long ManualOperation, long ManualBagOperation);

// Qualification is supplied by an exact-profile evidence policy. Source packet
// knowledge or a settings flag does not qualify receive/send behavior.
public interface IQuestParcelReceiptPolicy
{
    bool SupportsReceiveAll(string serverProfile);
    bool SupportsSend(string serverProfile, bool fromStack);
    // Optional historical observation. Server-calculated sends never price from it.
    uint? SendFeeGold(string serverProfile, string item);
    QuestParcelRoute? Route(string serverProfile);
    TimeSpan ReplyTimeout { get; }
    TimeSpan ReceiveSpacing { get; }
    int MaximumReceiveAttempts { get; }
    QuestParcelReply? Interpret(QuestParcelObservation observation, QuestParcelCheckpoint checkpoint);
}

public sealed class UnqualifiedQuestParcelReceiptPolicy : IQuestParcelReceiptPolicy
{
    public static UnqualifiedQuestParcelReceiptPolicy Instance { get; } = new();
    public bool SupportsReceiveAll(string serverProfile) => false;
    public bool SupportsSend(string serverProfile, bool fromStack) => false;
    public uint? SendFeeGold(string serverProfile, string item) => null;
    public QuestParcelRoute? Route(string serverProfile) => null;
    public TimeSpan ReplyTimeout => TimeSpan.FromSeconds(30);
    public TimeSpan ReceiveSpacing => TimeSpan.FromMilliseconds(1100);
    public int MaximumReceiveAttempts => 30;
    public QuestParcelReply? Interpret(QuestParcelObservation observation, QuestParcelCheckpoint checkpoint) => null;
}

public sealed record QuestParcelIntent(string AttemptId, Guid ClientOperationId, string UnitId,
    QuestParcelDirection Direction, string Character, string ServerProfile, long Generation,
    string? Item = null, int Quantity = 1, uint QuotedFeeGold = 0, uint ReservedGold = 0)
{
    public string CollectionId { get; init; } = "";
    public QuestParcelFeeMode FeeMode { get; init; }
}

public sealed record QuestParcelState(string Character, uint? Id, long Generation, long Revision,
    int? Map, Tile? Position, ImmutableDictionary<byte, Item> Inventory, uint? Gold,
    int? Weight, int? MaxWeight, Dialog? Dialog)
{
    public long GoldRevision { get; init; }
    public static QuestParcelState From(CharacterSnapshot snapshot) => new(snapshot.Name ?? "", snapshot.Id,
        snapshot.Generation, snapshot.Revision, snapshot.Map, snapshot.Position, snapshot.Inventory,
        snapshot.Gold, snapshot.Weight, snapshot.MaxWeight, snapshot.Dialog) { GoldRevision = snapshot.GoldRevision };
}

public sealed record QuestParcelObservation(QuestParcelState State, ServerNotice? ActiveStatus, DateTimeOffset At)
{
    public static QuestParcelObservation From(CharacterSnapshot snapshot) => new(QuestParcelState.From(snapshot),
        snapshot.ActiveStatus is { Type: 3 } notice && notice.Revision == snapshot.Revision ? notice : null, DateTimeOffset.UtcNow);
}

public sealed record QuestParcelReply(QuestParcelOutcome Outcome, QuestParcelReplySource Source,
    long Revision, string? Sender = null, string? Item = null, int Quantity = 0, uint? FeeGold = null);

public sealed record QuestParcelCheckpoint(QuestParcelIntent Intent, QuestParcelState Before, uint NpcId, string NpcName)
{
    public string Stage { get; init; } = "Prepared";
    public long WriteRevision { get; init; }
    public bool WriteIntent { get; init; }
    public bool Submitted { get; init; }
    public bool Uncertain { get; init; }
    public bool Interference { get; init; }
    public byte? SelectedSlot { get; init; }
    public string[] WrittenStages { get; init; } = [];
    public QuestParcelObservation[] Observations { get; init; } = [];
    public QuestParcelReply? Result { get; init; }
    public string? Failure { get; init; }
    public QuestParcelInputStamp? InputStamp { get; init; }
    public bool CleanupPending { get; init; }
}

public interface IQuestParcelJournal
{
    Task<IAsyncDisposable> AcquireAsync(Guid clientOperationId, CancellationToken cancellation);
    Task<QuestParcelCheckpoint[]> LoadAsync(Guid clientOperationId, CancellationToken cancellation);
    Task SaveAsync(QuestParcelCheckpoint checkpoint, CancellationToken cancellation);
}

public interface IQuestParcelSession
{
    QuestParcelInputStamp InputStamp => default;
    Task<QuestParcelCheckpoint> ReceiveParcelAsync(QuestParcelIntent intent, IQuestParcelJournal journal,
        IQuestParcelReceiptPolicy policy, CancellationToken cancellation);
    Task<QuestParcelCheckpoint> SendParcelAsync(QuestParcelIntent intent, IQuestParcelJournal journal,
        IQuestParcelReceiptPolicy policy, CancellationToken cancellation);
    // Read and journal observations only. Never opens an NPC, closes a popup or
    // repeats a receive, menu selection, quantity or recipient submission.
    Task<QuestParcelCheckpoint> ReconcileParcelAsync(QuestParcelCheckpoint checkpoint, IQuestParcelJournal journal,
        IQuestParcelReceiptPolicy policy, CancellationToken cancellation);
}

public sealed record QuestSupplyReturnParcelIntent(Guid ClientOperationId, string UnitId, string Character,
    string ServerProfile, long Generation, string Item, int Quantity = 1, uint QuotedFeeGold = 0, uint ReservedGold = 0)
{
    public QuestParcelFeeMode FeeMode { get; init; }
}

public sealed record QuestSupplyReturnParcelReceipt(string Character, long OriginalGeneration, long ObservingGeneration,
    string UnitId, string Item, bool SourceAccepted, bool Ambiguous, string AttemptId)
{
    public bool NotSubmitted { get; init; }
    public uint? ActualFeeGold { get; init; }
    public uint? GoldBefore { get; init; }
    public uint? GoldAfter { get; init; }
    public QuestParcelState? SourceBefore { get; init; }
    public QuestParcelState? SourceAfter { get; init; }
    public long? SourceWriteRevision { get; init; }
    public byte? SelectedSlot { get; init; }
    public bool CleanupPending { get; init; }
}

public interface IQuestSupplyReturnParcelOperations
{
    // Exact-profile source-receipt capability, independent of remembered postage.
    bool SupportsSend(string serverProfile) => false;
    uint? SendFeeGold(string serverProfile, string item) => null;
    Task<QuestSupplyReturnParcelReceipt> SendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation);
    Task<QuestSupplyReturnParcelReceipt> ReconcileSendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation);
}
