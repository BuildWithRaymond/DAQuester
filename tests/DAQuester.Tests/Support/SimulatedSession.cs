using DAQuester.Core;

namespace DAQuester.Tests;

// Shared in-memory session. Commands are recorded and only explicit fixture updates change state.
public class SimulatedSession : IGameSession
{
    public CharacterSnapshot Snapshot { get; private set; } = FoundationTests.Ready();
    public IReadOnlySet<string> Capabilities
    {
        get;
    } = new HashSet<string>
    {
        "navigate",
        "dialog",
        "inventory",
        "bank",
        "walk-trigger"
    };

    public event Action<CharacterSnapshot>? Changed;
    public event Action<string>? Interrupted;
    public List<GameCommand> Commands { get; } = [];

    public Action<GameCommand>? OnCommand;
    public void Set(CharacterSnapshot s)
    {
        Snapshot = s with
        {
            Revision = Snapshot.Revision + 1
        };
        Changed?.Invoke(Snapshot);
    }

    public void Interrupt() => Interrupted?.Invoke("Automation interrupted");
    public Task ExecuteAsync(GameCommand c, CancellationToken t)
    {
        t.ThrowIfCancellationRequested();
        Commands.Add(c);
        OnCommand?.Invoke(c);
        return Task.CompletedTask;
    }

    public async Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken token)
    {
        var end = DateTimeOffset.UtcNow.AddMilliseconds(100);
        while (DateTimeOffset.UtcNow < end)
        {
            token.ThrowIfCancellationRequested();
            if (Snapshot.Revision > after && test(Snapshot))
                return Snapshot;
            await Task.Delay(2, token);
        }

        throw new TimeoutException("Simulation evidence timeout");
    }
}
