namespace DAQuester.Core;

// Map questions the basher asks; GameSession answers from the packaged walkability.
public interface ICombatMap
{
    // Not a wall and no monster, merchant or player standing on it.
    bool IsWalkable(Tile tile);
    bool IsWarp(Tile tile);
    // Steps to reach the tile, 0 when it can't be reached.
    int PathLength(Tile from, Tile to);
    bool IsWall(Tile tile);
    (int Width, int Height)? Size
    {
        get;
    }
}

// Talos's per-character bashing settings, with its defaults.
public sealed record BashOptions
{
    public int MonsterWalkIntervalMs { get; init; } = 500;
    public int PingCompensationMs { get; init; } = 50;
    public int SkillIntervalMs { get; init; } = 100;
    public bool BashAsgall
    {
        get; init;
    }
    public bool BashAssails { get; init; } = true;
    public bool AvoidStrangerMobs
    {
        get; init;
    }
    public bool PriorityOnly
    {
        get; init;
    }
    public IReadOnlyCollection<ushort> PrioritySprites { get; init; } = [];
    // Players who never count as strangers, such as the user's own characters.
    public Func<string, bool>? IsFriend
    {
        get; init;
    }
    // One line per target switch and skill fired.
    public Action<string>? Note
    {
        get; init;
    }
}

// Turn decision from the server-confirmed facing and the one we last asked for.
static class Facing
{
    const int ResyncBaseMs = 1000, TurnResendMs = 600;
    internal readonly record struct Decision(bool Turn, bool Resync, DateTimeOffset LastInSync);

    internal static Decision Decide(byte confirmed, byte record, byte target, DateTimeOffset lastInSync, DateTimeOffset lastTurn, DateTimeOffset now, int pingMs)
    {
        bool outOfSync = confirmed != record;
        bool resync = outOfSync && (now - lastInSync).TotalMilliseconds > ResyncBaseMs + pingMs * 2;
        var inSync = outOfSync && !resync ? lastInSync : now;
        bool turn;
        if (record != target)
            turn = true;
        else if (resync)
            turn = true;
        else if (confirmed != target)
            turn = (now - lastTurn).TotalMilliseconds >= TurnResendMs;
        else
            turn = false;
        return new(turn, resync, inSync);
    }
}

// Which visible creatures are monsters worth bashing (Talos GameSession.IsValidCreature and its sprite lists).
public static class CombatTargets
{
    public static readonly IReadOnlySet<ushort> Invisible = new HashSet<ushort> { 676, 690, 691, 699, 700, 701, 532, 530, 528, 526, 492, 387, 289, 290, 292, 293, 294, 295, 296, 297, 298, 299, 253, 252, 195 };
    public static readonly IReadOnlySet<ushort> Undesirable = new HashSet<ushort> { 563, 564, 565, 566, 439, 456, 451, 53 };
    // On these maps only the listed monsters are fought.
    public static readonly IReadOnlyDictionary<int, ushort[]> WhitelistByMap = new Dictionary<int, ushort[]>
    {
        [7071] = [559, 246, 250, 929], // Battle of Mount Merry
        [10240] = [542, 544], // Andor 140 Boss Room
        [2120] = [876, 878], // Mount Giragan 1 - Yule Logs
        [2088] = [876, 878], // Mount Giragan 2 - Yule Logs
        [8111] = [205], // Blackstar Crypt 11 Boss
        [8496] = [164, 165, 166, 167], // Macabre Grave Yard Zombie Survival
        [8497] = [164, 165, 166, 167],
        [8498] = [164, 165, 166, 167],
        [8499] = [164, 165, 166, 167],
        [8500] = [46, 62, 814, 815], // Count Macabre Mansion Event
        [6999] = [492], // Water Dungeon 15 Boss (energy sphere)
        [509] = [912, 69, 321, 363, 365, 366, 367], // Loures Battle Ring Arena Event
        [8400] = [262, 394], // Mileth Floppy Field 1 Giant Floppy
        [10263] = [454], // Alsaids Fire Canyon 1 Molten Cube
        [8994] = [422], // Lost Ruins 2 Grimes
        [8983] = [404], // Lost Ruins 7 Law
        [8980] = [404], // Law Shortcut 1 Law
        [10000] = [422], // Asilon Town Lorai
        [8989] = [779, 782, 788], // Lost Ruins 4
        [8984] = [784, 785], // Lost Ruins 6
        [9377] = [692, 695], // Plamit's Cave 13-1 Boss Room
        [9378] = [692, 695], // Plamit Village
        [9379] = [692, 695], // Plamit Inn
        [9380] = [692, 695], // Plamit Bar
        [9381] = [692, 695], // Bank of Plamit
        [9382] = [692, 695], // Plamit Potion Shop
        [9383] = [692, 695], // Plamit Armory
        [9384] = [692, 695], // Plamit boss 8
        [6646] = [318], // Lynith Beach Way Boss 1
        [6647] = [318],
        [6648] = [318],
        [6649] = [318],
        [6651] = [316], // Lynith Beach Way Boss 2
        [6652] = [316],
        [6653] = [316],
        [6654] = [316],
        [6656] = [207], // Lynith Beach Way Boss 3
        [6657] = [207],
        [6658] = [207],
        [6659] = [207],
        [566] = [273, 266, 240, 272, 87], // Shinewood Forest 36-43
        [567] = [273, 266, 240, 272, 87],
        [568] = [273, 266, 240, 272, 87],
        [569] = [273, 266, 240, 272, 87],
        [570] = [273, 266, 240, 272, 87],
        [571] = [273, 266, 240, 272, 87],
        [572] = [273, 266, 240, 272, 87],
        [573] = [273, 266, 240, 272, 87]
    };
    public static readonly IReadOnlyDictionary<string, ushort[]> WhitelistByMapName = new Dictionary<string, ushort[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Shinewood Forest 3"] = [273, 266, 240, 272, 87],
        ["Shinewood Forest 4"] = [273, 266, 240, 272, 87],
        ["Aman Jungle"] = [856, 873, 874, 875],
        ["Yowien Territory"] = [634, 664],
        ["Beal na Carraige"] = [707, 780, 781],
        ["Cthonic Remains 4"] = [190, 210],
        ["Cthonic Remains 5"] = [190, 210],
        ["Cursed Home"] = [609, 610, 611, 612],
        ["Muisir"] = [926, 933, 940, 953, 954, 955, 960],
        ["Chadul's Mileth"] = [401],
        ["Chadul's Abel"] = [400],
        ["Chadul's Loures"] = [650],
        ["Chadul's Piet"] = [397],
        ["Blackstar Crypt 5"] = [401],
        ["Blackstar Crypt 11"] = [205],
        ["Blackstar Crypt Boss"] = [397]
    };

    static bool Valid(Entity e, int? map, Tile origin, int distance) =>
        !e.IsPlayer && e.Kind < 2 && e.Sprite is > 0 and <= 1000 && (e.Sprite == 492 && map == 6999 || !Invisible.Contains(e.Sprite) && !Undesirable.Contains(e.Sprite)) && BashingBase.Distance(origin, e.Position) <= distance;

    public static List<Entity> Nearby(CharacterSnapshot s, int distance = 12)
    {
        if (s.Position is not { } origin)
            return [];
        var list = s.Entities.Values.Where(e => Valid(e, s.Map, origin, distance)).ToList();
        var allowed = new HashSet<ushort>();
        if (s.Map is { } id && WhitelistByMap.TryGetValue(id, out var byId))
            allowed.UnionWith(byId);
        if (s.MapName is { } name && WhitelistByMapName.FirstOrDefault(p => name.StartsWith(p.Key, StringComparison.OrdinalIgnoreCase)).Value is { } byName)
            allowed.UnionWith(byName);
        return allowed.Count == 0 ? list : list.Where(e => allowed.Contains(e.Sprite)).ToList();
    }
}

// Ported from Talos Redux BashingBase: target choice, lock, chase, facing and refresh. Protection, assist,
// follow chains, waiting for curses and the Shinewood necklace swap are left out.
public abstract class BashingBase(IGameSession session, ICombatMap map, BashOptions options)
{
    const int TargetLockMaxRange = 8, AvoidStrangerMobTiles = 4;
    const double StuckBreakSeconds = 3.0;
    // Icons on our own status bar (Talos EffectsBar).
    protected const ushort SuainIcon = 50, PramhIcon = 90, BeagSuainIcon = 97, DionIcon = 53, AsgallIcon = 54;
    // A creature that reflected our attack with asgall is treated as asgalled this long.
    static readonly TimeSpan AsgallReflect = TimeSpan.FromSeconds(7);

    protected IGameSession Session { get; } = session;
    protected ICombatMap Map { get; } = map;
    protected BashOptions Options { get; } = options;
    protected CharacterSnapshot S => Session.Snapshot;
    protected Tile Here => S.Position ?? new(0, 0);
    protected DateTimeOffset LastDirectionInSync = DateTimeOffset.UtcNow;
    protected DateTimeOffset LastAssailed = DateTimeOffset.UtcNow;
    protected DateTimeOffset LastUsedSkill { get; set; } = DateTimeOffset.MinValue;
    protected List<Entity> NearbyPlayers { get; set; } = [];
    protected List<Entity> NearbyMonsters { get; set; } = [];
    protected List<Entity> KillableTargets { get; set; } = [];
    protected HashSet<string> BlockedSkills { get; } = new(StringComparer.OrdinalIgnoreCase);
    protected string? CurrentSkill
    {
        get; set;
    }
    public Entity? Target
    {
        get; protected set;
    }

    readonly Dictionary<uint, DateTimeOffset> asgalledUntil = [];
    readonly HashSet<uint> tagged = [];
    long cantAttackSeen, refusedSeen, asgallSeen;
    byte? requestedDirection;
    DateTimeOffset lastFaceTurn = DateTimeOffset.MinValue, lastPathfind = DateTimeOffset.MinValue, lastAssailSent = DateTimeOffset.MinValue;
    DateTimeOffset lastExp = DateTimeOffset.MinValue, lastRefresh = DateTimeOffset.MinValue;
    uint? lastExperience;
    static readonly TimeSpan minPathInterval = TimeSpan.FromMilliseconds(25);
    Tile? stuckCheckPos;
    DateTimeOffset stuckSince = DateTimeOffset.MinValue;
    bool pathStuck;

    protected int MonsterWalkIntervalMs => Options.MonsterWalkIntervalMs;
    protected int PingCompensation => Options.PingCompensationMs;
    protected bool BashAsgall => Options.BashAsgall;
    protected int SkillIntervalMs => Options.SkillIntervalMs;

    protected bool CanMove => !S.Effects.ContainsKey(BeagSuainIcon) && !S.Effects.ContainsKey(PramhIcon) && !S.Effects.ContainsKey(SuainIcon);
    protected bool CanPerformActions => !S.Effects.ContainsKey(PramhIcon) && !S.Effects.ContainsKey(SuainIcon);
    protected bool SelfDioned => S.Effects.ContainsKey(DionIcon) || S.Effects.ContainsKey(AsgallIcon);
    protected bool IsAsgalled(Entity e) => asgalledUntil.TryGetValue(e.Id, out var until) && until > DateTimeOffset.UtcNow;
    protected bool IsTaggedByOther(Entity e) => tagged.Contains(e.Id);
    protected void ClearTagged(Entity e) => tagged.Remove(e.Id);

    internal static int Distance(Tile a, Tile b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    // Talos Location.GetDirection: the way `to` must face to look at `from`.
    internal static byte DirectionOf(Tile from, Tile to) => from.Y < to.Y ? (byte)0 : from.X > to.X ? (byte)1 : from.Y > to.Y ? (byte)2 : from.X < to.X ? (byte)3 : (byte)0xFF;
    internal static Tile Offset(Tile p, byte direction) => direction switch
    {
        0 => p with { Y = p.Y - 1 },
        1 => p with { X = p.X + 1 },
        2 => p with { Y = p.Y + 1 },
        3 => p with { X = p.X - 1 },
        _ => p
    };

    // One tile past the target on the line from the caster; diagonals use the dominant axis.
    public static Tile TeleportLanding(Tile caster, Tile target) => Offset(target, DirectionOf(target, caster));
    public static bool IsTeleportLandingSafe(ICombatMap map, Tile landing) => map.IsWalkable(landing) && !map.IsWarp(landing);

    public async Task<bool> DoBashingAsync(CancellationToken ct)
    {
        if (!CanPerformActions)
            return true;
        Update();
        KillableTargets = FilterMonstersByCursedFased().ToList();
        var potentialTargets = GetOrderedPotentialTargets().ToList();
        if (potentialTargets.Count == 0)
        {
            Target = null;
            return false;
        }

        bool lockHeld = IsTargetLockValid(potentialTargets);
        if (!lockHeld)
        {
            var newTarget = potentialTargets.FirstOrDefault(MeetsKillCriteria) ?? potentialTargets.FirstOrDefault();
            if (newTarget?.Id != Target?.Id)
                Options.Note?.Invoke("Target " + Describe(newTarget) + " at " + (newTarget is null ? "?" : Distance(Here, newTarget.Position)) + " tiles, " + potentialTargets.Count + " in reach");
            Target = newTarget;
        }

        if (Target is null)
            return false;
        if (!await HandleMovement(Target, ct))
            return false;
        await EnsureWeaponEquipped(ct);
        if (await RefreshIfNeeded(ct))
            return true;
        await UseSkills(Target, ct);
        return true;
    }

    internal void Update()
    {
        var s = S;
        NearbyPlayers = s.Entities.Values.Where(e => e.IsPlayer && e.Name.Length > 0).ToList();
        NearbyMonsters = CombatTargets.Nearby(s);
        if (Target is { } t)
            Target = s.Entities.GetValueOrDefault(t.Id) ?? t;
        if (s.Experience != lastExperience)
        {
            lastExperience = s.Experience;
            lastExp = DateTimeOffset.UtcNow;
        }

        // Server replies arrive after the action that caused them; each is answered once.
        if (s.CantAttackRevision > cantAttackSeen)
        {
            cantAttackSeen = s.CantAttackRevision;
            if (Target is { } claimed)
                tagged.Add(claimed.Id);
        }

        if (s.SkillRefusedRevision > refusedSeen)
        {
            refusedSeen = s.SkillRefusedRevision;
            if (CurrentSkill is { } refused)
                BlockedSkills.Add(refused);
        }

        if (s.AsgallReflectRevision > asgallSeen)
        {
            asgallSeen = s.AsgallReflectRevision;
            if (Target is { } reflecting && !IsAsgalled(reflecting))
                asgalledUntil[reflecting.Id] = DateTimeOffset.UtcNow + AsgallReflect;
        }
    }

    static string Describe(Entity? e) => e is null ? "none" : e.Name.Length > 0 ? e.Name + " (" + e.Sprite + ")" : "sprite " + e.Sprite;

    async Task<bool> HandleMovement(Entity target, CancellationToken ct)
    {
        int distToTarget = Distance(Here, target.Position);
        if (NearbyMonsters.Any(m => m.Position == Here))
        {
            await TryUnstuck(ct);
            return true;
        }

        if (distToTarget == 0)
        {
            await TryUnstuck(ct);
            return true;
        }

        if (distToTarget > 1)
        {
            var currentPos = Here;
            if (currentPos == stuckCheckPos)
            {
                if (!pathStuck && (DateTimeOffset.UtcNow - stuckSince).TotalSeconds >= StuckBreakSeconds)
                    pathStuck = true;
            }
            else
            {
                stuckCheckPos = currentPos;
                stuckSince = DateTimeOffset.UtcNow;
                pathStuck = false;
            }

            if (!ShouldPathfindToTarget(target, distToTarget))
                return true;
            if (DateTimeOffset.UtcNow - lastPathfind < minPathInterval)
                return true;
            lastPathfind = DateTimeOffset.UtcNow;
            if (!await TryPathfindToPoint(target.Position, ct))
                return false;
        }
        else
        {
            pathStuck = false;
            if (!await TryFaceTarget(target, ct))
                return true;
        }

        return true;
    }

    bool ShouldPathfindToTarget(Entity target, int distance)
    {
        if (distance <= 3 && target.Direction == DirectionOf(Here, target.Position))
        {
            // A close monster walking toward us: hold still around its expected step instead of chasing.
            var lastStepTime = DateTimeOffset.UtcNow - target.LastStep;
            bool inStepWindow = lastStepTime > TimeSpan.FromMilliseconds(MonsterWalkIntervalMs - PingCompensation) && lastStepTime < TimeSpan.FromMilliseconds(MonsterWalkIntervalMs + PingCompensation);
            return !inStepWindow;
        }

        return true;
    }

    bool NearStranger(Entity mob, IEnumerable<Entity> strangers) => Options.AvoidStrangerMobs && strangers.Any(stranger => Distance(stranger.Position, mob.Position) <= AvoidStrangerMobTiles);
    protected IEnumerable<Entity> GetStrangers() => NearbyPlayers.Where(user => Options.IsFriend?.Invoke(user.Name) != true);

    protected virtual IEnumerable<Entity> FilterMonstersByCursedFased(IEnumerable<Entity>? monsters = null) => (monsters ?? NearbyMonsters).Where(monster => BashAsgall || !IsAsgalled(monster));

    protected virtual bool MeetsKillCriteria(Entity monster) => !IsAsgalled(monster) || BashAsgall;

    // Skills fired into a monster's step animation miss; hold them inside the window around its next step.
    internal virtual bool ShouldUseSkillsOnTarget(Entity target)
    {
        var timeSinceLastStep = DateTimeOffset.UtcNow - target.LastStep;
        var lowerBound = TimeSpan.FromMilliseconds(Math.Max(50, MonsterWalkIntervalMs - PingCompensation));
        var upperBound = TimeSpan.FromMilliseconds(MonsterWalkIntervalMs + PingCompensation);
        return timeSinceLastStep < lowerBound || timeSinceLastStep > upperBound;
    }

    // HP-based attacks are off: crashers are left out of this port.
    internal static bool CanFireHpCostSkill(SkillMeta meta) => !meta.CostsHp;

    protected abstract Task UseSkills(Entity target, CancellationToken ct);

    protected virtual async Task UseAssails(CancellationToken ct)
    {
        if (Options.BashAssails)
            await Assail(ct);
    }

    protected virtual IEnumerable<Entity> GetOrderedPotentialTargets(IEnumerable<Entity>? monsters = null)
    {
        var strangers = GetStrangers().ToList();
        var ordered = (monsters ?? NearbyMonsters).OrderBy(GetPriorityDistance).ThenBy(mob => Distance(mob.Position, Here)).ThenBy(mob => mob.HealthPercent).ThenBy(mob => mob.FirstSeen).AsEnumerable();
        if (Options.PriorityOnly)
            ordered = ordered.Where(mob => Options.PrioritySprites.Contains(mob.Sprite));
        return ordered.Where(mob => IsValidTarget(mob, strangers));
    }

    // Holds the current target until it dies, leaves, gets out of reach, or the chase is stuck.
    bool IsTargetLockValid(List<Entity> potentialTargets)
    {
        if (Target is null)
            return false;
        if (!potentialTargets.Any(t => t.Id == Target.Id))
            return false;
        if (Distance(Here, Target.Position) > TargetLockMaxRange)
            return false;
        if (pathStuck)
        {
            pathStuck = false;
            return false;
        }

        return true;
    }

    // An overlapping creature can't be hit, so it goes last; non-priority monsters count triple distance.
    int GetPriorityDistance(Entity mob)
    {
        int distance = Distance(mob.Position, Here);
        if (distance == 0)
            return int.MaxValue;
        return Options.PrioritySprites.Contains(mob.Sprite) ? distance : distance * 3;
    }

    bool IsValidTarget(Entity mob, IEnumerable<Entity> strangers)
    {
        if (NearStranger(mob, strangers))
            return false;
        if (Here == mob.Position || NearbyPlayers.Any(user => user.Position == mob.Position))
            return false;
        if (Distance(Here, mob.Position) == 1)
            return true;
        int pathCount = Map.PathLength(Here, mob.Position);
        return pathCount > 0 && pathCount < 15;
    }

    protected async Task<bool> TryFaceTarget(Entity target, CancellationToken ct)
    {
        if (!CanPerformActions)
            return false;
        var now = DateTimeOffset.UtcNow;
        byte targetDirection = DirectionOf(target.Position, Here);
        byte confirmed = S.Motion?.Direction ?? requestedDirection ?? targetDirection;
        byte record = requestedDirection ?? confirmed;
        var decision = Facing.Decide(confirmed, record, targetDirection, LastDirectionInSync, lastFaceTurn, now, PingCompensation);
        LastDirectionInSync = decision.LastInSync;
        if (decision.Resync)
            requestedDirection = confirmed;
        if (decision.Turn)
        {
            await Session.ExecuteAsync(new("turn", Amount: targetDirection), ct);
            requestedDirection = targetDirection;
            lastFaceTurn = now;
        }

        return true;
    }

    // The direction we last asked for, which Talos keeps as the server facing.
    protected byte ServerDirection => requestedDirection ?? S.Motion?.Direction ?? 0xFF;

    protected virtual async Task<bool> TryPathfindToPoint(Tile location, CancellationToken ct)
    {
        if (!CanMove)
            return false;
        try
        {
            await Session.ExecuteAsync(new("pathfind", Target: location), ct);
        }
        catch (Exception e) when (e is QuestBlockedException or TimeoutException)
        {
            return false;
        }

        requestedDirection = S.Motion?.Direction ?? requestedDirection;
        return true;
    }

    protected IEnumerable<Entity> GetSurroundingCreatures(IEnumerable<Entity>? creatures = null, int skillRange = 1) =>
        (creatures ?? KillableTargets).Where(mob => Distance(mob.Position, Here) <= skillRange);

    protected virtual async Task<bool> TryUnstuck(CancellationToken ct)
    {
        if (!CanMove)
            return false;
        foreach (byte dir in new byte[] { 0, 1, 2, 3 }.OrderBy(_ => Random.Shared.Next()))
        {
            var next = Offset(Here, dir);
            if (!Map.IsWalkable(next))
                continue;
            try
            {
                await Session.ExecuteAsync(new("step", Target: next), ct);
            }
            catch (Exception e) when (e is QuestBlockedException or TimeoutException)
            {
                return false;
            }

            requestedDirection = dir;
            return true;
        }

        return false;
    }

    // Talos BotEngine.EnsureWeaponEquipped: without a melee weapon or bow in hand, equip the best melee weapon
    // in the bag once. Talos reset its attempt flag on every call, so it retried forever; here a second
    // failure stops the fight.
    bool equipAttempted;
    async Task EnsureWeaponEquipped(CancellationToken ct)
    {
        if (WeaponRegistry.HoldsWeapon(S))
        {
            equipAttempted = false;
            return;
        }

        if (equipAttempted)
            throw new QuestBlockedException("No usable melee weapon is equipped or in the bag, so the fight stopped.");
        equipAttempted = true;
        if (WeaponRegistry.BestMelee(S) is not { } weapon)
            return;
        Options.Note?.Invoke("Equipping " + weapon.Name);
        long before = S.Revision;
        await Session.ExecuteAsync(new("equip", Slot: weapon.Slot), ct);
        try
        {
            await Session.WaitAsync(s => s.Equipment.GetValueOrDefault((byte)1) == weapon.Name, before, TimeSpan.FromMilliseconds(1500), ct);
        }
        catch (TimeoutException)
        {
        } // Checked again on the next tick.
    }

    // With no experience and no refresh for five seconds, refresh once and skip skills this tick.
    async Task<bool> RefreshIfNeeded(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - lastExp).TotalSeconds > 5.0 && (now - lastRefresh).TotalSeconds > 5.0)
        {
            lastRefresh = now;
            try
            {
                await Session.ExecuteAsync(new("refresh"), ct);
            }
            catch (QuestBlockedException)
            {
            } // An unanswered refresh is retried after the next five seconds.
            return true;
        }

        return false;
    }

    protected bool CanUseSkill(RankedSkill skill) => CanUseSkill(skill.Name);
    byte? SlotOf(string name) => S.Skills.Where(p => SkillDatabase.BaseName(p.Value) == name).Select(p => (byte?)p.Key).FirstOrDefault();
    protected bool CanUseSkill(string name)
    {
        var s = S;
        if (SlotOf(name) is null)
            return false;
        if (s.SkillCooldowns.TryGetValue(name, out var cooldown) && !cooldown.Over(DateTimeOffset.UtcNow))
            return false;
        return !s.SkillsBlockedOnMap && !s.Effects.ContainsKey(PramhIcon) && !s.Effects.ContainsKey(SuainIcon) && !BlockedSkills.Contains(name);
    }

    // Talos GameActions.UseSkill: refused when unusable, otherwise sent at once.
    protected async Task<bool> UseSkill(string name, CancellationToken ct)
    {
        if (!CanUseSkill(name))
            return false;
        byte slot = SlotOf(name)!.Value;
        CurrentSkill = name;
        await Session.ExecuteAsync(new("use-skill", Amount: slot), ct);
        return true;
    }

    // At most one assail every 250 ms.
    async Task Assail(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - lastAssailSent).TotalMilliseconds <= 250.0)
            return;
        lastAssailSent = now;
        await Session.ExecuteAsync(new("assail"), ct);
    }
}
