using DAQuester.Core;
namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    // All online waits share one allocator so local characters do not reserve
    // the same Tavern tile. Actual server occupancy still decides availability.
    readonly FiskWaitingArea bouquetWaitingArea = new();
    bool HasReadyWorkDuringBouquet(string name) => QueueRunning(name) && Queue(name).Quests.Any(id => id != FiskEvidence.Id && Availability(name, id).Ready);
    async Task WaitForBouquetQueueAsync(string name, DateTimeOffset due, CancellationToken ct)
    {
        // This callback parks an owned connected character while the queue retains
        // the paid continuation. It does not collect a Bouquet or create an outcome.
        if (!sessions.TryGetValue(name, out var session) || !session.Snapshot.Connected || Job(name, FiskEvidence.Id).FiskWait is not { Verified: true })
            throw new QuestBlockedException("Bouquet wait requires the saved acceptance and a fresh owned connection.");
        if (due > DateTimeOffset.UtcNow)
        {
            if (session.Snapshot.Map != FiskWaitingArea.Map)
                await session.ExecuteAsync(new("navigate", FiskWaitingArea.Map, new(10, 8), ExpectedGeneration: session.Snapshot.Generation), ct);
            await bouquetWaitingArea.ParkAsync(session, ct);
            var delay = due - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }
        bouquetWaitingArea.Release(session);
    }
}
