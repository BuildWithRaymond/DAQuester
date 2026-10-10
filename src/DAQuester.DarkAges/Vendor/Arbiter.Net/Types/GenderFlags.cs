namespace Arbiter.Net.Types;

[Flags]
public enum GenderFlags : byte
{
    None = 0,
    Male = 1,
    Female = 2,
    Unisex = Male | Female
}