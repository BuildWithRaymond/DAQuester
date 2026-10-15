using System.Diagnostics;
using System.IO.Pipes;
using DAQuester.DarkAges.Launcher;
using Xunit;

namespace DAQuester.AppTests;

public class WindowingLaunchHostTests
{
    static async Task<uint> Request(string name, uint request = 0x31585144)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(stop.Token);
        await pipe.WriteAsync(BitConverter.GetBytes(request), stop.Token);
        byte[] reply = new byte[4];
        await pipe.ReadExactlyAsync(reply, stop.Token);
        return BitConverter.ToUInt32(reply);
    }

    [Fact]
    public async Task Actual_pipe_origin_must_be_registered_before_one_blank_launch_is_requested()
    {
        int calls = 0, origin = 0;
        await using var host = new WindowingLaunchHost(pid => { calls++; origin = pid; return Task.CompletedTask; });
        host.Start();
        Assert.Equal(5u, await Request(host.Name));
        Assert.Equal(0, calls);
        host.Register(Environment.ProcessId);
        Assert.True(host.Owns(Environment.ProcessId));
        Assert.Equal(0u, await Request(host.Name));
        Assert.Equal(1, calls);
        Assert.Equal(Environment.ProcessId, origin);
        Assert.Equal(13u, await Request(host.Name, 0));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Failed_launch_has_one_failure_receipt_and_is_never_retried()
    {
        int calls = 0;
        await using var host = new WindowingLaunchHost(_ => { calls++; throw new IOException("Synthetic launch failure"); });
        host.Register(Environment.ProcessId);
        host.Start();
        Assert.Equal(31u, await Request(host.Name));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Shutdown_closes_a_connected_caller_without_a_request()
    {
        var host = new WindowingLaunchHost(_ => throw new InvalidOperationException("No launch expected"));
        host.Start();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var pipe = new NamedPipeClientStream(".", host.Name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(stop.Token);
        await host.DisposeAsync().AsTask().WaitAsync(stop.Token);
        byte[] reply = new byte[1];
        Assert.Equal(0, await pipe.ReadAsync(reply, stop.Token));
    }
}
