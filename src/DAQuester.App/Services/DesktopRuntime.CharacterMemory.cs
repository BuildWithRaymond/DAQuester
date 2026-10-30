using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    sealed record PendingCharacterMemorySave(string Character, CharacterSnapshot Snapshot);
    sealed class PendingCharacterMemorySources
    {
        public PendingCharacterMemorySave? Level, Class, Appearance, Legends;
        public IEnumerable<PendingCharacterMemorySave> Sources => new[] { Level, Class, Appearance, Legends }
            .OfType<PendingCharacterMemorySave>().Distinct();

        public void Remove(PendingCharacterMemorySave saved)
        {
            if (ReferenceEquals(Level, saved)) Level = null;
            if (ReferenceEquals(Class, saved)) Class = null;
            if (ReferenceEquals(Appearance, saved)) Appearance = null;
            if (ReferenceEquals(Legends, saved)) Legends = null;
        }
    }
    readonly Dictionary<string, PendingCharacterMemorySources> pendingCharacterMemorySaves = new(StringComparer.OrdinalIgnoreCase);
    readonly object characterMemorySaveGate = new();
    readonly SemaphoreSlim characterMemoryFlush = new(1, 1);
    Task? characterMemorySave;

    public CharacterMemory Memory(string name)
    {
        if (name.Length == 0)
            return new();
        try
        {
            return characterMemory.Load(name);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log("Saved character marks could not be read: " + e.Message);
            return new()
            {
                Character = name
            };
        }
    }

    void RememberCharacter(string name, GameSession session, CharacterSnapshot snapshot)
    {
        if (closing)
            return;
        var write = SaveCharacterMemoryAsync(name, session, snapshot);
        if (characterMemoryWrites.Add(write))
            _ = ForgetWriteAsync(write);
    }

    async Task ForgetWriteAsync(Task write)
    {
        try
        {
            await write;
        }
        finally
        {
            characterMemoryWrites.Remove(write);
        }
    }

    Task SaveCharacterMemoryAsync(string name, GameSession session, CharacterSnapshot snapshot)
    {
        if (sessions.GetValueOrDefault(name) != session || snapshot.Generation != session.Snapshot.Generation
            || !snapshot.Connected || snapshot.Id is null || !string.Equals(snapshot.Name, name, StringComparison.OrdinalIgnoreCase)
            || snapshot.Generation < 1 || snapshot.Revision < 1)
            return Task.CompletedTask;
        bool freshLegends = snapshot.LegendsKnown && snapshot.LegendsRevision > 0 && snapshot.LegendsRevision <= snapshot.Revision;
        if (snapshot.Level is not > 0 && snapshot.Class is null && snapshot.Appearance is null && !freshLegends)
            return Task.CompletedTask;
        // Keep at most four compact, original sources per character, rather
        // than a snapshot (and a waiting task) for every received packet. An
        // unknown profile on a later login must not erase earlier actual marks.
        var source = new PendingCharacterMemorySave(name, new()
        {
            Name = snapshot.Name, Id = snapshot.Id, Connected = snapshot.Connected,
            Generation = snapshot.Generation, Revision = snapshot.Revision,
            Level = snapshot.Level, Class = snapshot.Class, Appearance = snapshot.Appearance,
            LegendsKnown = snapshot.LegendsKnown, LegendsRevision = snapshot.LegendsRevision,
            Legends = snapshot.Legends, LegendEntries = snapshot.LegendEntries
        });
        lock (characterMemorySaveGate)
        {
            if (!pendingCharacterMemorySaves.TryGetValue(name, out var pending))
                pendingCharacterMemorySaves[name] = pending = new();
            if (snapshot.Level is > 0) KeepLatest(ref pending.Level, source);
            if (snapshot.Class is not null) KeepLatest(ref pending.Class, source);
            if (snapshot.Appearance is not null) KeepLatest(ref pending.Appearance, source);
            if (freshLegends)
                KeepLatest(ref pending.Legends, source);
            if (characterMemorySave is { IsCompleted: false }) return characterMemorySave;
            // Publish ownership before invoking the worker: a synchronous
            // completion or its notification can itself observe another packet.
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            characterMemorySave = completion.Task;
            _ = SaveCharacterMemoryCoreAsync(completion);
            return completion.Task;
        }
    }

    static void KeepLatest(ref PendingCharacterMemorySave? saved, PendingCharacterMemorySave source)
    {
        if (saved is null || source.Snapshot.Generation > saved.Snapshot.Generation
            || source.Snapshot.Generation == saved.Snapshot.Generation && source.Snapshot.Revision > saved.Snapshot.Revision)
            saved = source;
    }

    async Task SaveCharacterMemoryCoreAsync(TaskCompletionSource completion)
    {
        Exception? reportFailure = null;
        try
        {
            while (true)
            {
                await FlushCharacterMemorySavesAsync();
                lock (characterMemorySaveGate)
                {
                    // A new observation may have arrived after the flush went
                    // empty but before this continuation resumed. Keep its
                    // existing worker, or atomically release it before Notify.
                    if (pendingCharacterMemorySaves.Count > 0) continue;
                    if (ReferenceEquals(characterMemorySave, completion.Task)) characterMemorySave = null;
                    break;
                }
            }
            if (!closing) Notify();
        }
        catch (Exception e)
        {
            // Retain original sources without an automatic error retry loop.
            lock (characterMemorySaveGate)
                if (ReferenceEquals(characterMemorySave, completion.Task)) characterMemorySave = null;
            try { Log("Observed character marks could not be saved: " + e.Message); }
            catch (Exception error) { reportFailure = error; }
        }
        finally
        {
            lock (characterMemorySaveGate)
            {
                if (ReferenceEquals(characterMemorySave, completion.Task)) characterMemorySave = null;
                if (reportFailure is null) completion.TrySetResult();
                else completion.TrySetException(reportFailure);
            }
        }
    }

    async Task FlushCharacterMemorySavesAsync()
    {
        await characterMemoryFlush.WaitAsync();
        try
        {
            while (true)
            {
                PendingCharacterMemorySave? pending;
                lock (characterMemorySaveGate) pending = pendingCharacterMemorySaves.Values.SelectMany(value => value.Sources)
                    .OrderBy(value => value.Snapshot.Generation).ThenBy(value => value.Snapshot.Revision).FirstOrDefault();
                if (pending is null) return;
                await characterMemory.ObserveAsync(pending.Character, pending.Snapshot);
                lock (characterMemorySaveGate)
                {
                    var sources = pendingCharacterMemorySaves[pending.Character];
                    sources.Remove(pending);
                    if (!sources.Sources.Any()) pendingCharacterMemorySaves.Remove(pending.Character);
                }
            }
        }
        finally { characterMemoryFlush.Release(); }
    }

    async Task ReadOwnLegendAsync(string name, GameSession session)
    {
        if (Preview)
            return;
        try
        {
            var initial = session.Snapshot;
            if (!initial.InventoryKnown && !initial.LegendsKnown)
                await session.WaitAsync(s => s.InventoryKnown || s.LegendsKnown, initial.Revision, TimeSpan.FromSeconds(20), characterMemoryLifetime.Token);
            if (closing || shuttingDown
                || SchedulerReserves(name)
                || sessions.GetValueOrDefault(name) != session
                || !session.Snapshot.Connected
                || session.Snapshot.LegendsKnown
                || controlActions.ContainsKey(name)
                || QueueReserves(name)
                || RunnerBusy(name)
                || IsRunning(name)
                || calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted)
                return;
            await session.ExecuteAsync(new("inspect-self"), characterMemoryLifetime.Token);
        }
        catch (OperationCanceledException) when (characterMemoryLifetime.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Log(name + ": own legend refresh deferred: " + e.Message);
        }
    }

    public void RememberCharacter(string name)
    {
        if (name.Length == 0 || name.Equals(Settings.LastCharacter, StringComparison.OrdinalIgnoreCase))
            return;
        Settings.LastCharacter = name;
        try
        {
            var persisted = AppSettings.Load();
            persisted.LastCharacter = name;
            persisted.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log("Last selected character not saved: " + e.Message);
        }
    }
}
