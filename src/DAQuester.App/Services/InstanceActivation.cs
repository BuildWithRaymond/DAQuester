using DAQuester.DarkAges.Control;

namespace DAQuester.App.Services;

internal static class InstanceActivation
{
    public static async Task NotifyAsync(bool background, string pipeName = ControlPipe.Name)
    {
        if (background) return;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await ControlPipe.CallAsync(new("show-window"), deadline.Token, pipeName);
    }
}
