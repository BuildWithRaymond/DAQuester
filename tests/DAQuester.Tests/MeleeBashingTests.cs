using System.Collections.Immutable;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;
namespace DAQuester.Tests;

public class MeleeBashingTests
{
    sealed class Scripted : IGameSession
    {
        public CharacterSnapshot Snapshot { get; private set; } = FoundationTests.Ready(99) with { Name = "Basher", Id = 1, Class = "Monk", AbilityLevel = 99, ToNextLevel = 0, Map = 300, MapName = "Training Ground", Position = new(5, 5), Motion = new(0, 0, DateTimeOffset.UtcNow, 0, false), Equipment = ImmutableDictionary<byte, string>.Empty.Add(1, "Wolf Claw") };
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
        public event Action<CharacterSnapshot>? Changed;
        public event Action<string>? Interrupted
        {
            add
            {
            }
            remove
            {
            }
        }
        public List<GameCommand> Commands { get; } = [];
        public Dictionary<string, double> CooldownOnUse { get; } = new(StringComparer.OrdinalIgnoreCase);
        public void Set(Func<CharacterSnapshot, CharacterSnapshot> change)
        {
            Snapshot = change(Snapshot) with
            {
                Revision = Snapshot.Revision + 1
            };
            Changed?.Invoke(Snapshot);
        }
        public Task ExecuteAsync(GameCommand c, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            Commands.Add(c);
            switch (c.Kind)
            {
                case "turn":
                    Set(s => s with { Motion = s.Motion! with { Direction = (byte)c.Amount!.Value } });
                    break;
                case "pathfind":
                    var here = Snapshot.Position!;
                    var step = here.X != c.Target!.X ? here with
                    {
                        X = here.X + Math.Sign(c.Target.X - here.X)
                    } : here with
                    {
                        Y = here.Y + Math.Sign(c.Target.Y - here.Y)
                    };
                    Set(s => s with { Position = step });
                    break;
                case "equip" when Snapshot.Inventory.TryGetValue(c.Slot!.Value, out var weapon):
                    Set(s => s with { Equipment = s.Equipment.SetItem(1, weapon.Name), Inventory = s.Inventory.Remove(c.Slot.Value) });
                    break;
                case "use-skill":
                    string name = SkillDatabase.BaseName(Snapshot.Skills[(byte)c.Amount!.Value]);
                    if (CooldownOnUse.TryGetValue(name, out var seconds))
                        Set(s => s with { SkillCooldowns = s.SkillCooldowns.SetItem(name, new(DateTimeOffset.UtcNow, seconds)) });
                    break;
            }

            return Task.CompletedTask;
        }
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken token) => Task.FromResult(Snapshot);
        public void Skills(params string[] names) => Set(s => s with { Skills = names.Select((n, i) => (Slot: (byte)(i + 1), Name: n)).ToImmutableDictionary(p => p.Slot, p => p.Name) });
        public void Monster(uint id, Tile at, ushort sprite = 100, byte kind = 0, string name = "") => Set(s => s with { Entities = s.Entities.SetItem(id, new Entity(id, name, at, kind != 1) { Sprite = sprite, Kind = kind, FirstSeen = DateTimeOffset.UtcNow.AddSeconds(-id) }) });
    }

    sealed class OpenMap(params Tile[] walls) : ICombatMap
    {
        public bool IsWalkable(Tile tile) => !walls.Contains(tile);
        public bool IsWarp(Tile tile) => false;
        public int PathLength(Tile from, Tile to) => Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
        public bool IsWall(Tile tile) => walls.Contains(tile);
        public (int Width, int Height)? Size => (20, 20);
    }

    static string[] Used(Scripted q) => q.Commands.Where(c => c.Kind == "use-skill").Select(c => SkillDatabase.BaseName(q.Snapshot.Skills[(byte)c.Amount!.Value])).ToArray();

    // The first tick refreshes (no experience yet); the next ones fight.
    static async Task Ticks(DynamicMeleeBashing b, int count = 2)
    {
        for (int i = 0; i < count; i++)
            await b.DoBashingAsync(default);
    }

    [Fact]
    public void Skillbook_is_bucketed_strongest_first_and_assails_by_slot()
    {
        var q = new Scripted();
        q.Skills("Clobber", "Dune Swipe 3", "Cyclone Kick", "Assail", "Rescue", "Strikedown 2", "Mantis Kick", "Ambush");
        var c = SkillClassifier.Classify(q.Snapshot);
        Assert.Equal(["Cyclone Kick", "Dune Swipe 3"], c.Surrounding.Select(s => s.Name));
        Assert.Equal(["Clobber", "Assail"], c.Assails.Select(s => s.Name));
        Assert.Equal(["Strikedown 2", "Mantis Kick"], c.Frontal.Select(s => s.Name));
        Assert.Equal("Ambush", Assert.Single(c.TeleportBehinds).Name);
        Assert.DoesNotContain(c.Melee.Concat(c.Buffs), s => s.Name == "Rescue");
        Assert.Null(SkillDatabase.Lookup("Cyclone Kick 2"));
        // Live skill names carry their level (Cerda, 2026-10-12).
        q.Skills("Mass Strike 11 (Lev:100/100)", "Pounce 9 (Lev:100/100)", "Kelberoth Strike (Lev:100/100)");
        c = SkillClassifier.Classify(q.Snapshot);
        Assert.Equal(("Mass Strike 11", "Pounce 9", "Kelberoth Strike"), (Assert.Single(c.Surrounding).Name, Assert.Single(c.GapClosers).Name, Assert.Single(c.Crashers).Name));
    }

    [Fact]
    public async Task Adjacent_monster_is_faced_assailed_and_hit_with_the_strongest_ready_skill()
    {
        var q = new Scripted();
        q.Skills("Dune Swipe 3", "Cyclone Kick");
        q.Monster(100, new(5, 6));
        var b = new DynamicMeleeBashing(q, new OpenMap(), new());
        await Ticks(b);
        Assert.Equal(new GameCommand("turn", Amount: 2), q.Commands.First(c => c.Kind == "turn"));
        // Like Talos, the first assail waits one second from the basher starting.
        Assert.DoesNotContain(q.Commands, c => c.Kind == "assail");
        await Task.Delay(1050);
        await Ticks(b, 1);
        Assert.Contains(q.Commands, c => c.Kind == "assail");
        Assert.Equal("Cyclone Kick", Used(q).First());
        Assert.Equal(100u, b.Target!.Id);
    }

    [Fact]
    public async Task A_skill_on_cooldown_is_skipped_for_the_next_one()
    {
        var q = new Scripted();
        q.Skills("Dune Swipe 3", "Cyclone Kick");
        q.Set(s => s with { SkillCooldowns = s.SkillCooldowns.SetItem("Cyclone Kick", new(DateTimeOffset.UtcNow, 10)) });
        q.Monster(100, new(5, 6));
        await Ticks(new DynamicMeleeBashing(q, new OpenMap(), new()));
        Assert.Equal(["Dune Swipe 3"], Used(q));
    }

    [Fact]
    public async Task Long_cooldown_area_skills_wait_for_a_crowd_unless_nothing_else_is_ready()
    {
        var q = new Scripted();
        q.Skills("Cyclone Kick", "Stab Twice");
        q.Set(s => s with { SkillCooldowns = s.SkillCooldowns.SetItem("Cyclone Kick", new(DateTimeOffset.UtcNow.AddMinutes(-1), 20)) });
        q.Monster(100, new(5, 6));
        await Ticks(new DynamicMeleeBashing(q, new OpenMap(), new()));
        Assert.Equal(["Stab Twice"], Used(q));

        q = new Scripted();
        q.Skills("Cyclone Kick");
        q.Set(s => s with { SkillCooldowns = s.SkillCooldowns.SetItem("Cyclone Kick", new(DateTimeOffset.UtcNow.AddMinutes(-1), 20)) });
        q.Monster(100, new(5, 6));
        await Ticks(new DynamicMeleeBashing(q, new OpenMap(), new()));
        Assert.Equal(["Cyclone Kick"], Used(q));
    }

    [Fact]
    public async Task A_distant_monster_is_chased_one_step_at_a_time()
    {
        var q = new Scripted();
        q.Skills("Cyclone Kick");
        q.Monster(100, new(5, 9));
        await Ticks(new DynamicMeleeBashing(q, new OpenMap(), new()));
        Assert.Equal(new GameCommand("pathfind", Target: new(5, 9)), q.Commands.First(c => c.Kind == "pathfind"));
        Assert.Empty(Used(q));
    }

    [Fact]
    public async Task Players_merchants_hidden_sprites_and_far_monsters_are_never_targets()
    {
        var q = new Scripted();
        q.Skills("Cyclone Kick");
        q.Set(s => s with { Entities = s.Entities.SetItem(7, new Entity(7, "Stranger", new(5, 6), true) { IsPlayer = true, Kind = 4 }) });
        q.Monster(8, new(6, 5), kind: 2, name: "Shopkeeper");
        q.Monster(9, new(4, 5), sprite: 676);
        q.Monster(10, new(5, 30));
        var b = new DynamicMeleeBashing(q, new OpenMap(), new());
        Assert.False(await b.DoBashingAsync(default));
        Assert.Empty(q.Commands);
    }

    [Fact]
    public async Task Priority_only_fights_just_the_listed_monsters()
    {
        var q = new Scripted();
        q.Skills("Cyclone Kick");
        q.Monster(100, new(5, 6), sprite: 200);
        q.Monster(101, new(5, 8), sprite: 300);
        var b = new DynamicMeleeBashing(q, new OpenMap(), new()
        {
            PriorityOnly = true,
            PrioritySprites = [300]
        });
        await Ticks(b);
        Assert.Equal(101u, b.Target!.Id);
    }

    [Fact]
    public async Task A_tagged_monster_is_rescued_back_first()
    {
        var q = new Scripted();
        q.Skills("Cyclone Kick", "Rescue");
        q.Monster(100, new(5, 6));
        var b = new DynamicMeleeBashing(q, new OpenMap(), new()
        {
            SkillIntervalMs = 0
        });
        await Ticks(b);
        q.Set(s => s with { CantAttackRevision = s.Revision + 1 });
        await Task.Delay(5);
        await Ticks(b, 1);
        Assert.Equal(["Cyclone Kick", "Rescue"], Used(q));
    }

    [Fact]
    public async Task A_monster_that_reflected_with_asgall_is_left_alone()
    {
        var q = new Scripted();
        q.Skills("Cyclone Kick");
        q.Monster(100, new(5, 6));
        var b = new DynamicMeleeBashing(q, new OpenMap(), new());
        await Ticks(b);
        q.Commands.Clear();
        q.Set(s => s with { AsgallReflectRevision = s.Revision + 1 });
        await Task.Delay(1100);
        await Ticks(b, 1);
        Assert.DoesNotContain(q.Commands, c => c.Kind is "use-skill" or "assail");
    }

    [Fact]
    public async Task Asleep_or_stunned_the_basher_does_nothing()
    {
        var q = new Scripted();
        q.Skills("Cyclone Kick");
        q.Monster(100, new(5, 6));
        q.Set(s => s with { Effects = s.Effects.SetItem(90, 3) });
        Assert.True(await new DynamicMeleeBashing(q, new OpenMap(), new()).DoBashingAsync(default));
        Assert.Empty(q.Commands);
    }

    [Fact]
    public async Task Skills_wait_out_the_monsters_step_window()
    {
        var q = new Scripted();
        q.Skills("Cyclone Kick");
        q.Monster(100, new(5, 6));
        q.Set(s => s with { Entities = s.Entities.SetItem(100, s.Entities[100] with { LastStep = DateTimeOffset.UtcNow.AddMilliseconds(-480) }) });
        await Ticks(new DynamicMeleeBashing(q, new OpenMap(), new()));
        Assert.Empty(Used(q));
    }

    // Cerda, 2026-10-12: Staff of Clarity in hand, druid claws in the bag.
    [Fact]
    public async Task A_staff_in_hand_is_swapped_for_the_best_melee_weapon_the_character_may_wield()
    {
        var q = new Scripted();
        q.Skills("Mass Strike 11 (Lev:100/100)");
        q.Set(s => s with { AbilityLevel = 95, Equipment = s.Equipment.SetItem(1, "Staff of Clarity"), Inventory = s.Inventory.SetItem(4, new(4, "Ancient Hy-brasyl Tonfa", 1)).SetItem(25, new(25, "Blackstar Night Claw", 1)).SetItem(30, new(30, "Two-Handed Gladius", 1)) });
        q.Monster(100, new(5, 6));
        await Ticks(new DynamicMeleeBashing(q, new OpenMap(), new()));
        Assert.Equal((byte?)25, Assert.Single(q.Commands, c => c.Kind == "equip").Slot);
        Assert.Equal("Blackstar Night Claw", q.Snapshot.Equipment[1]);
        Assert.Contains("Mass Strike 11", Used(q));
    }

    [Fact]
    public async Task With_no_usable_weapon_the_fight_stops_instead_of_retrying_forever()
    {
        var q = new Scripted();
        q.Skills("Mass Strike 11");
        q.Set(s => s with { Equipment = s.Equipment.SetItem(1, "Staff of Clarity"), Inventory = s.Inventory.SetItem(30, new(30, "Two-Handed Gladius", 1)) });
        q.Monster(100, new(5, 6));
        var b = new DynamicMeleeBashing(q, new OpenMap(), new());
        await b.DoBashingAsync(default);
        var e = await Assert.ThrowsAsync<QuestBlockedException>(() => b.DoBashingAsync(default));
        Assert.Contains("No usable melee weapon", e.Message);
        Assert.DoesNotContain(q.Commands, c => c.Kind == "equip");
    }

    [Fact]
    public void Teleport_behind_never_lands_on_a_wall()
    {
        Assert.Equal(new Tile(5, 9), BashingBase.TeleportLanding(new(5, 5), new(5, 8)));
        Assert.False(BashingBase.IsTeleportLandingSafe(new OpenMap(new Tile(5, 9)), new(5, 9)));
    }

    [Fact]
    public async Task With_nothing_in_reach_roaming_walks_random_reachable_waypoints()
    {
        var q = new Scripted();
        var walls = Enumerable.Range(0, 20).Select(x => new Tile(x, 10)).ToArray();
        var wander = new RandomWaypoints(q, new OpenMap(walls));
        await wander.Step(default);
        // (20 + 20) / 8 = 5 waypoints, none on a wall, all inside the border.
        Assert.InRange(wander.Waypoints.Count, 1, 5);
        Assert.All(wander.Waypoints, w => Assert.True(w.Y != 10 && w.X is >= 1 and <= 18 && w.Y is >= 1 and <= 18));
        await wander.Step(default);
        Assert.Equal(new GameCommand("pathfind", Target: wander.Waypoints[0]), Assert.Single(q.Commands));

        var fight = new Scripted();
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(fight, new OpenMap(), new(), null, TimeSpan.FromMinutes(2), stop.Token, roam: true));
        Assert.Contains(fight.Commands, c => c.Kind == "pathfind");
    }

    [Fact]
    public async Task A_fight_ends_when_nothing_is_left_in_reach()
    {
        var q = new Scripted();
        Assert.Equal(FightEnd.NoTargets, await MeleeFight.RunAsync(q, new OpenMap(), new(), null, TimeSpan.FromMilliseconds(50), default));
        q.Monster(100, new(5, 6));
        Assert.Equal(FightEnd.GoalReached, await MeleeFight.RunAsync(q, new OpenMap(), new(), s => s.Entities.Count > 0, TimeSpan.FromSeconds(5), default));
    }

    static byte[] Packet<T>(ServerCommand command, T message) where T : ServerMessage
    {
        var b = new NetworkPacketBuilder(command);
        try
        {
            message.Serialize(ref b);
            return b.ToPacket().Data;
        }
        finally
        {
            b.Dispose();
        }
    }

    static StateTracker Tracker()
    {
        var t = new StateTracker(1);
        t.Identity("Basher", 1);
        t.Observe((byte)ServerCommand.MapSize, Packet(ServerCommand.MapSize, new ServerMapSizeMessage { MapId = 300, Width = 20, Height = 20, Name = "Training Ground" }));
        t.Observe((byte)ServerCommand.AddSkill, Packet(ServerCommand.AddSkill, new ServerAddSkillMessage { Slot = 4, Name = "Cyclone Kick (Lev:100/100)" }));
        t.Observe((byte)ServerCommand.DrawObjects, Packet(ServerCommand.DrawObjects, new ServerDrawObjectsMessage { Entities = [new ServerCreatureEntity { Id = 100, X = 5, Y = 6, Sprite = 120, Direction = WorldDirection.Up, CreatureType = CreatureType.Monster }] }));
        return t;
    }

    [Fact]
    public void Combat_packets_fill_the_snapshot()
    {
        var t = Tracker();
        var monster = t.Snapshot.Entities[100];
        Assert.Equal((120, 0, 100), ((int)monster.Sprite, (int)monster.Kind, (int)monster.HealthPercent));
        t.Observe((byte)ServerCommand.ActionDelay, Packet(ServerCommand.ActionDelay, new ServerActionDelayMessage { AbilityType = AbilityType.Skill, Slot = 4, Seconds = 12 }));
        Assert.Equal(12, t.Snapshot.SkillCooldowns["cyclone kick"].Seconds);
        t.Observe((byte)ServerCommand.DamageEffect, Packet(ServerCommand.DamageEffect, new ServerDamageEffectMessage { EntityId = 100, Percent = 40 }));
        Assert.Equal(40, t.Snapshot.Entities[100].HealthPercent);
        t.Observe((byte)ServerCommand.MoveObject, Packet(ServerCommand.MoveObject, new ServerMoveObjectMessage { EntityId = 100, OriginX = 5, OriginY = 6, Direction = WorldDirection.Left }));
        Assert.Equal((new Tile(4, 6), (byte)3), (t.Snapshot.Entities[100].Position, t.Snapshot.Entities[100].Direction));
        Assert.True(DateTimeOffset.UtcNow - t.Snapshot.Entities[100].LastStep < TimeSpan.FromSeconds(5));
        t.Observe((byte)ServerCommand.Spelled, Packet(ServerCommand.Spelled, new ServerSpelledMessage { Icon = 90, Duration = StatusEffectDuration.Red }));
        Assert.True(t.Snapshot.Effects.ContainsKey(90));
        t.Observe((byte)ServerCommand.Spelled, Packet(ServerCommand.Spelled, new ServerSpelledMessage { Icon = 90, Duration = StatusEffectDuration.None }));
        Assert.False(t.Snapshot.Effects.ContainsKey(90));
        t.Observe((byte)ServerCommand.EffectLayer, Packet(ServerCommand.EffectLayer, new ServerEffectLayerMessage { TargetId = 1, SourceId = 1, TargetAnimation = 268, SourceAnimation = 268, AnimationDuration = 100 }));
        Assert.Equal(t.Snapshot.Revision, t.Snapshot.AsgallReflectRevision);
    }

    static byte[] Message(string text)
    {
        var b = new NetworkPacketBuilder(ServerCommand.Message);
        try
        {
            b.AppendByte(3);
            b.AppendString16(text);
            return b.ToPacket().Data;
        }
        finally
        {
            b.Dispose();
        }
    }

    [Fact]
    public void Combat_replies_are_recorded_and_a_no_skills_map_clears_on_leaving()
    {
        var t = Tracker();
        t.Observe(0x0A, Message("You can't use skills here."));
        Assert.True(t.Snapshot.SkillsBlockedOnMap);
        t.Observe(0x0A, Message("You can't attack."));
        Assert.Equal(t.Snapshot.Revision, t.Snapshot.CantAttackRevision);
        t.Observe(0x0A, Message("You can't use the weapon when holding it."));
        Assert.Equal(t.Snapshot.Revision, t.Snapshot.SkillRefusedRevision);
        t.Observe((byte)ServerCommand.MapSize, Packet(ServerCommand.MapSize, new ServerMapSizeMessage { MapId = 301, Width = 20, Height = 20, Name = "Elsewhere" }));
        Assert.False(t.Snapshot.SkillsBlockedOnMap);
    }

    [Fact]
    public void An_undecodable_combat_packet_is_skipped_without_stopping_anything()
    {
        var t = Tracker();
        long before = t.Snapshot.Revision;
        t.Observe((byte)ServerCommand.ActionDelay, [1]);
        Assert.Equal(before, t.Snapshot.Revision);
    }
}
