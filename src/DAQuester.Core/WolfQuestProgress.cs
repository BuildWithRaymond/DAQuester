namespace DAQuester.Core;

// Dialogue acceptance, target-bound combat and final rewards have separate receipts.
public sealed record WolfQuestProgress
{
    public bool Briefed { get; init; }
    public Transaction? Acceptance { get; init; }
    public ServerNotice? ObjectiveReceipt { get; init; }
    public Dialog? LastPage { get; init; }
    public Dialog? SubmittedPage { get; init; }
    public long? HuntGeneration { get; init; }
    public long HuntRevision { get; init; }
    public OwnMonsterDefeat? Defeat { get; init; }
    public Transaction? TurnIn { get; init; }
    public long LegendBaselineRevision { get; init; }
    public ServerNotice[] RewardMessages { get; init; } = [];
    public Tile? Landing { get; init; }
    public Tile? Away { get; init; }
    public Tile? Interior { get; init; }
    public long? EntryGeneration { get; init; }
    public bool EntrySettled { get; init; }
}
