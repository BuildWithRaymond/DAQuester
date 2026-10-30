using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.DarkAges;

public sealed partial class GameSession : IGameSession
{
    readonly ProxyConnection connection;
    readonly MapRepository maps;
    readonly SemaphoreSlim actions = new(1, 1);
    readonly int cadence;
    DateTimeOffset lastMove;
    readonly BounceGuard bounce = new(3);
    long dismissedMerchantRevision = -1;
    volatile bool townWeaponRemovalRequired;
    Dialog? preparedDepositPrompt;
    readonly EvidenceHistory history = new();
    public CharacterSnapshot Snapshot => connection.Tracker.Snapshot;
    public long VisibleMoves => connection.VisibleMoves;
    public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>
    {
        "navigate",
        "dialog",
        "inventory",
        "sort-bag",
        "bank",
        "walk-trigger",
        "ground-item",
        "speech",
        "aislingexchange-pair",
        "item",
        "skill",
        "spell",
        "equipment",
        "status-message",
        "legend",
        "trade"
    };

    public event Action<CharacterSnapshot>? Changed;
    public event Action<string>? Interrupted;

    public GameSession(ProxyConnection c, MapRepository maps, int cadence = 400)
    {
        connection = c;
        this.maps = maps;
        this.cadence = cadence;
        npcObservedMap = c.Tracker.Snapshot.Map;
        history.Record(c.Tracker.Snapshot);
        if (ObservedMap is { } initialMap)
            maps.RecordVisited(initialMap);
        townWeaponRemovalRequired = IsWeaponNotice(c.Tracker.Snapshot) || IsWeaponStatus(c.Tracker.Snapshot);
        c.Tracker.Changed += s =>
        {
            ObserveEntryRefusal(s);
            ObserveNpcLanding(s);
            if (ObservedMap is { } visitedMap)
                maps.RecordVisited(visitedMap);
            // The entry map packet can clear this popup before the walker sees it.
            // Keep the restriction until authoritative equipment confirms removal.
            if (!s.Connected)
                townWeaponRemovalRequired = false;
            else if (IsWeaponNotice(s) || IsWeaponStatus(s))
                townWeaponRemovalRequired = true;
            history.Record(s);
            PulseMovement();
            Changed?.Invoke(s);
        };
        c.MovementChanged += PulseMovement;
        c.Interrupted += reason => Interrupted?.Invoke(reason);
    }

    public async Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken cancellation)
    {
        long generation = Snapshot.Generation;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(timeout);
        var pulse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Notify(CharacterSnapshot _) => pulse.TrySetResult();
        Changed += Notify;
        try
        {
            while (true)
            {
                var s = Snapshot;
                if (!s.Connected || s.Generation != generation)
                    throw new IOException("Session disconnected or replaced.");
                cancellation.ThrowIfCancellationRequested();
                if (history.Find(generation, after, test) is { } evidence)
                    return evidence;
                if (s.Revision > after && test(s))
                    return s;
                await pulse.Task.WaitAsync(limit.Token);
                pulse = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Server evidence timeout.");
        }
        finally
        {
            Changed -= Notify;
        }
    }

    static ClientPacket Packet(IClientMessage message)
    {
        var code = ClientMessageFactory.Default.GetMessageCommand(message.GetType())
            ?? throw new InvalidOperationException("Unknown client command.");
        var builder = new NetworkPacketBuilder(code);
        try
        {
            message.Serialize(ref builder);
            return (ClientPacket)builder.ToPacket();
        }
        finally
        {
            builder.Dispose();
        }
    }

    Task Send(IClientMessage m, CancellationToken ct) => connection.SendAsync(Packet(m), Snapshot.Generation, ct);
}
