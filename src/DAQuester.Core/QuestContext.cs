namespace DAQuester.Core;

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

// Shared session access and checkpoint writes for one running quest.
public class QuestContext(
    IGameSession session,
    IQuestJournal journal,
    Calibration calibration,
    QuestJob job,
    Action<QuestJob>? saved = null,
    IQuestHelpers? helpers = null,
    HuntingServices? hunting = null)
{
    // Scheduler owns logged-out waits; an ordinary queue can instead retain an
    // online paid continuation and yield only to work already ready for this character.
    public bool YieldScheduledWaits { get; init; }
    public bool DeferTimedWaits { get; init; }
    public Func<bool>? HasReadyQueuedWork { get; init; }
    public FiskWaitingArea WaitingArea { get; init; } = new();
    public Func<IReadOnlyList<(QuestDefinition Recipe, QuestJob Job)>>? PreparationBatch { get; init; }
    public uint SupplyGoldReserve { get; init; }
    public QuestSupplyIntegration? SupplyIntegration { get; init; }
    public IGameSession Session { get; } = session;
    public HuntingServices? Hunting { get; } = hunting;
    public Calibration Calibration { get; } = calibration;
    public IQuestHelpers? Helpers { get; } = helpers;
    public QuestJob Job { get; private set; } = job;
    // An exact research status can be replaced by a potion message between awaits.
    internal CharacterSnapshot? ResearchResultObservation { get; set; }

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
