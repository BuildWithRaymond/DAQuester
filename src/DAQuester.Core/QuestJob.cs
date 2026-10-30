namespace DAQuester.Core;

// Resource baseline saved before an action whose outcome may need reconciliation.
public record Transaction(
    string Kind,
    long Generation,
    long Revision,
    uint Gold,
    int Branches,
    uint Experience,
    DateTimeOffset Started,
    bool TerminalSeen = false)
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

public record HairstyleAttempt(
    DateTimeOffset CompletedAt,
    Dialog Completion,
    Dictionary<string, int> Rewards,
    bool LegendReward);

// Durable progress for one character and quest, including submissions awaiting verified receipts.
public record QuestJob
{
    public Guid? SupplyJourneyId { get; init; }
    public WolfQuestProgress? Wolf { get; init; }
    public PermanentCompletionEvidence[] CalloughOutcomes { get; init; } = [];
    public StickCleanupProgress? StickCleanup { get; init; }
    public WizardyResearchOptions? ResearchOptions { get; init; }
    public WizardyResearchProgress? Research { get; init; }
    public ObservedQuestProgress? Route { get; init; }
    public string? JourneyVariant { get; init; }
    public DanaanOptions? DanaanOptions { get; init; }
    public DanaanCourtReceipt? CourtRecovery { get; init; }
    public DanaanVarunaReceipt? VarunaRecovery { get; init; }
    public DanaanRiteReceipt? RiteRecovery { get; init; }
    public DanaanBlanchefleurReceipt? BlanchefleurRecovery { get; init; }
    public DanaanReplyReceipt[] DanaanReplies { get; init; } = [];
    public DanaanDreamerReceipt? DreamerReturnRecovery { get; init; }
    public DanaanDonnanAssignment? DonnanAssignment { get; init; }
    public DachaidhPayment? DachaidhPayment { get; init; }
    public FiskBouquetWait? FiskWait { get; init; }
    public DaydreamerProgress? Daydreamer { get; init; }
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
    public bool AltarEligibilityConfirmed { get; init; }
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
