using System.Net.Sockets;
using System.Reflection;
using System.Text;
using DAQuester.Core;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

public sealed class LoginRegistrationRaceTests
{
    static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    static void Invoke(ProxyConnection connection, string method, params object[] arguments)
    {
        var member = typeof(ProxyConnection).GetMethod(method, PrivateInstance);
        Assert.NotNull(member);
        member.Invoke(connection, arguments);
    }

    static object PrivateField(object target, string name)
    {
        var field = target.GetType().GetField(name, PrivateInstance);
        Assert.NotNull(field);
        return field.GetValue(target)!;
    }

    static byte[] Metadata() => new byte[] { 3, 9, 1, 2, 3, 4, 5, 6, 7, 8, 9, 5 }
        .Concat(Encoding.ASCII.GetBytes("Agent")).Concat(new byte[] { 0, 0, 0, 42 }).ToArray();

    static (ProxyConnection Connection, LoginRegistrationTransfer Transfer) Ready(bool direct)
    {
        // State-only synthetic connections: no sockets are connected and no game executable runs.
        var connection = new ProxyConnection(new TcpClient(), new TcpClient(), 18, _ => Task.FromResult(0));
        typeof(ProxyConnection).GetField("name", PrivateInstance)!.SetValue(connection, "Agent");
        connection.Tracker.Identity("Agent", 77);
        if (direct) connection.Tracker.Observe(0x0A, DaydreamerProtocolTests.Message(2, "Your expiration date is 10-29"));
        var metadata = Metadata();
        var payload = new byte[] { 1, 0, 0, 127, 10, 51, (byte)metadata.Length }.Concat(metadata).ToArray();
        var source = new ServerNotice(17, 5, 2, "Your expiration date is 10-29", DateTimeOffset.UtcNow);
        var transfer = Assert.IsType<LoginRegistrationTransfer>(LoginRegistrationTransfer.TryCreate(source, "Agent", payload, DateTimeOffset.UtcNow));
        Assert.True(transfer.ObserveHandshake(metadata, DateTimeOffset.UtcNow));
        connection.IncomingLoginRegistration = transfer;
        return (connection, transfer);
    }

    static byte[] LoginRequest() => new byte[] { 5 }.Concat(Encoding.ASCII.GetBytes("Agent")).Concat(new byte[] { 1, 88 }).ToArray();

    [Theory]
    [InlineData("login", false)]
    [InlineData("handshake", false)]
    [InlineData("login", true)]
    [InlineData("handshake", true)]
    public async Task A_new_login_boundary_revokes_an_already_adopted_binding(string boundary, bool direct)
    {
        var (connection, _) = Ready(direct);
        await using var cleanup = connection;
        Invoke(connection, "AdoptLoginRegistration", 77u);
        Assert.True(DaydreamerEvidence.HasCurrentRegistration(connection.Tracker.Snapshot));

        Invoke(connection, boundary == "login" ? "ObserveLoginSubmission" : "ObserveLoginHandshake",
            boundary == "login" ? LoginRequest() : Metadata());

        Assert.False(DaydreamerEvidence.HasCurrentRegistration(connection.Tracker.Snapshot));
        Assert.Null(connection.Tracker.Snapshot.Registration);
        Assert.Null(connection.Tracker.Snapshot.LoginRegistration);
        Assert.Null(connection.Tracker.Snapshot.ActiveStatus);
        Assert.Null(connection.Tracker.Snapshot.DaydreamerStatus);
    }

    [Theory]
    [InlineData("login", false)]
    [InlineData("handshake", false)]
    [InlineData("login", true)]
    [InlineData("handshake", true)]
    public async Task A_login_boundary_during_adoption_cannot_leave_the_old_binding_authorized(string boundary, bool direct)
    {
        var (connection, transfer) = Ready(direct);
        await using var cleanup = connection;
        var transferGate = PrivateField(transfer, "gate");
        var connectionGate = PrivateField(connection, "loginRegistrationGate");
        Task adoption, submission;
        using var submissionStarted = new ManualResetEventSlim();
        Monitor.Enter(transferGate);
        try
        {
            adoption = Task.Run(() => Invoke(connection, "AdoptLoginRegistration", 77u));
            // Old code detaches the proof before waiting for its lock. Correct code
            // holds the connection gate across consume/adoption instead.
            Assert.True(SpinWait.SpinUntil(() => connection.IncomingLoginRegistration is null ||
                LockedElsewhere(connectionGate), TimeSpan.FromSeconds(5)), "Adoption did not reach the controlled boundary.");
            bool adoptionHoldsGate = LockedElsewhere(connectionGate);
            submission = Task.Run(() =>
            {
                submissionStarted.Set();
                Invoke(connection, boundary == "login" ? "ObserveLoginSubmission" : "ObserveLoginHandshake",
                    boundary == "login" ? LoginRequest() : Metadata());
            });
            Assert.True(submissionStarted.Wait(TimeSpan.FromSeconds(5)));
            // Without an adoption gate, force the competing boundary to finish
            // before allowing Consume. A clear-only fix must still fail here.
            if (!adoptionHoldsGate)
                Assert.True(SpinWait.SpinUntil(() => submission.IsCompleted, TimeSpan.FromSeconds(5)),
                    "Submission did not reach the controlled boundary.");
        }
        finally { Monitor.Exit(transferGate); }

        await Task.WhenAll(adoption, submission).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(connection.Tracker.Snapshot.LoginRegistration);
        Assert.Null(connection.Tracker.Snapshot.Registration);
        Assert.False(DaydreamerEvidence.HasCurrentRegistration(connection.Tracker.Snapshot));
    }

    static bool LockedElsewhere(object gate)
    {
        if (!Monitor.TryEnter(gate)) return true;
        Monitor.Exit(gate);
        return false;
    }
}
