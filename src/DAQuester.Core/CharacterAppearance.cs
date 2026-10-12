namespace DAQuester.Core;
// Presentation data only. These fields never prove quest actions or rewards.
public sealed record CharacterAppearance(ushort Head, byte Face, byte Body, byte HairColor, byte SkinColor, ushort Arms, ushort Armor, byte PantsColor, byte Boots, byte BootsColor, ushort Weapon, byte Shield, ushort Overcoat, byte OvercoatColor, ushort Accessory1, byte Accessory1Color, ushort Accessory2, byte Accessory2Color, ushort Accessory3, byte Accessory3Color, byte RestPosition, ushort? Monster, bool Hidden, bool Translucent);
// Walking uses a total cycle duration; an observed body action supplies its frame interval.
public sealed record CharacterMotion(byte Direction, byte Animation, DateTimeOffset StartedAt, int DurationMilliseconds, bool Walking);
