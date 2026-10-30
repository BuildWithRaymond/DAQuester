using System.Net;
using System.Net.Sockets;
using Arbiter.Net;
using Arbiter.Net.Client;
using Arbiter.Net.Server;
using Arbiter.Net.Security;
using Arbiter.Net.Serialization;
using DAQuester.DarkAges.Protocol;

namespace DAQuester.DarkAges.Transport;

public sealed partial class ProxyConnection(TcpClient client, TcpClient remote, long generation, Func<IPEndPoint, Task<int>> redirect) : IAsyncDisposable
{
    readonly SemaphoreSlim toServer = new(1, 1);
    readonly SemaphoreSlim toClient = new(1, 1);
    readonly CancellationTokenSource lifetime = new();
    readonly ClientPacketEncryptor clientCrypto = new();
    readonly ServerPacketEncryptor serverCrypto = new();
    byte sequence, walkCounter;
    byte clientSequence;
    bool presenting;
    long visibleMoves;
    public long VisibleMoves => Interlocked.Read(ref visibleMoves);
    long manualOperationRevision;
    internal long ManualOperationRevision => Interlocked.Read(ref manualOperationRevision);
    long manualBagOperationRevision;
    internal long ManualBagOperationRevision => Interlocked.Read(ref manualBagOperationRevision);

    string? name;
    bool identified;
    internal DAQuester.Core.SavedLoginTicket? LoginTicket
    {
        get;
        set;
    }

    readonly HashSet<byte> serverOpcodes = [];
    public StateTracker Tracker { get; } = new(generation);

    public event Action<string>? Interrupted;
    public event Action<string>? Diagnostic;
    public void Report(string line) => Diagnostic?.Invoke(line);
    public event Action<ProxyConnection>? Authenticated;
    public event Action<LoginResponse>? LoginResponded;
    public event Action<ClientPacket>? DialogAction;
    public event Action<ClientPacket>? ExchangeAction;
    public event Action<ClientPacket>? DropAction;
    public bool Connected => !lifetime.IsCancellationRequested && client.Connected && remote.Connected;

    public async Task RunAsync(CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        client.NoDelay = true;
        remote.NoDelay = true;
        try
        {
            var a = Pump(client.GetStream(), true, linked.Token);
            var b = Pump(remote.GetStream(), false, linked.Token);
            await Task.WhenAny(a, b);
            linked.Cancel();
            try
            {
                await Task.WhenAll(a, b);
            }
            catch (OperationCanceledException)
            {
            }
        }
        catch (Exception e)
        {
            Diagnostic?.Invoke("Transport failed: " + Flatten(e));
        }
        finally
        {
            lifetime.Cancel();
            lock (movementGate)
            lock (loginRegistrationGate)
                Tracker.Disconnect();
            Interrupt("Disconnected");
        }
    }

    static string Flatten(Exception e) => e.ToString().ReplaceLineEndings(" | ");
    // Subscribers run inside the packet loop; their failures must not drop the connection.
    void Interrupt(string reason)
    {
        try
        {
            Interrupted?.Invoke(reason);
        }
        catch (Exception e)
        {
            Diagnostic?.Invoke("Interrupt handler failed: " + Flatten(e));
        }
    }

    static string ClientOpcodeName(byte opcode) => opcode switch
    {
        0x06 => "walk",
        0x11 => "turn",
        0x39 => "merchant reply",
        0x3A => "dialog reply",
        0x43 => "click",
        _ => "client packet"
    };
    // Client packets for the replay: (opcode, payload, sent by DAQuester rather than the game client).
    public event Action<byte, byte[], bool>? ClientObserved;
    // Login, character creation and password change carry credentials and are never reported.
    internal static bool CarriesCredentials(byte opcode) => opcode is 0x02 or 0x03 or 0x26;
    // Sorting owns only the bag. This separate epoch leaves quest/manual-input
    // semantics unchanged while detecting requests whose inventory replies may lag.
    internal static bool AffectsBag(byte opcode, byte[] payload) => opcode is
        0x07 or 0x08 or 0x0F or 0x13 or 0x1A or 0x1C or 0x29 or
        0x39 or 0x3A or 0x3E or 0x41 or 0x44 or 0x4A or 0x55
        || opcode == 0x30 && (payload.Length != 3 || payload[0] == 0);
    void ReportClient(byte opcode, byte[] payload, bool bot)
    {
        if (!bot && AffectsBag(opcode, payload))
            Interlocked.Increment(ref manualBagOperationRevision);
        if (!bot && (opcode is 0x06 or 0x11 or 0x39 or 0x3A or 0x3F or 0x4A
            || opcode == 0x43 && (payload.Length > 0 && payload[0] == 3 || ClickedEntity(payload) is not null)))
            Interlocked.Increment(ref manualOperationRevision);
        if (!CarriesCredentials(opcode))
            ClientObserved?.Invoke(opcode, payload.ToArray(), bot);
    }

    void InterruptForClientPacket(byte opcode, string reason, uint? target = null)
    {
        CancelMovement();
        string on = target is { } id ? " on " + (Tracker.Snapshot.Entities.TryGetValue(id, out var e) && e.Name.Length > 0 ? e.Name : "#" + id) : "";
        Diagnostic?.Invoke($"{Tracker.Snapshot.Name ?? "Connecting"}: client opcode 0x{opcode:X2} ({ClientOpcodeName(opcode)}){on} interrupted automation");
        Interrupt(reason);
    }

    // A left click on the ground or on yourself only focuses the window; clicking another entity is an interaction.
    uint? ClickedEntity(byte[] data)
    {
        if (data.Length < 5 || data[0] != 1)
            return null;
        uint id = (uint)(data[1] << 24 | data[2] << 16 | data[3] << 8 | data[4]);
        return id == Tracker.Snapshot.Id ? null : id;
    }

    async Task Pump(NetworkStream source, bool fromClient, CancellationToken token)
    {
        var frames = new FrameBuffer();
        var bytes = new byte[8192];
        while (!token.IsCancellationRequested)
        {
            int n = await source.ReadAsync(bytes, token);
            if (n == 0)
                break;
            frames.Append(bytes[..n]);
            while (frames.Read() is { } frame)
            {
                if (fromClient)
                {
                    await toServer.WaitAsync(token);
                    try
                    {
                        var wire = new ClientPacket(frame[3], frame[4..]);
                        var plain = (ClientPacket)clientCrypto.Decrypt(wire);
                        bool loginRewritten = false;
                        if ((byte)plain.Command == 0x03 && LoginTicket is { } ticket)
                        {
                            var replacement = ticket.Rewrite(plain.Data, DateTimeOffset.UtcNow);
                            if (replacement.Blocked)
                                continue;
                            if (replacement.Payload is { } data && !ReferenceEquals(data, plain.Data))
                            {
                                plain = new ClientPacket(0x03, data, plain.Checksum)
                                {
                                    Sequence = plain.Sequence
                                };
                                loginRewritten = true;
                            }
                        }

                        if ((byte)plain.Command == 0x03)
                            ObserveLoginSubmission(plain.Data);

                        if ((byte)plain.Command == 0x10)
                        {
                            ObserveLoginHandshake(plain.Data);
                            var r = new NetworkPacketReader(plain);
                            byte seed = r.ReadByte();
                            var key = r.ReadBytes(r.ReadByte());
                            name = r.ReadString8();
                            var p = new NetworkEncryptionParameters(seed, key, name);
                            clientCrypto.Parameters = p;
                            serverCrypto.Parameters = p;
                        }

                        if ((byte)plain.Command == 0x06)
                        {
                            // Reanchor pending movement from server replies without pausing the run.
                            CancelMovement();
                            if (plain.Data.Length >= 2)
                                plain.Data[1] = walkCounter++;
                        }

                        if ((byte)plain.Command is 0x39 or 0x3A)
                            DialogAction?.Invoke(plain);
                        if ((byte)plain.Command == 0x4A)
                            ExchangeAction?.Invoke(plain);
                        if ((byte)plain.Command == 0x08)
                            DropAction?.Invoke(plain);
                        ReportClient((byte)plain.Command, plain.Data, false);
                        var encrypted = clientCrypto.Encrypt(plain, sequence);
                        if (ClientPacketEncryptor.IsEncrypted((byte)plain.Command))
                            sequence++;
                        try
                        {
                            await remote.GetStream().WriteAsync(encrypted.ToArray(), token);
                        }
                        finally
                        {
                            if (loginRewritten)
                                System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain.Data);
                        }
                    }
                    finally
                    {
                        toServer.Release();
                    }
                }
                else
                {
                    var wire = new ServerPacket(frame[3], frame[4..]);
                    var plain = (ServerPacket)serverCrypto.Decrypt(wire);
                    if ((byte)plain.Command == 0x02)
                    {
                        var response = LoginResponse.Parse(plain.Data);
                        ObserveLoginResponse(response);
                        try
                        {
                            // Shape only: keep credentials, keys and raw login packets out of diagnostics.
                            string result = plain.Data.Length > 0 ? plain.Data[0].ToString() : "missing";
                            string messageLength = plain.Data.Length > 1 ? plain.Data[1].ToString() : "missing";
                            Diagnostic?.Invoke($"Server login response: {plain.Data.Length} payload bytes, result {result}, message length {messageLength}.");
                            LoginResponded?.Invoke(response);
                        }
                        catch (Exception error) { Diagnostic?.Invoke("Login response observer failed: " + Flatten(error)); }
                    }
                    if (serverOpcodes.Add((byte)plain.Command))
                        Diagnostic?.Invoke("Server opcode " + ((byte)plain.Command).ToString("X2") + " observed");
                    if ((byte)plain.Command == 0x00 && plain.Data.Length >= 7 && plain.Data[0] == 0)
                    {
                        var r = new NetworkPacketReader(plain);
                        r.ReadByte();
                        r.ReadUInt32();
                        byte seed = r.ReadByte();
                        var key = r.ReadBytes(r.ReadByte());
                        await toServer.WaitAsync(token);
                        try
                        {
                            var p = new NetworkEncryptionParameters(seed, key);
                            clientCrypto.Parameters = p;
                            serverCrypto.Parameters = p;
                        }
                        finally
                        {
                            toServer.Release();
                        }
                    }

                    if ((byte)plain.Command == 0x03)
                    {
                        var r = new NetworkPacketReader(plain);
                        var address = r.ReadIPv4Address();
                        int port = r.ReadUInt16();
                        var registration = PrepareLoginRegistration(plain.Data);
                        int local = LoginRedirect is { } loginRedirect
                            ? await loginRedirect(new(address, port), registration)
                            : await redirect(new(address, port));
                        // Unique one-shot listener per redirect prevents cross-character handoffs.
                        wire.Data[0] = 1;
                        wire.Data[1] = 0;
                        wire.Data[2] = 0;
                        wire.Data[3] = 127;
                        wire.Data[4] = (byte)(local >> 8);
                        wire.Data[5] = (byte)local;
                        frame = wire.ToArray();
                        Interrupt("Server redirect. Resume on replacement connection.");
                    }

                    if (((byte)plain.Command is 0x05 or 0x52) && name is not null && plain.Data.Length >= 4)
                    {
                        var r = new NetworkPacketReader(plain);
                        uint ownId = r.ReadUInt32();
                        bool newlyIdentified = false;
                        lock (movementGate)
                        lock (loginRegistrationGate)
                        {
                            Tracker.Identity(name, ownId);
                            AdoptLoginRegistration(ownId);
                            if (!identified)
                            {
                                identified = true;
                                newlyIdentified = true;
                            }
                        }
                        if (newlyIdentified)
                            Authenticated?.Invoke(this);
                    }

                    await ForwardToClient(frame, plain, token);
                }
            }
        }
    }

    async Task ForwardToClient(byte[] frame, ServerPacket plain, CancellationToken ct)
    {
        await toClient.WaitAsync(ct);
        try
        {
            ServerPacket? presentation = null;
            try
            {
                lock (movementGate)
                {
                    var before = Tracker.Snapshot;
                    // Client login boundaries revoke registration under the same
                    // lock order, so a server snapshot cannot restore old proof.
                    lock (loginRegistrationGate)
                        Tracker.Observe((byte)plain.Command, plain.Data); // Fully decode before confirming a queued step.
                    presentation = ObserveMovement(before, (byte)plain.Command, plain.Data);
                    if (Tracker.WorldMap is not null)
                        ResetMovement();
                }
            }
            catch (Exception e)
            {
                CancelMovement();
                Interrupt("Unrecognized server state packet " + ((byte)plain.Command).ToString("X2"));
                Diagnostic?.Invoke("State decode " + ((byte)plain.Command).ToString("X2") + ": " + Flatten(e));
            }

            bool encrypted = ServerPacketEncryptor.IsEncrypted((byte)plain.Command);
            if (presenting && encrypted)
                frame = serverCrypto.Encrypt(plain, clientSequence++).ToArray();
            else if (encrypted)
                clientSequence = unchecked((byte)(frame[4] + 1));
            await client.GetStream().WriteAsync(frame, lifetime.Token);
            if (presentation is not null)
            {
                presenting = true;
                await client.GetStream().WriteAsync(serverCrypto.Encrypt(presentation, clientSequence++).ToArray(), lifetime.Token);
            }
        }
        finally
        {
            toClient.Release();
        }
    }

    public async Task PresentMerchantCloseAsync(long expectedGeneration, long dialogRevision, CancellationToken ct)
    {
        await toClient.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var s = Tracker.Snapshot;
            if (!Connected || !s.Connected || s.Generation != expectedGeneration)
                throw new IOException("Session generation no longer active.");
            if (s.Dialog is not { IsPursuit: false } d || d.Revision != dialogRevision)
                throw new DAQuester.Core.QuestBlockedException("Stale merchant dialog.");
            // Pinned Arbiter CloseCurrentDialog: merchant menus close only in the client.
            // Do not publish an invented server snapshot or use this as quest evidence.
            presenting = true;
            await client.GetStream().WriteAsync(serverCrypto.Encrypt(new ServerPacket(0x30, [0x0A, 0]), clientSequence++).ToArray(), lifetime.Token);
        }
        finally
        {
            toClient.Release();
        }
    }

    public async Task SendAsync(ClientPacket packet, long expectedGeneration, CancellationToken token, DAQuester.Core.Tile? movementOrigin = null, bool presentMovement = false, int movementCapacity = 1, Func<bool>? movementAllowed = null, Action? beforeWrite = null, Action? validateSend = null)
    {
        await toServer.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (!Connected || Tracker.Snapshot.Generation != expectedGeneration || !Tracker.Snapshot.Connected)
                throw new IOException("Session generation no longer active.");
            validateSend?.Invoke();
            if ((byte)packet.Command is 0x39 or 0x3A)
                DialogAction?.Invoke(packet);
            if ((byte)packet.Command == 0x4A)
                ExchangeAction?.Invoke(packet);
            if ((byte)packet.Command == 0x08)
                DropAction?.Invoke(packet);
            bool movement = (byte)packet.Command == 0x06 && packet.Data.Length >= 1;
            if (movement)
                await toClient.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                lock (movementGate)
                {
                    if (movement)
                    {
                        if (movementAllowed?.Invoke() == false)
                            throw new MovementReplanException();
                        RegisterMovement(packet.Data[0], expectedGeneration, movementOrigin, presentMovement, movementCapacity);
                        if (packet.Data.Length >= 2)
                            packet.Data[1] = walkCounter++;
                    }

                    if ((byte)packet.Command == 0x38)
                        refreshRequested = true;
                }

                ReportClient((byte)packet.Command, packet.Data, true);
                var encrypted = clientCrypto.Encrypt(packet, sequence);
                var wire = encrypted.ToArray();
                // Observer callbacks and packet construction cannot supply a reply to a write that has not started.
                beforeWrite?.Invoke();
                if (ClientPacketEncryptor.IsEncrypted((byte)packet.Command))
                    sequence++;
                await remote.GetStream().WriteAsync(wire, lifetime.Token);
                if (movement && presentMovement)
                {
                    MovementIntent intent;
                    lock (movementGate)
                    {
                        intent = pendingSteps.Last().Intent;
                    }

                    presenting = true;
                    await client.GetStream().WriteAsync(serverCrypto.Encrypt(PlayerPresentation.Move(intent), clientSequence++).ToArray(), lifetime.Token);
                }
            }
            finally
            {
                if (movement)
                    toClient.Release();
            }
        }
        finally
        {
            toServer.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        client.Dispose();
        remote.Dispose();
        return ValueTask.CompletedTask;
    }
}
