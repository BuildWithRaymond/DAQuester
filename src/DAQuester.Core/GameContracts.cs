namespace DAQuester.Core;

public record InventoryGoal(string Item, int Count);

// Optional guards bind a command to the connection and page the caller inspected.
// Omitted guards retain existing command behavior; discovery selections require them.
public record GameCommand(
    string Kind,
    int? Map = null,
    Tile? Target = null,
    string? Text = null,
    uint? Amount = null,
    Dialog? Dialog = null,
    byte? Slot = null,
    uint? EntityId = null,
    InventoryGoal? StopWhen = null,
    string? ExpectedResource = null,
    bool PreservePopup = false,
    int Tolerance = 0,
    string? ReplyFrom = null,
    int? Quantity = null,
    long? ExpectedGeneration = null,
    long? ExpectedDialogRevision = null,
    Tile? InwardTarget = null,
    bool ReadNext = false,
    int MaxPages = 12,
    string? ExpectedPageText = null,
    TimeSpan? ReplyTimeout = null,
    string? StopAtVisibleNpc = null,
    bool ReturnOnHealthLoss = false);

// Exposes observed character state and server acknowledgements for one connection.
public interface IGameSession
{
    CharacterSnapshot Snapshot
    {
        get;
    }

    IReadOnlySet<string> Capabilities
    {
        get;
    }

    event Action<CharacterSnapshot>? Changed;
    // Connection or protocol failure; ordinary player input does not raise this event.
    event Action<string>? Interrupted;
    Task ExecuteAsync(GameCommand command, CancellationToken cancellation);
    Task<CharacterSnapshot> WaitAsync(
        Func<CharacterSnapshot, bool> predicate,
        long after,
        TimeSpan timeout,
        CancellationToken cancellation);
}

public sealed record NpcTravelCheckpoint(
    long Generation,
    int Map,
    string Character,
    string Npc,
    DateTimeOffset ObservedAt)
{
    public long Revision { get; init; }
}

public interface INpcConnectionHandoff
{
    // A submitted travel destination is a reconciliation receipt, not permission
    // to repeat the NPC choice when the transport changes connections.
    NpcTravelCheckpoint? SubmittedNpcTravel { get; }
}
