using System.Net;
using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.App.Services;

public sealed record QuestAvailability(string Status, string Reason, bool CanQueue, bool Warning = false)
{
    // A started altar run waits for its next offering itself.
    public bool Ready => Status is "Ready" or "Choices" or "Resume ready" or "Next offering";
}

public sealed record QuestChoiceResult(MundaneBirthOptions? BirthOptions = null, EmeraldDawnOptions? EmeraldOptions = null, bool AllowUnsafeWalking = false, SickChildOptions? SickChildOptions = null, bool OpenCharacters = false, bool AltarNoMarkAccepted = false);
/// <summary>Owns game connections and durable operations independently of the desktop presentation.</summary>
public sealed partial class DesktopRuntime
{
    readonly ProxyHost host = new();
    readonly ReplayRecorder recorder = new();
    readonly Dictionary<string, GameSession> sessions = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, QuestRunner> runners = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, CancellationTokenSource> runnerPreparations = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Task> permanentTasks = new(StringComparer.OrdinalIgnoreCase);
    readonly FileQuestJournal journal;
    readonly FileCharacterMemory characterMemory;
    readonly HashSet<Task> characterMemoryWrites = [];
    readonly CancellationTokenSource characterMemoryLifetime = new();
    readonly Dictionary<(string Character, string Quest), bool> repeatChoices = [];
    readonly List<string> logs = [];
    readonly Dictionary<string, QuestDefinition> recipes;
    readonly Dictionary<int, string> mapNames;
    readonly Dictionary<string, CharacterSnapshot> previewSnapshots = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, QuestQueueState> previewQueues = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> previewRunning = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> previewQueueRunning = new(StringComparer.OrdinalIgnoreCase);
    bool closing, started, shuttingDown;
    bool logFileFailed;
    Task? shutdownTask;
    string CalibrationPath => Path.Combine(AppSettings.DataPath, "calibration", Settings.ServerProfile + ".json");
    public AppSettings Settings
    {
        get;
    }
    public IReadOnlyDictionary<string, QuestDefinition> Recipes => recipes;
    public Calibration Calibration
    {
        get; private set;
    }
    public UpdateCoordinator Updates
    {
        get;
    }
    public bool Ready
    {
        get; private set;
    }
    public string ConnectionStatus { get; private set; } = "Preparing connection…";
    public string? ConnectionError
    {
        get; private set;
    }
    public bool Preview
    {
        get;
    }
    public string[] Characters => Settings.Characters.Concat(sessions.Keys).Concat(previewSnapshots.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public Action<Action> Dispatch
    {
        get;
        set;
    } = action => action();

    public event Action? Changed;
    public event Action<string>? Diagnostic;
    public Func<string, QuestDefinition, QuestJob, string?, Task<QuestChoiceResult?>>? ChooseQuestAsync
    {
        get;
        set;
    }
    public string SelectedQuestId
    {
        get;
        set;
    } = "spare-a-stick";

    public string[] Logs
    {
        get
        {
            lock (logs)
                return logs.ToArray();
        }
    }

    public int RecordedEvents => recorder.Count;

    public bool RecordingEnabled
    {
        get => recorder.Enabled;
        set
        {
            recorder.Enabled = value;
            Notify();
        }
    }

    public string[] ControlActions => controlActions.Keys.ToArray();
    public TreeRegion[] CandidateRegions => TreePatrol.Load(Path.Combine(AppSettings.Content, "calibration", "mileth-tree-candidates.json"));
    public bool QuestActionsRunning => pendingLaunches.Count > 0 || runnerPreparations.Count > 0 || queuePreparations.Count > 0 || queueSaving.Count > 0 || queueCoordinators.Values.Any(q => q.IsRunning) || runners.Values.Any(r => r.IsRunning) || controlActions.Count > 0 || calibrationTasks.Values.Any(t => !t.IsCompleted) || permanentTasks.Values.Any(t => !t.IsCompleted);
    public bool ConnectedGame => sessions.Values.Any(s => s.Snapshot.Connected);
    public bool LaunchedClientRunning => launchedClients.Any(p =>
    {
        try
        {
            return !p.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    });

    public DesktopRuntime(bool preview = false, IUpdateBackend? updateBackend = null)
    {
        Preview = preview;
        Settings = AppSettings.Load();
        journal = new(Path.Combine(AppSettings.DataPath, "journal"));
        characterMemory = new(Path.Combine(AppSettings.DataPath, "characters"), Settings.ServerProfile);
        recipes = Directory.GetFiles(Path.Combine(AppSettings.Content, "recipes"), "*.json").Select(path => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(path), AppSettings.Json)!).ToDictionary(q => q.Id, StringComparer.OrdinalIgnoreCase);
        using (var routes = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.Content, "routes.json"))))
            mapNames = routes.RootElement.EnumerateArray().ToDictionary(m => m.GetProperty("id").GetInt32(), m => m.GetProperty("name").GetString() ?? "Unknown area");
        Directory.CreateDirectory(Path.GetDirectoryName(CalibrationPath)!);
        if (!File.Exists(CalibrationPath))
            File.Copy(Path.Combine(AppSettings.Content, "calibration", "datester-7.41.json"), CalibrationPath);
        Calibration = JsonSerializer.Deserialize<Calibration>(File.ReadAllText(CalibrationPath), AppSettings.Json)!;
        Updates = new(updateBackend ?? new VelopackUpdates(), preview && updateBackend is null)
        {
            AutomaticChecks = Settings.AutomaticUpdateChecks
        };
        Updates.Changed += Notify;
        host.Diagnostic += Log;
        host.Connected += connection =>
        {
            connection.Tracker.Observed += (opcode, payload) => recorder.Record(opcode, payload, connection.Tracker.Snapshot);
            connection.ClientObserved += (opcode, payload, bot) => recorder.RecordClient(opcode, payload, bot, connection.Tracker.Snapshot);
            connection.Tracker.ServerMessage += (_, text) => Log((connection.Tracker.Snapshot.Name ?? "Connecting") + ": server: " + text);
            connection.DialogAction += packet => recorder.RecordDialogPacket(packet, connection.Tracker.Snapshot);
            connection.ExchangeAction += packet => recorder.RecordExchangePacket(packet, connection.Tracker.Snapshot);
            connection.DropAction += packet => recorder.RecordDropPacket(packet, connection.Tracker.Snapshot);
            connection.Authenticated += authenticated => Ui(() => _ = BindAsync(authenticated));
        };
    }

    // Read from the proxy threads while walking, so it is swapped whole rather than edited.
    volatile System.Collections.Immutable.ImmutableHashSet<string> ownCharacters = System.Collections.Immutable.ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);
    void RefreshOwnCharacters() => ownCharacters = System.Collections.Immutable.ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, sessions.Where(p => p.Value.Snapshot.Connected).Select(p => p.Key));

    void Ui(Action action)
    {
        if (!closing)
            Dispatch(action);
    }

    void Notify() => Ui(() => Changed?.Invoke());
    public void Refresh() => Notify();
    public void Log(string message) => Ui(() =>
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
        string? failure = null;
        lock (logs)
            logs.Add(line);
        try
        {
            File.AppendAllText(Path.Combine(AppSettings.DataPath, "diagnostics.log"), line + Environment.NewLine);
            logFileFailed = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (!logFileFailed)
            {
                logFileFailed = true;
                failure = DateTime.Now.ToString("HH:mm:ss") + "  diagnostics.log not written: " + e.Message;
                lock (logs)
                    logs.Add(failure);
            }
        }

        lock (logs)
            while (logs.Count > 2000)
                logs.RemoveAt(0);
        Diagnostic?.Invoke(line);
        if (failure is not null)
            Diagnostic?.Invoke(failure);
    });
    public CharacterSnapshot Snapshot(string name) => Preview && previewSnapshots.TryGetValue(name, out var snapshot) ? snapshot : sessions.GetValueOrDefault(name)?.Snapshot ?? new()
    {
        Name = name
    };
    public Dialog? CurrentDialog(string name) => Snapshot(name).Dialog;
    public long VisibleMoves(string name) => sessions.GetValueOrDefault(name)?.VisibleMoves ?? 0;
    public bool IsRunning(string name) => Preview && previewRunning.Contains(name) || runners.TryGetValue(name, out var owner) && owner.IsRunning;
    public string? RunningQuestId(string name) => calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted && calibrationRuns.TryGetValue(name, out var calibration) ? calibration.Job.Quest : runners.TryGetValue(name, out var runner) && runner.IsRunning ? runner.Job.Quest : null;
    public bool RunnerBusy(string name) => runnerPreparations.ContainsKey(name);
    public void SetPreviewSnapshot(CharacterSnapshot snapshot)
    {
        RequirePreview();
        if (string.IsNullOrWhiteSpace(snapshot.Name))
            throw new ArgumentException("Synthetic snapshot requires a character name.", nameof(snapshot));
        previewSnapshots[snapshot.Name] = snapshot;
        Notify();
    }

    public void SetPreviewQueue(QuestQueueState state)
    {
        RequirePreview();
        if (string.IsNullOrWhiteSpace(state.Character))
            throw new ArgumentException("Synthetic queue requires a character name.", nameof(state));
        previewQueues[state.Character] = state;
        Notify();
    }

    public void SetPreviewRunning(string name, bool running)
    {
        RequirePreview();
        if (running)
            previewRunning.Add(name);
        else
            previewRunning.Remove(name);
        Notify();
    }

    public void SetPreviewQueueRunning(string name, bool running)
    {
        RequirePreview();
        if (running)
            previewQueueRunning.Add(name);
        else
            previewQueueRunning.Remove(name);
        Notify();
    }

    void RequirePreview()
    {
        if (!Preview)
            throw new InvalidOperationException("Synthetic state is available only in isolated previews.");
    }

    void RequireOpen()
    {
        if (closing || shuttingDown)
            throw new InvalidOperationException("DAQuester is closing. Wait for progress to finish saving.");
    }

    public string MapName(int? map) => map is { } id ? mapNames.GetValueOrDefault(id, "Unknown area") : "Location appears after sign-in";
    public QuestJob Job(string name, string id) => calibrationRuns.TryGetValue(name, out var run) && run.Job.Quest == id ? run.Job : runners.TryGetValue(name, out var runner) && runner.Job.Quest == id ? runner.Job : journal.Load(name, id);
    public CharacterMemory Memory(string name)
    {
        if (name.Length == 0)
            return new();
        try
        {
            return characterMemory.Load(name);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log("Saved character marks could not be read: " + e.Message);
            return new()
            {
                Character = name
            };
        }
    }

    public EvidenceReport Analyze() => recorder.Analyze();
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
                if (PermanentQuestCompletion.Matches(snapshot) && journal.Load(name, "spare-a-stick").PermanentCompletion is null && (!permanentTasks.TryGetValue(name, out var task) || task.IsCompleted))
                    permanentTasks[name] = ObservePermanentAsync(name, session, snapshot);
                else if (!controlActions.ContainsKey(name) && (!calibrationTasks.TryGetValue(name, out var capture) || capture.IsCompleted) && runners.TryGetValue(name, out var runner) && runner.Job.Quest == "spare-a-stick")
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

    async Task ObserveOrdinaryCompletionAsync(string name, GameSession session, QuestRunner runner, CharacterSnapshot snapshot)
    {
        try
        {
            if (sessions.GetValueOrDefault(name) == session && runners.GetValueOrDefault(name) == runner)
                await runner.ReconcileAsync(snapshot);
        }
        catch (Exception e)
        {
            Log(name + ": reward confirmation could not be saved: " + e.Message);
        }
    }

    async Task ObservePermanentAsync(string name, GameSession session, CharacterSnapshot snapshot)
    {
        try
        {
            var completed = new QuestCompletionTracker(journal.Load(name, "spare-a-stick"));
            var job = await PermanentQuestCompletion.ObserveAsync(journal, snapshot, async () =>
            {
                CancelControl(name);
                if (calibrationTasks.TryGetValue(name, out var capture))
                    await capture;
                if (runners.TryGetValue(name, out var runner) && runner.Job.Quest == "spare-a-stick")
                    await runner.PauseAsync("Callough permanent outcome observed");
            });
            if (job is null || sessions.GetValueOrDefault(name) != session)
                return;
            if (runners.TryGetValue(name, out var current) && current.Job.Quest == "spare-a-stick")
                runners.Remove(name);
            calibrationRuns.Remove(name);
            Notify();
            if (completed.Observe(job) is { } outcome)
                PublishCompletion(outcome);
            Log(name + ": permanently finished; repeats disabled. Callough: " + job.PermanentCompletion!.Dialog.Text);
        }
        catch (Exception e)
        {
            Log(name + ": permanent outcome could not be saved: " + e.Message);
        }
    }

    void RememberCharacter(string name, GameSession session, CharacterSnapshot snapshot)
    {
        if (closing)
            return;
        var write = SaveCharacterMemoryAsync(name, session, snapshot);
        characterMemoryWrites.Add(write);
        _ = ForgetWriteAsync(write);
    }

    async Task ForgetWriteAsync(Task write)
    {
        try
        {
            await write;
        }
        finally
        {
            characterMemoryWrites.Remove(write);
        }
    }

    async Task SaveCharacterMemoryAsync(string name, GameSession session, CharacterSnapshot snapshot)
    {
        if (sessions.GetValueOrDefault(name) != session || snapshot.Generation != session.Snapshot.Generation)
            return;
        try
        {
            var before = Memory(name);
            var saved = await characterMemory.ObserveAsync(name, snapshot);
            if (saved != before && sessions.GetValueOrDefault(name) == session && !closing)
                Notify();
        }
        catch (Exception e)
        {
            Log(name + ": character marks could not be saved: " + e.Message);
        }
    }

    async Task ReadOwnLegendAsync(string name, GameSession session)
    {
        if (Preview)
            return;
        try
        {
            var initial = session.Snapshot;
            if (!initial.InventoryKnown && !initial.LegendsKnown)
                await session.WaitAsync(s => s.InventoryKnown || s.LegendsKnown, initial.Revision, TimeSpan.FromSeconds(20), characterMemoryLifetime.Token);
            if (closing || shuttingDown || sessions.GetValueOrDefault(name) != session || !session.Snapshot.Connected || session.Snapshot.LegendsKnown || controlActions.ContainsKey(name) || QueueReserves(name) || RunnerBusy(name) || IsRunning(name) || calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted)
                return;
            await session.ExecuteAsync(new("inspect-self"), characterMemoryLifetime.Token);
        }
        catch (OperationCanceledException) when (characterMemoryLifetime.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Log(name + ": own legend refresh deferred: " + e.Message);
        }
    }

    public void SetFastMode(string name, bool enabled)
    {
        var previous = Settings.FastModeOffCharacters;
        try
        {
            var persisted = AppSettings.Load();
            persisted.FastModeOffCharacters = persisted.FastModeOffCharacters.Where(n => !n.Equals(name, StringComparison.OrdinalIgnoreCase)).Concat(enabled ? [] : new[] { name }).ToArray();
            persisted.Save();
            Settings.FastModeOffCharacters = persisted.FastModeOffCharacters;
            if (sessions.TryGetValue(name, out var session))
                session.FastMode = enabled;
            Notify();
        }
        catch
        {
            Settings.FastModeOffCharacters = previous;
            Notify();
            throw;
        }
    }

    public string FastModeDescription(string name) => sessions.GetValueOrDefault(name)?.FastModeDescription ?? "ready when connected";
    public async Task StopOwnersAsync(string? name = null)
    {
        CancelLaunches(name);
        CancelControl(name);
        bool Match(string character) => name is null || character.Equals(name, StringComparison.OrdinalIgnoreCase);
        foreach (var prepare in runnerPreparations.Where(p => Match(p.Key)).Select(p => p.Value).ToArray())
            prepare.Cancel();
        foreach (var prepare in queuePreparations.Where(p => Match(p.Key)).Select(p => p.Value).ToArray())
            prepare.Cancel();
        // Start all cancellations before awaiting any potentially failing checkpoint.
        var queues = Characters.Concat(queueCache.Keys).Concat(queueCoordinators.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Where(Match).Select(character => PauseQueueAsync(character, true)).ToArray();
        var quests = runners.Where(p => Match(p.Key)).Select(p => p.Value.PauseAsync("Stopped; progress saved")).ToArray();
        await Task.WhenAll(queues.Concat(quests).Append(AwaitLaunchesAsync(name)).Append(WaitForQueueSavesAsync(name)).Append(WaitForRunnerPreparationsAsync(name)).Append(WaitForControlActionsAsync(name)).Concat(calibrationTasks.Where(p => Match(p.Key)).Select(p => p.Value)).Concat(permanentTasks.Where(p => Match(p.Key)).Select(p => p.Value)));
        if (Preview)
            previewRunning.RemoveWhere(Match);
        Notify();
    }

    public void RequestInstall() => Updates.RequestInstall(QuestActionsRunning, ConnectedGame, LaunchedClientRunning);
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
            await StopOwnersAsync();
            bool prepared = Updates.PrepareExit(QuestActionsRunning, ConnectedGame, LaunchedClientRunning);
            if (installRequested && !prepared)
                throw new InvalidOperationException(Updates.Status + " Finish quests and close game clients, then try again.");
            AutoExport("closing");
            closing = true;
            characterMemoryLifetime.Cancel();
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

    public Task<string> ExportAsync()
    {
        var folder = Path.Combine(AppSettings.DataPath, "replays");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".json");
        recorder.Export(path);
        Log("Sanitized replay: " + path);
        Notify();
        return Task.FromResult(path);
    }

    void AutoExport(string why)
    {
        if (Preview || recorder.Count == 0)
            return;
        try
        {
            var folder = Path.Combine(AppSettings.DataPath, "replays");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".json");
            recorder.Export(path);
            Log("Sanitized replay saved (" + why + "): " + path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log("Replay could not be saved (" + why + "): " + e.Message);
        }
    }

    public void SaveConnectionProfile(string json)
    {
        var value = JsonSerializer.Deserialize<AppSettings>(json, AppSettings.Json) ?? throw new InvalidDataException("Connection profile is missing.");
        if (value.LocalPorts.Length != 3 || value.RemotePorts.Length != 3 || value.LocalPorts.Concat(value.RemotePorts).Any(p => p is < 1 or > 65535) || !IPAddress.TryParse(value.RemoteAddress, out _))
            throw new InvalidDataException("Use a valid IP address and exactly three local and remote ports.");
        // Only connection fields change here; accounts and consent keep their own workflows.
        var copy = AppSettings.Load();
        copy.LocalPorts = value.LocalPorts;
        copy.RemotePorts = value.RemotePorts;
        copy.RemoteAddress = value.RemoteAddress;
        copy.MovementMilliseconds = value.MovementMilliseconds;
        copy.Save();
        Settings.LocalPorts = copy.LocalPorts;
        Settings.RemotePorts = copy.RemotePorts;
        Settings.RemoteAddress = copy.RemoteAddress;
        Settings.MovementMilliseconds = copy.MovementMilliseconds;
        Log("Connection profile saved; restart DAQuester to apply it.");
        Notify();
    }

    public void SaveCalibration(string json)
    {
        if (QuestActionsRunning)
            throw new QuestBlockedException("Stop active operations before saving calibration.");
        var value = JsonSerializer.Deserialize<Calibration>(json, AppSettings.Json) ?? throw new InvalidDataException("Calibration is missing.");
        File.WriteAllText(CalibrationPath, JsonSerializer.Serialize(value, AppSettings.Json));
        Calibration = value;
        runners.Clear();
        Log(value.Ready ? "Calibration saved; automatic runs enabled." : "Calibration draft saved.");
        Notify();
    }

    public static string PlayerReason(string value) => value.Replace("observed progress", "progress", StringComparison.OrdinalIgnoreCase).Replace("server-observed", "confirmed", StringComparison.OrdinalIgnoreCase).Replace("server identity", "character details", StringComparison.OrdinalIgnoreCase).Replace("server level", "level", StringComparison.OrdinalIgnoreCase).Replace("server inventory", "inventory", StringComparison.OrdinalIgnoreCase).Replace("Local control", "Control bridge", StringComparison.OrdinalIgnoreCase).Replace("local control", "control bridge", StringComparison.OrdinalIgnoreCase).Replace("Connection replaced", "Your character reconnected", StringComparison.OrdinalIgnoreCase).Replace("preflight", "quest requirements", StringComparison.OrdinalIgnoreCase);
}
