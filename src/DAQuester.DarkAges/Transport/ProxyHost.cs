using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace DAQuester.DarkAges.Transport;

public class ProxyHost : IAsyncDisposable
{
    readonly CancellationTokenSource lifetime = new();
    readonly ConcurrentBag<TcpListener> listeners = [];
    readonly ConcurrentBag<ProxyConnection> connections = [];
    // Epoch-based generations do not start over at one after app restart.
    // Shared by all hosts and within JSON's exact integer range.
    static long generation = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1024;
    public event Action<ProxyConnection>? Connected;
    public event Action<string>? Diagnostic;
    public Task StartAsync(int port, IPEndPoint remote, CancellationToken cancellation = default)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        listeners.Add(listener);
        _ = Accept(listener, remote, false, cancellation);
        return Task.CompletedTask;
    }

    public Task<int> StartOwnedAsync(IPEndPoint remote, DAQuester.Core.SavedLoginTicket? login, Action<ProxyConnection>? configure = null, CancellationToken cancellation = default)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        listeners.Add(listener);
        void Associate(ProxyConnection c)
        {
            c.LoginTicket = login;
            configure?.Invoke(c);
        }

        _ = Accept(listener, remote, true, cancellation, Associate);
        return Task.FromResult(((IPEndPoint)listener.LocalEndpoint).Port);
    }

    async Task<int> Redirect(IPEndPoint remote, Action<ProxyConnection>? configure = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        listeners.Add(listener);
        _ = Accept(listener, remote, true, lifetime.Token, configure);
        await Task.CompletedTask;
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    async Task Accept(TcpListener listener, IPEndPoint endpoint, bool once, CancellationToken token, Action<ProxyConnection>? configure = null)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        if (once)
            linked.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            do
            {
                var client = await listener.AcceptTcpClientAsync(linked.Token);
                if (once)
                    listener.Stop();
                _ = Connect(client, endpoint, lifetime.Token, configure);
            }
            while (!once && !linked.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Diagnostic?.Invoke("Listener: " + e.GetType().Name);
        }
        finally
        {
            if (once)
                listener.Stop();
        }
    }

    async Task Connect(TcpClient client, IPEndPoint endpoint, CancellationToken token, Action<ProxyConnection>? configure = null)
    {
        var remote = new TcpClient();
        try
        {
            await remote.ConnectAsync(endpoint.Address, endpoint.Port, token);
            await using var c = new ProxyConnection(client, remote, Interlocked.Increment(ref generation), target => Redirect(target, configure));
            configure?.Invoke(c);
            connections.Add(c);
            c.Diagnostic += m => Diagnostic?.Invoke(m);
            Connected?.Invoke(c);
            await c.RunAsync(token);
        }
        catch (Exception e)
        {
            Diagnostic?.Invoke("Connection: " + e.GetType().Name);
            client.Dispose();
            remote.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        foreach (var l in listeners)
            l.Stop();
        foreach (var c in connections)
            await c.DisposeAsync();
    }
}
