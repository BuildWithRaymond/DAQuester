namespace Arbiter.Net.Types;

public enum WorldMessageType : byte
{
    Whisper = 0,
    BarMessage2 = 1,
    BarMessage3 = 2,
    BarMessage = 3,
    BarMessage4 = 4,
    WorldShout = 5,
    BarMessageNoHistory = 6,
    UserSettings = 7,
    ScrollablePopup = 8,
    Popup = 9,
    SignPost = 10,
    GroupChat = 11,
    GuildChat = 12,
    ClosePopup = 17,
    FloatingMessage = 18
}