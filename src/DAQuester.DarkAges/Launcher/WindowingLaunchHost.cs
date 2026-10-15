using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DAQuester.DarkAges.Launcher;

// A per-app capability for X's new-client button, not a general control bridge.
public sealed class WindowingLaunchHost(Func<int, Task> launch, Action<string>? diagnostic = null) : IAsyncDisposable
{
    readonly CancellationTokenSource lifetime = new();
    readonly ConcurrentDictionary<int, Process> owners = new();
    readonly List<Task> callers = [];
    Task? loop;
    public string Name { get; } = "DAQuester.NewClient." + Guid.NewGuid().ToString("N");

    public void Register(int processId)
    {
        var process = Process.GetProcessById(processId);
        _ = process.SafeHandle; // Pin this process identity before its main thread resumes.
        owners.AddOrUpdate(processId, process, (_, previous) => { previous.Dispose(); return process; });
    }

    public bool Owns(int processId)
    {
        try { return owners.TryGetValue(processId, out var process) && !process.HasExited; }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception) { return false; }
    }

    NamedPipeServerStream CreatePipe() => new(Name, PipeDirection.InOut, 16,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    public void Start() => loop ??= AcceptAsync(CreatePipe());

    async Task AcceptAsync(NamedPipeServerStream pipe)
    {
        try
        {
            while (true)
            {
                await pipe.WaitForConnectionAsync(lifetime.Token);
                callers.RemoveAll(t => t.IsCompleted);
                callers.Add(ServeAsync(pipe));
                pipe = CreatePipe();
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (IOException error) { diagnostic?.Invoke("X new-client listener stopped: " + error.Message); }
        finally { pipe.Dispose(); }
    }

    async Task ServeAsync(NamedPipeServerStream pipe)
    {
        using (pipe)
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(25));
            try
            {
                byte[] request = new byte[4];
                await pipe.ReadExactlyAsync(request, deadline.Token);
                uint result;
                if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint pid) || pid > int.MaxValue || !Owns((int)pid))
                    result = 5; // ERROR_ACCESS_DENIED; never trust a PID from the request.
                else if (BitConverter.ToUInt32(request) != 0x31585144)
                    result = 13; // ERROR_INVALID_DATA
                else
                {
                    await launch((int)pid).WaitAsync(deadline.Token);
                    result = 0;
                }
                await pipe.WriteAsync(BitConverter.GetBytes(result), deadline.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                diagnostic?.Invoke("X new-client handoff not confirmed: " + error.Message);
                try { await pipe.WriteAsync(BitConverter.GetBytes(error is OperationCanceledException ? 1460u : 31u), lifetime.Token); }
                catch (Exception writeError) when (writeError is IOException or OperationCanceledException) { }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (loop is not null) await loop;
        await Task.WhenAll(callers);
        foreach (var process in owners.Values) process.Dispose();
        owners.Clear();
        lifetime.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);
}
