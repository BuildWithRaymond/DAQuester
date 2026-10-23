using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class OwnMonsterDefeatTests
{
    static void Observe(StateTracker tracker, ServerMessage message)
    {
        var opcode = ServerMessageFactory.Default.GetMessageCommand(message.GetType())!.Value;
        var packet = new NetworkPacketBuilder(opcode);
        try
        {
            message.Serialize(ref packet);
            tracker.Observe((byte)opcode, packet.ToPacket().Data);
        }
        finally { packet.Dispose(); }
    }

    static StateTracker Ready(long generation = 7, bool identity = true, bool map = true)
    {
        var tracker = new StateTracker(generation);
        if (identity)
            tracker.Identity("Hunter", 42);
        if (map)
            Observe(tracker, new ServerMapSizeMessage { MapId = 464, Width = 50, Height = 50, Name = "Wolf clearing" });
        return tracker;
    }

    static void Creature(StateTracker tracker, uint id = 900, ushort sprite = 2, CreatureType kind = CreatureType.Monster) =>
        Observe(tracker, new ServerDrawObjectsMessage { Entities = [new ServerCreatureEntity { Id = id, X = 6, Y = 6, Sprite = sprite, CreatureType = kind }] });

    static void Defeat(StateTracker tracker, uint target = 900, uint source = 42, ushort animation = 263) =>
        Observe(tracker, new ServerEffectLayerMessage { TargetId = target, SourceId = source, TargetAnimation = animation });

    [Theory]
    [InlineData(263)]
    [InlineData(278)]
    public void Own_killing_blow_records_exact_observed_monster_and_connection(ushort animation)
    {
        var tracker = Ready();
        Creature(tracker);
        var before = DateTimeOffset.UtcNow;
        Defeat(tracker, animation: animation);
        var receipt = Assert.Single(tracker.Snapshot.OwnMonsterDefeats);
        Assert.Equal(7, receipt.Generation);
        Assert.Equal(tracker.Snapshot.Revision, receipt.Revision);
        Assert.Equal(464, receipt.Map);
        Assert.Equal(900u, receipt.EntityId);
        Assert.Equal(2, receipt.Sprite);
        Assert.InRange(receipt.At, before, DateTimeOffset.UtcNow);
        Assert.Empty(tracker.Snapshot.Feedback);
    }

    [Theory]
    [InlineData(43, 263)]
    [InlineData(42, 268)]
    [InlineData(0, 278)]
    public void Other_sources_and_non_defeat_animations_do_not_prove_own_kill(uint source, ushort animation)
    {
        var tracker = Ready();
        Creature(tracker);
        Defeat(tracker, source: source, animation: animation);
        Assert.Empty(tracker.Snapshot.OwnMonsterDefeats);
    }

    [Theory]
    [InlineData(CreatureType.Passable)]
    [InlineData(CreatureType.Mundane)]
    [InlineData(CreatureType.Solid)]
    [InlineData(CreatureType.Aisling)]
    public void Non_monster_creatures_do_not_prove_quest_kills(CreatureType kind)
    {
        var tracker = Ready();
        Creature(tracker, kind: kind);
        Defeat(tracker);
        Assert.Empty(tracker.Snapshot.OwnMonsterDefeats);
    }

    [Fact]
    public void Player_disguised_as_monster_is_excluded()
    {
        var tracker = Ready();
        Observe(tracker, new ServerDrawHumanObjectsMessage { EntityId = 900, X = 6, Y = 6, MonsterSprite = 2, Name = "Other" });
        Defeat(tracker);
        Assert.True(tracker.Snapshot.Entities[900].IsPlayer);
        Assert.Empty(tracker.Snapshot.OwnMonsterDefeats);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Unknown_identity_or_map_cannot_prove_quest_kill(bool identity, bool map)
    {
        var tracker = Ready(identity: identity, map: map);
        Creature(tracker);
        Defeat(tracker);
        Assert.Empty(tracker.Snapshot.OwnMonsterDefeats);
    }

    [Fact]
    public void Unknown_target_dead_target_and_duplicate_effect_do_not_add_receipts()
    {
        var tracker = Ready();
        Defeat(tracker);
        Assert.Empty(tracker.Snapshot.OwnMonsterDefeats);
        Creature(tracker);
        Observe(tracker, new ServerDamageEffectMessage { EntityId = 900, Percent = 0 });
        Defeat(tracker);
        Assert.Empty(tracker.Snapshot.OwnMonsterDefeats);
        Creature(tracker, 901);
        Defeat(tracker, 901);
        Defeat(tracker, 901, animation: 278);
        Assert.Single(tracker.Snapshot.OwnMonsterDefeats);
    }

    [Fact]
    public void Identity_changes_disconnects_and_new_generations_clear_old_receipts()
    {
        var tracker = Ready();
        Creature(tracker);
        Defeat(tracker);
        tracker.Identity("Hunter", 42);
        Assert.Single(tracker.Snapshot.OwnMonsterDefeats);
        tracker.Identity("Other", 43);
        Assert.Empty(tracker.Snapshot.OwnMonsterDefeats);
        Creature(tracker, 901);
        Defeat(tracker, 901, 43);
        Assert.Single(tracker.Snapshot.OwnMonsterDefeats);
        tracker.Disconnect();
        Assert.Empty(tracker.Snapshot.OwnMonsterDefeats);
        var replacement = Ready(8);
        Creature(replacement);
        Defeat(replacement);
        Assert.Equal(8, Assert.Single(replacement.Snapshot.OwnMonsterDefeats).Generation);
    }

    [Fact]
    public void Defeat_history_keeps_only_the_latest_32_receipts()
    {
        var tracker = Ready();
        for (uint id = 900; id < 940; id++)
        {
            Creature(tracker, id);
            Defeat(tracker, id);
        }
        Assert.Equal(32, tracker.Snapshot.OwnMonsterDefeats.Length);
        Assert.Equal(908u, tracker.Snapshot.OwnMonsterDefeats[0].EntityId);
        Assert.Equal(939u, tracker.Snapshot.OwnMonsterDefeats[^1].EntityId);
    }
}
