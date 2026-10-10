namespace Arbiter.Net.Types;

[Flags]
public enum MailFlags
{
    None = 0x0,
    Parcel = 0x01,
    Mail = 0x10
}