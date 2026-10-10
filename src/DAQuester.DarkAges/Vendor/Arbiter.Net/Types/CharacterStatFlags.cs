namespace Arbiter.Net.Types;

[Flags]
public enum CharacterStatFlags : byte
{
    None = 0,
    Strength = 0x1,
    Dexterity = 0x2,
    Intelligence = 0x4,
    Wisdom = 0x8,
    Constitution = 0x10,
}