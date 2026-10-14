using DAQuester.Core;

namespace DAQuester.App.Services;

// Execution always uses Load, which reads the real journal. Only player-facing
// captions use ReadForDisplay. Never publish a proposed save as a durable receipt.
internal sealed class PresentationJournal(IQuestJournal source) : IQuestJournal
{
    sealed record Entry(string Character, string Quest, QuestJob Job);
    readonly object gate = new();
    readonly Dictionary<(string Character, string Quest), Entry> display = [];
    static (string Character, string Quest) Key(string character, string quest) => (character.ToUpperInvariant(), quest);

    public QuestJob Load(string character, string quest) => source.Load(character, quest);

    internal QuestJob ReadForDisplay(string character, string quest)
    {
        lock (gate)
        {
            var key = Key(character, quest);
            if (!display.TryGetValue(key, out var entry))
                display[key] = entry = new(character, quest, source.Load(character, quest));
            return entry.Job;
        }
    }

    public async Task SaveAsync(QuestJob job, CancellationToken cancellation = default)
    {
        try
        {
            await source.SaveAsync(job, cancellation);
        }
        finally
        {
            // Even an uncertain write must cause the next caption to consult the
            // saved file. Neither success nor failure authorizes a predicted result.
            lock (gate)
                display.Remove(Key(job.Character, job.Quest));
        }
    }

    internal void InvalidateDisplay()
    {
        lock (gate)
            display.Clear();
    }

    internal async Task RefreshDisplayAsync()
    {
        KeyValuePair<(string Character, string Quest), Entry>[] captured;
        lock (gate)
            captured = display.ToArray();
        // External checkpoint changes are inspected away from the UI thread.
        // Do not hold the cache lock during disk I/O or block live saves on a scan.
        var refreshed = await Task.Run(() => captured.Select(pair => new Entry(
            pair.Value.Character, pair.Value.Quest, source.Load(pair.Value.Character, pair.Value.Quest))).ToArray()).ConfigureAwait(false);
        lock (gate)
            for (int index = 0; index < captured.Length; index++)
            {
                var pair = captured[index];
                // A save, explicit refresh or newer read wins over this older scan.
                // Entry identity prevents a late worker from restoring stale receipts.
                if (display.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value))
                    display[pair.Key] = refreshed[index];
            }
    }
}
