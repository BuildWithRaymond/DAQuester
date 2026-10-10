namespace Arbiter.Net.Types;

[Flags]
public enum MapFlags : byte
{
    None = 0,
    Snow = 0x1,
    Rain = 0x2,
    Darkness = Snow | Rain,
    NoMap = 0x40,
    Winter = 0x80
}