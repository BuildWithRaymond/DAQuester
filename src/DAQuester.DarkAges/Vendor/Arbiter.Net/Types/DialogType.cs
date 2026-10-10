namespace Arbiter.Net.Types;

public enum DialogType : byte
{
    Popup = 0x0,
    Menu = 0x2,
    TextInput = 0x4,
    Speak = 0x5,
    CreatureMenu = 0x6,
    Protected = 0x9,
    CloseDialog = 0xA
}