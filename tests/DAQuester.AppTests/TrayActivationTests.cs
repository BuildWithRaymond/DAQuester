using DAQuester.App.Services;
using System.Reflection;
using DAQuester.DarkAges.Control;
using Xunit;

namespace DAQuester.AppTests;

public class TrayActivationTests
{
    [Fact]
    public async Task Runtime_activation_shows_the_window_without_enabling_the_scheduler()
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            int shown = 0;
            runtime.ShowWindowRequested = () => shown++;
            var handler = typeof(DesktopRuntime).GetMethod("ControlAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task<object?>)handler.Invoke(runtime, [new ControlRequest("show-window")])!;
            Assert.Equal(1, shown);
            Assert.False(runtime.SchedulerEnabled);
            Assert.False(runtime.QuestActionsRunning);
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task Ordinary_second_launch_requests_only_window_activation()
    {
        string pipe = "DAQuester-test-activation-" + Guid.NewGuid().ToString("N");
        int requests = 0;
        await using var server = new ControlServer(r =>
        {
            Assert.Equal("show-window", r.Method);
            Assert.Null(r.Character);
            Assert.Null(r.Arguments);
            requests++;
            return Task.FromResult<object?>(new { shown = true });
        }, pipe);
        server.Start();
        await InstanceActivation.NotifyAsync(false, pipe);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Background_second_launch_leaves_the_window_hidden()
    {
        // There is no server: a background second launch must not even connect.
        await InstanceActivation.NotifyAsync(true, "DAQuester-test-unused-" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void Sign_in_startup_uses_the_stable_packaged_launcher()
    {
        string root = Path.Combine(Path.GetTempPath(), "DAQuester package");
        string launcher = Path.Combine(root, "DAQuester.exe");
        var checkedPaths = new List<string>();
        string? command = WindowsLoginStartup.ResolveCommand(root, path => { checkedPaths.Add(path); return path == launcher; });
        Assert.Equal('"' + launcher + "\" --background", command);
        Assert.Equal([launcher], checkedPaths);
        Assert.DoesNotContain("current", command!);
    }

    [Fact]
    public void Sign_in_startup_is_unavailable_without_a_packaged_launcher()
    {
        Assert.Null(WindowsLoginStartup.ResolveCommand(null, _ => throw new Exception("No path should be checked")));
        Assert.Null(WindowsLoginStartup.ResolveCommand(Path.GetTempPath(), _ => false));
    }
}
