using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DAQuester.Core;

public sealed class FileQuestQueueStore(string directory)
{
    readonly SemaphoreSlim gate = new(1, 1);
    string PathFor(string character)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(character);
        return Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(character.ToUpperInvariant()))) + ".json");
    }

    public QuestQueueState Load(string character)
    {
        var path = PathFor(character);
        if (!File.Exists(path))
            return new()
            {
                Character = character
            };
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(stream);
        var state = document.RootElement.Deserialize<QuestQueueState>() ?? throw new InvalidDataException("Unreadable character queue.");
        if (!string.Equals(state.Character, character, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Character queue identity does not match.");
        state = state.Normalize();
        // Older queues may be paused in a retired travel phase. Keep quest receipts
        // and unfinished checkpoints, but never resume that travel action.
        bool retiredTravel = state.State == "Returning home" || document.RootElement.TryGetProperty("HomePending", out var pending) && pending.ValueKind == JsonValueKind.True;
        if (retiredTravel && state.ActiveQuest is null)
            return state with
            {
                State = "Idle",
                Reason = state.Quests.Length == 0 ? "Queue completed" : "Ready to start"
            };
        if (state.State is "Running" or "Returning home")
        {
            return state with
            {
                State = "Paused",
                Reason = "Restored. Resume after fresh character state."
            };
        }
        return state;
    }

    public async Task SaveAsync(QuestQueueState state, CancellationToken cancellation = default)
    {
        var path = PathFor(state.Character);
        await gate.WaitAsync(cancellation);
        try
        {
            await AtomicJsonFile.WriteAsync(path, state.Normalize(), "Could not replace character queue checkpoint. Previous progress remains saved.", cancellation);
        }
        finally
        {
            gate.Release();
        }
    }
}
