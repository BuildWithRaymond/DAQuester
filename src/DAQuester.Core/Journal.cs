using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DAQuester.Core;

public class FileQuestJournal(string directory) : IQuestJournal
{
    readonly SemaphoreSlim gate = new(1, 1);
    string PathFor(string character, string quest)
    {
        string key = character.ToUpperInvariant() + "|" + quest;
        string filename = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json";
        return Path.Combine(directory, filename);
    }

    public QuestJob Load(string character, string quest)
    {
        var path = PathFor(character, quest);
        if (!File.Exists(path))
            return new()
            {
                Character = character,
                Quest = quest
            };
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var job = JsonSerializer.Deserialize<QuestJob>(reader.ReadToEnd()) ?? throw new InvalidDataException("Unreadable quest journal.");
        return job.PermanentCompletion is not null ? job : job with
        {
            Status = "Paused",
            Reason = "Restored. Resume after fresh server state."
        };
    }

    public async Task SaveAsync(QuestJob job, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            await AtomicJsonFile.WriteAsync(PathFor(job.Character, job.Quest), job, $"Could not replace quest checkpoint for {job.Character}/{job.Quest}: file locked or write access denied. Progress remains in the previous checkpoint.", cancellation);
        }
        finally
        {
            gate.Release();
        }
    }
}
