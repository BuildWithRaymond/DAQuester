using System.Runtime.InteropServices;

namespace DAQuester.App.Controls;

internal static class MotionPreferences
{
    public static bool Enabled => !OperatingSystem.IsWindows()
        || !SystemParametersInfo(0x1042, 0, out bool enabled, 0) || enabled;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);
}
