using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DAQuester.Core;

public sealed class FileAislingExchangeSupplyStore(string directory) : IQuestSupplyJourneyStore
{
    readonly SemaphoreSlim gate = new(1, 1);

    string PathFor(string character, Guid journeyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(character);
        if (journeyId == Guid.Empty) throw new ArgumentException("A supply journey identity is required.", nameof(journeyId));
        string key = character.ToUpperInvariant() + "|" + journeyId.ToString("N");
        return Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json");
    }

    public AislingExchangeSupplyJourney? Load(string character, Guid journeyId)
    {
        string path = PathFor(character, journeyId);
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var value = JsonSerializer.Deserialize<AislingExchangeSupplyJourney>(stream)
            ?? throw new InvalidDataException("AislingExchange supply checkpoint is unreadable.");
        if (value.Id != journeyId || !value.Character.Equals(character, StringComparison.OrdinalIgnoreCase) ||
            value.PersonalBaseline is null || value.Operations is null || value.ReturnOperations is null || value.ReturnManifest is null
            || value.ReturnOperations.Any(operation => operation.Submission.JourneyId != journeyId
                || !operation.Submission.Character.Equals(character, StringComparison.OrdinalIgnoreCase)
                || operation.Submission.ServerProfile != value.ServerProfile)
            || value.Operations.Any(o => o.Submission.JourneyId != journeyId ||
                !o.Submission.Character.Equals(character, StringComparison.OrdinalIgnoreCase) || o.Submission.ServerProfile != value.ServerProfile))
            throw new InvalidDataException("AislingExchange supply checkpoint identity does not match.");
        return value;
    }

    public AislingExchangeSupplyJourney[] PendingReturns(string character, string serverProfile)
    {
        if (!Directory.Exists(directory)) return [];
        var pending = new List<AislingExchangeSupplyJourney>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024)
                throw new InvalidDataException("AislingExchange supply checkpoint exceeds its safe read limit.");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var identity = JsonSerializer.Deserialize<AislingExchangeSupplyJourney>(stream)
                ?? throw new InvalidDataException("AislingExchange supply checkpoint is unreadable.");
            if (!string.Equals(identity.Character, character, StringComparison.OrdinalIgnoreCase) || identity.ServerProfile != serverProfile) continue;
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(PathFor(character, identity.Id)), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("AislingExchange supply checkpoint filename does not match its identity.");
            var journey = Load(character, identity.Id)!;
            bool checkpointsPending = journey.ReturnOperations.Any(QuestSupplyIntegration.ReturnCheckpointPending);
            if (journey.Ended && journey.ReturnManifest.Length > 0 && (checkpointsPending
                || journey.ReturnOperations.Length == 0 && journey.ReturnStatus is not ("Complete" or "Rejected" or "Cancelled" or "Partial")))
                pending.Add(journey);
        }
        return pending.OrderBy(value => value.Id).ToArray();
    }

    public async Task SaveAsync(AislingExchangeSupplyJourney journey, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            await AtomicJsonFile.WriteAsync(PathFor(journey.Character, journey.Id), journey,
                "AislingExchange supply checkpoint could not be saved. Previous progress remains intact.", cancellation);
        }
        finally { gate.Release(); }
    }
}
