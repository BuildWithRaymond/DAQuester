using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class QuestParcelJournalTests
{
    static IQuestParcelJournal Create(string directory)
    {
        var type = typeof(IQuestParcelJournal).Assembly.GetType("DAQuester.Core.FileQuestParcelJournal");
        Assert.NotNull(type);
        return Assert.IsAssignableFrom<IQuestParcelJournal>(Activator.CreateInstance(type, directory));
    }

    [Fact]
    public async Task Durable_attempts_keep_the_original_inventory_and_observations_across_instances()
    {
        string directory = Path.Combine(Path.GetTempPath(), "quest-parcel-" + Guid.NewGuid().ToString("N"));
        try
        {
            var id = Guid.NewGuid();
            var state = new QuestParcelState("Synthetic", 7, 17, 5, 100, new(2, 3),
                ImmutableDictionary<byte, Item>.Empty.Add(4, new(4, "Mold", 3)), 5000, 3, 100, null);
            var checkpoint = new QuestParcelCheckpoint(new("receive-1", id, "unit-1", QuestParcelDirection.Receive,
                "Synthetic", "synthetic-profile", 17), state, 42, "Postmaster")
            {
                WriteIntent = true, WriteRevision = 5, Submitted = true,
                Observations = [new(state, new(17, 6, 3, "Synthetic parcel receipt", DateTimeOffset.UtcNow), DateTimeOffset.UtcNow)]
            };
            var first = Create(directory);
            await first.SaveAsync(checkpoint, default);
            var second = Create(directory);
            var restored = Assert.Single(await second.LoadAsync(id, default));
            Assert.Equal(checkpoint.Intent, restored.Intent);
            Assert.Equal(checkpoint.Before.Inventory[4], restored.Before.Inventory[4]);
            Assert.Equal(checkpoint.Observations[0].ActiveStatus, restored.Observations[0].ActiveStatus);
            Assert.True(restored.WriteIntent);
            Assert.Single(Directory.GetFiles(directory, "*.json"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Operation_ownership_is_shared_by_distinct_journal_instances()
    {
        string directory = Path.Combine(Path.GetTempPath(), "quest-parcel-" + Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid();
        var first = Create(directory);
        var second = Create(directory);
        var lease = await first.AcquireAsync(id, default);
        var waiting = second.AcquireAsync(id, default);
        Assert.False(waiting.IsCompleted);
        await lease.DisposeAsync();
        await using var next = await waiting;
    }
}
