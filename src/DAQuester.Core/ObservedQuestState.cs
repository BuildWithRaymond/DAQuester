namespace DAQuester.Core;

// Text comes from retained server captures; IDs and menu selections are resolved live.
public sealed record ObservedQuestPage(string Speaker, string Text, string Kind, string? Choice = null)
{
    public bool Root
    {
        get; init;
    }
}
// A stage declares both captured dialogue and expected resource changes. A matching
// page alone cannot prove that the server accepted payment or delivered an item.
public sealed record ObservedQuestTask(string Id, string Kind, int Map = 0, string Npc = "", Tile? Approach = null)
{
    public string? Speech
    {
        get; init;
    }
    public ObservedQuestPage[] Pages { get; init; } = [];
    public InventoryGoal[] Inputs { get; init; } = [];
    public InventoryGoal[] Outputs { get; init; } = [];
    public uint Cost
    {
        get; init;
    }
    public uint Experience
    {
        get; init;
    }
    public string? Item
    {
        get; init;
    }
    public int Quantity { get; init; } = 1;
    public int WeightReserve { get; init; } = 1;
    public ushort Sprite
    {
        get; init;
    }
    public ushort Monster
    {
        get; init;
    }
    public string Evidence { get; init; } = "";
    public string Label { get; init; } = "";
    public string? ApproachStrategy
    {
        get; init;
    }
    public int? ArrivalMap
    {
        get; init;
    }
    public int[] ArrivalMaps { get; init; } = [];
    public Dictionary<int, Tile> ArrivalAway { get; init; } = [];
    public Tile? Away
    {
        get; init;
    }
}

public sealed record ObservedQuestProgress
{
    // Task/Page locate the next action; Baseline covers the entire stage, while
    // SubmittedPage identifies the one write-ahead action awaiting a receipt.
    public int Task
    {
        get; init;
    }
    public int Page
    {
        get; init;
    }
    public Transaction? Baseline
    {
        get; init;
    }
    public Dialog? LastReply
    {
        get; init;
    }
    public Dialog? SubmittedPage
    {
        get; init;
    }
    public long? BoundGeneration
    {
        get; init;
    }
    public uint? BoundEntity
    {
        get; init;
    }
    public string[] RewardMessages { get; init; } = [];
    // Reward proof is saved before terminal popup closure. Recovery must close
    // that popup without returning to the reward claim.
    public bool CleanupPending
    {
        get; init;
    }
    public string? Resource
    {
        get; init;
    }
    public int Quantity
    {
        get; init;
    }
    public uint QuotedGold
    {
        get; init;
    }
    public byte? EquipmentSlot
    {
        get; init;
    }
    public DateTimeOffset? RedirectObservedAt
    {
        get; init;
    }
    public NpcTravelCheckpoint? TravelRedirect
    {
        get; init;
    }
    public NpcTravelCheckpoint? UnconfirmedTravel
    {
        get; init;
    }
}

public sealed record ObservedJourneyVariant(string TaskId, int Page, string Speaker, string Text)
{
    public string[] Choices { get; init; } = [];
    public ObservedQuestTask[] Journey { get; init; } = [];
}

public sealed record DanaanOptions(string Avatar, bool SuppliedMoldRiteAccepted)
{
    public void Validate()
    {
        if (Avatar != "cail")
            throw new QuestBlockedException("Choose Donnan / Cail. Other Danaan routes still need server evidence.");
        if (!SuppliedMoldRiteAccepted)
            throw new QuestBlockedException("Read and accept Greim's supplied-Mold rite warning before starting Danaan.");
    }
}

public sealed record FiskBouquetWait(DateTimeOffset AcceptedAt, Dialog Acceptance, uint GoldBefore, uint GoldAfter, bool FlowersConsumed)
{
    // Retain the original paid acceptance across later "not ready" replies.
    // Retries may move NextRun, but must not reset this receipt or its first due time.
    public DateTimeOffset Due => AcceptedAt.AddHours(1);
    public bool Verified => Acceptance.Name == "Viveka" && Acceptance.IsPursuit &&
        Acceptance.Text == FiskEvidence.BouquetAccepted && Acceptance.ObservedAt == AcceptedAt &&
        Acceptance.Generation > 0 && Acceptance.Revision > 0 && FlowersConsumed &&
        GoldBefore >= 20000 && GoldAfter == GoldBefore - 20000;
}

// An online continuation releases the queue to already-ready work. It is not a
// completed attempt, repeat cooldown, or permission to log out a player session.
public sealed class QuestDeferredWaitException(DateTimeOffset due) : Exception
{
    public DateTimeOffset Due { get; } = due;
}
