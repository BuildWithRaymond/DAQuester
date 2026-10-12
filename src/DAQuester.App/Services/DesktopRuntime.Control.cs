using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Control;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Diagnostics;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    ControlServer? controlServer;
    readonly Dictionary<string, CancellationTokenSource> controlActions = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, AutomaticCalibration> calibrationRuns = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Task> calibrationTasks = new(StringComparer.OrdinalIgnoreCase);
    Task<object?> OnUiAsync(Func<Task<object?>> action)
    {
        var done = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (closing || shuttingDown)
        {
            done.SetException(new IOException("DAQuester is closing."));
            return done.Task;
        }

        Dispatch(async () =>
        {
            try
            {
                RequireOpen();
                done.SetResult(await action());
            }
            catch (Exception e)
            {
                done.SetException(e);
            }
        });
        return done.Task;
    }

    void CancelControl(string? name = null)
    {
        foreach (var pair in controlActions.ToArray())
            if (name is null || name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase))
                pair.Value.Cancel();
    }

    async Task WaitForControlActionsAsync(string? character)
    {
        while (controlActions.Keys.Any(name => character is null || name.Equals(character, StringComparison.OrdinalIgnoreCase)))
            await Task.Delay(20);
    }

    async Task<object?> ControlAsync(ControlRequest r)
    {
        bool Flag(string key) => r.Arguments is { } a && a.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
        int Number(string key, int fallback) => r.Arguments is { } a && a.TryGetProperty(key, out var v) ? v.GetInt32() : fallback;
        string? TextArgument(string key) => r.Arguments is { } a && a.TryGetProperty(key, out var v) ? v.GetString() : null;
        string name = r.Character ?? "";
        if (r.Method == "state")
            return new
            {
                characters = sessions.Where(p => name.Length == 0 || p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(p => new { character = p.Key, snapshot = p.Value.Snapshot, job = Job(p.Key, RunningQuestId(p.Key) ?? SelectedQuestId), visibleSteps = p.Value.VisibleMoves }).ToArray(),
                queues = Characters.Where(n => name.Length == 0 || n.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(Queue).ToArray(),
                calibration = Calibration,
                quests = recipes.Values.Select(q => new { q.Id, q.Name, q.Repeatable }).ToArray(),
                selectedQuest = SelectedQuestId,
                recordedEvents = recorder.Count,
                controlActions = controlActions.Keys.ToArray()
            };
        if (r.Method == "stop")
        {
            await StopOwnersAsync(r.Character);
            return new
            {
                stopped = r.Character ?? "all"
            };
        }

        if (r.Method == "export")
        {
            var folder = Path.Combine(AppSettings.DataPath, "replays");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".json");
            recorder.Export(path);
            return new
            {
                path,
                events = recorder.Count
            };
        }

        if (r.Method == "analyze")
            return new
            {
                evidence = recorder.Analyze(),
                candidateRegions = CandidateRegions
            };
        if (r.Method == "log")
        {
            var lines = Logs;
            int count = Number("count", lines.Length);
            return new
            {
                lines = lines.TakeLast(Math.Max(0, count)).ToArray(),
                logFile = Path.Combine(AppSettings.DataPath, "diagnostics.log"),
                logFileFailed
            };
        }

        if (r.Method == "calibration")
        {
            if (TextArgument("json") is { } json)
                SaveCalibration(json);
            return new
            {
                calibration = Calibration,
                path = CalibrationPath
            };
        }

        if (r.Method == "settings")
        {
            if (TextArgument("json") is { } json)
                SaveConnectionProfile(json);
            return new
            {
                settings = Settings,
                path = Path.Combine(AppSettings.DataPath, "profiles.json")
            };
        }

        if (r.Method == "launch")
        {
            int pid = await LaunchAsync(name, manualOnly: name.Length == 0);
            return new
            {
                pid
            };
        }

        if (r.Method == "map")
        {
            int map = Number("map", 500);
            var repository = new MapRepository(Path.GetDirectoryName(Settings.ClientPath)!, Path.Combine(AppSettings.Content, "routes.json"), Settings.ServerProfile);
            var record = repository.Record(map);
            var g = repository.Load(map);
            var rows = Enumerable.Range(0, record.Height).Select(y => new string(Enumerable.Range(0, record.Width).Select(x => g.Blocked(new(x, y)) ? '#' : '.').ToArray())).ToArray();
            return new
            {
                record,
                collisionRows = rows,
                candidateRegions = map == 500 ? CandidateRegions : [],
                candidatePatrol = map == 500 ? TreePatrol.Build(g, new(13, 69), CandidateRegions).Where(t => !record.Portals.Any(p => p.From == t)).ToArray() : []
            };
        }

        if (!sessions.TryGetValue(name, out var session) || !session.Snapshot.Connected)
            throw new QuestBlockedException("Connected character required: " + name);
        if (r.Method == "calibrate")
            return await StartAutomaticCalibration(name);
        if (r.Method == "start")
        {
            if (QueueReserves(name))
                throw new QuestBlockedException("A queue owns this character. Stop it before starting a separate quest.");
            string id = TextArgument("quest") ?? "spare-a-stick";
            if (!recipes.TryGetValue(id, out var selected))
                throw new QuestBlockedException("Unsupported quest: " + id);
            MundaneBirthOptions? options = null;
            if (id == "mundane-birth" && (TextArgument("season") is not null || TextArgument("town") is not null || r.Arguments is { } args && args.TryGetProperty("age", out _)))
                options = new(TextArgument("season") ?? "", Number("age", 0), TextArgument("town") ?? "");
            EmeraldDawnOptions? emeraldOptions = null;
            if (id == "emerald-dawn" && TextArgument("outcome") is { } outcome)
                emeraldOptions = new(outcome);
            var runner = await PrepareRunnerAsync(name, selected, options, emeraldOptions, Flag("allowUnsafeWalking"));
            _ = runner.RunAsync(selected.Repeatable && Flag("repeat"), startNewAttempt: true);
            return new
            {
                started = name,
                quest = id,
                birthChoices = runner.Job.BirthOptions,
                emeraldChoices = runner.Job.EmeraldOptions
            };
        }

        if (r.Method == "fight")
        {
            ushort[] sprites = r.Arguments is { } fa && fa.TryGetProperty("sprites", out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Select(v => checked((ushort)v.GetInt32())).ToArray() : [];
            bool roam = r.Arguments is not { } ra || !ra.TryGetProperty("roam", out var rv) || rv.ValueKind != JsonValueKind.False;
            StartFight(name, new BashOptions { PrioritySprites = sprites, PriorityOnly = Flag("priorityOnly") }, TimeSpan.FromSeconds(Number("idleSeconds", roam ? 120 : 30)), roam);
            return new
            {
                fighting = name,
                sprites,
                priorityOnly = Flag("priorityOnly"),
                roam
            };
        }

        if (r.Method != "command")
            throw new InvalidOperationException("Unsupported control method: " + r.Method);
        if (RunnerBusy(name))
            throw new QuestBlockedException("A quest is already preparing for this character.");
        if (QueueReserves(name))
            throw new QuestBlockedException("A queue owns this character. Stop it before a control action.");
        var command = JsonSerializer.Deserialize<GameCommand>(r.Arguments?.GetRawText() ?? "{}", AppSettings.Json) ?? throw new InvalidDataException("Game command missing.");
        if (command.Kind is not ("navigate" or "visit" or "step" or "open" or "choose" or "next" or "close" or "input" or "say" or "collect-ground" or "pickup" or "inspect-self"))
            throw new QuestBlockedException("Command is unavailable through control bridge.");
        if (controlActions.ContainsKey(name))
            throw new QuestBlockedException("A control action already owns this character. Stop it first.");
        long generation = session.Snapshot.Generation;
        if (runners.TryGetValue(name, out var owner))
            await owner.PauseAsync("Local control owns character");
        RequireOpen();
        if (RunnerBusy(name) || QueueReserves(name) || controlActions.ContainsKey(name) || runners.GetValueOrDefault(name) != owner)
            throw new QuestBlockedException("Another action owns this character.");
        if (sessions.GetValueOrDefault(name) != session || !session.Snapshot.Connected || session.Snapshot.Generation != generation)
            throw new QuestBlockedException("Character connection changed. Resume after fresh state.");
        using var cancellation = new CancellationTokenSource();
        controlActions[name] = cancellation;
        void Interrupt(string reason)
        {
            cancellation.Cancel();
            Log(name + ": control paused: " + reason);
        }

        session.Interrupted += Interrupt;
        try
        {
            long before = session.Snapshot.Revision;
            await session.ExecuteAsync(command with
            {
                Dialog = command.Kind is "choose" or "next" or "close" or "input" ? session.Snapshot.Dialog : null
            }, cancellation.Token);
            if (command.Kind == "choose")
                await session.WaitAsync(s => s.Dialog is { } d && d.Revision > before, before, TimeSpan.FromSeconds(5), cancellation.Token);
            return new
            {
                snapshot = session.Snapshot
            };
        }
        finally
        {
            session.Interrupted -= Interrupt;
            controlActions.Remove(name);
            Notify();
        }
    }

    // A melee fight owned by the control bridge; stop, the app's stop button or manual input ends it.
    void StartFight(string name, BashOptions options, TimeSpan idle, bool roam)
    {
        RequireOpen();
        if (!sessions.TryGetValue(name, out var session) || !session.Snapshot.Connected)
            throw new QuestBlockedException("Connect " + name + " first.");
        if (RunnerBusy(name) || QueueReserves(name) || IsRunning(name) || controlActions.ContainsKey(name))
            throw new QuestBlockedException("Another action owns this character. Stop it first.");
        var cancellation = new CancellationTokenSource();
        controlActions[name] = cancellation;
        void Interrupt(string reason)
        {
            cancellation.Cancel();
            Log(name + ": fight paused: " + reason);
        }

        session.Interrupted += Interrupt;
        Log(name + ": fight started");
        Notify();
        _ = Run();
        async Task Run()
        {
            try
            {
                var fight = options with
                {
                    IsFriend = session.IsOwnCharacter,
                    Note = line => Log(name + ": " + line)
                };
                var end = await Task.Run(() => MeleeFight.RunAsync(session, session, fight, null, idle, cancellation.Token, roam));
                Log(name + ": fight ended · " + (end == FightEnd.NoTargets ? (roam ? "no monster found for " : "nothing in reach for ") + idle.TotalSeconds + " s" : "goal reached"));
            }
            catch (OperationCanceledException)
            {
                Log(name + ": fight stopped");
            }
            catch (Exception e)
            {
                Log(name + ": fight stopped · " + e.Message);
            }
            finally
            {
                session.Interrupted -= Interrupt;
                controlActions.Remove(name);
                cancellation.Dispose();
                Notify();
            }
        }
    }

    public async Task<object?> StartAutomaticCalibration(string name)
    {
        RequireOpen();
        if (Preview)
            throw new InvalidOperationException("Preview does not calibrate game characters.");
        if (RunnerBusy(name))
            throw new QuestBlockedException("A quest is already preparing for this character.");
        if (QueueReserves(name))
            throw new QuestBlockedException("A queue owns this character. Stop it before calibration.");
        if (!sessions.TryGetValue(name, out var session) || !session.Snapshot.Connected)
            throw new QuestBlockedException("Connect " + name + " first.");
        if (controlActions.ContainsKey(name))
            throw new QuestBlockedException("An action already owns this character. Stop it first.");
        if (!recorder.Enabled)
            throw new QuestBlockedException("Enable Record evidence before automatic calibration.");
        long generation = session.Snapshot.Generation;
        if (runners.TryGetValue(name, out var owner))
            await owner.PauseAsync("Automatic calibration owns character");
        RequireOpen();
        if (RunnerBusy(name) || QueueReserves(name) || controlActions.ContainsKey(name) || runners.GetValueOrDefault(name) != owner)
            throw new QuestBlockedException("Another action owns this character.");
        if (sessions.GetValueOrDefault(name) != session || !session.Snapshot.Connected || session.Snapshot.Generation != generation)
            throw new QuestBlockedException("Character connection changed. Resume after fresh state.");
        var repository = new MapRepository(Path.GetDirectoryName(Settings.ClientPath)!, Path.Combine(AppSettings.Content, "routes.json"), Settings.ServerProfile);
        var map = repository.Record(500);
        // Use the weapon-shop return landing, not the wall-flagged entrance door.
        var patrol = TreePatrol.Build(repository.Load(500), new(13, 69), CandidateRegions).Where(t => !map.Portals.Any(p => p.From == t)).ToArray();
        var cancellation = new CancellationTokenSource();
        controlActions[name] = cancellation;
        var run = new AutomaticCalibration(session, journal, recorder, Calibration, patrol);
        calibrationRuns[name] = run;
        run.Changed += _ => Ui(() => Notify());
        void Interrupt(string reason)
        {
            cancellation.Cancel();
            Log(name + ": calibration paused: " + reason);
        }

        session.Interrupted += Interrupt;
        string folder = Path.Combine(AppSettings.DataPath, "replays");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "calibration-" + name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".json");
        async Task Run()
        {
            try
            {
                var result = await run.RunAsync(path, cancellation.Token);
                if (run.Job.PermanentCompletion is not null)
                {
                    Log(name + ": permanently finished; repeats disabled. Callough: " + run.Job.PermanentCompletion.Dialog.Text);
                    return;
                }

                Calibration = Calibration with
                {
                    Validated = Calibration.Validated || result.Validated,
                    Patrol = result.Patrol,
                    PatrolBasis = result.PatrolBasis,
                    ObservedDropPositions = result.ObservedDropPositions,
                    Accepted = Calibration.Accepted.Concat(result.Accepted).Distinct().ToArray(),
                    Completed = Calibration.Completed.Concat(result.Completed).Distinct().ToArray(),
                    Refused = Calibration.Refused.Concat(result.Refused).Distinct().ToArray(),
                    Cooldown = Calibration.Cooldown.Concat(result.Cooldown).Distinct().ToArray(),
                    EvidenceFiles = Calibration.EvidenceFiles.Concat(result.EvidenceFiles).Distinct().ToArray(),
                    ValidatedCharacters = Calibration.ValidatedCharacters.Concat(result.ValidatedCharacters).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                };
                File.WriteAllText(CalibrationPath, JsonSerializer.Serialize(Calibration, AppSettings.Json));
                runners.Remove(name);
                Log(name + ": " + (Calibration.Ready ? "Server evidence validated patrol and replies; automatic recipe enabled." : "Completion proven; another collection capture is needed to validate acceptance/patrol."));
            }
            catch (Exception e)
            {
                recorder.Export(path);
                Log(name + ": calibration paused: " + e.Message + ". Replay: " + path);
            }
            finally
            {
                session.Interrupted -= Interrupt;
                controlActions.Remove(name);
                cancellation.Dispose();
                Notify();
            }
        }

        calibrationTasks[name] = Run();
        Log(name + ": automatic calibration started; manual input or Stop pauses it.");
        return new
        {
            started = name,
            candidateTiles = patrol.Length,
            evidencePath = path
        };
    }
}
