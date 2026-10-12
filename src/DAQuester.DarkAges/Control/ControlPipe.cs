using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace DAQuester.DarkAges.Control;

public static class ControlPipe
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    public const string Name = "DAQuester.Control.v1";
    public static async Task<object?> CallAsync(ControlRequest request, CancellationToken cancellation = default, string pipeName = Name)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, cancellation);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, Json).AsMemory(), cancellation);
        var line = await reader.ReadLineAsync(cancellation) ?? throw new IOException("DAQuester control connection closed.");
        using var doc = JsonDocument.Parse(line);
        if (doc.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.GetString());
        return doc.RootElement.GetProperty("result").Clone();
    }
}

public sealed class ControlServer(Func<ControlRequest, Task<object?>> handler, string pipeName = ControlPipe.Name) : IAsyncDisposable
{
    readonly CancellationTokenSource lifetime = new();
    readonly List<Task> clients = [];
    Task? loop;
    public void Start() => loop = AcceptAsync();
    async Task AcceptAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 16, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(lifetime.Token);
                clients.RemoveAll(t => t.IsCompleted);
                clients.Add(ServeAsync(pipe));
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
        }
    }

    async Task ServeAsync(NamedPipeServerStream pipe)
    {
        using (pipe)
        using (var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true))
        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        }

        )
        {
            try
            {
                using var readLimit = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                readLimit.CancelAfter(TimeSpan.FromSeconds(10));
                var line = await reader.ReadLineAsync(readLimit.Token) ?? throw new IOException("Empty control request.");
                if (line.Length > 65536)
                    throw new InvalidDataException("Control request too large.");
                var request = JsonSerializer.Deserialize<ControlRequest>(line, ControlPipe.Json) ?? throw new InvalidDataException("Invalid control request.");
                object? result = await handler(request);
                await writer.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    result
                }, ControlPipe.Json));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                try
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        error = e.Message
                    }, ControlPipe.Json));
                }
                catch (IOException)
                {
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (loop is not null)
            await loop;
        await Task.WhenAll(clients);
        lifetime.Dispose();
    }
}
