using System.Diagnostics;
using DAQuester.DarkAges.Launcher;
using Xunit;

namespace DAQuester.AppTests;

public class WindowingStartupTests
{
    [Fact]
    public async Task X_plus_hands_off_once_from_the_real_synthetic_process()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "WindowingFixture.exe");
        string library = Path.Combine(directory, ClientLauncher.WindowingFile);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "WindowingFixture.exe"), file);
        File.Copy(ClientLauncher.WindowingPath, library);
        int calls = 0;
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new WindowingLaunchHost(pid =>
        {
            Interlocked.Increment(ref calls);
            // Release upstream's synthetic parent wait. This is a test marker,
            // not a client creation receipt or a game interaction.
            File.WriteAllBytes(Path.Combine(directory, "x-fixture-result.bin"), new byte[12]);
            received.TrySetResult(pid);
            return Task.CompletedTask;
        });
        host.Start();
        var native = ClientLauncher.NativeSuspendedClient.Create(file, host.Name);
        using var child = Process.GetProcessById(native.Id);
        host.Register(native.Id);
        try
        {
            Assert.NotEqual(0u, native.LoadWindowing(library));
            native.Resume();
            Assert.Equal(child.Id, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await child.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, child.ExitCode);
            Assert.True(File.Exists(Path.Combine(directory, "x-ui-parent-result.bin")));
            Assert.Equal(1, calls); // The fixture clicks the actual caption twice.
            Assert.False(host.Owns(child.Id));
        }
        finally
        {
            if (!child.HasExited) native.Abort();
            native.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_x86_loader_finishes_before_synthetic_entry_and_first_graphics(bool unicodePath)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"),
            unicodePath ? "synthetic ü folder" : "synthetic");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "WindowingFixture.exe");
        string library = Path.Combine(directory, ClientLauncher.WindowingFile);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "WindowingFixture.exe"), file);
        File.Copy(ClientLauncher.WindowingPath, library);
        ClientLauncher.ValidateWindowing(library);

        // This is upstream's synthetic DirectDraw fixture, never a game client.
        // Its entry point requires X to be loaded already, then verifies one
        // wrapped graphics device/window without calling an initializer itself.
        var native = ClientLauncher.NativeSuspendedClient.Create(file);
        using var child = Process.GetProcessById(native.Id);
        try
        {
            Assert.NotEqual(0u, native.LoadWindowing(library));
            native.Resume();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await child.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, child.ExitCode);
            byte[] receipt = File.ReadAllBytes(Path.Combine(directory, "x-fixture-result.bin"));
            Assert.Equal(12, receipt.Length);
            Assert.Equal((uint)native.Id, BitConverter.ToUInt32(receipt, 0));
            Assert.Equal(1u, BitConverter.ToUInt32(receipt, 4));
            Assert.Equal(1u, BitConverter.ToUInt32(receipt, 8));
        }
        finally
        {
            if (!child.HasExited) native.Abort();
            native.Dispose();
        }
    }
}
