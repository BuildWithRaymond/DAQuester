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
    internal sealed class Scripted : IGameSession
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
        public Dictionary<ushort, string> Names { get; } = new() { [333] = "Wolf's Teeth", [500] = "Wolf's Skin", [501] = "fior sal" };
        // Ground items whose name differs from their sprite's usual item (lookalikes).
        public Dictionary<uint, string> ItemNames { get; } = [];
        public List<(string Name, uint Quantity)> Drops { get; } = [];
        // Maps whose stairs turn the character back.
        public HashSet<int> RefuseMaps { get; } = [];
        // The server raises this stat instead of the one asked for.
        public Stat? WrongStat
        {
            get; set;
        }
        public bool RepairBroke
        {
            get; set;
        }
        public Action? OnWait
        {
            get; set;
        }
        public void Ground(uint id, ushort sprite, Tile at) => Set(s => s with { GroundItems = s.GroundItems.SetItem(id, new(id, sprite, at)) });
        static CharacterSnapshot Add(CharacterSnapshot s, string item, uint quantity, ushort sprite = 0)
        {
            if (s.Inventory.Values.FirstOrDefault(i => i.Name == item && i.Stackable) is { } stack)
                return s with
                {
                    Inventory = s.Inventory.SetItem(stack.Slot, stack with
                    {
                        Quantity = stack.Quantity + quantity
                    })
                };
            byte slot = Enumerable.Range(1, 59).Select(x => (byte)x).First(x => !s.Inventory.ContainsKey(x));
            return s with
            {
                Inventory = s.Inventory.SetItem(slot, new Item(slot, item, quantity) { Stackable = item != "fior sal", Sprite = sprite })
            };
        }
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
                case "navigate" when RefuseMaps.Contains(c.Map!.Value):
                    Set(s => s with { Position = new(12, 23) });
                    throw new EntranceRefusedException("You almost understand the secrets of this entrance.", new(11, 23));
                case "navigate":
                    Set(s => s with { Map = c.Map, Position = c.Target });
                    break;
                case "step":
                    Set(s => s with { Position = c.Target });
                    break;
                case "repair" when RepairBroke:
                    throw new ResourceUnavailableException("Skan won't repair: \"Come back when you have money.\"");
                case "repair":
                    Set(s => s with { Durability = s.Durability.Clear(), Gold = s.Gold - 100 });
                    break;
                case "raise-stat":
                    var raised = WrongStat ?? (Stat)c.Amount!.Value;
                    Set(s => s with
                    {
                        Stats = s.Stats! with
                        {
                            Str = (byte)(s.Stats.Str + (raised == Stat.Str ? 1 : 0)),
                            Int = (byte)(s.Stats.Int + (raised == Stat.Int ? 1 : 0)),
                            Wis = (byte)(s.Stats.Wis + (raised == Stat.Wis ? 1 : 0)),
                            Con = (byte)(s.Stats.Con + (raised == Stat.Con ? 1 : 0)),
                            Dex = (byte)(s.Stats.Dex + (raised == Stat.Dex ? 1 : 0)),
                            Unspent = (byte)(s.Stats.Unspent - 1)
                        }
                    });
                    break;
                case "equip" when Snapshot.Inventory.TryGetValue(c.Slot!.Value, out var weapon):
                    Set(s => s with { Equipment = s.Equipment.SetItem(1, weapon.Name), Inventory = s.Inventory.Remove(c.Slot.Value) });
                    break;
                case "loot":
                    // The top of a pile (the newest drop) comes up first.
                    if (Snapshot.GroundItems.Values.Where(g => g.Position == c.Target).OrderByDescending(g => g.Id).FirstOrDefault() is { } top)
                    {
                        if (top.Sprite == ItemLooter.GoldSprite)
                            Set(s => s with { Gold = s.Gold + 100, GroundItems = s.GroundItems.Remove(top.Id) });
                        else
                            Set(s => Add(s, ItemNames.GetValueOrDefault(top.Id) ?? Names[top.Sprite], 1, top.Sprite) with { GroundItems = s.GroundItems.Remove(top.Id) });
                    }

                    break;
                case "drop" when Snapshot.Inventory.TryGetValue(c.Slot!.Value, out var dropped):
                    Drops.Add((dropped.Name, c.Amount!.Value));
                    Set(s => s with { Inventory = dropped.Quantity > c.Amount ? s.Inventory.SetItem(dropped.Slot, dropped with { Quantity = dropped.Quantity - c.Amount.Value }) : s.Inventory.Remove(dropped.Slot) });
                    break;
                case "use-skill":
                    string name = SkillDatabase.BaseName(Snapshot.Skills[(byte)c.Amount!.Value]);
                    if (CooldownOnUse.TryGetValue(name, out var seconds))
                        Set(s => s with { SkillCooldowns = s.SkillCooldowns.SetItem(name, new(DateTimeOffset.UtcNow, seconds)) });
                    break;
            }

            return Task.CompletedTask;
        }
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken token)
        {
            OnWait?.Invoke();
            return Task.FromResult(Snapshot);
        }
        public void Skills(params string[] names) => Set(s => s with { Skills = names.Select((n, i) => (Slot: (byte)(i + 1), Name: n)).ToImmutableDictionary(p => p.Slot, p => p.Name) });
        public void Monster(uint id, Tile at, ushort sprite = 100, byte kind = 0, string name = "") => Set(s => s with { Entities = s.Entities.SetItem(id, new Entity(id, name, at, kind != 1) { Sprite = sprite, Kind = kind, FirstSeen = DateTimeOffset.UtcNow.AddSeconds(-id) }) });
    }

    sealed class OpenMap(params Tile[] walls) : ICombatMap
    {
        public Tile[] Warps { get; init; } = [];
        public bool IsWalkable(Tile tile) => !walls.Contains(tile);
        public bool IsWarp(Tile tile) => Warps.Contains(tile);
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

    static readonly Retreat mileth = new(500, new(90, 56), "Mileth");

    [Fact]
    public async Task Below_30_percent_health_the_fight_breaks_off_and_walks_out()
    {
        var q = new Scripted();
        q.Skills("Assail");
        q.Monster(10, new(5, 6));
        q.Set(s => s with { Hp = 290, MaxHp = 1000 });
        var walked = await Assert.ThrowsAsync<RetreatedException>(() => MeleeFight.RunAsync(q, new OpenMap(), new(), null, TimeSpan.FromMinutes(1), default, retreat: mileth));
        Assert.Equal("Basher's health fell to 29% in Training Ground, so Basher walked out to Mileth and stopped.", walked.Message);
        Assert.Equal(new GameCommand("navigate", 500, new(90, 56), Tolerance: 1), Assert.Single(q.Commands));

        var stay = new Scripted();
        stay.Set(s => s with { Hp = 290, MaxHp = 1000 });
        var e = await Assert.ThrowsAsync<QuestBlockedException>(() => MeleeFight.RunAsync(stay, new OpenMap(), new(), null, TimeSpan.FromMinutes(1), default));
        Assert.Equal("Basher's health fell to 29% in Training Ground, so the fight stopped.", e.Message);
        Assert.Empty(stay.Commands);
    }

    [Fact]
    public async Task At_30_percent_health_the_fight_goes_on_and_at_zero_it_stops()
    {
        var q = new Scripted();
        q.Set(s => s with { Hp = 300, MaxHp = 1000 });
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(q, new OpenMap(), new(), null, TimeSpan.FromMinutes(1), stop.Token, retreat: mileth));
        Assert.DoesNotContain(q.Commands, c => c.Kind == "navigate");

        q.Set(s => s with { Hp = 0 });
        var e = await Assert.ThrowsAsync<QuestBlockedException>(() => MeleeFight.RunAsync(q, new OpenMap(), new(), null, TimeSpan.FromMinutes(1), default, retreat: mileth));
        Assert.Equal("Basher died in Training Ground, so the fight stopped.", e.Message);
    }

    [Fact]
    public async Task The_fighter_never_refreshes_beside_stairs()
    {
        var beside = new Scripted();
        beside.Monster(10, new(5, 6));
        await Ticks(new DynamicMeleeBashing(beside, new OpenMap { Warps = [new(6, 4)] }, new()), 1);
        Assert.DoesNotContain(beside.Commands, c => c.Kind == "refresh");
        var open = new Scripted();
        open.Monster(10, new(5, 6));
        await Ticks(new DynamicMeleeBashing(open, new OpenMap(), new()), 1);
        Assert.Contains(open.Commands, c => c.Kind == "refresh");
    }

    [Fact]
    public async Task Lingering_beside_a_warp_moves_her_clear_and_drops_the_target_that_held_her_there()
    {
        var q = new Scripted();
        q.Monster(10, new(5, 6));
        var warp = new OpenMap { Warps = [new(4, 5)] };
        var notes = new List<string>();
        var basher = new DynamicMeleeBashing(q, warp, new()
        {
            WarpLinger = TimeSpan.FromSeconds(1),
            Note = notes.Add
        });
        await basher.DoBashingAsync(default);
        Assert.Equal(10u, basher.Target?.Id);
        Assert.DoesNotContain(notes, n => n.StartsWith("Moving off"));
        await Task.Delay(1100);
        Assert.True(await basher.DoBashingAsync(default));
        Assert.Equal(new GameCommand("pathfind", Target: new(6, 5)), q.Commands[^1]);
        Assert.Contains(notes, n => n.StartsWith("Moving off the warp side at (5,5)"));
        Assert.Null(basher.Target);
        Assert.False(BashingBase.BesideWarp(warp, BashingBase.SafeTileNear(warp, new(5, 5))!));
        // Once clear, the monster that held her there is skipped for a while.
        q.Set(s => s with { Position = new(8, 5) });
        await basher.DoBashingAsync(default);
        Assert.Null(basher.Target);
    }

    [Fact]
    public async Task A_monster_on_the_stairs_is_never_a_target_and_roaming_keeps_off_warp_sides()
    {
        var q = new Scripted();
        q.Monster(10, new(5, 8));
        var stairs = new OpenMap { Warps = [new(5, 8)] };
        Assert.False(await new DynamicMeleeBashing(q, stairs, new()).DoBashingAsync(default));
        var row = new OpenMap { Warps = Enumerable.Range(1, 18).Select(x => new Tile(x, 9)).ToArray() };
        var wander = new RandomWaypoints(new Scripted(), row);
        await wander.Step(default);
        Assert.NotEmpty(wander.Waypoints);
        Assert.All(wander.Waypoints, w => Assert.False(BashingBase.BesideWarp(row, w)));
    }

    [Fact]
    public async Task Leveling_that_starts_beside_stairs_and_hurt_moves_clear_then_rests_to_full_first()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 2, Position = new(12, 23), Level = 5, Hp = 400, MaxHp = 940 });
        q.OnWait = () => q.Set(s => s with { Hp = s.MaxHp });
        var stairs = new OpenMap { Warps = [new(11, 23)] };
        var leveling = new HuntingEngine(q, stairs, HuntingGrounds.MilethCrypt, new(), new(), _ => { }, mileth, new(), targetLevel: 6)
        {
            NewJudge = _ => Judged(5, 5),
            Fight = (goal, ct) =>
            {
                q.Set(s => s with { Level = 6 });
                return Task.FromResult(FightEnd.GoalReached);
            }
        };
        await leveling.RunAsync(default);
        var walks = q.Commands.Where(c => c.Kind == "navigate").ToArray();
        Assert.Equal(3, walks.Length);
        Assert.Equal(2, walks[0].Map);
        Assert.False(BashingBase.BesideWarp(stairs, walks[0].Target!));
        Assert.Equal(new GameCommand[] { new("navigate", 500, new(90, 56), Tolerance: 1), new("navigate", 1, new(13, 48)) }, walks[1..3]);
    }

    [Fact]
    public async Task The_survey_picks_up_what_her_kill_dropped_and_drops_it_again_but_not_her_own_drops()
    {
        var q = new Scripted();
        var notes = new List<string>();
        var survey = new DropSurvey(q, [], notes.Add);
        q.Changed += survey.Observe;
        survey.Observe(q.Snapshot);
        q.Monster(10, new(5, 7));
        q.Ground(900, 500, new(5, 7));
        q.Set(s => s with { Entities = s.Entities.Remove(10) });
        Assert.Equal(new[] { new Tile(5, 7) }, survey.Pending);
        Assert.True(await survey.Tick(default));
        Assert.Equal(new GameCommand("loot", Target: new(5, 7)), q.Commands[^1]);
        Assert.Contains(notes, n => n.StartsWith("Picked up Wolf's Skin x1 (sprite 500) at (5,7)"));
        Assert.True(await survey.Tick(default));
        Assert.Equal(new[] { ("Wolf's Skin", 1u) }, q.Drops);
        // Her own drop landing beside a fresh kill is not picked back up.
        q.Monster(11, new(5, 6));
        q.Set(s => s with { Entities = s.Entities.Remove(11) });
        q.Ground(901, 500, new(5, 5));
        Assert.Empty(survey.Pending);
        Assert.False(await survey.Tick(default));
    }

    [Fact]
    public async Task The_survey_leaves_a_pile_already_on_the_floor_alone()
    {
        var q = new Scripted();
        var notes = new List<string>();
        var survey = new DropSurvey(q, [], notes.Add);
        q.Changed += survey.Observe;
        survey.Observe(q.Snapshot);
        // Seen before the kill: never a drop.
        q.Ground(800, 500, new(5, 7));
        await Task.Delay(700);
        q.Monster(10, new(5, 7));
        q.Set(s => s with { Entities = s.Entities.Remove(10) });
        Assert.Empty(survey.Pending);

        // Five items at once beside a death is a pile, not a drop.
        q.Monster(11, new(6, 6));
        q.Set(s => s with
        {
            Entities = s.Entities.Remove(11),
            GroundItems = s.GroundItems.SetItems(Enumerable.Range(900, 5).Select(i => KeyValuePair.Create((uint)i, new GroundItem((uint)i, 243, new(6, 6)))))
        });
        Assert.Empty(survey.Pending);
        Assert.Contains(notes, n => n.StartsWith("5 items beside a death"));

        // Items coming into view far away don't count either.
        q.Monster(12, new(15, 15));
        q.Set(s => s with { Entities = s.Entities.Remove(12) });
        q.Ground(950, 500, new(15, 15));
        Assert.Empty(survey.Pending);
    }

    [Fact]
    public async Task The_survey_keeps_gold_and_named_items()
    {
        var q = new Scripted();
        var survey = new DropSurvey(q, ["wolf's skin"]);
        q.Changed += survey.Observe;
        survey.Observe(q.Snapshot);
        q.Monster(10, new(5, 6));
        q.Ground(900, 500, new(5, 6));
        q.Ground(901, ItemLooter.GoldSprite, new(5, 6));
        q.Set(s => s with { Entities = s.Entities.Remove(10) });
        while (await survey.Tick(default) && q.Commands.Count < 10)
        {
        }

        Assert.Equal(2, q.Commands.Count(c => c.Kind == "loot"));
        Assert.Empty(q.Drops);
        Assert.Equal(1099u, q.Snapshot.Gold);
        Assert.Equal(1, q.Snapshot.Count("Wolf's Skin"));
    }

    [Fact]
    public void The_best_path_raises_CON_to_8_WIS_to_8_CON_to_22_WIS_to_40_then_saves()
    {
        Assert.Equal(Stat.Con, StatPaths.Next(new(3, 3, 3, 3, 3, 2), StatPaths.Best));
        Assert.Equal(Stat.Wis, StatPaths.Next(new(3, 3, 3, 8, 3, 2), StatPaths.Best));
        Assert.Equal(Stat.Wis, StatPaths.Next(new(5, 3, 3, 11, 3, 2), StatPaths.Best));
        Assert.Equal(Stat.Con, StatPaths.Next(new(5, 3, 8, 11, 3, 2), StatPaths.Best));
        Assert.Equal(Stat.Wis, StatPaths.Next(new(5, 3, 8, 22, 3, 2), StatPaths.Best));
        Assert.Null(StatPaths.Next(new(5, 3, 40, 22, 3, 2), StatPaths.Best));
        // Strict: CON 8, WIS 8, CON 13, WIS 13, then nothing; STR stays at 3.
        Assert.Equal(Stat.Con, StatPaths.Next(new(3, 3, 3, 3, 3, 2), StatPaths.Strict));
        Assert.Equal(Stat.Wis, StatPaths.Next(new(3, 3, 3, 8, 3, 2), StatPaths.Strict));
        Assert.Equal(Stat.Con, StatPaths.Next(new(3, 3, 8, 8, 3, 2), StatPaths.Strict));
        Assert.Equal(Stat.Wis, StatPaths.Next(new(3, 3, 8, 13, 3, 2), StatPaths.Strict));
        Assert.Null(StatPaths.Next(new(3, 3, 13, 13, 3, 2), StatPaths.Strict));
    }

    [Fact]
    public async Task Points_are_spent_one_at_a_time_and_a_wrong_stat_stops_spending()
    {
        var q = new Scripted();
        q.Set(s => s with { Stats = new(5, 3, 3, 11, 3, 3) });
        var notes = new List<string>();
        await new StatSpender(q, StatPaths.Best, notes.Add).SpendAsync(default);
        Assert.Equal(new CharacterStats(5, 3, 6, 11, 3, 0), q.Snapshot.Stats);
        Assert.Equal(3, q.Commands.Count(c => c is { Kind: "raise-stat", Amount: 8 }));

        var done = new Scripted();
        done.Set(s => s with { Stats = new(5, 3, 40, 22, 3, 4) });
        await new StatSpender(done, StatPaths.Best, notes.Add).SpendAsync(default);
        Assert.Empty(done.Commands);
        Assert.Contains("Stat path done; 4 points saved for the class choice.", notes);

        var wrong = new Scripted { WrongStat = Stat.Str };
        wrong.Set(s => s with { Stats = new(5, 3, 3, 11, 3, 2) });
        var spender = new StatSpender(wrong, StatPaths.Best, notes.Add);
        await spender.SpendAsync(default);
        await spender.SpendAsync(default);
        Assert.True(spender.Stopped);
        Assert.Single(wrong.Commands, c => c.Kind == "raise-stat");
        Assert.Contains(notes, n => n.StartsWith("Stat spending stopped: raising Wis"));
    }

    [Fact]
    public async Task Cheap_1v1_kills_mark_the_floor_safe_and_leveling_stops_once_listed_floors_are_safe()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 1, Position = new(5, 5), Level = 5, Hp = 1000, MaxHp = 1000, Experience = 0 });
        var progress = new HuntingProgress();
        string report = "";
        int fights = 0;
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new(), progress, p => report = HuntingGrounds.MilethCrypt.Report("Basher", p), mileth, new()
        {
            SafeAfter = TimeSpan.Zero
        }, untilSafe: [1])
        {
            Fight = (goal, ct) =>
            {
                if (++fights == 1)
                    for (uint id = 20; id < 23; id++)
                    {
                        q.Monster(id, new(5, 6), sprite: 53);
                        q.Set(s => s with { Hp = s.Hp - 20u });
                        q.Set(s => s with { Entities = s.Entities.Remove(id) });
                        q.Set(s => s with { Experience = s.Experience + 100u });
                    }

                return Task.FromResult(FightEnd.NoTargets);
            }
        };
        await leveling.RunAsync(default);
        Assert.Equal(1, fights);
        Assert.Equal(5, progress.SafeAt[1]);
        Assert.Equal(3, progress.Fights["1@5"].Kills);
        Assert.Equal(2.0, progress.Fights["1@5"].AverageCost, 1);
        Assert.Contains("| 1 | - | - | - | 5 |", report);
        Assert.Contains("| 1 | 5 | 3 | 2.0% |", report);
    }

    [Fact]
    public async Task Two_walk_outs_from_a_floor_mark_it_too_hard_and_all_health_lost_there_is_counted()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 2, Position = new(20, 20), Level = 6, Hp = 1000, MaxHp = 1000 });
        var progress = new HuntingProgress { AllowedAt = { [2] = 6 } };
        int fights = 0;
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new(), progress, _ => { }, mileth, new(), targetLevel: 7)
        {
            NewJudge = _ => Judged(5, 5),
            Fight = (goal, ct) =>
            {
                if (++fights <= 2)
                {
                    // Worn down by hits that never belong to a kill, then walked out.
                    q.Set(s => s with { Hp = 600u });
                    q.Set(s => s with { Hp = 280u });
                    q.Set(s => s with { Map = 500, Position = new(90, 56), Hp = 1000u });
                    throw new RetreatedException("walked out");
                }

                q.Set(s => s with { Level = 7 });
                return Task.FromResult(FightEnd.GoalReached);
            }
        };
        await leveling.RunAsync(default);
        Assert.Equal(6, progress.TooHardAt[2]);
        Assert.Equal(2, progress.At(2, 6).WalkOuts);
        Assert.Equal(144.0, progress.At(2, 6).HealthLost, 1);
        Assert.Equal(new GameCommand[] { new("navigate", 2, new(44, 23)), new("navigate", 1, new(13, 48)) }, q.Commands.Where(c => c.Kind == "navigate").ToArray());
    }

    [Fact]
    public async Task After_a_walk_out_she_rests_clear_of_the_crypt_door()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 1, Position = new(10, 10), Level = 5, Hp = 1000, MaxHp = 1000 });
        q.OnWait = () => q.Set(s => s with { Hp = s.MaxHp });
        var door = new OpenMap { Warps = [new(90, 55)] };
        int fights = 0;
        var leveling = new HuntingEngine(q, door, HuntingGrounds.MilethCrypt, new(), new(), _ => { }, mileth, new(), targetLevel: 6)
        {
            NewJudge = _ => Judged(15, 5),
            Fight = (goal, ct) =>
            {
                if (++fights == 1)
                {
                    q.Set(s => s with { Map = 500, Position = new(90, 56), Hp = 400u });
                    throw new RetreatedException("walked out");
                }

                q.Set(s => s with { Level = 6 });
                return Task.FromResult(FightEnd.GoalReached);
            }
        };
        await leveling.RunAsync(default);
        var walks = q.Commands.Where(c => c.Kind == "navigate").ToArray();
        Assert.Equal(500, walks[0].Map);
        Assert.False(BashingBase.BesideWarp(door, walks[0].Target!));
        Assert.Equal(new GameCommand("navigate", 1, new(13, 48)), walks[1]);
    }

    [Fact]
    public void Passive_monsters_cannot_make_a_floor_safe_while_an_aggressive_type_is_unjudged()
    {
        var judge = new FloorJudge(new());
        judge.Observe(Fighting(1000, 0));
        // Two centipedes walk up to her while she stands still.
        judge.Observe(Fighting(1000, 0, Mob(1, new(5, 8)), Mob(2, new(8, 5))));
        judge.Observe(Fighting(1000, 0, Mob(1, new(5, 6)), Mob(2, new(8, 5))));
        judge.Observe(Fighting(1000, 0, Mob(1, new(5, 6)), Mob(2, new(6, 5))));
        Assert.Equal(new ushort[] { 52 }, judge.Aggressive);
        for (uint i = 0; i < 3; i++)
            judge.Add(new(10 + i, 7, 1, 1, true));
        Assert.Equal(FloorVerdict.Undecided, judge.Verdict);
        Assert.Contains("aggressive sprite 52 not judged yet", judge.Describe());
        for (uint i = 0; i < 3; i++)
            judge.Add(new(20 + i, 52, 4, 3, true));
        Assert.Equal(FloorVerdict.Easy, judge.Verdict);
        for (uint i = 0; i < 3; i++)
            judge.Add(new(30 + i, 52, 40, 3, true));
        // Centipedes now too hard on their own: avoided, and the rats keep the floor in play.
        Assert.Equal(new ushort[] { 52 }, judge.Avoided);
        Assert.Equal(FloorVerdict.Fine, judge.Verdict);
    }

    [Fact]
    public async Task A_floor_is_not_safe_before_five_minutes_on_it_and_too_hard_revokes_safe()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 1, Position = new(5, 5), Level = 5, Hp = 1000, MaxHp = 1000, Experience = 0 });
        var progress = new HuntingProgress();
        int fights = 0;
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new(), progress, _ => { }, mileth, new(), targetLevel: 6)
        {
            Fight = (goal, ct) =>
            {
                if (++fights == 1)
                    for (uint id = 20; id < 23; id++)
                    {
                        q.Monster(id, new(5, 6), sprite: 53);
                        q.Set(s => s with { Hp = s.Hp - 20u });
                        q.Set(s => s with { Entities = s.Entities.Remove(id) });
                        q.Set(s => s with { Experience = s.Experience + 100u });
                    }
                else
                    q.Set(s => s with { Level = 6 });
                return Task.FromResult(FightEnd.NoTargets);
            }
        };
        await leveling.RunAsync(default);
        Assert.False(progress.SafeAt.ContainsKey(1));

        var p = new HuntingProgress { SafeAt = { [2] = 6 } };
        p.MarkTooHard(2, 5);
        Assert.Equal(6, p.SafeAt[2]);
        p.MarkTooHard(2, 6);
        Assert.False(p.SafeAt.ContainsKey(2));
    }

    static readonly IReadOnlyCollection<ushort> centipedes = [52];

    [Fact]
    public async Task Spiders_are_fought()
    {
        var q = new Scripted();
        q.Monster(10, new(5, 6), sprite: 53);
        var basher = new DynamicMeleeBashing(q, new OpenMap(), new());
        await basher.DoBashingAsync(default);
        Assert.Equal(10u, basher.Target?.Id);
    }

    [Fact]
    public async Task An_aggressive_monster_is_fought_before_a_closer_rat_and_takes_over_mid_fight()
    {
        var q = new Scripted();
        q.Monster(10, new(5, 7), sprite: 7);
        q.Monster(11, new(5, 1), sprite: 52);
        var basher = new DynamicMeleeBashing(q, new OpenMap(), new()
        {
            AggressiveSprites = () => centipedes
        });
        await basher.DoBashingAsync(default);
        Assert.Equal(11u, basher.Target?.Id);

        var r = new Scripted();
        r.Monster(10, new(5, 6), sprite: 7);
        var fighter = new DynamicMeleeBashing(r, new OpenMap(), new()
        {
            AggressiveSprites = () => centipedes
        });
        await fighter.DoBashingAsync(default);
        Assert.Equal(10u, fighter.Target?.Id);
        r.Monster(11, new(9, 5), sprite: 52);
        await fighter.DoBashingAsync(default);
        Assert.Equal(11u, fighter.Target?.Id);
    }

    [Fact]
    public async Task Two_aggressive_monsters_are_fought_not_run_from()
    {
        var q = new Scripted();
        q.Set(s => s with { Hp = 900, MaxHp = 1000 });
        q.Monster(10, new(5, 6), sprite: 52);
        q.Monster(11, new(6, 6), sprite: 52);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(q, new OpenMap(), new() { AggressiveSprites = () => centipedes }, null, TimeSpan.FromMinutes(1), stop.Token));
        Assert.DoesNotContain(q.Commands, c => c.Kind == "navigate");
    }

    [Fact]
    public async Task A_centipede_beside_two_others_is_still_targeted_before_a_closer_rat()
    {
        var q = new Scripted();
        q.Monster(10, new(5, 9), sprite: 52);
        q.Monster(11, new(6, 9), sprite: 52);
        q.Monster(12, new(5, 10), sprite: 52);
        q.Monster(13, new(5, 7), sprite: 7);
        var basher = new DynamicMeleeBashing(q, new OpenMap(), new()
        {
            AggressiveSprites = () => centipedes
        });
        await basher.DoBashingAsync(default);
        Assert.Equal(10u, basher.Target?.Id);
        var fresh = new Scripted();
        fresh.Monster(10, new(5, 9), sprite: 52);
        fresh.Monster(13, new(5, 7), sprite: 7);
        var plain = new DynamicMeleeBashing(fresh, new OpenMap(), new());
        await plain.DoBashingAsync(default);
        Assert.Equal(13u, plain.Target?.Id);
    }

    [Fact]
    public async Task Roaming_waypoints_keep_clear_of_a_pack()
    {
        var q = new Scripted();
        q.Monster(10, new(15, 15), sprite: 52);
        q.Monster(11, new(16, 15), sprite: 52);
        q.Monster(12, new(15, 16), sprite: 52);
        var wander = new RandomWaypoints(q, new OpenMap(), aggressive: () => centipedes);
        await wander.Step(default);
        Assert.NotEmpty(wander.Waypoints);
        Assert.All(wander.Waypoints, w => Assert.False(BashingBase.PackNear(q.Snapshot.Entities.Values, centipedes, w, 8)));
    }

    [Fact]
    public void A_monster_type_too_hard_on_its_own_is_avoided_and_the_floor_stays_in_play()
    {
        var judge = new FloorJudge(new());
        for (uint i = 0; i < 3; i++)
        {
            judge.Add(new(i, 52, 40, 6, true));
            judge.Add(new(10 + i, 53, 2, 3, true));
        }

        Assert.Equal(new ushort[] { 52 }, judge.Avoided);
        // Spiders are easy, but a floor with an avoided type is only ever fine.
        Assert.Equal(FloorVerdict.Fine, judge.Verdict);
        Assert.Contains("sprite 52 avoided", judge.Describe());

        var seeded = new FloorJudge(new());
        seeded.Seed([52, 53], [52]);
        for (uint i = 0; i < 3; i++)
            seeded.Add(new(i, 7, 1, 1, true));
        // Saved aggressive spiders still need their own kills; saved too-hard centipedes are left alone.
        Assert.Equal(FloorVerdict.Undecided, seeded.Verdict);
        for (uint i = 0; i < 3; i++)
            seeded.Add(new(10 + i, 53, 2, 3, true));
        Assert.Equal(FloorVerdict.Fine, seeded.Verdict);
    }

    [Fact]
    public void Health_lost_with_nothing_beside_her_counts_against_the_fight_and_the_nearest_caster()
    {
        var at = DateTimeOffset.UnixEpoch;
        var judge = new FloorJudge(new(), () => at);
        judge.Observe(Fighting(1000, 0, Mob(1, new(5, 6), 53), Mob(2, new(5, 8))));
        at += TimeSpan.FromSeconds(1);
        judge.Observe(Fighting(950, 0, Mob(1, new(5, 6), 53), Mob(2, new(5, 8))));
        // The spider steps off; the centipede's curse keeps landing from range.
        judge.Observe(Fighting(950, 0, Mob(1, new(9, 5), 53), Mob(2, new(5, 8))));
        at += TimeSpan.FromSeconds(1);
        judge.Observe(Fighting(700, 0, Mob(1, new(9, 5), 53), Mob(2, new(5, 8))));
        judge.Observe(Fighting(700, 0, Mob(1, new(5, 6), 53), Mob(2, new(5, 8))));
        judge.Observe(Fighting(700, 0, Mob(2, new(5, 8))));
        judge.Observe(Fighting(700, 500, Mob(2, new(5, 8))));
        var kill = Assert.Single(judge.Kills);
        Assert.Equal(30.0, kill.CostPercent, 1);
        Assert.Equal((ushort)52, judge.Culprit);
    }

    [Fact]
    public async Task A_walk_out_blames_the_type_that_took_the_most_health_and_she_avoids_it_there_after()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 5, Position = new(20, 20), Level = 8, Hp = 1000, MaxHp = 1000 });
        var progress = new HuntingProgress { AllowedAt = { [2] = 6, [5] = 8 } };
        var notes = new List<string>();
        int fights = 0;
        IReadOnlyCollection<ushort> avoidedOnReturn = [];
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new() { Note = notes.Add }, progress, _ => { }, mileth, new(), targetLevel: 9)
        {
            Fight = (goal, ct) =>
            {
                if (++fights == 1)
                {
                    q.Set(s => s with { Entities = ImmutableDictionary<uint, Entity>.Empty.Add(2, Mob(2, new(20, 24))) });
                    q.Set(s => s with { Hp = 250u });
                    q.Set(s => s with { Map = 500, Position = new(90, 56), Hp = 1000u, Entities = ImmutableDictionary<uint, Entity>.Empty });
                    throw new RetreatedException("walked out");
                }

                avoidedOnReturn = progress.AvoidAt(5, 8);
                q.Set(s => s with { Level = 9 });
                return Task.FromResult(FightEnd.GoalReached);
            }
        };
        await leveling.RunAsync(default);
        Assert.Equal(8, progress.TooHardMonsters["5#52"]);
        Assert.False(progress.TooHard(5, 8));
        Assert.Equal(new ushort[] { 52 }, avoidedOnReturn);
        Assert.Equal(new GameCommand("navigate", 5, new(1, 31)), q.Commands.Last(c => c.Kind == "navigate"));
        Assert.Contains(notes, n => n.StartsWith("Sprite 52 took the most health"));
        Assert.Empty(progress.AvoidAt(5, 9));
    }

    [Fact]
    public async Task An_avoided_monster_closing_in_is_walked_away_from_and_never_targeted_from_range()
    {
        ushort[] avoid = [52];
        var q = new Scripted();
        q.Set(s => s with { Hp = 900, MaxHp = 1000 });
        q.Monster(10, new(5, 8), sprite: 52);
        q.Monster(11, new(5, 3), sprite: 53);
        var basher = new DynamicMeleeBashing(q, new OpenMap(), new() { AvoidSprites = () => avoid, AggressiveSprites = () => centipedes });
        await basher.DoBashingAsync(default);
        Assert.Equal(11u, basher.Target?.Id);

        var r = new Scripted();
        r.Set(s => s with { Hp = 900, MaxHp = 1000 });
        r.Monster(10, new(5, 7), sprite: 52);
        using (var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(r, new OpenMap(), new() { AvoidSprites = () => avoid }, null, TimeSpan.FromMinutes(1), stop.Token));
        var away = r.Commands.First(c => c.Kind == "navigate").Target!;
        // A short step clear of it, not a walk across the floor.
        Assert.True(Math.Max(Math.Abs(away.X - 5), Math.Abs(away.Y - 7)) >= MeleeFight.AvoidClearance);
        Assert.True(Math.Abs(away.X - 5) + Math.Abs(away.Y - 5) <= 2 * MeleeFight.AvoidSearch);
    }

    [Fact]
    public async Task A_spider_beside_her_is_finished_before_stepping_away_from_a_centipede_and_a_far_centipede_is_ignored()
    {
        ushort[] avoid = [52];
        var q = new Scripted();
        q.Set(s => s with { Hp = 900, MaxHp = 1000 });
        q.Monster(10, new(5, 7), sprite: 52);
        q.Monster(11, new(5, 6), sprite: 53);
        using (var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(q, new OpenMap(), new() { AvoidSprites = () => avoid }, null, TimeSpan.FromMinutes(1), stop.Token));
        Assert.DoesNotContain(q.Commands, c => c.Kind == "navigate");

        var far = new Scripted();
        far.Set(s => s with { Hp = 900, MaxHp = 1000 });
        far.Monster(10, new(5, 8), sprite: 52);
        using (var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(far, new OpenMap(), new() { AvoidSprites = () => avoid }, null, TimeSpan.FromMinutes(1), stop.Token));
        Assert.DoesNotContain(far.Commands, c => c.Kind == "navigate");
    }

    [Fact]
    public void Monster_types_too_hard_are_kept_per_map_up_to_a_level_and_shown_in_the_report()
    {
        var p = new HuntingProgress { SafeAt = { [5] = 8 } };
        p.MarkMonsterTooHard(5, 8, 52);
        p.MarkMonsterTooHard(5, 7, 52);
        Assert.Equal(new ushort[] { 52 }, p.AvoidAt(5, 8));
        Assert.Empty(p.AvoidAt(5, 9));
        Assert.Empty(p.AvoidAt(2, 8));
        Assert.False(p.SafeAt.ContainsKey(5));
        Assert.Contains("| 5 | 52 | 8 |", HuntingGrounds.MilethCrypt.Report("Aria", p));
    }

    [Fact]
    public async Task A_pet_beside_her_is_never_attacked_or_judged_while_listed_monsters_are()
    {
        var q = new Scripted();
        q.Monster(10, new(5, 6), sprite: 594);
        q.Monster(11, new(5, 9), sprite: 53);
        var basher = new DynamicMeleeBashing(q, new OpenMap(), new() { PrioritySprites = HuntingGrounds.MilethCrypt.Monsters, PriorityOnly = true });
        await basher.DoBashingAsync(default);
        Assert.Equal(11u, basher.Target?.Id);

        var at = DateTimeOffset.UnixEpoch;
        var judge = new FloorJudge(new(), () => at, HuntingGrounds.MilethCrypt.Monsters);
        judge.Observe(Fighting(1000, 0));
        judge.Observe(Fighting(1000, 0, Mob(1, new(5, 6), 594)));
        at += TimeSpan.FromSeconds(1);
        judge.Observe(Fighting(900, 0, Mob(1, new(5, 6), 594)));
        judge.Observe(Fighting(900, 0));
        judge.Observe(Fighting(900, 500));
        Assert.Empty(judge.Kills);
        Assert.Null(judge.Culprit);
    }

    static FloorQuarters Covered(params int[] quarters)
    {
        var f = new FloorQuarters();
        var at = DateTimeOffset.UnixEpoch;
        var spots = new[] { new Tile(3, 3), new Tile(15, 3), new Tile(3, 15), new Tile(15, 15) };
        foreach (int q in quarters)
        {
            f.Pause();
            for (int i = 0; i < 6; i++)
            {
                f.Observe(FoundationTests.Ready(5) with
                {
                    Position = spots[q]
                }, (20, 20), [], at);
                at += TimeSpan.FromSeconds(5);
            }
        }

        return f;
    }

    static Scripted Swarmed(uint hp)
    {
        var q = new Scripted();
        q.Set(s => s with { Hp = hp, MaxHp = 1000u });
        q.Monster(10, new(5, 6), sprite: 52);
        q.Monster(11, new(4, 5), sprite: 52);
        q.Monster(12, new(6, 5), sprite: 52);
        return q;
    }

    [Fact]
    public async Task Three_touching_makes_her_walk_to_the_least_covered_quarter_and_hurt_with_every_quarter_tried_is_an_overrun()
    {
        var q = Swarmed(900);
        using (var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(q, new OpenMap(), new() { AggressiveSprites = () => centipedes }, null, TimeSpan.FromMinutes(1), stop.Token, quarters: Covered(1, 2)));
        var away = q.Commands.First(c => c.Kind == "navigate").Target!;
        Assert.True(new[] { new Tile(5, 6), new Tile(4, 5), new Tile(6, 5) }.All(m => Math.Max(Math.Abs(m.X - away.X), Math.Abs(m.Y - away.Y)) >= MeleeFight.EscapeDistance));
        Assert.Equal(3, FloorQuarters.Of(away, (20, 20)));

        // Hurt but quarters untried: walk away, not up a floor.
        var hurtEarly = Swarmed(400);
        using (var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(hurtEarly, new OpenMap(), new() { AggressiveSprites = () => centipedes }, null, TimeSpan.FromMinutes(1), stop.Token));
        Assert.Contains(hurtEarly.Commands, c => c.Kind == "navigate");

        var hurtLate = Swarmed(400);
        Assert.Equal(FightEnd.Overrun, await MeleeFight.RunAsync(hurtLate, new OpenMap(), new()
        {
            AggressiveSprites = () => centipedes
        }, null, TimeSpan.FromMinutes(1), default, quarters: Covered(0, 1, 2, 3)));
        Assert.DoesNotContain(hurtLate.Commands, c => c.Kind == "navigate");
    }

    [Fact]
    public async Task Two_touching_and_a_third_nearby_are_fought()
    {
        var q = new Scripted();
        q.Set(s => s with { Hp = 900, MaxHp = 1000 });
        q.Monster(10, new(5, 6), sprite: 52);
        q.Monster(11, new(6, 5), sprite: 52);
        q.Monster(12, new(5, 8), sprite: 52);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MeleeFight.RunAsync(q, new OpenMap(), new() { AggressiveSprites = () => centipedes }, null, TimeSpan.FromMinutes(1), stop.Token));
        Assert.DoesNotContain(q.Commands, c => c.Kind == "navigate");
    }

    [Fact]
    public async Task Roaming_covers_the_least_visited_quarter()
    {
        var wander = new RandomWaypoints(new Scripted(), new OpenMap(), quarters: Covered(0, 1, 2));
        await wander.Step(default);
        Assert.NotEmpty(wander.Waypoints);
        Assert.All(wander.Waypoints, w => Assert.Equal(3, FloorQuarters.Of(w, (20, 20))));
    }

    [Fact]
    public async Task An_overrun_sends_her_up_a_floor()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 2, Position = new(20, 20), Level = 6, Hp = 1000, MaxHp = 1000 });
        int fights = 0;
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new(), new(), _ => { }, mileth, new(), targetLevel: 7)
        {
            NewJudge = _ => Judged(15, 5),
            Fight = (goal, ct) =>
            {
                if (++fights == 1)
                    return Task.FromResult(FightEnd.Overrun);
                q.Set(s => s with { Level = 7 });
                return Task.FromResult(FightEnd.GoalReached);
            }
        };
        await leveling.RunAsync(default);
        Assert.Equal(new GameCommand("navigate", 1, new(2, 6)), q.Commands.Single(c => c.Kind == "navigate"));
    }

    static readonly Retreat vestibule = new(425, new(5, 8), "the Crypt Vestibule");

    [Fact]
    public async Task Worn_gear_is_repaired_at_the_vestibule_once_nothing_is_on_her_and_no_money_stops_leveling()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 2, Position = new(20, 20), Level = 6, Hp = 1000, MaxHp = 1000, Durability = s.Durability.SetItem("Ragged Shirt", 20) });
        var notes = new List<string>();
        int fights = 0;
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new()
        {
            Note = notes.Add
        }, new()
        {
            AllowedAt = { [2] = 6 }
        }, _ => { }, vestibule, new(), targetLevel: 7)
        {
            NewJudge = _ => Judged(15, 5),
            Fight = (goal, ct) =>
            {
                if (++fights > 1)
                    q.Set(s => s with { Level = 7 });
                return Task.FromResult(goal(q.Snapshot) ? FightEnd.GoalReached : FightEnd.NoTargets);
            }
        };
        await leveling.RunAsync(default);
        var actions = q.Commands.Where(c => c.Kind is "navigate" or "repair").ToArray();
        Assert.Equal(new GameCommand[] { new("navigate", 425, new(5, 8)), new("repair"), new("navigate", 2, new(44, 23)) }, actions);
        Assert.Null(q.Snapshot.WorstDurability);
        Assert.Contains("Gear at 20%; repairing at the Crypt Vestibule", notes);

        var broke = new Scripted { RepairBroke = true };
        broke.Set(s => s with { Map = 425, Position = new(5, 8), Level = 6, Hp = 1000, MaxHp = 1000, Durability = s.Durability.SetItem("Ragged Shirt", 20) });
        var stopped = new HuntingEngine(broke, new OpenMap(), HuntingGrounds.MilethCrypt, new(), new(), _ => { }, vestibule, new());
        var e = await Assert.ThrowsAsync<QuestBlockedException>(() => stopped.RunAsync(default));
        Assert.Equal("Skan won't repair: \"Come back when you have money.\"; leveling stopped in the Crypt Vestibule.", e.Message);
    }

    static Entity Mob(uint id, Tile at, ushort sprite = 52) => new(id, "", at, true) { Sprite = sprite };
    static CharacterSnapshot Fighting(uint hp, uint xp, params Entity[] mobs) => FoundationTests.Ready(5) with { Map = 2, Position = new(5, 5), Hp = hp, MaxHp = 1000, Experience = xp, Entities = mobs.ToImmutableDictionary(m => m.Id) };

    [Fact]
    public void A_kill_with_one_monster_beside_her_is_a_1v1_sample_and_a_crowded_one_is_not()
    {
        var at = DateTimeOffset.UnixEpoch;
        var judge = new FloorJudge(new(), () => at);
        judge.Observe(Fighting(1000, 0));
        judge.Observe(Fighting(1000, 0, Mob(1, new(5, 6))));
        at += TimeSpan.FromSeconds(2);
        judge.Observe(Fighting(900, 0, Mob(1, new(5, 6))));
        at += TimeSpan.FromSeconds(2);
        judge.Observe(Fighting(850, 0, Mob(1, new(5, 6))));
        judge.Observe(Fighting(850, 0));
        at += TimeSpan.FromSeconds(0.4);
        judge.Observe(Fighting(850, 510));
        var kill = Assert.Single(judge.Kills);
        Assert.Equal((15.0, 4.0, true), (Math.Round(kill.CostPercent, 1), kill.Seconds, kill.OneOnOne));

        judge.Observe(Fighting(850, 510, Mob(2, new(5, 6)), Mob(3, new(4, 5))));
        at += TimeSpan.FromSeconds(1);
        judge.Observe(Fighting(800, 510, Mob(2, new(5, 6)), Mob(3, new(4, 5))));
        judge.Observe(Fighting(800, 1020, Mob(3, new(4, 5))));
        Assert.False(judge.Kills[^1].OneOnOne);
        // A monster that walks off without experience is no kill.
        judge.Observe(Fighting(800, 1020));
        at += TimeSpan.FromSeconds(3);
        judge.Observe(Fighting(800, 1020));
        Assert.Equal(2, judge.Kills.Count);
    }

    [Fact]
    public void A_floor_is_judged_on_1v1_cost_and_kill_speed()
    {
        static FloorVerdict Judge(double cost, double seconds)
        {
            var judge = new FloorJudge(new());
            for (uint i = 0; i < 3; i++)
                judge.Add(new(i, 52, cost, seconds, true));
            return judge.Verdict;
        }

        Assert.Equal(FloorVerdict.TooHard, Judge(30, 5));
        Assert.Equal(FloorVerdict.TooHard, Judge(5, 25));
        Assert.Equal(FloorVerdict.Easy, Judge(5, 5));
        Assert.Equal(FloorVerdict.Fine, Judge(15, 5));
        var few = new FloorJudge(new());
        few.Add(new(1, 52, 50, 50, true));
        few.Add(new(2, 52, 50, 50, false));
        Assert.Equal(FloorVerdict.Undecided, few.Verdict);
    }

    [Fact]
    public void Three_monsters_beside_her_while_she_keeps_losing_health_is_surrounded()
    {
        var at = DateTimeOffset.UnixEpoch;
        var judge = new FloorJudge(new(), () => at);
        var pack = new[] { Mob(1, new(5, 6)), Mob(2, new(4, 5)), Mob(3, new(6, 5)) };
        judge.Observe(Fighting(1000, 0));
        judge.Observe(Fighting(950, 0, pack));
        Assert.False(judge.Surrounded);
        at += TimeSpan.FromSeconds(3.1);
        judge.Observe(Fighting(900, 0, pack));
        Assert.True(judge.Surrounded);
        at += TimeSpan.FromSeconds(4);
        Assert.False(judge.Surrounded);
    }

    [Fact]
    public void The_crypt_plan_enters_the_deepest_known_floor_and_falls_back_past_floors_too_hard()
    {
        var p = new HuntingProgress();
        Assert.Equal(0, HuntingGrounds.MilethCrypt.Entry(p, 5));
        p.AllowedAt[2] = 4;
        p.AllowedAt[5] = 7;
        Assert.Equal(1, HuntingGrounds.MilethCrypt.Entry(p, 5));
        Assert.Equal(2, HuntingGrounds.MilethCrypt.Entry(p, 7));
        p.TooHardAt[5] = 7;
        Assert.Equal(1, HuntingGrounds.MilethCrypt.Entry(p, 7));
        Assert.Equal(2, HuntingGrounds.MilethCrypt.Entry(p, 8));
        p.TooHardAt[2] = 7;
        Assert.Equal(0, HuntingGrounds.MilethCrypt.Up(p, 2, 7));
        p.TooHardAt[1] = 7;
        Assert.Null(HuntingGrounds.MilethCrypt.Up(p, 2, 7));
        p.RefusedAt[5] = 6;
        Assert.False(HuntingGrounds.MilethCrypt.TryDown(p, 1, 6, FloorVerdict.Easy));
        Assert.False(HuntingGrounds.MilethCrypt.TryDown(p, 1, 9, FloorVerdict.Fine));
        Assert.True(HuntingGrounds.MilethCrypt.TryDown(new(), 1, 6, FloorVerdict.Easy));
    }

    static FloorJudge Judged(double cost, double seconds)
    {
        var judge = new FloorJudge(new());
        for (uint i = 0; i < 3; i++)
            judge.Add(new(i, 52, cost, seconds, true));
        return judge;
    }

    [Fact]
    public async Task Refused_stairs_are_recorded_and_she_moves_clear_of_them_and_keeps_leveling_on_her_floor()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 2, Position = new(14, 25), Level = 5, Hp = 940, MaxHp = 940 });
        q.RefuseMaps.Add(5);
        var progress = new HuntingProgress();
        var notes = new List<string>();
        var stairs = new OpenMap { Warps = [new(11, 23)] };
        var leveling = new HuntingEngine(q, stairs, HuntingGrounds.MilethCrypt, new()
        {
            Note = notes.Add
        }, progress, _ => { }, mileth, new(), targetLevel: 7)
        {
            NewJudge = _ => Judged(5, 5),
            Fight = (goal, ct) =>
            {
                q.Set(s => s with { Level = s.Level + 1 });
                return Task.FromResult(FightEnd.GoalReached);
            }
        };
        await leveling.RunAsync(default);
        Assert.Equal(6, progress.RefusedAt[5]);
        var away = q.Commands.Single(c => c.Kind == "navigate" && c.Map == 2);
        Assert.False(BashingBase.BesideWarp(stairs, away.Target!));
        Assert.Equal(2, q.Snapshot.Map);
        Assert.Contains(notes, n => n.Contains("refused Basher at level 6"));
    }

    [Fact]
    public async Task A_run_capped_at_crypt_2_1_enters_no_deeper_and_never_takes_the_stairs_down()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 500, Position = new(90, 56), Level = 8, Hp = 1000, MaxHp = 1000 });
        var progress = new HuntingProgress { AllowedAt = { [2] = 6, [5] = 8 } };
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new(), progress, _ => { }, mileth, new(), targetLevel: 10)
        {
            DeepestFloor = 1,
            NewJudge = _ => Judged(5, 5),
            Fight = (goal, ct) =>
            {
                q.Set(s => s with { Level = s.Level + 1 });
                return Task.FromResult(FightEnd.GoalReached);
            }
        };
        await leveling.RunAsync(default);
        Assert.Equal(new GameCommand("navigate", 2, new(44, 23)), q.Commands.First(c => c.Kind == "navigate"));
        Assert.DoesNotContain(q.Commands, c => c.Kind == "navigate" && c.Map == 5);
    }

    [Fact]
    public async Task A_floor_too_hard_sends_her_past_floors_also_too_hard_and_crypt_1_1_too_hard_stops_her()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 5, Position = new(10, 10), Level = 5, Hp = 940, MaxHp = 940 });
        var progress = new HuntingProgress { TooHardAt = { [2] = 5 } };
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new(), progress, _ => { }, mileth, new())
        {
            NewJudge = _ => Judged(30, 5),
            Fight = (goal, ct) => Task.FromResult(FightEnd.NoTargets)
        };
        var e = await Assert.ThrowsAsync<QuestBlockedException>(() => leveling.RunAsync(default));
        Assert.Equal("Crypt 1-1 was too hard at level 5, so Basher walked out to Mileth and stopped.", e.Message);
        Assert.Equal(new GameCommand[] { new("navigate", 1, new(2, 6)), new("navigate", 500, new(90, 56), Tolerance: 1) }, q.Commands.Where(c => c.Kind == "navigate").ToArray());
        Assert.Equal((5, 5, 5), (progress.TooHardAt[5], progress.TooHardAt[2], progress.TooHardAt[1]));
    }

    [Fact]
    public async Task Low_health_on_the_way_down_walks_her_out_and_she_rests_to_full_before_returning()
    {
        var q = new Scripted();
        q.Set(s => s with { Map = 1, Position = new(10, 10), Level = 5, Hp = 940, MaxHp = 940 });
        q.OnWait = () => q.Set(s => s with { Hp = s.MaxHp });
        int fights = 0;
        var leveling = new HuntingEngine(q, new OpenMap(), HuntingGrounds.MilethCrypt, new(), new(), _ => { }, mileth, new(), targetLevel: 7)
        {
            NewJudge = _ => Judged(5, 5),
            Fight = (goal, ct) =>
            {
                q.Set(s => s with { Level = s.Level + 1, Hp = ++fights == 1 ? 200u : s.Hp });
                return Task.FromResult(FightEnd.GoalReached);
            }
        };
        await leveling.RunAsync(default);
        // Not down to Crypt 2-1 at 21% health: out to Mileth, rest, then back in from the top.
        Assert.Equal(new GameCommand[] { new("navigate", 500, new(90, 56), Tolerance: 1), new("navigate", 1, new(13, 48)) }, q.Commands.Where(c => c.Kind == "navigate").ToArray());
        Assert.Equal(940u, q.Snapshot.Hp);
    }

    static readonly LootTarget teeth = new("Wolf's Teeth", 333, 1);

    [Fact]
    public async Task The_target_item_is_walked_to_and_picked_up_without_counting_as_an_extra()
    {
        var q = new Scripted();
        q.Ground(900, 333, new(5, 10));
        var loot = new ItemLooter(q, teeth);
        while (!loot.Done(q.Snapshot) && q.Commands.Count < 20)
            Assert.True(await loot.Tick(default));
        // Two tiles short is close enough for the pickup packet.
        Assert.Equal(new Tile(5, 8), q.Snapshot.Position);
        Assert.Equal(new GameCommand("loot", Target: new(5, 10)), q.Commands[^1]);
        Assert.Empty(loot.Extras);
        Assert.False(await loot.Tick(default));
    }

    [Fact]
    public async Task A_target_under_a_pile_is_dug_out_and_only_the_extras_picked_up_are_dropped()
    {
        var q = new Scripted();
        q.Set(s => s with { Inventory = s.Inventory.SetItem(9, new Item(9, "Wolf's Skin", 3) { Stackable = true }) });
        q.Ground(900, 333, new(5, 6));
        q.Ground(901, 500, new(5, 6));
        q.Ground(902, 501, new(5, 6));
        var loot = new ItemLooter(q, teeth);
        for (int i = 0; i < 3; i++)
            await loot.Tick(default);
        Assert.True(loot.Done(q.Snapshot));
        Assert.Equal(["fior sal", "Wolf's Skin"], loot.Extras.Select(e => e.Name).Order());
        await loot.Tick(default);
        // The pre-existing three skins stay; only the one picked up goes back down.
        Assert.Equal([("Wolf's Skin", 1u), ("fior sal", 1u)], q.Drops.OrderByDescending(d => d.Name.Length));
        Assert.Equal(3u, q.Snapshot.Inventory[9].Quantity);
        Assert.Equal(1, q.Snapshot.Count("Wolf's Teeth"));
    }

    // Dark Things: Centipede's Gland and Scorpion's Sting share image 327.
    [Fact]
    public async Task A_lookalike_with_the_target_sprite_is_kept_rather_than_dropped_and_picked_up_again()
    {
        var q = new Scripted();
        q.Ground(900, 327, new(5, 6));
        q.Ground(901, 327, new(5, 6));
        q.ItemNames[900] = "Centipede's Gland";
        q.ItemNames[901] = "Scorpion's Sting";
        var loot = new ItemLooter(q, new("Centipede's Gland", 327, 1));
        for (int i = 0; i < 4; i++)
            await loot.Tick(default);
        Assert.True(loot.Done(q.Snapshot));
        Assert.Equal(1, q.Snapshot.Count("Scorpion's Sting"));
        Assert.Empty(loot.Extras);
        Assert.Empty(q.Drops);
    }

    [Fact]
    public async Task Extras_wait_while_a_target_still_lies_in_reach()
    {
        var q = new Scripted();
        q.Ground(900, 333, new(6, 5));
        q.Ground(901, 501, new(6, 5));
        var loot = new ItemLooter(q, teeth with
        {
            Count = 2
        });
        await loot.Tick(default);
        q.Ground(903, 333, new(4, 5));
        await loot.Tick(default);
        Assert.Empty(q.Drops);
        Assert.Single(loot.Extras);
    }

    [Fact]
    public async Task Gold_in_reach_is_picked_up_until_the_gold_cap()
    {
        var q = new Scripted();
        q.Ground(910, ItemLooter.GoldSprite, new(6, 6));
        q.Ground(911, ItemLooter.GoldSprite, new(5, 9));
        var loot = new ItemLooter(q, teeth);
        Assert.True(await loot.Tick(default));
        Assert.Equal(1099u, q.Snapshot.Gold);
        // Gold out of reach is not walked to.
        Assert.False(await loot.Tick(default));
        q.Ground(912, ItemLooter.GoldSprite, new(5, 4));
        q.Set(s => s with { GoldCapAt = s.Gold });
        Assert.False(await loot.Tick(default));
    }

    [Fact]
    public async Task A_full_bag_is_refused_before_setting_out_and_stops_the_farm()
    {
        var q = new Scripted();
        var full = Enumerable.Range(1, 59).Select(i => new Item((byte)i, "Rock", 1)).ToImmutableDictionary(i => i.Slot);
        Assert.Equal("Free a bag slot for the Wolf's Teeth.", ItemLooter.NotReady(q.Snapshot with
        {
            Inventory = full
        }, teeth));
        var stacked = full.SetItem(4, new Item(4, "Wolf's Teeth", 1) { Stackable = true });
        Assert.Null(ItemLooter.NotReady(q.Snapshot with
        {
            Inventory = stacked
        }, teeth));
        Assert.Contains("too heavy", ItemLooter.NotReady(q.Snapshot with
        {
            Weight = 100,
            MaxWeight = 100
        }, teeth));
        var loot = new ItemLooter(q, teeth);
        q.Set(s => s with { BagFullRevision = s.Revision + 1 });
        await Assert.ThrowsAsync<QuestBlockedException>(() => loot.Tick(default));
    }

    [Fact]
    public async Task A_farm_fight_ends_once_the_bag_holds_the_item()
    {
        var q = new Scripted();
        q.Ground(900, 333, new(6, 5));
        var loot = new ItemLooter(q, teeth);
        Assert.Equal(FightEnd.GoalReached, await MeleeFight.RunAsync(q, new OpenMap(), new(), loot.Done, TimeSpan.FromSeconds(5), default, roam: true, loot));
        Assert.Equal(1, q.Snapshot.Count("Wolf's Teeth"));
    }

    [Fact]
    public void Gold_cap_and_bag_full_replies_and_the_stackable_flag_are_tracked()
    {
        var t = Tracker();
        t.Observe((byte)ServerCommand.AddInventory, Packet(ServerCommand.AddInventory, new ServerAddInventoryMessage { Slot = 3, Name = "Wolf's Teeth", Quantity = 2, IsStackable = true }));
        Assert.True(t.Snapshot.Inventory[3].Stackable);
        t.Observe((byte)ServerCommand.Status, Packet(ServerCommand.Status, new ServerStatusMessage { Fields = StatsFieldFlags.ExperienceGold, Gold = 50_000_000, TotalExperience = 1, ToNextLevel = 0, TotalAbility = 0, ToNextAbility = 0 }));
        // A pile too big to take is not the cap; one that would just cross it is.
        t.Observe(0x0A, Message("You can't carry more that 100000000 gold."));
        Assert.Null(t.Snapshot.GoldCapAt);
        t.Observe(0x0A, Message("You can't carry more that 50050000 gold."));
        Assert.Equal(50_000_000u, t.Snapshot.GoldCapAt);
        t.Observe(0x0A, Message("Wolf's Teeth, You can't have more than 10"));
        Assert.Equal(t.Snapshot.Revision, t.Snapshot.BagFullRevision);
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
