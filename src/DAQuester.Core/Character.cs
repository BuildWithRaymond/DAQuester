using System.Collections.Immutable;

namespace DAQuester.Core;
public record Tile(int X, int Y);
public record Item(byte Slot, string Name, uint Quantity);
public enum EligibilityKind
{
    Eligible,
    Blocked,
    Unknown
}

public record Eligibility(EligibilityKind Kind, string Reason);
public record CharacterSnapshot
{
    public CharacterAppearance? Appearance { get; init; }
    public CharacterMotion? Motion { get; init; }
    public long Generation { get; init; }
    public long Revision { get; init; }
    public string? Name { get; init; }
    public uint? Id { get; init; }
    public bool Connected { get; init; }
    public int? Level { get; init; }
    public uint? Gold { get; init; }
    public uint? Experience { get; init; }
    public uint? Hp { get; init; }
    public int? Weight { get; init; }
    public int? MaxWeight { get; init; }
    public uint? Mana { get; init; }
    public string? Class { get; init; }
    public string? DisplayClass { get; init; }
    public bool? IsMaster { get; init; }
    public bool? IsPure { get; init; }
    public bool SpellsKnown { get; init; }
    public string? MapName { get; init; }
    public int? Map { get; init; }
    public Tile? Position { get; init; }
    public bool InventoryKnown { get; init; }
    public ImmutableDictionary<byte, Item> Inventory { get; init; } = ImmutableDictionary<byte, Item>.Empty;

    public int Count(string name) => checked((int)Inventory.Values.Where(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(i => (long)i.Quantity));
    public ImmutableDictionary<uint, Entity> Entities { get; init; } = ImmutableDictionary<uint, Entity>.Empty;
    public ImmutableDictionary<uint, GroundItem> GroundItems { get; init; } = ImmutableDictionary<uint, GroundItem>.Empty;
    public ImmutableDictionary<byte, string> Equipment { get; init; } = ImmutableDictionary<byte, string>.Empty;
    public ImmutableDictionary<byte, string> Skills { get; init; } = ImmutableDictionary<byte, string>.Empty;
    public ImmutableDictionary<byte, string> Spells { get; init; } = ImmutableDictionary<byte, string>.Empty;
    public ImmutableArray<string> Legends { get; init; } = [];
    public bool LegendsKnown { get; init; }
    public long LegendsRevision { get; init; }
    public Dialog? Dialog { get; init; }
    public ServerNotice? Notice { get; init; }
}

public record ServerNotice(long Generation, long Revision, byte Type, string Text, DateTimeOffset ObservedAt);
public record Entity(uint Id, string Name, Tile Position, bool Blocks)
{
    public bool IsPlayer { get; init; }
}

public record GroundItem(uint Id, ushort Sprite, Tile Position);
public record DialogChoice(string Text, ushort Pursuit, byte Option);
public record Dialog(long Generation, long Revision, uint EntityId, byte EntityType, string Name, string Text, bool IsPursuit, ushort Pursuit, ushort Step, byte Type, ImmutableArray<DialogChoice> Choices, string? Prompt = null, bool HasNext = false)
{
    public DateTimeOffset? ObservedAt { get; init; }
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
