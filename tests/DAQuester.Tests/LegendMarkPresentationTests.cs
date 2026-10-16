using System.Text.Json;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Serialization;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class LegendMarkPresentationTests
{
    [Fact]
    public void Own_self_look_retains_actual_icons_in_order_alongside_quest_text()
    {
        var tracker = new StateTracker(7);
        tracker.Identity("ResearchActor", 42);
        var message = new ServerSelfLookMessage { LegendMarks =
        [
            new ServerLegendMark { Icon = LegendMarkIcon.Wizard, Text = "First mark - Deoch 223, Summer" },
            new ServerLegendMark { Icon = LegendMarkIcon.Heart, Text = "Second mark" },
            new ServerLegendMark { Icon = LegendMarkIcon.None, Text = "Second mark" }
        ] };
        var builder = new NetworkPacketBuilder(ServerCommand.SelfLook);
        try
        {
            message.Serialize(ref builder);
            tracker.Observe(0x39, builder.ToPacket().Data);
        }
        finally { builder.Dispose(); }
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(tracker.Snapshot));
        var entries = json.RootElement.GetProperty("LegendEntries").EnumerateArray().ToArray();
        Assert.Equal(new[] { 3, 6, 8 }, entries.Select(e => e.GetProperty("Icon").GetInt32()));
        Assert.Equal(tracker.Snapshot.Legends, entries.Select(e => e.GetProperty("Text").GetString()));
    }

    [Fact]
    public async Task Icon_memory_survives_restart_and_rejects_stale_or_other_character_observations()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N"));
        CharacterSnapshot Snapshot(long generation, long revision, string name, int icon) => JsonSerializer.Deserialize<CharacterSnapshot>(
            JsonSerializer.Serialize(new { Name = name, Id = 42, Connected = true, Generation = generation, Revision = revision,
                LegendsKnown = true, LegendsRevision = revision, Legends = new[] { "Same mark" },
                LegendEntries = new[] { new { Text = "Same mark", Icon = icon } } }))!;
        var store = new FileCharacterMemory(directory, "test");
        await store.ObserveAsync("ResearchActor", Snapshot(7, 10, "ResearchActor", 6));
        await store.ObserveAsync("ResearchActor", Snapshot(6, 20, "ResearchActor", 1));
        await store.ObserveAsync("ResearchActor", Snapshot(8, 21, "OtherActor", 2));
        var restored = new FileCharacterMemory(directory, "test").Load("ResearchActor");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(restored));
        Assert.Equal(6, Assert.Single(json.RootElement.GetProperty("LegendEntries").EnumerateArray()).GetProperty("Icon").GetInt32());
        Assert.Equal("Same mark", Assert.Single(restored.Legends));
    }
}
