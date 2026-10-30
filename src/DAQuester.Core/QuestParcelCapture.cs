namespace DAQuester.Core;

public sealed record QuestParcelCaptureRequest(Guid OperationId, string Action, string Character,
    string ServerProfile, long ExpectedGeneration, uint ExpectedCharacterId,
    uint? ExpectedNpcId = null, long? ExpectedDialogRevision = null)
{
    public byte? ExpectedSlot { get; init; }
    public string? ExpectedItem { get; init; }
    public uint? ExpectedQuantity { get; init; }
    public uint? ExpectedGold { get; init; }
}

public sealed record QuestParcelCaptureResult(Guid OperationId, string Action, bool Reconciled,
    QuestParcelCheckpoint Checkpoint);

// Diagnostics collect evidence only. They do not qualify a production receipt,
// identify a parcel sender, or credit provider/worker inventory.
public interface IQuestParcelCaptureSession
{
    Task<QuestParcelCaptureResult> CaptureParcelAsync(QuestParcelCaptureRequest request,
        IQuestParcelJournal journal, CancellationToken cancellation);
}
