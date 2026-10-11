using System.Reflection;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;
public sealed class QueueOwnershipTests
{
    [Fact]
    public async Task Queue_interruption_and_stop_preserve_ownership_and_pending_edits()
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var runtime = new DesktopRuntime(preview: true);
        try
        {
            await Boundary(runtime, false);
            await Boundary(runtime, true);
            await FailedSaveStop(runtime);
            await PendingEditStop(runtime);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    static T Field<T>(DesktopRuntime runtime, string name) => (T)typeof(DesktopRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
    static FileQuestQueueStore Store() => new(Path.Combine(AppSettings.DataPath, "queue-checks", Guid.NewGuid().ToString("N")));
    static async Task Boundary(DesktopRuntime runtime, bool disconnected)
    {
        var session = new SyntheticSession("QueueBoundary");
        int attempts = 0;
        var queue = new QuestQueueCoordinator(Store(), new() { Character = "QueueBoundary", Quests = ["first", "second"] }, () => session.Snapshot, (id, _, _) =>
        {
            attempts++;
            return Task.FromResult(new QuestJob { Character = "QueueBoundary", Quest = id, Step = "complete", Status = "Completed", CompletedAt = DateTimeOffset.UtcNow });
        }, (_, _) => throw new InvalidOperationException("Synthetic queue cannot travel"));
        queue.Changed += state =>
        {
            if (state.Quests.Length == 1 && state.ActiveQuest is null)
            {
                if (disconnected)
                    session.Disconnect();
                else
                    session.Interrupt();
            }
        };
        await runtime.RunQueueCoordinatorAsync(queue, session);
        if (attempts != 1 || !queue.State.Quests.SequenceEqual(new[] { "second" }) || queue.State.State != "Paused")
            throw new InvalidOperationException((disconnected ? "Disconnect" : "Manual input") + " between entries must pause the whole queue.");
    }

    static async Task FailedSaveStop(DesktopRuntime runtime)
    {
        var owners = Field<Dictionary<string, QuestQueueCoordinator>>(runtime, "queueCoordinators");
        const string first = "QueueBlockedSave", second = "QueueOtherOwner";
        string directory = Path.Combine(AppSettings.DataPath, "queue-checks", Guid.NewGuid().ToString("N"));
        var startedA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        QuestQueueCoordinator Create(string name, FileQuestQueueStore store, TaskCompletionSource started) => new(store, new() { Character = name, Quests = ["fixture"] }, () => new() { Name = name, Connected = true, Generation = 1 }, async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new();
        }, (_, _) => throw new InvalidOperationException("Synthetic queue cannot travel"));
        var a = Create(first, new(directory), startedA);
        var b = Create(second, Store(), startedB);
        owners.Add(first, a);
        owners.Add(second, b);
        Task runA = a.RunAsync(), runB = b.RunAsync();
        await Task.WhenAll(startedA.Task, startedB.Task);
        using var locked = new FileStream(Directory.GetFiles(directory, "*.json").Single(), FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            Exception? failure = null;
            try
            {
                await runtime.StopOwnersAsync();
            }
            catch (Exception error)
            {
                failure = error;
            }

            if (failure is null)
                throw new InvalidOperationException("A failed checkpoint save must be reported.");
            if (a.IsRunning || b.IsRunning)
                throw new InvalidOperationException("Stop must cancel every owner despite a failed checkpoint save.");
        }
        finally
        {
            locked.Dispose();
            await Task.WhenAll(a.PauseAsync(stop: true), b.PauseAsync(stop: true));
            try
            {
                await Task.WhenAll(runA, runB);
            }
            catch (IOException)
            {
            }

            owners.Remove(first);
            owners.Remove(second);
        }
    }

    static async Task PendingEditStop(DesktopRuntime runtime)
    {
        const string name = "QueuePendingEdit";
        var owners = Field<Dictionary<string, QuestQueueCoordinator>>(runtime, "queueCoordinators");
        var saving = Field<HashSet<string>>(runtime, "queueSaving");
        var cache = Field<Dictionary<string, QuestQueueState>>(runtime, "queueCache");
        var store = Field<FileQuestQueueStore>(runtime, "queueStore");
        var old = new QuestQueueState
        {
            Character = name,
            Quests = ["mundane-birth", "shinewood-faerie"]
        };
        owners.Add(name, new(store, old, () => new() { Name = name, Connected = true, Generation = 1 }, (_, _, _) => throw new NotSupportedException(), (_, _) => throw new NotSupportedException()));
        saving.Add(name);
        Task? stop = null;
        try
        {
            stop = runtime.StopOwnersAsync();
            await Task.Yield();
            if (stop.IsCompleted || owners[name].State.Reason != old.Reason)
                throw new InvalidOperationException("Stop must wait for the pending queue edit before saving.");
            var next = old with
            {
                Quests = ["shinewood-faerie", "mundane-birth"],
                Home = new(1, new(2, 3), "Synthetic home")
            };
            await store.SaveAsync(next);
            cache[name] = next;
            owners.Remove(name);
            saving.Remove(name);
            await stop;
            var saved = store.Load(name);
            if (!saved.Quests.SequenceEqual(next.Quests) || saved.Home != next.Home)
                throw new InvalidOperationException("Stop must preserve the newly saved queue order and home.");
        }
        finally
        {
            saving.Remove(name);
            owners.Remove(name);
            if (stop is not null)
                await stop;
            cache.Remove(name);
        }
    }

    sealed class SyntheticSession(string name) : IGameSession
    {
        public CharacterSnapshot Snapshot { get; private set; } = new()
        {
            Name = name,
            Generation = 1,
            Connected = true
        };
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();

        public event Action<CharacterSnapshot>? Changed;
        public event Action<string>? Interrupted;
        public void Interrupt() => Interrupted?.Invoke("Manual movement paused automation.");
        public void Disconnect()
        {
            Snapshot = Snapshot with
            {
                Connected = false
            };
            Changed?.Invoke(Snapshot);
        }

        public Task ExecuteAsync(GameCommand command, CancellationToken cancellation) => throw new NotSupportedException("Synthetic checks never execute game commands.");
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation) => throw new NotSupportedException();
    }
}
