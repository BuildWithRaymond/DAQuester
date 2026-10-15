namespace DAQuester.DarkAges.Launcher;

// Owns only the new process created for this launch, never an attached client.
internal interface ISuspendedClient : IDisposable
{
    int Id { get; }
    nint Allocate(byte[] bytes);
    void Patch(int address, byte[] bytes);
    uint LoadWindowing(string library, CancellationToken cancellation = default);
    void Resume();
    void Abort();
}
