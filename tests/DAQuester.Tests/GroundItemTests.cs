using System.Text.Json;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Serialization;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class GroundItemTests
{
    static void Observe(StateTracker t, ServerMessage m, ServerCommand code)
    {
        var b = new NetworkPacketBuilder(code);
        try
        {
            m.Serialize(ref b);
            t.Observe((byte)code, b.ToPacket().Data);
        }
        finally
        {
            b.Dispose();
        }
    }

    [Fact]
    public void Ground_sprite_and_location_survive_decode_and_removed_objects_and_maps_clear_them()
    {
        var t = new StateTracker(1);
        t.Identity("Aware", 42);
        Observe(t, new ServerMapSizeMessage { MapId = 504, Width = 100, Height = 100 }, ServerCommand.MapSize);
        Observe(t, new ServerDrawObjectsMessage { Entities = [new ServerItemEntity { Id = 77, X = 13, Y = 19, Sprite = 569 }, new ServerItemEntity { Id = 78, X = 14, Y = 19, Sprite = 2286 }] }, ServerCommand.DrawObjects);
        using (var json = JsonDocument.Parse(JsonSerializer.Serialize(t.Snapshot)))
        {
            Assert.True(json.RootElement.TryGetProperty("GroundItems", out var ground));
            Assert.Equal(569, ground.GetProperty("77").GetProperty("Sprite").GetInt32());
            Assert.Equal(13, ground.GetProperty("77").GetProperty("Position").GetProperty("X").GetInt32());
        }

        Observe(t, new ServerRemoveObjectsMessage { EntityId = 77 }, ServerCommand.RemoveObjects);
        using (var json = JsonDocument.Parse(JsonSerializer.Serialize(t.Snapshot)))
            Assert.False(json.RootElement.GetProperty("GroundItems").TryGetProperty("77", out _));
        Observe(t, new ServerMapSizeMessage { MapId = 306, Width = 15, Height = 15 }, ServerCommand.MapSize);
        using (var json = JsonDocument.Parse(JsonSerializer.Serialize(t.Snapshot)))
            Assert.Empty(json.RootElement.GetProperty("GroundItems").EnumerateObject());
    }
}
