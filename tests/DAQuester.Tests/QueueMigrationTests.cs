using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class QueueMigrationTests
{
    [Theory]
    [InlineData("Returning home")]
    [InlineData("Paused")]
    public async Task Retired_travel_phase_loads_idle_and_preserves_verified_history(string phase)
    {
        var (store, path) = WriteLegacyQueue(phase, activeQuest: null);
        var queue = store.Load("Synthetic");
        Assert.Equal("Idle", queue.State);
        Assert.Equal("Queue completed", queue.Reason);
        Assert.False(queue.Locked);
        Assert.Single(queue.History);
        Assert.Empty(queue.Quests);
        await store.SaveAsync(queue);
        using var saved = JsonDocument.Parse(File.ReadAllText(path));
        Assert.False(saved.RootElement.TryGetProperty("Home", out _));
        Assert.False(saved.RootElement.TryGetProperty("ReturnHome", out _));
        Assert.False(saved.RootElement.TryGetProperty("HomePending", out _));
    }

    [Fact]
    public void Retired_fields_do_not_discard_an_unfinished_quest_checkpoint()
    {
        var (store, _) = WriteLegacyQueue("Paused", activeQuest: "spare-a-stick");
        var queue = store.Load("Synthetic");
        Assert.Equal("Paused", queue.State);
        Assert.Equal("spare-a-stick", queue.ActiveQuest);
        Assert.True(queue.Locked);
        Assert.Equal(["spare-a-stick"], queue.Quests);
        Assert.Single(queue.History);
    }

    private static (FileQuestQueueStore Store, string Path) WriteLegacyQueue(string phase, string? activeQuest)
    {
        string directory = Path.Combine(Path.GetTempPath(), "DAQuester-queue-migration-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        string filename = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("SYNTHETIC"))) + ".json";
        string path = Path.Combine(directory, filename);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Character = "Synthetic",
            Quests = activeQuest is null ? Array.Empty<string>() : [activeQuest],
            State = phase,
            Reason = "Home arrival remains unconfirmed",
            ActiveQuest = activeQuest,
            Home = new
            {
                Map = 500,
                Position = new
                {
                    X = 20,
                    Y = 20
                },
                Label = "Mileth"
            },
            ReturnHome = true,
            HomePending = true,
            History = new[] { new QueueOutcome("mundane-birth", "Completed", DateTimeOffset.UtcNow, "Verified") }
        }));
        return (new FileQuestQueueStore(directory), path);
    }
}
