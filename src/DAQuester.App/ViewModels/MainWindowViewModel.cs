using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Arbiter.Net.Types;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public DesktopRuntime Runtime
    {
        get;
    }
    internal DesktopPresentation Presentation
    {
        get; private set;
    }

    readonly List<QuestItemVM> library = [];
    readonly List<AsyncCommand> commands = [];
    readonly HashSet<string> removedAccounts = new(StringComparer.OrdinalIgnoreCase);
    bool refreshing;
    bool questConfirmationPending;
    public Func<string, string, string?, Task<bool>>? ConfirmPaulinQuestAsync
    {
        get; set;
    }
    string page = "Dashboard", search = "", filter = "Available", errorMessage = "", noticeMessage = "", clientPath, accountStatus = "";
    bool legendMarksOnly;
    bool automaticUpdateChecks;
    bool keepRunningInTray, startWithWindows;
    CharacterItemVM? selectedCharacter;
    QuestItemVM? selectedQuest;
    public ObservableCollection<CharacterItemVM> Characters { get; } = [];
    // The header switcher lists characters by name; the Characters page keeps their order, so a new card stays last.
    public ObservableCollection<CharacterItemVM> SwitcherCharacters { get; } = [];
    bool sortingSwitcher;
    public ObservableCollection<QuestItemVM> Quests { get; } = [];
    public ObservableCollection<QueueItemVM> QueueItems { get; } = [];
    public ObservableCollection<HistoryItemVM> History { get; } = [];

    public CharacterItemVM? SelectedCharacter
    {
        get => selectedCharacter;
        set
        {
            // Re-sorting moves the selected row, and the switcher briefly reports no selection.
            if (sortingSwitcher && value is null)
                return;
            if (Set(ref selectedCharacter, value))
            {
                InvalidateBagSortConfirmation();
                // A card not yet saved has no name to reopen on.
                if (value is { OriginalName.Length: > 0 })
                    Runtime.RememberCharacter(value.OriginalName);
                Refresh();
            }
        }
    }

    void SortSwitcher()
    {
        sortingSwitcher = true;
        try
        {
            Sync(SwitcherCharacters, Characters.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        finally
        {
            sortingSwitcher = false;
        }

        Raise(nameof(SelectedCharacter));
    }

    public QuestItemVM? SelectedQuest
    {
        get => selectedQuest;
        set
        {
            if (Set(ref selectedQuest, value))
            {
                if (value?.RecipeId is { Length: > 0 } id)
                    Runtime.SelectedQuestId = id;
                Refresh();
            }
        }
    }

    public QueueItemVM? SelectedQueueItem
    {
        get;
        set;
    }
    public string CharacterName => SelectedCharacter?.OriginalName ?? "";

    public string Page
    {
        get => page;
        set
        {
            if (Set(ref page, value))
            {
                InvalidateBagSortConfirmation();
                RaiseAll();
                Refresh();
            }
        }
    }

    public bool IsPlanner => Page == "Planner";
    public bool IsCharacters => Page == "Characters";
    public bool IsHistory => Page == "History";
    public bool IsScheduler => Page == "Scheduler";
    public bool IsTasks => Page == "Tasks";
    public bool IsSettings => Page == "Settings";
    public string PageTitle => Page switch
    {
        "History" => "Run history",
        "Tasks" => "Auto Tasks",
        _ => Page == "Planner" ? "Quest planner" : Page
    };
    public string PageSubtitle => Page switch
    {
        "Dashboard" => "Every character. One clear view.",
        "Characters" => "Every character. Their own journey.",
        "History" => "Verified outcomes and saved attempts.",
        "Scheduler" => "Log in, run a quest, log out. Completely optional.",
        "Tasks" => "Useful work for your characters. Repeat on your terms.",
        "Settings" => "A companion that works your way.",
        _ => "Your quests. Your order. Your next adventure."
    };

    public string Search
    {
        get => search;
        set
        {
            if (Set(ref search, value))
                Refresh();
        }
    }

    public string Filter
    {
        get => filter;
        set
        {
            if (Set(ref filter, value))
                Refresh();
        }
    }

    public bool IsAllFilter => Filter == "All";
    public bool IsAvailableFilter => Filter == "Available";
    public bool IsCompletedFilter => Filter == "Completed";
    public bool IsGuidesFilter => Filter == "Guides";
    public bool LegendMarksOnly
    {
        get => legendMarksOnly;
        set
        {
            if (Set(ref legendMarksOnly, value))
                Refresh();
        }
    }
    IEnumerable<QuestItemVM> CharacterFilteredLibrary
    {
        get
        {
            var snapshot = Runtime.Snapshot(CharacterName);
            var appearance = snapshot is { Connected: true, Id: not null, Appearance: not null }
                && string.Equals(snapshot.Name, CharacterName, StringComparison.OrdinalIgnoreCase)
                ? snapshot.Appearance : Runtime.Memory(CharacterName).Appearance;
            BodySprite? body = appearance is null ? null : (BodySprite)appearance.Body;
            return library.Where(q => (!Runtime.Recipes.TryGetValue(q.RecipeId, out var recipe) || QuestVisibleForCharacter(recipe)) && (q.Id switch
            {
                "capofdanaan" => body is BodySprite.Male or BodySprite.MaleGhost or BodySprite.MaleInvisible
                    or BodySprite.MaleJester or BodySprite.MaleHead or BodySprite.MaleBlank,
                "bandofdanaan" => body is BodySprite.Female or BodySprite.FemaleGhost or BodySprite.FemaleInvisible
                    or BodySprite.FemaleHead or BodySprite.FemaleBlank,
                _ => true
            }));
        }
    }
    IEnumerable<QuestItemVM> LegendFilteredLibrary => CharacterFilteredLibrary.Where(q => !LegendMarksOnly || q.HasLegendReward);
    public int AllCount => LegendFilteredLibrary.Count();
    public int AvailableCount => LegendFilteredLibrary.Count(q => q.QueueEligible);
    public int CompletedCount => LegendFilteredLibrary.Count(q => q.IsCompleted);
    public int GuidesCount => LegendFilteredLibrary.Count(q => q.IsGuideOnly);
    bool QuestVisibleForCharacter(QuestDefinition recipe) => !recipe.IsTask || recipe.QuestClasses.Any(c =>
        string.Equals(c, Runtime.CharacterClass(CharacterName)?.Trim(), StringComparison.OrdinalIgnoreCase));
    public string LibrarySummary => $"{Quests.Count} of {AllCount} quests{(LegendMarksOnly ? " with legend marks" : "")} · {Runtime.Recipes.Values.Count(QuestVisibleForCharacter)} automatic journeys";
    public string LibrarySelectionHint => QueueLocked ? "Stop this run before editing the queue. Select any row to read its details."
        : CharacterName.Length == 0 ? "Add a character to plan a queue. Select any row to read its details."
        : "Check a box to add or remove a quest. Select a row to read its details.";
    public bool EmptyLibrary => Quests.Count == 0;
    public bool CharactersEmpty => Characters.Count == 0;
    public bool HistoryEmpty => History.Count == 0;
    public string SelectedCharacterInfo => SelectedCharacter?.LevelText ?? "Choose your character";
    public string? LaunchBlocker => Runtime.Settings.Characters.Length == 0 ? Runtime.LaunchBlocker("") : SelectedCharacter?.Saved == true ? Runtime.LaunchBlocker(CharacterName) : "Save this character’s edits before launching.";
    public string LaunchTooltip => LaunchBlocker ?? Runtime.LaunchHint(Runtime.Settings.Characters.Length == 0 ? "" : CharacterName) ?? (Runtime.Settings.Characters.Length == 0 ? "Open Dark Ages without entering a login." : "Launch Dark Ages");
    public string ConnectionStatus => CharacterName.Length == 0 ? "No character selected" : Runtime.Snapshot(CharacterName).Connected ? "Connected · fresh state" : Runtime.Preview ? "Offline · sample workspace" : Runtime.ConnectionError is not null ? "Connection unavailable · check Settings" : "Offline · connect to begin";
    public string AccountStatus
    {
        get => accountStatus;
        set => Set(ref accountStatus, value);
    }

    public string ErrorMessage
    {
        get => errorMessage;
        set
        {
            if (Set(ref errorMessage, value))
                Raise(nameof(HasError));
        }
    }

    public bool HasError => ErrorMessage.Length > 0;

    public string NoticeMessage
    {
        get => noticeMessage;
        set
        {
            if (Set(ref noticeMessage, value))
                Raise(nameof(HasNotice));
        }
    }

    public bool HasNotice => NoticeMessage.Length > 0;
    public bool InstallRequested
    {
        get; private set;
    }

    public event Action? RequestClose;
    // Raised after Add character so the view can bring the new card into view.
    public event Action<CharacterItemVM>? CharacterAdded;
    public Func<Task<string?>>? PickClientAsync
    {
        get;
        set;
    }
    public RelayCommand NavigateCommand
    {
        get;
    }
    public RelayCommand FilterCommand
    {
        get;
    }
    public RelayCommand DismissErrorCommand
    {
        get;
    }
    public RelayCommand DismissNoticeCommand
    {
        get;
    }
    public RelayCommand AddCharacterCommand
    {
        get;
    }
    public RelayCommand DiscardAccountsCommand
    {
        get;
    }
    public AsyncCommand LaunchSelectedCommand
    {
        get;
    }
    public AsyncCommand StartQueueCommand
    {
        get;
    }
    public AsyncCommand StopQueueCommand
    {
        get;
    }
    public AsyncCommand ClearQueueCommand
    {
        get;
    }
    public AsyncCommand StopAllCommand
    {
        get;
    }
    public AsyncCommand SaveAccountsCommand
    {
        get;
    }
    public AsyncCommand SaveSettingsCommand
    {
        get;
    }
    public AsyncCommand CheckUpdatesCommand
    {
        get;
    }
    public AsyncCommand DownloadUpdateCommand
    {
        get;
    }
    public AsyncCommand InstallUpdateCommand
    {
        get;
    }
    public AsyncCommand BrowseClientCommand
    {
        get;
    }

    public MainWindowViewModel(DesktopRuntime runtime, Func<DateTimeOffset>? forecastClock = null)
    {
        Runtime = runtime;
        Presentation = runtime.CapturePresentation();
        this.forecastClock = forecastClock ?? (() => DateTimeOffset.UtcNow);
        InitializeTasks();
        InitializeDashboard();
        InitializeBagSorting();
        clientPath = runtime.Settings.ClientPath;
        automaticUpdateChecks = runtime.Settings.AutomaticUpdateChecks;
        keepRunningInTray = runtime.Settings.KeepRunningInTray;
        try { startWithWindows = runtime.LoginStartup.ReadCommand() is not null; }
        catch (Exception error) { ReportError(error); }
        journeyCollapsed = runtime.Settings.JourneyCollapsed;
        NavigateCommand = new(p => Page = p as string ?? "Planner");
        FilterCommand = new(p => Filter = p as string ?? "Available");
        ToggleJourneyCommand = new(_ => JourneyCollapsed = !JourneyCollapsed);
        DismissErrorCommand = new(_ => ErrorMessage = "");
        DismissNoticeCommand = new(_ => NoticeMessage = "");
        AddCharacterCommand = new(_ =>
        {
            var item = new CharacterItemVM(this, "");
            Characters.Add(item);
            SortSwitcher();
            SelectedCharacter = item;
            AccountEdited();
            Raise(nameof(CharactersEmpty));
            CharacterAdded?.Invoke(item);
        });
        DiscardAccountsCommand = new(_ =>
        {
            removedAccounts.Clear();
            Characters.Clear();
            selectedCharacter = null;
            LoadCharacters();
            Refresh();
            AccountStatus = "Edits discarded.";
        });
        LaunchSelectedCommand = Command(_ => runtime.Settings.Characters.Length == 0 ? runtime.LaunchAsync("", manualOnly: true) : runtime.LaunchAsync(CharacterName), _ => LaunchBlocker is null, allowConcurrent: true);
        StartQueueCommand = Command(_ => runtime.StartQueueAsync(CharacterName), _ => runtime.QueueRunning(CharacterName) || !runtime.QueueBusy(CharacterName) && runtime.QueueStartReason(CharacterName, QueueState).Length == 0, allowConcurrent: true);
        StopQueueCommand = Command(_ => runtime.PauseQueueAsync(CharacterName, true), _ => QueueLocked);
        ClearQueueCommand = Command(_ => runtime.EditQueueAsync(CharacterName, s => s with { Quests = [], ActiveQuest = null }), _ => !QueueLocked && !QueueEmpty);
        StopAllCommand = Command(_ => runtime.StopOwnersAsync());
        AddSelectedQuestToQueueCommand = Command(_ => AddSelectedQuestToQueueAsync(), _ => CanAddSelectedQuestToQueue);
        ChangeChoicesCommand = Command(_ => runtime.ChangeQuestChoicesAsync(CharacterName, SelectedQuestId), _ => CanChangeChoices);
        SaveAccountsCommand = Command(_ =>
        {
            SaveAccounts();
            return Task.CompletedTask;
        });
        SaveSettingsCommand = Command(_ =>
        {
            SaveSettings();
            return Task.CompletedTask;
        });
        CheckUpdatesCommand = Command(_ => runtime.Updates.CheckAsync(), _ => CanCheckUpdates);
        DownloadUpdateCommand = Command(_ => runtime.Updates.DownloadAsync(), _ => CanDownloadUpdate);
        InstallUpdateCommand = Command(_ =>
        {
            runtime.RequestInstall();
            InstallRequested = true;
            RequestClose?.Invoke();
            return Task.CompletedTask;
        }, _ => CanInstallUpdate);
        BrowseClientCommand = Command(async _ =>
        {
            if (PickClientAsync is not null && await PickClientAsync() is { } path)
                ClientPath = path;
        });
        LoadCharacters();
        LoadLibrary();
        SelectedCharacter = Characters.FirstOrDefault(c => c.OriginalName.Equals(runtime.Settings.LastCharacter, StringComparison.OrdinalIgnoreCase)) ?? Characters.FirstOrDefault();
        SelectedQuest = library.FirstOrDefault();
        Runtime.Changed += RefreshFromRuntime;
        Refresh();
    }

    internal AsyncCommand Command(Func<object?, Task> action, Func<object?, bool>? can = null, bool allowConcurrent = false)
    {
        var command = new AsyncCommand(async p =>
        {
            await action(p);
            Refresh();
        }, can, ReportError, allowConcurrent);
        commands.Add(command);
        return command;
    }

    public async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception e)
        {
            ReportError(e);
        }
        finally
        {
            Refresh();
        }
    }

    public void ReportError(Exception error)
    {
        ErrorMessage = DesktopRuntime.PlayerReason(error.Message);
        Runtime.Log(error.Message);
        Refresh();
    }

    void LoadCharacters()
    {
        foreach (string name in Runtime.Characters)
            if (!removedAccounts.Contains(name) && !Characters.Any(c => c.OriginalName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                Characters.Add(new(this, name));
    }

    void LoadLibrary()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.Content, "catalog.json")));
        foreach (var q in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            string Get(string key) => q.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
            string name = Get("name");
            var recipe = Get("id") switch
            {
                "capofdanaan" or "bandofdanaan" => Runtime.Recipes.GetValueOrDefault(DanaanEvidence.Id),
                WolfQuestEvidence.CatalogId => Runtime.Recipes.GetValueOrDefault(WolfQuestEvidence.Id),
                _ => Runtime.Recipes.Values.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            };
            var (description, region) = Details(recipe?.Id);
            if (recipe?.IsTask == true && recipe.QuestClasses.Length == 0) continue;
            bool givesLegend = q.TryGetProperty("givesLegend", out var legend) && legend.ValueKind == JsonValueKind.True;
            library.Add(new(this, Get("id"), recipe?.Id ?? "", name, Get("rewards"), Get("requiredItems"), Get("level"), Get("source"), description, region, givesLegend, Get("legendEvidence")));
        }

        library.Sort((a, b) =>
        {
            int progression = QuestProgression.Order(a.Id, a.Level).CompareTo(QuestProgression.Order(b.Id, b.Level));
            return progression != 0 ? progression : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });
    }

    static (string, string) Details(string? id) => id switch
    {
        "spare-a-stick" => ("Gather six branches for Callough. Each ordinary attempt verifies its rewards; a permanent outcome ends repeats.", "MILETH · CALLOUGH"),
        "mundane-birth" => ("Choose your season, age and hometown. Riona records a lasting birth legend.", "MILETH · RIONA"),
        "perfect-hairstyle" => ("Visit Eeva for a ponytail. Weekly attempts continue until your own Suomi legend is awarded.", "SUOMI · EEVA"),
        "entered-muisir-village" => ("Bring Kiril five Undine Mushrooms. Verify your Muisir legend and four Shrinking Potions.", "UNDINE · KIRIL"),
        FiskEvidence.Id => ("Keep Fisk's secret, arrange his bouquet and deliver it discreetly. The paid hour wait is saved; other ready queued quests can run before collection.", "SUOMI · FISK"),
        DanaanEvidence.Id => ("Follow the Donnan / Cail route to Danaan's gift. Bring Mold for Greim's approved rite. Other offered routes pause for review; consecration is separate.", "LOURES · DANAAN"),
        WolfQuestEvidence.Id => ("Speak to Throttler, hunt the West Woodland wolf and return for Vanilla Potion or Fifleaf deum, 60,000 experience and your legend mark.", "WEST WOODLAND · THROTTLER"),
        "emerald-dawn" => ("Recover an Emerald Knight Insignia, hear Berg's story, then give it to the Knights, the Necromancers, or sell it to Skan.", "SUOMI · SHINEWOOD"),
        "shinewood-faerie" => ("Try for a Pet Faerie and learn eligible missing spells. Prepare a safe return before entering the forest.", "SHINEWOOD · FAERIE"),
        "cured-sick-child" => ("Pick White Blossoms at Mehadi and give Paulin five, one a day or with help from your other characters. Locks out Aided the Pact of Anaman.", "LOURES · PAULIN"),
        "aided-pact-of-anaman" => ("Pick Golden Blossoms at Mehadi and give Paulin five for the physician, one a day or with help from your other characters. The legend mark is brown and other players can't see it. Locks out Cured the Sick Child.", "LOURES · PAULIN"),
        "altar-worship" => ("Buy a Leather Shield from Torrance in the Mileth Armor Shop and drop it on the Mileth Altar, one offering every 3 hours. The quest finishes after the 10th. Each offering costs 750 gold; a Leather Shield already in the bag is used first.", "MILETH · ALTAR"),
        DaydreamerEvidence.Id => ("Visit Celesta in Pravat Cave and choose Avid Daydreamer. Each queue run makes one daily check-in; a recurring schedule handles later visits at least 24 hours apart. The first verified count of 30 completes the goal. Further daily check-ins require explicit continuation.", "PRAVAT CAVE · CELESTA"),
        AwakeningEvidence.Id => ("Enters through Torrance for level 1–3 starters, or resumes the tutorial: Raghnall's combat talk for a Stick, Bartley's Interface and General Info lessons for the 'nis' spell and Look skill, then the exit and the wake-up in Mileth Inn. Ends at level 4 with a Dirk, two rings, a Tutorial Book and 2,000 gold.", "TUTORIAL · MILETH INN"),
        NewCharacterPath.FirstStepsId => ("Buys the starter gear as the character reaches each level, levels to 7 in Mileth Crypt 1-1 and 2-1, and ends by buying and equipping a Loures Saber in Piet. Needs 50,000 gold to start. Up to level 11, stat points go into CON and WIS only, so this never rules out any class or stat plan.", "MILETH · CRYPT"),
        NewCharacterPath.ClimbId => ("Fights in Mileth Crypt 1-1 and 2-1 until level 11. Needs the Peasant's First Steps gear. Up to level 11, stat points go into CON and WIS only, so this never rules out any class or stat plan.", "MILETH · CRYPT"),
        _ => ("This quest is a guide for manual play. Open its source for the full journey.", "QUEST GUIDE")
    };
    void RefreshFromRuntime() => Refresh(false);
    public void Refresh() => Refresh(true);

    void Refresh(bool reloadSaved)
    {
        if (refreshing)
            return;
        refreshing = true;
        try
        {
            if (reloadSaved)
                Runtime.InvalidatePresentation();
            Presentation = Runtime.CapturePresentation();
            LoadCharacters();
            SortSwitcher();
            if (selectedCharacter is null)
                selectedCharacter = Characters.FirstOrDefault();
            var queue = QueueState;
            RefreshJourneyOverviews();
            foreach (var character in Characters)
                character.Refresh();
            foreach (var quest in library)
                quest.Refresh(CharacterName, queue);
            if (selectedQuest is { Id: "capofdanaan" or "bandofdanaan" } && !CharacterFilteredLibrary.Contains(selectedQuest))
                SelectedQuest = CharacterFilteredLibrary.FirstOrDefault(q => q.RecipeId == DanaanEvidence.Id);
            if (selectedQuest?.RecipeId == AltarWorshipEvidence.Id && !CharacterFilteredLibrary.Contains(selectedQuest))
                SelectedQuest = CharacterFilteredLibrary.FirstOrDefault();
            var visible = LegendFilteredLibrary.Where(q => (Search.Length == 0 || string.Join(' ', q.Name, q.Rewards, q.Requirements, q.Level, q.Reason).Contains(Search, StringComparison.OrdinalIgnoreCase)) && (Filter switch
            {
                "Available" => q.QueueEligible,
                "Completed" => q.IsCompleted,
                "Guides" => q.IsGuideOnly,
                _ => true
            })).ToArray();
            Sync(Quests, visible);
            var rows = queue.Quests.Select((id, index) =>
            {
                var row = QueueItems.FirstOrDefault(r => r.Character.Equals(CharacterName, StringComparison.OrdinalIgnoreCase) && r.Id == id) ?? new QueueItemVM(this, CharacterName, id);
                row.Refresh(index + 1, queue);
                return row;
            }).ToArray();
            Sync(QueueItems, rows);
            RefreshHistory();
            RefreshDashboard();
            RefreshTasks();
            WarmArtwork();
            RaiseAll();
            foreach (var command in commands)
                command.Notify();
        }
        finally
        {
            refreshing = false;
        }
    }

    static void Sync<T>(ObservableCollection<T> collection, IReadOnlyList<T> desired)
    {
        for (int i = collection.Count - 1; i >= 0; i--)
            if (!desired.Contains(collection[i]))
                collection.RemoveAt(i);
        for (int i = 0; i < desired.Count; i++)
        {
            if (i < collection.Count && EqualityComparer<T>.Default.Equals(collection[i], desired[i]))
                continue;
            int old = collection.IndexOf(desired[i]);
            if (old >= 0)
                collection.Move(old, i);
            else
                collection.Insert(i, desired[i]);
        }
    }

    void RefreshHistory()
    {
        var rows = new List<HistoryItemVM>();
        foreach (string character in Runtime.Characters)
        {
            var queue = Runtime.Queue(character);
            rows.AddRange(queue.History.Select(h => new HistoryItemVM(character, Runtime.QuestName(h.Quest), h.Outcome switch
            {
                "Completed" => "Completed",
                "AttemptCompleted" => "Attempt recorded",
                "PermanentlyCompleted" => "Completed",
                _ => h.Outcome
            }, h.At, DesktopRuntime.PlayerReason(h.Summary), Runtime)));
            foreach (var recipe in Runtime.Recipes.Values)
            {
                var job = Presentation.Job(character, recipe.Id);
                foreach (var attempt in job.HairstyleAttempts)
                    if (!queue.History.Any(h => h.Quest == recipe.Id && h.At == attempt.CompletedAt))
                        rows.Add(new(character, recipe.Name, attempt.LegendReward ? "Completed" : "Attempt recorded", attempt.CompletedAt, string.Join(" · ", attempt.Rewards.Select(r => $"{r.Value:N0} {r.Key}")), Runtime));
                if (job.CompletedAt is { } at && !queue.History.Any(h => h.Quest == recipe.Id && h.At == at))
                    rows.Add(new(character, recipe.Name, QuestRunPolicy.Finished(recipe, job) ? "Completed" : "Attempt recorded", at, DesktopRuntime.PlayerReason(job.CompletionText ?? "Verified outcome saved"), Runtime));
            }
        }

        Sync(History, rows.DistinctBy(r => (r.Character, r.Quest, r.At)).OrderByDescending(r => r.At).ToArray());
        foreach (var entry in History)
            entry.RaiseAll();
    }

    public async Task ToggleQuestAsync(QuestItemVM quest)
    {
        string character = CharacterName, id = quest.RecipeId;
        bool removing = quest.IsQueued;
        if (!quest.CanQueue || questConfirmationPending)
        {
            quest.Raise(nameof(QuestItemVM.IsQueued));
            return;
        }

        await RunAsync(async () =>
        {
            if (!removing && SickChildEvidence.For(id) is not null)
            {
                var before = Runtime.Queue(character).Quests.ToArray();
                string other = id == SickChildEvidence.Id ? SickChildEvidence.AnamanId : SickChildEvidence.Id;
                string? replacing = before.Contains(other) ? other : null;
                questConfirmationPending = true;
                try
                {
                    if (ConfirmPaulinQuestAsync is null || !await ConfirmPaulinQuestAsync(character, id, replacing))
                        return;
                    if (character != CharacterName || Runtime.QueueReserves(character))
                        return;
                    await Runtime.EditQueueAsync(character, s =>
                    {
                        if (!s.Quests.SequenceEqual(before))
                            throw new QuestBlockedException("The queue changed while choosing. Review it and choose again.");
                        return s with
                        {
                            Quests = replacing is null ? s.Quests.Append(id).ToArray() : s.Quests.Select(q => q == other ? id : q).ToArray()
                        };
                    });
                }
                finally { questConfirmationPending = false; }
                return;
            }
            await Runtime.EditQueueAsync(character, s => s with { Quests = removing ? s.Quests.Where(q => q != id).ToArray() : s.Quests.Append(id).ToArray(), ActiveQuest = removing && s.ActiveQuest == id ? null : s.ActiveQuest });
        });
    }

    public async Task MoveQueueAsync(string character, string id, int target)
    {
        if (!character.Equals(CharacterName, StringComparison.OrdinalIgnoreCase) || QueueLocked)
            return;
        await RunAsync(() => Runtime.MoveQueueAsync(character, id, target));
        SelectedQueueItem = QueueItems.FirstOrDefault(r => r.Id == id);
        Raise(nameof(SelectedQueueItem));
    }

    internal void OpenGuide(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            try
            {
                Process.Start(new ProcessStartInfo(source) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                ReportError(e);
            }
    }
}
