using System.Collections.Immutable;

namespace DAQuester.Core;

public record Tile(int X, int Y);
public record Item(byte Slot, string Name, uint Quantity)
{
    public bool Stackable
    {
        get; init;
    }
    public ushort Sprite
    {
        get; init;
    }
}
public enum EligibilityKind
{
    Eligible,
    Blocked,
    Unknown
}

public record Eligibility(EligibilityKind Kind, string Reason);
// Presentation observations only; these never establish quest completion or rewards.
public sealed record CharacterFeedback(long Generation, long Revision, string Kind, uint Value, DateTimeOffset At);
public record CharacterSnapshot
{
    // Current server status is diagnostic context, not automatic quest evidence.
    public ServerNotice? ActiveStatus
    {
        get; init;
    }
    public ImmutableArray<CharacterFeedback> Feedback { get; init; } = [];
    public const int InventoryCapacity = 59;
    [System.Text.Json.Serialization.JsonIgnore]
    public int FreeInventorySlots => Math.Max(0, InventoryCapacity - Inventory.Count);

    public CharacterAppearance? Appearance
    {
        get; init;
    }
    public CharacterMotion? Motion
    {
        get; init;
    }
    public long Generation
    {
        get; init;
    }
    public long Revision
    {
        get; init;
    }
    public string? Name
    {
        get; init;
    }
    public uint? Id
    {
        get; init;
    }
    public bool Connected
    {
        get; init;
    }
    public int? Level
    {
        get; init;
    }
    public uint? Gold
    {
        get; init;
    }
    public uint? Experience
    {
        get; init;
    }
    public int? AbilityLevel
    {
        get; init;
    }
    // Zero once a character is master (Talos's master test for weapons).
    public uint? ToNextLevel
    {
        get; init;
    }
    public uint? Hp
    {
        get; init;
    }
    public uint? MaxHp
    {
        get; init;
    }
    public int? Weight
    {
        get; init;
    }
    public int? MaxWeight
    {
        get; init;
    }
    public uint? Mana
    {
        get; init;
    }
    public string? Class
    {
        get; init;
    }
    public string? DisplayClass
    {
        get; init;
    }
    public bool? IsMaster
    {
        get; init;
    }
    public bool? IsPure
    {
        get; init;
    }
    public bool SpellsKnown
    {
        get; init;
    }
    public string? MapName
    {
        get; init;
    }
    public int? Map
    {
        get; init;
    }
    public Tile? Position
    {
        get; init;
    }
    public bool InventoryKnown
    {
        get; init;
    }
    public ImmutableDictionary<byte, Item> Inventory { get; init; } = ImmutableDictionary<byte, Item>.Empty;

    public int Count(string name) => checked((int)Inventory.Values.Where(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(i => (long)i.Quantity));
    public ImmutableDictionary<uint, Entity> Entities { get; init; } = ImmutableDictionary<uint, Entity>.Empty;
    public ImmutableDictionary<uint, GroundItem> GroundItems { get; init; } = ImmutableDictionary<uint, GroundItem>.Empty;
    public ImmutableDictionary<byte, string> Equipment { get; init; } = ImmutableDictionary<byte, string>.Empty;
    public ImmutableDictionary<byte, string> Skills { get; init; } = ImmutableDictionary<byte, string>.Empty;
    public ImmutableDictionary<byte, string> Spells { get; init; } = ImmutableDictionary<byte, string>.Empty;
    public ImmutableArray<string> Legends { get; init; } = [];
    public bool LegendsKnown
    {
        get; init;
    }
    public long LegendsRevision
    {
        get; init;
    }
    public Dialog? Dialog
    {
        get; init;
    }
    public ServerNotice? Notice
    {
        get; init;
    }
    public Exchange? Exchange
    {
        get; init;
    }
    public ImmutableDictionary<string, SkillCooldown> SkillCooldowns { get; init; } = ImmutableDictionary.Create<string, SkillCooldown>(StringComparer.OrdinalIgnoreCase);
    // Own status-bar icons (0x3A): icon to duration colour.
    public ImmutableDictionary<ushort, byte> Effects { get; init; } = ImmutableDictionary<ushort, byte>.Empty;
    public bool SkillsBlockedOnMap
    {
        get; init;
    }
    // Revisions of the combat replies the basher answers: "You can't attack.", an equipment refusal, an asgall reflect.
    public long CantAttackRevision
    {
        get; init;
    }
    public long SkillRefusedRevision
    {
        get; init;
    }
    public long AsgallReflectRevision
    {
        get; init;
    }
    // Gold carried when the server refused a pickup at the gold cap; cleared once gold drops below it.
    public uint? GoldCapAt
    {
        get; init;
    }
    public long BagFullRevision
    {
        get; init;
    }
    public long EntranceRefusedRevision
    {
        get; init;
    }
    public string? EntranceRefusedText
    {
        get; init;
    }
    // Item name -> durability percent, from "The durability of X is now N%"; cleared by a repair.
    public ImmutableDictionary<string, int> Durability
    {
        get; init;
    } = ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
    public int? WorstDurability => Durability.IsEmpty ? null : Durability.Values.Min();
    // Base stats and unspent points from the server's full stat block.
    public CharacterStats? Stats
    {
        get; init;
    }
}

// Party 0 is this character, 1 the trade partner (live 2026-10-11).
public record ExchangeItem(byte Party, string Name);
public record Exchange(long Generation, long Revision, uint PartnerId, string PartnerName)
{
    public ImmutableArray<ExchangeItem> Items { get; init; } = [];
    public uint OwnGold
    {
        get; init;
    }
    public uint PartnerGold
    {
        get; init;
    }
    public bool OwnAccepted
    {
        get; init;
    }
    public bool PartnerAccepted
    {
        get; init;
    }
    public bool Cancelled
    {
        get; init;
    }
    public string? Message
    {
        get; init;
    }
}

public record ServerNotice(long Generation, long Revision, byte Type, string Text, DateTimeOffset ObservedAt);
public record Entity(uint Id, string Name, Tile Position, bool Blocks)
{
    public bool IsPlayer
    {
        get; init;
    }
    public ushort Sprite
    {
        get; init;
    }
    // Arbiter CreatureType: 0 monster, 1 passable, 2 merchant, 3 solid, 4 aisling.
    public byte Kind
    {
        get; init;
    }
    public byte Direction
    {
        get; init;
    }
    public DateTimeOffset LastStep
    {
        get; init;
    }
    public DateTimeOffset FirstSeen
    {
        get; init;
    }
    public byte HealthPercent { get; init; } = 100;
}

// A skill's last server cooldown (0x3F); it stays recorded after it runs out, as the skill's known length.
public record CharacterStats(byte Str, byte Int, byte Wis, byte Con, byte Dex, byte Unspent);
public record SkillCooldown(DateTimeOffset Started, double Seconds)
{
    public bool Over(DateTimeOffset now) => now >= Started.AddSeconds(Seconds);
}

public record GroundItem(uint Id, ushort Sprite, Tile Position);
public record DialogChoice(string Text, ushort Pursuit, byte Option);
public record ShopItem(string Name, uint Price);
public record Dialog(long Generation, long Revision, uint EntityId, byte EntityType, string Name, string Text, bool IsPursuit, ushort Pursuit, ushort Step, byte Type, ImmutableArray<DialogChoice> Choices, string? Prompt = null, bool HasNext = false)
{
    public DateTimeOffset? ObservedAt
    {
        get; init;
    }
    // A merchant's item list (menu type 4); buying names the item instead of choosing an option.
    public ImmutableArray<ShopItem> Items { get; init; } = [];
}

public static class StickEligibility
{
    public static Eligibility Check(CharacterSnapshot s, DateTimeOffset? last, DateTimeOffset now, bool gathering = false)
    {
        if (!s.Connected || s.Name is null || s.Id is null)
            return new(EligibilityKind.Unknown, "Waiting for connected character identity.");
        if (s.Level is null)
            return new(EligibilityKind.Unknown, "Waiting for server level.");
        if (s.Level < 1 || s.Level > 11)
            return new(EligibilityKind.Blocked, "Requires level 1–11.");
        if (s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for health.");
        if (s.Hp == 0)
            return new(EligibilityKind.Blocked, "Character is dead.");
        if (s.Gold is null || !s.InventoryKnown || s.Weight is null || s.MaxWeight is null)
            return new(EligibilityKind.Unknown, "Waiting for inventory, gold and weight.");
        if (s.Gold >= 1000)
            return new(EligibilityKind.Blocked, "Bank excess gold; leave 999 carried.");
        if (s.Weight >= s.MaxWeight || s.Inventory.Count >= 59)
            return new(EligibilityKind.Blocked, "Free inventory slots and weight for branches and rewards.");
        if (last.HasValue && now < last.Value.AddHours(8))
            return new(EligibilityKind.Blocked, "Eight-hour cooldown until " + last.Value.AddHours(8).ToLocalTime().ToString("g"));
        return new(EligibilityKind.Eligible, "Eligible");
    }
}
