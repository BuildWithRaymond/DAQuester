using System.Net;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    void RequireOpen()
    {
        if (closing || shuttingDown)
            throw new InvalidOperationException("DAQuester is closing. Wait for progress to finish saving.");
    }

    public async Task StartAsync()
    {
        RequireOpen();
        if (started || Preview)
            return;
        started = true;
        try
        {
            if (Settings.LocalPorts.Length != 3 || Settings.RemotePorts.Length != 3)
                throw new InvalidDataException("Configure exactly three local and remote ports.");
            for (int i = 0; i < 3; i++)
                await host.StartAsync(Settings.LocalPorts[i], new(IPAddress.Parse(Settings.RemoteAddress), Settings.RemotePorts[i]));
            Ready = true;
            ConnectionStatus = "Connection ready";
            ConnectionError = null;
            Log("Loopback proxies ready on " + string.Join(", ", Settings.LocalPorts));
        }
        catch (Exception e)
        {
            ConnectionError = e.Message;
            ConnectionStatus = "Connection unavailable. Check the connection profile in profiles.json, then restart DAQuester.";
            Log("Proxy startup blocked: " + e.Message);
        }

        controlServer = new(request => OnUiAsync(() => ControlAsync(request)));
        controlServer.Start();
        Log("Local control bridge ready; current Windows user only.");
        await Updates.StartAsync();
        Notify();
    }

    async Task BindAsync(ProxyConnection connection)
    {
        try
        {
            if (closing || shuttingDown)
                return;
            string name = connection.Tracker.Snapshot.Name!;
            if (runnerPreparations.TryGetValue(name, out var preparing))
                preparing.Cancel();
            if (runners.Remove(name, out var old))
                await old.PauseAsync("Connection replaced");
            if (closing || shuttingDown)
                return;
            CancelControl(name);
            var maps = new MapRepository(Path.GetDirectoryName(Settings.ClientPath)!, Path.Combine(AppSettings.Content, "routes.json"), Settings.ServerProfile);
            var session = new GameSession(connection, maps, Settings.MovementMilliseconds)
            {
                FastMode = Settings.FastModeEnabled(name),
                IsOwnCharacter = n => ownCharacters.Contains(n)
            };
            sessions[name] = session;
            session.Changed += snapshot => Ui(() =>
            {
                if (sessions.GetValueOrDefault(name) != session)
                    return;
                if (ownCharacters.Contains(name) != snapshot.Connected)
                    RefreshOwnCharacters();
                RememberCharacter(name, session, snapshot);
                Notify();
                if (PermanentQuestCompletion.Matches(snapshot)
                    && journal.Load(name, "spare-a-stick").PermanentCompletion is null
                    && (!permanentTasks.TryGetValue(name, out var task) || task.IsCompleted))
                    permanentTasks[name] = ObservePermanentAsync(name, session, snapshot);
                else if (!controlActions.ContainsKey(name)
                    && (!calibrationTasks.TryGetValue(name, out var capture) || capture.IsCompleted)
                    && runners.TryGetValue(name, out var runner) && runner.Job.Quest == "spare-a-stick")
                    _ = ObserveOrdinaryCompletionAsync(name, session, runner, snapshot);
            });
            session.Interrupted += Log;
            // Evidence lives in memory; a dropped connection is exactly when it must not be lost.
            session.Interrupted += reason =>
            {
                if (reason == "Disconnected")
                    Ui(() => AutoExport(name + " disconnected"));
            };
            RememberCharacter(name, session, session.Snapshot);
            _ = ReadOwnLegendAsync(name, session);
            Notify();
            Log(name + " connected; server state tracking active.");
        }
        catch (Exception e)
        {
            Log("Character connection could not be bound: " + e.Message);
        }
    }

    public Task StopOwnersAsync(string? name = null) => StopOwnersCoreAsync(name, false);

    async Task StopOwnersCoreAsync(string? name, bool preserveSchedulerEnabled)
    {
        bool stopScheduler = name is null || SchedulerReserves(name);
        Task schedulerSave = Task.CompletedTask;
        if (stopScheduler)
        {
            if (preserveSchedulerEnabled)
                scheduler.Suspend();
            else
                schedulerSave = scheduler.SetEnabledAsync(false, false);
        }
        CancelLaunches(name);
        CancelControl(name);
        bool Match(string character) => name is null || character.Equals(name, StringComparison.OrdinalIgnoreCase);
        foreach (var prepare in runnerPreparations.Where(p => Match(p.Key)).Select(p => p.Value).ToArray())
            prepare.Cancel();
        foreach (var prepare in queuePreparations.Where(p => Match(p.Key)).Select(p => p.Value).ToArray())
            prepare.Cancel();
        // Start all cancellations before awaiting any potentially failing checkpoint.
        var queues = Characters.Concat(queueCache.Keys).Concat(queueCoordinators.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Match)
            .Select(character => PauseQueueAsync(character, true))
            .ToArray();
        var quests = runners.Where(p => Match(p.Key)).Select(p => p.Value.PauseAsync("Stopped; progress saved")).ToArray();
        await Task.WhenAll(queues.Concat(quests)
            .Append(schedulerSave)
            .Append(stopScheduler ? scheduler.WaitForIdleAsync() : Task.CompletedTask)
            .Append(AwaitLaunchesAsync(name))
            .Append(WaitForQueueSavesAsync(name))
            .Append(WaitForRunnerPreparationsAsync(name))
            .Append(WaitForControlActionsAsync(name))
            .Concat(calibrationTasks.Where(p => Match(p.Key)).Select(p => p.Value))
            .Concat(permanentTasks.Where(p => Match(p.Key)).Select(p => p.Value)));
        if (Preview)
            previewRunning.RemoveWhere(Match);
        Notify();
    }

    public Task ShutdownAsync(bool installRequested = false)
    {
        if (shutdownTask is { IsCompleted: false })
            return shutdownTask;
        return shutdownTask = ShutdownCoreAsync(installRequested);
    }

    async Task ShutdownCoreAsync(bool installRequested)
    {
        if (closing)
            return;
        shuttingDown = true;
        try
        {
            await StopOwnersCoreAsync(null, true);
            bool prepared = Updates.PrepareExit(QuestActionsRunning, ConnectedGame, LaunchedClientRunning);
            if (installRequested && !prepared)
                throw new InvalidOperationException(Updates.Status + " Finish quests and close game clients, then try again.");
            if (presentationRefresh is { } displayRead)
                await displayRead;
            AutoExport("closing");
            closing = true;
            characterMemoryLifetime.Cancel();
            if (windowingHost is not null)
                await windowingHost.DisposeAsync();
            await host.DisposeAsync();
            if (controlServer is not null)
                await controlServer.DisposeAsync();
            await Task.WhenAll(characterMemoryWrites.ToArray());
            foreach (var process in launchedClients)
                process.Dispose();
            characterMemoryLifetime.Dispose();
            Updates.Changed -= Notify;
        }
        finally
        {
            shuttingDown = false;
        }
    }
}
