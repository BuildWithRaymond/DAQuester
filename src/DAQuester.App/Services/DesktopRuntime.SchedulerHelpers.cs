using System.Diagnostics;
using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    async Task<IQuestHelper> AcquireScheduledHelperAsync(string name, CancellationToken cancellation)
    {
        if (scheduler.LoginBlock(name) is { } block)
            throw new ScheduledLoginException(name, block);
        if (HelperBusy(name) is { } busy || Snapshot(name).Connected || OwnsLiveClient(name))
            throw new ScheduledLoginException(name, name + " is already online or busy. Scheduled helpers require their own new game window.");
        if (!CredentialVault.Exists(name))
            throw new ScheduledLoginException(name, name + " needs a saved login before scheduled helper work.");
        schedulerHelpers.Add(name);
        Process? client = null;
        IGameSession? session = null;
        using var cancelLaunch = cancellation.Register(() => Ui(() => CancelLaunches(name)));
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var launch = LaunchOwnedCharacterAsync(name, false, scheduled: true);
            launchTasks[name] = launch;
            await launch;
            client = ownedClients.GetValueOrDefault(name) ?? throw new QuestBlockedException("Scheduled helper's game window was not found.");
            for (int wait = 0; !(sessions.TryGetValue(name, out var found) && (session = found).Snapshot.Connected); wait++)
            {
                if (wait >= 200)
                    throw new QuestBlockedException("Scheduled helper's fresh connection was not ready.");
                await Task.Delay(100, cancellation);
            }
            var released = new CancellationTokenSource();
            controlActions[name] = released;
            void Interrupted(string reason)
            {
                released.Cancel();
            }
            session!.Interrupted += Interrupted;
            var lease = new ScheduledHelperLease(name, session, released, async () =>
            {
                try
                {
                    await CloseSchedulerClientAsync(name, client, session);
                }
                finally
                {
                    session.Interrupted -= Interrupted;
                    if (controlActions.GetValueOrDefault(name) == released)
                        controlActions.Remove(name);
                    schedulerHelpers.Remove(name);
                    released.Dispose();
                    Notify();
                }
            });
            try
            {
                if (!session.Snapshot.InventoryKnown)
                    await session.WaitAsync(s => s.InventoryKnown, session.Snapshot.Revision - 1, TimeSpan.FromSeconds(20), cancellation);
                return lease;
            }
            catch { await lease.DisposeAsync(); throw; }
        }
        catch (Exception error)
        {
            try
            {
                if (schedulerHelpers.Contains(name))
                {
                    client ??= ownedClients.GetValueOrDefault(name);
                    if (client is not null)
                        await CloseSchedulerClientAsync(name, client, session);
                }
            }
            finally { schedulerHelpers.Remove(name); }
            if (error is OperationCanceledException)
                throw;
            await scheduler.BlockLoginAsync(name, error.Message);
            throw new ScheduledLoginException(name, name + ": " + error.Message);
        }
    }

    sealed class ScheduledHelperLease(string name, IGameSession session, CancellationTokenSource released, Func<Task> cleanup) : IQuestHelper
    {
        Task? cleanupTask;
        public string Name => name;
        public IGameSession Session => session;
        public bool LoggedInForQuest => true;
        public CancellationToken Released { get; } = released.Token;
        public Task LogOutAsync(CancellationToken cancellation) => cleanupTask ??= cleanup();
        public ValueTask DisposeAsync() => new(cleanupTask ??= cleanup());
    }
}
