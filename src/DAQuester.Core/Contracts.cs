using System.Collections.Immutable;

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
    string? StopAtVisibleNpc = null);
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
    Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation);
}

public sealed record NpcTravelCheckpoint(long Generation, int Map, string Character, string Npc, DateTimeOffset ObservedAt)
{
    public long Revision { get; init; }
}
public interface INpcConnectionHandoff
{
    // A submitted travel destination is a reconciliation receipt, not permission
    // to repeat the NPC choice when the transport changes connections.
    NpcTravelCheckpoint? SubmittedNpcTravel { get; }
}

public record QuestStep(string Id, string Action, string? Next, Dictionary<string, string>? Arguments = null, Dictionary<string, string>? Transitions = null);
public record QuestDefinition(string Id, int Version, string Name, string Source, string[] Capabilities, QuestStep[] Steps, double CooldownHours = 8)
{
    public ObservedQuestTask[] Journey { get; init; } = [];
    public Dictionary<string, ObservedJourneyVariant> JourneyVariants { get; init; } = [];
    public QuestRequirement[] Requirements
    {
        get;
        init;
    } = [];
    public string[] OutcomeConditions
    {
        get;
        init;
    } = [];
    public bool Repeatable
    {
        get;
        init;
    } = true;
}

public record PatrolWaypoint(int Map, Tile Position);
public record Calibration
{
    public string ServerProfile
    {
        get;
        init;
    } = "datester-7.41";
    public bool Validated
    {
        get;
        init;
    }
    public string BranchName
    {
        get;
        init;
    } = "Branch";
    public int BranchWeight
    {
        get;
        init;
    } = 1;
    public int RewardWeightReserve
    {
        get;
        init;
    } = 5;
    public Tile[] Patrol
    {
        get;
        init;
    } = [];
    public PatrolWaypoint[] AdditionalPatrol
    {
        get;
        init;
    } = [];
    public string PatrolBasis
    {
        get;
        init;
    } = "Configured route; collection is proved by server inventory";
    public Tile[] ObservedDropPositions
    {
        get;
        init;
    } = [];
    public string[] Accepted
    {
        get;
        init;
    } = [];
    public string[] Completed
    {
        get;
        init;
    } = [];
    public string[] Refused
    {
        get;
        init;
    } = [];
    public string[] Cooldown
    {
        get;
        init;
    } = [];
    public string[] EvidenceFiles
    {
        get;
        init;
    } = [];
    public string[] ValidatedCharacters
    {
        get;
        init;
    } = [];
    public bool Ready => Validated && Patrol.Length > 0 && Accepted.Length > 0 && Completed.Length > 0 && EvidenceFiles.Length > 0;

    public bool Matches(string text, string[] patterns) => patterns.Any(p => p.Length > 0 && text.Equals(p, StringComparison.Ordinal));
}

public record Transaction(string Kind, long Generation, long Revision, uint Gold, int Branches, uint Experience, DateTimeOffset Started, bool TerminalSeen = false)
{
    public long? TerminalRevision
    {
        get;
        init;
    }
    public Dictionary<string, int> InventoryCounts
    {
        get;
        init;
    } = [];
    public bool InventoryBaselineKnown
    {
        get;
        init;
    }
    public uint? EntityId
    {
        get;
        init;
    }
    public byte? EntityType
    {
        get;
        init;
    }
    public string[] Legends
    {
        get;
        init;
    } = [];
    public string[] Spells
    {
        get;
        init;
    } = [];
    public bool SpellsBaselineKnown
    {
        get;
        init;
    }
}

public record HairstyleAttempt(DateTimeOffset CompletedAt, Dialog Completion, Dictionary<string, int> Rewards, bool LegendReward);
public record QuestJob
{
    public ObservedQuestProgress? Route { get; init; }
    public string? JourneyVariant { get; init; }
    public DanaanOptions? DanaanOptions { get; init; }
    public DanaanCourtReceipt? CourtRecovery { get; init; }
    public DanaanVarunaReceipt? VarunaRecovery { get; init; }
    public DanaanBlanchefleurReceipt? BlanchefleurRecovery { get; init; }
    public DanaanReplyReceipt[] DanaanReplies { get; init; } = [];
    public DanaanDreamerReceipt? DreamerReturnRecovery { get; init; }
    public DanaanDonnanAssignment? DonnanAssignment { get; init; }
    public DachaidhPayment? DachaidhPayment { get; init; }
    public FiskBouquetWait? FiskWait { get; init; }
    public string Character
    {
        get;
        init;
    } = "";
    public string Quest
    {
        get;
        init;
    } = "spare-a-stick";
    public int Version
    {
        get;
        init;
    } = 1;
    public string Step
    {
        get;
        init;
    } = "preflight";
    public string Status
    {
        get;
        init;
    } = "Paused";
    public string Reason
    {
        get;
        init;
    } = "Ready";
    public bool Accepted
    {
        get;
        init;
    }
    public long? AcceptedGeneration
    {
        get;
        init;
    }
    public bool Repeat
    {
        get;
        init;
    }
    public DateTimeOffset? CompletedAt
    {
        get;
        init;
    }
    public DateTimeOffset? NextRun
    {
        get;
        init;
    }
    public PermanentCompletionEvidence? PermanentCompletion
    {
        get;
        init;
    }
    public Transaction? Pending
    {
        get;
        init;
    }
    public QuestPreparationState? Preparation { get; init; }
    public Dictionary<string, int> Rewards
    {
        get;
        init;
    } = [];
    public MundaneBirthOptions? BirthOptions
    {
        get;
        init;
    }
    public EmeraldDawnOptions? EmeraldOptions
    {
        get;
        init;
    }
    public bool EmeraldMioasSeen
    {
        get;
        init;
    }
    public bool EmeraldBergBriefed
    {
        get;
        init;
    }
    public bool EmeraldDruidSeen
    {
        get;
        init;
    }
    public EmeraldRecoveryReceipt? EmeraldRecovery
    {
        get;
        init;
    }
    public string[] FaerieGoals
    {
        get;
        init;
    } = [];
    public FaerieGoalReceipt[] FaerieReceipts
    {
        get;
        init;
    } = [];
    public string[] FaerieUnavailable
    {
        get;
        init;
    } = [];
    public Dialog? FaeriePetWait
    {
        get;
        init;
    }
    public string? FaerieCurrentGoal
    {
        get;
        init;
    }
    public Dialog? FaerieClosedDialog
    {
        get;
        init;
    }
    public long? FaerieWalkAcknowledgedGeneration
    {
        get;
        init;
    }
    public DateTimeOffset? FaerieWalkAcknowledgedAt
    {
        get;
        init;
    }
    public FaerieReturnReceipt? FaerieReturn
    {
        get;
        init;
    }
    public ServerNotice? BirthCompletion
    {
        get;
        init;
    }
    public string? CompletionText
    {
        get;
        init;
    }
    public Dialog? CompletionDialog
    {
        get;
        init;
    }
    public string? CompletionLegend
    {
        get;
        init;
    }
    public Dialog? HairstyleCompletion
    {
        get;
        init;
    }
    public bool? LegendReward
    {
        get;
        init;
    }
    public DateTimeOffset? LastAttemptAt
    {
        get;
        init;
    }
    public HairstyleAttempt[] HairstyleAttempts
    {
        get;
        init;
    } = [];
    public SickChildOptions? SickChildOptions
    {
        get; init;
    }
    public SickChildTurnIn[] SickChildTurnIns { get; init; } = [];
    public SickChildPick[] SickChildPicks { get; init; } = [];
    public AltarOffering[] AltarOfferings { get; init; } = [];
    public bool AltarNoMarkAccepted
    {
        get; init;
    }
    // The player read what Peasant's First Steps does before it first ran.
    public bool FirstStepsBriefed
    {
        get; init;
    }
    // The player confirmed Crypt Climb ends the Terror of the Alley / Garden window.
    public bool TerrorWindowAccepted
    {
        get; init;
    }
}

public interface IQuestJournal
{
    QuestJob Load(string character, string quest);
    Task SaveAsync(QuestJob job, CancellationToken cancellation = default);
}

public interface IQuestAction
{
    Task ExecuteAsync(QuestContext context, QuestStep step, CancellationToken cancellation);
}

public class QuestBlockedException(string reason) : Exception(reason);
public sealed class ResourceUnavailableException(string reason) : QuestBlockedException(reason);
// The server turned the character back at a staircase it is not yet allowed through.
public sealed class EntranceRefusedException(string serverText, Tile stairs) : QuestBlockedException(serverText)
{
    public Tile Stairs { get; } = stairs;
}

// Health ran low and the character walked out to safety.
public sealed class RetreatedException(string reason) : QuestBlockedException(reason);
public sealed class QuestPopupException(Dialog dialog, int? map) : QuestBlockedException("Collection paused for a popup; it has been preserved for review.")
{
    public Dialog Dialog { get; } = dialog;
    public int? Map { get; } = map;
}

public class QuestContext(IGameSession session, IQuestJournal journal, Calibration calibration, QuestJob job, Action<QuestJob>? saved = null, IQuestHelpers? helpers = null, HuntingServices? hunting = null)
{
    // Scheduler owns logged-out waits; an ordinary queue can instead retain an
    // online paid continuation and yield only to work already ready for this character.
    public bool YieldScheduledWaits { get; init; }
    public bool DeferTimedWaits { get; init; }
    public Func<bool>? HasReadyQueuedWork { get; init; }
    public FiskWaitingArea WaitingArea { get; init; } = new();
    public Func<IReadOnlyList<(QuestDefinition Recipe, QuestJob Job)>>? PreparationBatch { get; init; }
    public uint SupplyGoldReserve { get; init; }
    public IGameSession Session { get; } = session;
    public HuntingServices? Hunting { get; } = hunting;
    public Calibration Calibration { get; } = calibration;
    public IQuestHelpers? Helpers { get; } = helpers;
    public QuestJob Job { get; private set; } = job;

    public async Task SaveAsync(QuestJob value)
    {
        await journal.SaveAsync(value);
        Job = value;
        saved?.Invoke(value);
    }

    public async Task BeginAsync(string kind, uint? entityId = null, string? reason = null, Dialog? observedDialog = null)
    {
        var s = Session.Snapshot;
        if (observedDialog is { } d && (s.Dialog != d || d.Generation != s.Generation || d.Revision <= 0 || d.Revision > s.Revision || entityId != d.EntityId))
            throw new QuestBlockedException("Fresh observed popup required before saving its transaction.");
        await SaveAsync(Job with
        {
            Reason = reason ?? Job.Reason,
            CompletionDialog = observedDialog ?? Job.CompletionDialog,
            Pending = new(kind, s.Generation, observedDialog is { } known ? known.Revision - 1 : s.Revision, s.Gold ?? 0, s.Count(Calibration.BranchName), s.Experience ?? 0, DateTimeOffset.UtcNow)
            {
                EntityId = entityId,
                EntityType = observedDialog?.EntityType,
                Legends = s.Legends.ToArray(),
                Spells = s.Spells.Values.ToArray(),
                SpellsBaselineKnown = s.SpellsKnown,
                InventoryBaselineKnown = s.InventoryKnown,
                InventoryCounts = s.Inventory.Values.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => checked((int)g.Sum(i => (long)i.Quantity)), StringComparer.OrdinalIgnoreCase)
            }
        });
    }

    public void Gate(bool allowGold = false)
    {
        PermanentQuestCompletion.Check(Session.Snapshot);
        var e = StickEligibility.Check(Session.Snapshot, Job.CompletedAt, DateTimeOffset.UtcNow);
        if (e.Kind != EligibilityKind.Eligible && !(allowGold && e.Reason.StartsWith("Bank excess")))
            throw new QuestBlockedException(e.Reason);
    }
}
