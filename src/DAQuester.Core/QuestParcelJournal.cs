using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DAQuester.Core;

public sealed class FileQuestParcelJournal(string directory) : IQuestParcelJournal
{
    static readonly ConcurrentDictionary<string, SemaphoreSlim> Owners = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, SemaphoreSlim> Writers = new(StringComparer.OrdinalIgnoreCase);
    static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 64 };
    readonly string root = Path.GetFullPath(directory);

    string PathFor(Guid operation)
    {
        if (operation == Guid.Empty) throw new ArgumentException("A parcel operation identity is required.", nameof(operation));
        return Path.Combine(root, operation.ToString("N") + ".json");
    }

    public async Task<IAsyncDisposable> AcquireAsync(Guid clientOperationId, CancellationToken cancellation)
    {
        var gate = Owners.GetOrAdd(PathFor(clientOperationId), _ => new(1, 1));
        await gate.WaitAsync(cancellation);
        return new Lease(gate);
    }

    public async Task<QuestParcelCheckpoint[]> LoadAsync(Guid clientOperationId, CancellationToken cancellation)
    {
        string path = PathFor(clientOperationId);
        if (!File.Exists(path)) return [];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 64 * 1024 * 1024) throw new InvalidDataException("The parcel journal exceeds its safe read limit.");
        var values = await JsonSerializer.DeserializeAsync<QuestParcelCheckpoint[]>(stream, Json, cancellation)
            ?? throw new InvalidDataException("The parcel journal is unreadable.");
        if (values.Any(c => c.Intent.ClientOperationId != clientOperationId || string.IsNullOrWhiteSpace(c.Intent.AttemptId)
            || c.Before.Character != c.Intent.Character || c.Before.Generation != c.Intent.Generation || c.Observations is null)
            || values.Select(c => c.Intent.AttemptId).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidDataException("The parcel journal identity does not match its operation.");
        return values;
    }

    public async Task SaveAsync(QuestParcelCheckpoint checkpoint, CancellationToken cancellation)
    {
        string path = PathFor(checkpoint.Intent.ClientOperationId);
        var gate = Writers.GetOrAdd(path, _ => new(1, 1));
        await gate.WaitAsync(cancellation);
        try
        {
            var existing = (await LoadAsync(checkpoint.Intent.ClientOperationId, cancellation)).ToList();
            int index = existing.FindIndex(c => c.Intent.AttemptId == checkpoint.Intent.AttemptId);
            if (index < 0) existing.Add(checkpoint); else existing[index] = checkpoint;
            await AtomicJsonFile.WriteAsync(path, existing, "The parcel checkpoint could not be saved. No further parcel action was authorized.", cancellation);
        }
        finally { gate.Release(); }
    }

    sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        int released;
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref released, 1) == 0) gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
