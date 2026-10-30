using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Channels;
using Arbiter.Net.Server;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

public sealed class ProxyUpdateAdmissionTests
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    // Reflection keeps the first RED run compilable before the new admission API exists.
    static bool HasConnections(ProxyHost host)
    {
        var property = typeof(ProxyHost).GetProperty("HasConnections");
        Assert.NotNull(property);
        return Assert.IsType<bool>(property.GetValue(host));
    }

    static bool TryPause(ProxyHost host)
    {
        var method = typeof(ProxyHost).GetMethod("TryPauseForUpdate");
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(host, null));
    }

    static void Resume(ProxyHost host)
    {
        var method = typeof(ProxyHost).GetMethod("ResumeAfterUpdateFailure");
        Assert.NotNull(method);
        method.Invoke(host, null);
    }

    static TcpListener[] Listeners(ProxyHost host) =>
        ((IEnumerable<TcpListener>)typeof(ProxyHost).GetField("listeners", Private)!.GetValue(host)!).ToArray();

    static async Task Until(Func<bool> condition, CancellationToken cancellation)
    {
        while (!condition())
            await Task.Delay(10, cancellation);
    }

    [Fact]
    public async Task Pending_owned_listener_blocks_update_without_a_connected_socket_and_cancellation_releases_it()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var pending = new CancellationTokenSource();
        using var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        await using var host = new ProxyHost();
        Assert.False(HasConnections(host));

        await host.StartOwnedAsync((IPEndPoint)remote.LocalEndpoint, null, cancellation: pending.Token);

        Assert.True(HasConnections(host));
        Assert.False(TryPause(host));
        Assert.False(remote.Pending());
        pending.Cancel();
        await Until(() => !HasConnections(host), deadline.Token);
        Assert.True(TryPause(host));
    }

    [Fact]
    public async Task Pre_authentication_connection_blocks_update_without_being_disconnected()
    {
        await using var f = new Fixture();
        int port = await f.Host.StartOwnedAsync(f.RemoteEndpoint, null);
        using var client = await f.Client(port);
        using var server = await f.Remote.AcceptTcpClientAsync(f.Deadline.Token);
        var connection = await f.Accepted.Reader.ReadAsync(f.Deadline.Token);

        Assert.False(connection.Tracker.Snapshot.Connected);
        Assert.True(HasConnections(f.Host));
        Assert.False(TryPause(f.Host));
        await f.Forward(server, client);

        client.Dispose();
        server.Dispose();
        await Until(() => !HasConnections(f.Host), f.Deadline.Token);
        Assert.True(TryPause(f.Host));
    }

    [Fact]
    public async Task In_flight_connection_is_counted_before_configuration_or_authenticated_events()
    {
        await using var f = new Fixture();
        var configuring = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        int port = await f.Host.StartOwnedAsync(f.RemoteEndpoint, null, _ =>
        {
            configuring.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Synthetic configuration barrier timed out.");
        });
        using var client = await f.Client(port);
        using var server = await f.Remote.AcceptTcpClientAsync(f.Deadline.Token);
        try
        {
            await configuring.Task.WaitAsync(f.Deadline.Token);
            Assert.False(f.Accepted.Reader.TryRead(out _));
            Assert.True(HasConnections(f.Host));
            Assert.False(TryPause(f.Host));
        }
        finally
        {
            release.Set();
        }

        await f.Accepted.Reader.ReadAsync(f.Deadline.Token);
        await f.Forward(server, client);
    }

    [Fact]
    public async Task Idle_permanent_listener_can_pause_and_rejects_new_admission_until_resumed()
    {
        await using var f = new Fixture();
        await f.Host.StartAsync(0, f.RemoteEndpoint);
        int port = ((IPEndPoint)Listeners(f.Host).Single().LocalEndpoint).Port;
        Assert.False(HasConnections(f.Host));
        Assert.True(TryPause(f.Host));
        Assert.False(TryPause(f.Host));

        using (var rejected = await f.Client(port))
        {
            try
            {
                Assert.Equal(0, await rejected.GetStream().ReadAsync(new byte[1], f.Deadline.Token));
            }
            catch (IOException error) when (error.InnerException is SocketException)
            {
                // Windows may report an abort rather than EOF for a newly rejected socket.
            }
        }

        Assert.False(HasConnections(f.Host));
        Assert.False(f.Remote.Pending());
        Assert.False(f.Accepted.Reader.TryRead(out _));
        Resume(f.Host);
        using var client = await f.Client(port);
        using var server = await f.Remote.AcceptTcpClientAsync(f.Deadline.Token);
        await f.Accepted.Reader.ReadAsync(f.Deadline.Token);
        Assert.True(HasConnections(f.Host));
        await f.Forward(server, client);
    }

    [Fact]
    public async Task Paused_host_rejects_new_owned_listeners_before_creation_and_resume_allows_them()
    {
        using var pending = new CancellationTokenSource();
        await using var f = new Fixture();
        Assert.True(TryPause(f.Host));
        int before = Listeners(f.Host).Length;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await f.Host.StartOwnedAsync(f.RemoteEndpoint, null, cancellation: pending.Token));

        Assert.Equal(before, Listeners(f.Host).Length);
        Assert.False(HasConnections(f.Host));
        Resume(f.Host);
        await f.Host.StartOwnedAsync(f.RemoteEndpoint, null, cancellation: pending.Token);
        Assert.True(HasConnections(f.Host));
        pending.Cancel();
        await Until(() => !HasConnections(f.Host), f.Deadline.Token);
    }

    [Fact]
    public async Task Redirect_listener_keeps_update_blocked_after_the_parent_connection_finishes()
    {
        await using var f = new Fixture();
        int port = await f.Host.StartOwnedAsync(f.RemoteEndpoint, null);
        using var client = await f.Client(port);
        using var server = await f.Remote.AcceptTcpClientAsync(f.Deadline.Token);
        var parent = await f.Accepted.Reader.ReadAsync(f.Deadline.Token);
        int childPort = await parent.LoginRedirect!(f.RemoteEndpoint, null);
        client.Dispose();
        server.Dispose();
        var active = typeof(ProxyHost).GetField("activeConnections", Private);
        Assert.NotNull(active);
        await Until(() => Assert.IsType<int>(active.GetValue(f.Host)) == 0, f.Deadline.Token);

        Assert.True(HasConnections(f.Host));
        Assert.False(TryPause(f.Host));

        using var childClient = await f.Client(childPort);
        using var childServer = await f.Remote.AcceptTcpClientAsync(f.Deadline.Token);
        await f.Accepted.Reader.ReadAsync(f.Deadline.Token);
        await f.Forward(childServer, childClient);
    }

    [Fact]
    public async Task Failed_remote_connect_releases_its_admission_claim()
    {
        await using var f = new Fixture();
        using var unavailable = new TcpListener(IPAddress.Loopback, 0);
        unavailable.Start();
        var endpoint = (IPEndPoint)unavailable.LocalEndpoint;
        unavailable.Stop();
        int port = await f.Host.StartOwnedAsync(endpoint, null);
        using var client = await f.Client(port);

        await Until(() => !HasConnections(f.Host), f.Deadline.Token);

        Assert.True(TryPause(f.Host));
        Assert.False(f.Accepted.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Failed_connect_closes_the_socket_before_releasing_ownership_even_if_diagnostics_throw()
    {
        await using var f = new Fixture();
        using var unavailable = new TcpListener(IPAddress.Loopback, 0);
        unavailable.Start();
        var endpoint = (IPEndPoint)unavailable.LocalEndpoint;
        unavailable.Stop();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.Diagnostic += _ =>
        {
            failed.TrySetResult();
            throw new IOException("Synthetic diagnostic observer failure.");
        };
        int port = await f.Host.StartOwnedAsync(endpoint, null);
        using var client = await f.Client(port);
        await failed.Task.WaitAsync(f.Deadline.Token);
        await Until(() => !HasConnections(f.Host), f.Deadline.Token);

        using var readDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            Assert.Equal(0, await client.GetStream().ReadAsync(new byte[1], readDeadline.Token));
        }
        catch (IOException error) when (error.InnerException is SocketException)
        {
            // Either EOF or a reset proves the failed proxy no longer owns a live socket.
        }
        Assert.True(TryPause(f.Host));
    }

    sealed class Fixture : IAsyncDisposable
    {
        public ProxyHost Host { get; } = new();
        public TcpListener Remote { get; } = new(IPAddress.Loopback, 0);
        public IPEndPoint RemoteEndpoint => (IPEndPoint)Remote.LocalEndpoint;
        public CancellationTokenSource Deadline { get; } = new(TimeSpan.FromSeconds(10));
        public Channel<ProxyConnection> Accepted { get; } = Channel.CreateUnbounded<ProxyConnection>();

        public Fixture()
        {
            Remote.Start();
            Host.Connected += connection => Accepted.Writer.TryWrite(connection);
        }

        public async Task<TcpClient> Client(int port)
        {
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, Deadline.Token);
            return client;
        }

        public async Task Forward(TcpClient server, TcpClient client)
        {
            byte[] wire = new ServerPacket(0x00, new byte[] { 1, 2 }).ToArray();
            await server.GetStream().WriteAsync(wire, Deadline.Token);
            var received = new byte[wire.Length];
            await client.GetStream().ReadExactlyAsync(received, Deadline.Token);
            Assert.Equal(wire, received);
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            Remote.Stop();
            Deadline.Dispose();
        }
    }
}
