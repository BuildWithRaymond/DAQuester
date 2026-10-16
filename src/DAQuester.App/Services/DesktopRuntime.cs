using System.Net;
using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.App.Services;

/// <summary>Owns game connections and durable operations independently of the desktop presentation.</summary>
public sealed partial class DesktopRuntime
{
    readonly ProxyHost host = new();
    readonly ReplayRecorder recorder = new();
    readonly Dictionary<string, GameSession> sessions = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, QuestRunner> runners = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, CancellationTokenSource> runnerPreparations = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Task> permanentTasks = new(StringComparer.OrdinalIgnoreCase);
    readonly PresentationJournal journal;
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
    readonly Dictionary<string, string> previewRunningQuests = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> previewQueueRunning = new(StringComparer.OrdinalIgnoreCase);
    bool closing;
    bool started;
    bool shuttingDown;
    bool logFileFailed;
    Task? shutdownTask;
    Task? presentationRefresh;
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
    public string[] Characters => Settings.Characters
        .Concat(sessions.Keys)
        .Concat(previewSnapshots.Keys)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    public Action<Action> Dispatch
    {
        get;
        set;
    } = action => action();
    // Display invalidations may wait for a render slot. Game receipts, logs and
    // ownership work continue through Dispatch and are never batched here.
    public Action<Action>? DispatchPresentation
    {
        get; set;
    }
    int presentationPending;

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
    public bool QuestActionsRunning => scheduler.IsRunning
        || pendingLaunches.Count > 0
        || runnerPreparations.Count > 0
        || queuePreparations.Count > 0
        || queueSaving.Count > 0
        || queueCoordinators.Values.Any(q => q.IsRunning)
        || runners.Values.Any(r => r.IsRunning)
        || controlActions.Count > 0
        || calibrationTasks.Values.Any(t => !t.IsCompleted)
        || permanentTasks.Values.Any(t => !t.IsCompleted);
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

    public DesktopRuntime(bool preview = false, IUpdateBackend? updateBackend = null, IQuestJournal? journal = null)
    {
        Preview = preview;
        Settings = AppSettings.Load();
        this.journal = new(journal ?? new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal")));
        var schedulerStore = new FileQuestSchedulerStore(Path.Combine(AppSettings.DataPath, "scheduler.json"));
        try
        {
            scheduler = new(schedulerStore, ExecuteScheduledQuestAsync);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            scheduler = new(schedulerStore, ExecuteScheduledQuestAsync, new());
            Log("Saved scheduler could not be read; scheduling is disabled: " + error.Message);
        }
        scheduler.Changed += Notify;
        characterMemory = new(Path.Combine(AppSettings.DataPath, "characters"), Settings.ServerProfile);
        recipes = Directory.GetFiles(Path.Combine(AppSettings.Content, "recipes"), "*.json")
            .Select(path => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(path), AppSettings.Json)!)
            .ToDictionary(q => q.Id, StringComparer.OrdinalIgnoreCase);
        using (var routes = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.Content, "routes.json"))))
            mapNames = routes.RootElement.EnumerateArray().ToDictionary(
                m => m.GetProperty("id").GetInt32(),
                m => m.GetProperty("name").GetString() ?? "Unknown area");
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
            connection.Tracker.ServerMessage += (type, text) =>
            {
                if (DAQuester.DarkAges.Protocol.StateTracker.IsActiveMessage(type))
                    Log((connection.Tracker.Snapshot.Name ?? "Connecting") + ": server: " + text);
            };
            connection.DialogAction += packet => recorder.RecordDialogPacket(packet, connection.Tracker.Snapshot);
            connection.ExchangeAction += packet => recorder.RecordExchangePacket(packet, connection.Tracker.Snapshot);
            connection.DropAction += packet => recorder.RecordDropPacket(packet, connection.Tracker.Snapshot);
            connection.Authenticated += authenticated => Ui(() => _ = BindAsync(authenticated));
        };
    }

    // Read from the proxy threads while walking, so it is swapped whole rather than edited.
    volatile System.Collections.Immutable.ImmutableHashSet<string> ownCharacters =
        System.Collections.Immutable.ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);
    void RefreshOwnCharacters() => ownCharacters = System.Collections.Immutable.ImmutableHashSet.CreateRange(
        StringComparer.OrdinalIgnoreCase,
        sessions.Where(p => p.Value.Snapshot.Connected).Select(p => p.Key));

    void Ui(Action action)
    {
        if (!closing)
            Dispatch(action);
    }

    void Notify()
    {
        if (closing || Interlocked.CompareExchange(ref presentationPending, 1, 0) != 0)
            return;
        try
        {
            (DispatchPresentation ?? Dispatch)(() =>
            {
                // Clear before notifying: a change during rendering needs a later
                // pass, while the queued callback always reads the latest state.
                Interlocked.Exchange(ref presentationPending, 0);
                if (!closing)
                    Changed?.Invoke();
            });
        }
        catch
        {
            Interlocked.Exchange(ref presentationPending, 0);
            throw;
        }
    }
    public void Refresh()
    {
        journal.InvalidateDisplay();
        Notify();
    }

    internal void InvalidatePresentation() => journal.InvalidateDisplay();

    internal Task RefreshPresentationAsync()
    {
        if (closing || shuttingDown)
            return Task.CompletedTask;
        return presentationRefresh is { IsCompleted: false }
            ? presentationRefresh
            : presentationRefresh = RefreshPresentationCoreAsync();
    }

    async Task RefreshPresentationCoreAsync()
    {
        try
        {
            await journal.RefreshDisplayAsync();
            Notify();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Log("Saved display progress could not be refreshed: " + error.Message);
        }
    }
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
    public CharacterSnapshot Snapshot(string name) => Preview && previewSnapshots.TryGetValue(name, out var snapshot)
        ? snapshot
        : sessions.GetValueOrDefault(name)?.Snapshot ?? new()
        {
            Name = name
        };
    public Dialog? CurrentDialog(string name) => Snapshot(name).Dialog;
    public long VisibleMoves(string name) => sessions.GetValueOrDefault(name)?.VisibleMoves ?? 0;
    public bool IsRunning(string name) => Preview && previewRunning.Contains(name) || runners.TryGetValue(name, out var owner) && owner.IsRunning;
    public string? RunningQuestId(string name) => Preview && previewRunning.Contains(name)
        ? previewRunningQuests.GetValueOrDefault(name)
        : calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted && calibrationRuns.TryGetValue(name, out var calibration)
            ? calibration.Job.Quest
            : runners.TryGetValue(name, out var runner) && runner.IsRunning
                ? runner.Job.Quest
                : null;
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

    public void SetPreviewRunning(string name, bool running, string? questId = null)
    {
        RequirePreview();
        if (running)
            previewRunning.Add(name);
        else
            previewRunning.Remove(name);
        if (running && questId is not null)
            previewRunningQuests[name] = questId;
        else
            previewRunningQuests.Remove(name);
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

    public string MapName(int? map) => map is { } id ? mapNames.GetValueOrDefault(id, "Unknown area") : "Location appears after sign-in";
    public QuestJob Job(string name, string id) => Job(name, id, journal.Load);
    internal QuestJob Job(string name, string id, Func<string, string, QuestJob> readSaved) =>
        calibrationRuns.TryGetValue(name, out var run) && run.Job.Quest == id
            ? run.Job
            : runners.TryGetValue(name, out var runner) && runner.Job.Quest == id
                ? runner.Job
                : readSaved(name, id);
    internal DesktopPresentation CapturePresentation() => new(this, journal.ReadForDisplay);
    internal DesktopPresentation CaptureFreshPresentation() => new(this, journal.Load);
    public EvidenceReport Analyze() => recorder.Analyze();
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

    public void SetFastMode(string name, bool enabled)
    {
        var previous = Settings.FastModeOffCharacters;
        try
        {
            var persisted = AppSettings.Load();
            persisted.FastModeOffCharacters = persisted.FastModeOffCharacters
                .Where(n => !n.Equals(name, StringComparison.OrdinalIgnoreCase))
                .Concat(enabled ? [] : new[] { name })
                .ToArray();
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
    public void RequestInstall() => Updates.RequestInstall(QuestActionsRunning, ConnectedGame, LaunchedClientRunning);

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
        if (value.LocalPorts.Length != 3 || value.RemotePorts.Length != 3
            || value.LocalPorts.Concat(value.RemotePorts).Any(p => p is < 1 or > 65535)
            || !IPAddress.TryParse(value.RemoteAddress, out _))
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

    public static string PlayerReason(string value) => value
        .Replace("observed progress", "progress", StringComparison.OrdinalIgnoreCase)
        .Replace("server-observed", "confirmed", StringComparison.OrdinalIgnoreCase)
        .Replace("server identity", "character details", StringComparison.OrdinalIgnoreCase)
        .Replace("server level", "level", StringComparison.OrdinalIgnoreCase)
        .Replace("server inventory", "inventory", StringComparison.OrdinalIgnoreCase)
        .Replace("Local control", "Control bridge", StringComparison.OrdinalIgnoreCase)
        .Replace("local control", "control bridge", StringComparison.OrdinalIgnoreCase)
        .Replace("Connection replaced", "Your character reconnected", StringComparison.OrdinalIgnoreCase)
        .Replace("preflight", "quest requirements", StringComparison.OrdinalIgnoreCase);
}
