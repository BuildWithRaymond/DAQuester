using DAQuester.App;
using DAQuester.App.Controls;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace DAQuester.AppTests;

public class PresentationTests
{
    [Fact]
    public async Task IncomingChoiceControlsRemainAvailableUntilSubmission()
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewSnapshot(new()
            {
                Name = "Elara",
                Id = 1,
                Connected = true,
                Generation = 1,
                Revision = 1
            });
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "perfect-hairstyle");
            Assert.False(vm.ChangeChoicesVisible);
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "emerald-dawn");
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = "emerald-dawn",
                Step = "recover",
                EmeraldMioasSeen = true,
                EmeraldBergBriefed = true
            });
            vm.Refresh();
            Assert.True(vm.ChangeChoicesVisible);
            Assert.True(vm.ChangeChoicesCommand.CanExecute(null));
            runtime.SetPreviewRunning("Elara", true);
            Assert.False(vm.ChangeChoicesCommand.CanExecute(null));
            runtime.SetPreviewRunning("Elara", false);
            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = "emerald-dawn",
                Step = "deliver",
                Pending = new("emerald-delivery-dialog", 1, 1, 0, 0, 0, DateTimeOffset.UtcNow)
            });
            vm.Refresh();
            Assert.False(vm.ChangeChoicesCommand.CanExecute(null));
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "mundane-birth");
            Assert.True(vm.ChangeChoicesVisible);
            Assert.True(vm.ChangeChoicesCommand.CanExecute(null));
            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = "mundane-birth",
                CompletedAt = DateTimeOffset.UtcNow
            });
            vm.Refresh();
            Assert.False(vm.ChangeChoicesCommand.CanExecute(null));
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task IndividualControlsFollowActualRunAndSavedProgress()
    {
        var runtime = Runtime();
        try
        {
            runtime.SetPreviewSnapshot(new()
            {
                Name = "Elara",
                Id = 1,
                Connected = true,
                Generation = 1,
                Revision = 1
            });
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "perfect-hairstyle");
            var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = "perfect-hairstyle",
                Reason = "Preflight interrupted"
            });
            vm.Refresh();
            Assert.False(vm.ResumeQuestCommand.CanExecute(null));
            Assert.False(vm.PauseQuestCommand.CanExecute(null));
            Assert.False(vm.StopQuestCommand.CanExecute(null));

            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = "perfect-hairstyle",
                Pending = new("offer", 1, 1, 0, 0, 0, DateTimeOffset.UtcNow)
            });
            vm.Refresh();
            Assert.True(vm.ResumeQuestCommand.CanExecute(null));
            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = "perfect-hairstyle",
                Accepted = true,
                AcceptedGeneration = 1
            });
            vm.Refresh();
            Assert.True(vm.ResumeQuestCommand.CanExecute(null));

            await journal.SaveAsync(new()
            {
                Character = "Elara",
                Quest = "perfect-hairstyle",
                Step = "eeva",
                Reason = "Paused by player"
            });
            vm.Refresh();
            Assert.True(vm.ResumeQuestCommand.CanExecute(null));
            Assert.False(vm.PauseQuestCommand.CanExecute(null));
            Assert.False(vm.StopQuestCommand.CanExecute(null));

            runtime.SetPreviewRunning("Elara", true);
            Assert.False(vm.ResumeQuestCommand.CanExecute(null));
            Assert.True(vm.PauseQuestCommand.CanExecute(null));
            Assert.True(vm.StopQuestCommand.CanExecute(null));
            runtime.SetPreviewRunning("Elara", false);
            runtime.SetPreviewQueue(new()
            {
                Character = "Elara",
                State = "Paused",
                Quests = ["perfect-hairstyle"],
                ActiveQuest = "perfect-hairstyle"
            });
            Assert.False(vm.PauseQuestCommand.CanExecute(null));
            Assert.False(vm.StopQuestCommand.CanExecute(null));
            Assert.True(vm.StopQueueCommand.CanExecute(null));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task JourneyAddToQueueAddsOnceAndKeepsOtherCharactersSeparate()
    {
        var runtime = Runtime();
        try
        {
            var vm = new MainWindowViewModel(runtime);
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "perfect-hairstyle");
            Assert.True(vm.AddJourneyToQueueCommand.CanExecute(null));
            await vm.AddJourneyToQueueCommand.ExecuteAsync();
            Assert.Equal(["perfect-hairstyle"], runtime.Queue("Elara").Quests);
            Assert.False(vm.AddJourneyToQueueCommand.CanExecute(null));
            await vm.AddJourneyToQueueCommand.ExecuteAsync();
            Assert.Single(runtime.Queue("Elara").Quests);
            Assert.Empty(runtime.Queue("Wren").Quests);
            vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
            Assert.True(vm.AddJourneyToQueueCommand.CanExecute(null));
            vm.SelectedQuest = vm.Quests.First(q => q.RecipeId.Length == 0);
            Assert.False(vm.AddJourneyToQueueCommand.CanExecute(null));
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == "perfect-hairstyle");
            runtime.SetPreviewQueue(new()
            {
                Character = "Wren",
                State = "Running",
                Quests = ["emerald-dawn"],
                ActiveQuest = "emerald-dawn"
            });
            Assert.False(vm.AddJourneyToQueueCommand.CanExecute(null));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Fact]
    public async Task SavedLaunchActionsWaitForConnectionReadiness()
    {
        var runtime = Runtime(preview: false);
        var vm = new MainWindowViewModel(runtime);
        Assert.False(vm.LaunchSelectedCommand.CanExecute(null));
        Assert.Contains("connection isn't ready", vm.LaunchTooltip);
        Assert.All(vm.Characters, c => Assert.False(c.LaunchCommand.CanExecute(null)));
        Assert.All(vm.Characters, c => Assert.Contains("connection isn't ready", c.LaunchStatus));
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task RenamingObservedAccountDoesNotRecreateItsOldEditorRow()
    {
        var runtime = Runtime();
        runtime.SetPreviewSnapshot(new()
        {
            Name = "Elara",
            Id = 1,
            Connected = true,
            Generation = 7,
            Revision = 1
        });
        var vm = new MainWindowViewModel(runtime);
        vm.Characters.Single(c => c.Name == "Elara").Name = "ElaraPrime";
        await vm.SaveAccountsCommand.ExecuteAsync();
        Assert.False(vm.HasError);
        Assert.Equal(["ElaraPrime", "Wren"], AppSettings.Load().Characters);
        Assert.DoesNotContain(vm.Characters, c => c.Name == "Elara");
        Assert.Equal(2, vm.Characters.Count);
        vm.Characters.Single(c => c.Name == "ElaraPrime").Name = "Elara";
        await vm.SaveAccountsCommand.ExecuteAsync();
        Assert.False(vm.HasError);
        Assert.Equal(["Elara", "Wren"], AppSettings.Load().Characters);
        Assert.Equal(2, vm.Characters.Count);
        await runtime.ShutdownAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyAccountLaunchRemainsAvailableWithoutInventingOrSavingACharacter(bool draftAccount)
    {
        var runtime = Runtime();
        runtime.Settings.Characters = [];
        runtime.Settings.Save();
        var vm = new MainWindowViewModel(runtime);
        if (draftAccount)
        {
            vm.AddCharacterCommand.Execute(null);
            vm.SelectedCharacter!.Name = "Elara";
        }

        Assert.True(vm.LaunchSelectedCommand.CanExecute(null));
        await vm.LaunchSelectedCommand.ExecuteAsync();
        Assert.Contains("Preview cannot launch game clients", vm.ErrorMessage);
        Assert.Empty(AppSettings.Load().Characters);
        Assert.False(runtime.LaunchedClientRunning);
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task HeaderSwitcherReopensOnTheCharacterUsedLast()
    {
        var runtime = Runtime();
        runtime.Settings.Characters = ["Bren", "Aria", "Cato"];
        runtime.Settings.Save();
        var vm = new MainWindowViewModel(runtime);
        Assert.Equal("Bren", vm.SelectedCharacter!.DisplayName);
        vm.SelectedCharacter = vm.Characters.Single(c => c.DisplayName == "Aria");
        Assert.Equal("Aria", AppSettings.Load().LastCharacter);
        // A new, unsaved card is never remembered.
        vm.AddCharacterCommand.Execute(null);
        Assert.Equal("Aria", AppSettings.Load().LastCharacter);
        runtime.Settings.LastCharacter = "aria";
        Assert.Equal("Aria", new MainWindowViewModel(runtime).SelectedCharacter!.DisplayName);
        runtime.Settings.LastCharacter = "Removed";
        Assert.Equal("Bren", new MainWindowViewModel(runtime).SelectedCharacter!.DisplayName);
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task HeaderSwitcherListsCharactersByNameWhileTheCharactersPageKeepsItsOrder()
    {
        var runtime = Runtime();
        runtime.Settings.Characters = ["kullan", "Zemial", "verranjr", "Cerda"];
        runtime.Settings.Save();
        var vm = new MainWindowViewModel(runtime);
        Assert.Equal(["Cerda", "kullan", "verranjr", "Zemial"], vm.SwitcherCharacters.Select(c => c.DisplayName));
        Assert.Equal(["kullan", "Zemial", "verranjr", "Cerda"], vm.Characters.Select(c => c.DisplayName));
        var selected = vm.SelectedCharacter;
        vm.AddCharacterCommand.Execute(null);
        Assert.Same(vm.Characters[^1], vm.SelectedCharacter);
        Assert.Contains(vm.SelectedCharacter!, vm.SwitcherCharacters);
        vm.SelectedCharacter = selected;
        vm.Refresh();
        Assert.Same(selected, vm.SelectedCharacter);
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task QueueToggleCanPauseWhileItsRunTaskIsStillActive()
    {
        var complete = new TaskCompletionSource();
        bool running = false;
        var command = new AsyncCommand(async _ =>
        {
            if (running)
            {
                complete.SetResult();
                return;
            }

            running = true;
            await complete.Task;
        }, allowConcurrent: true);
        var run = command.ExecuteAsync();
        Assert.True(running);
        Assert.True(command.CanExecute(null));
        await command.ExecuteAsync();
        await run;
    }

    static DesktopRuntime Runtime(bool preview = true)
    {
        AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        var settings = AppSettings.Load();
        settings.Characters = ["Elara", "Wren"];
        settings.ClientPath = "";
        settings.Save();
        return new(preview: preview);
    }

    [Theory]
    [InlineData("Elara", "Wren", false, 0, 3, false)]
    [InlineData("Elara", "Elara", true, 0, 3, false)]
    [InlineData("Elara", "elara", false, 0, 3, true)]
    [InlineData("Elara", "Elara", false, -1, 3, false)]
    [InlineData("Elara", "Elara", false, 3, 3, false)]
    [InlineData("Elara", "Elara", false, 0, 0, false)]
    public void DragRespectsCharacterOwnershipAndLockedCheckpoint(string owner, string selected, bool locked, int source, int count, bool expected) => Assert.Equal(expected, QueueReorder.CanMove(owner, selected, locked, source, count));
    [Theory]
    [InlineData(0, 3, 3, 2)]
    [InlineData(2, 0, 3, 0)]
    [InlineData(1, 1, 3, 1)]
    [InlineData(1, 2, 3, 1)]
    public void InsertionBoundaryAccountsForRemovedSource(int source, int boundary, int count, int expected) => Assert.Equal(expected, QueueReorder.InsertionTarget(source, boundary, count));
    [Fact]
    public void InvalidDragBoundaryRejectsOperation() => Assert.Throws<ArgumentOutOfRangeException>(() => QueueReorder.InsertionTarget(0, 4, 3));
    [Fact]
    public async Task SwitchingCharacterDuringQueueSavePreservesOriginalOwner()
    {
        var runtime = Runtime();
        var vm = new MainWindowViewModel(runtime);
        var birth = vm.Quests.Single(q => q.RecipeId == "mundane-birth");
        var save = vm.ToggleQuestAsync(birth);
        vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren");
        await save;
        Assert.Equal(["mundane-birth"], runtime.Queue("Elara").Quests);
        Assert.Empty(runtime.Queue("Wren").Quests);
        Assert.Empty(vm.QueueItems);
        Assert.Equal(["mundane-birth"], new FileQuestQueueStore(Path.Combine(AppSettings.DataPath, "queues")).Load("elara").Quests);
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task SavedObservedLevelAndLegendGatePlanningWithoutInventingConnection()
    {
        var runtime = Runtime();
        var memory = new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), runtime.Settings.ServerProfile);
        await memory.ObserveAsync("Elara", new()
        {
            Name = "Elara",
            Id = 1,
            Connected = true,
            Generation = 1,
            Revision = 2,
            Level = 99,
            LegendsKnown = true,
            LegendsRevision = 2,
            Legends = [MuisirEvidence.Legend]
        });
        var vm = new MainWindowViewModel(runtime);
        Assert.Equal("Blocked", vm.Quests.Single(q => q.RecipeId == "spare-a-stick").Status);
        Assert.Equal("Completed", vm.Quests.Single(q => q.RecipeId == "entered-muisir-village").Status);
        Assert.Contains("last observed", vm.SelectedCharacter!.LevelText);
        Assert.False(runtime.Snapshot("Elara").Connected);
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task SavedAppearanceFeedsChooserJourneyAndHistoryWithoutInventingLiveEvidence()
    {
        var runtime = Runtime();
        var look = CharacterPreviewAppearance.From(null).Look with
        {
            Head = 9,
            Face = 0
        };
        await new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), runtime.Settings.ServerProfile)
            .ObserveAsync("Elara", new()
            {
                Name = "Elara",
                Id = 1,
                Connected = true,
                Generation = 1,
                Revision = 1,
                Appearance = look
            });
        runtime.SetPreviewQueue(new()
        {
            Character = "Elara",
            History = [new("perfect-hairstyle", "Completed", DateTimeOffset.UtcNow, "Saved outcome")]
        });
        var vm = new MainWindowViewModel(runtime);
        var row = Assert.Single(vm.History);
        Assert.Equal(look, vm.SelectedCharacter!.RememberedAppearance);
        Assert.Equal(look, vm.JourneyRememberedAppearance);
        Assert.Equal(look, row.RememberedAppearance);
        Assert.False(runtime.Snapshot("Elara").Connected);
        Assert.Null(runtime.Snapshot("Elara").Appearance);
        bool notified = false;
        row.PropertyChanged += (_, _) => notified = true;
        var live = look with
        {
            Head = 12
        };
        runtime.SetPreviewSnapshot(new()
        {
            Name = "Elara",
            Id = 1,
            Connected = true,
            Generation = 2,
            Revision = 1,
            Appearance = live
        });
        vm.Refresh();
        Assert.Same(row, Assert.Single(vm.History));
        Assert.True(notified);
        Assert.Same(live, row.PortraitCharacter!.Appearance);
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task NavigationRetainsSearchAndSelectedCharacterQueue()
    {
        var runtime = Runtime();
        var vm = new MainWindowViewModel(runtime)
        {
            Search = "birth"
        };
        vm.Page = "Characters";
        vm.Page = "Settings";
        vm.Page = "Planner";
        Assert.Equal("birth", vm.Search);
        Assert.Single(vm.Quests);
        Assert.Equal("Elara", vm.CharacterName);
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task PreviewRejectsEveryGameplayEntryPoint()
    {
        var runtime = Runtime();
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.LaunchAsync("Elara"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartQuestAsync("Elara", "spare-a-stick"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartQueueAsync("Elara"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAutomaticCalibration("Elara"));
        await runtime.StartAsync();
        Assert.False(runtime.Ready);
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task ExplicitAccountRemovalStaysRemovedDuringBackgroundRefresh()
    {
        var runtime = Runtime();
        var vm = new MainWindowViewModel(runtime);
        vm.Characters.Single(c => c.Name == "Wren").RemoveCommand.Execute(null);
        vm.Refresh();
        Assert.DoesNotContain(vm.Characters, c => c.Name == "Wren");
        await vm.SaveAccountsCommand.ExecuteAsync();
        Assert.Equal(["Elara"], AppSettings.Load().Characters);
        await runtime.ShutdownAsync();
    }
}
