namespace Arbiter.Net.Types;

[Flags]
public enum EntityTypeFlags : byte
{
    None = 0,
    Creature = 0x1,
    Item = 0x2,
    Reactor = 0x4
}