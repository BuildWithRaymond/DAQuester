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
 public async Task SavedLaunchActionsWaitForConnectionReadiness()
 {
  var runtime = Runtime(preview: false); var vm = new MainWindowViewModel(runtime);
  Assert.False(vm.LaunchSelectedCommand.CanExecute(null));
  Assert.Contains("connection isn't ready", vm.LaunchTooltip);
  Assert.All(vm.Characters, c => Assert.False(c.LaunchCommand.CanExecute(null)));
  Assert.All(vm.Characters, c => Assert.Contains("connection isn't ready", c.LaunchStatus));
  await runtime.ShutdownAsync();
 }
 [Fact]
 public async Task RenamingObservedAccountDoesNotRecreateItsOldEditorRow()
 {
  var runtime = Runtime(); runtime.SetPreviewSnapshot(new() { Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 1 });
  var vm = new MainWindowViewModel(runtime); vm.Characters.Single(c => c.Name == "Elara").Name = "ElaraPrime";
  await vm.SaveAccountsCommand.ExecuteAsync();
  Assert.False(vm.HasError); Assert.Equal(["ElaraPrime", "Wren"], AppSettings.Load().Characters);
  Assert.DoesNotContain(vm.Characters, c => c.Name == "Elara"); Assert.Equal(2, vm.Characters.Count);
  vm.Characters.Single(c => c.Name == "ElaraPrime").Name = "Elara";
  await vm.SaveAccountsCommand.ExecuteAsync();
  Assert.False(vm.HasError); Assert.Equal(["Elara", "Wren"], AppSettings.Load().Characters); Assert.Equal(2, vm.Characters.Count);
  await runtime.ShutdownAsync();
 }
 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public async Task EmptyAccountLaunchRemainsAvailableWithoutInventingOrSavingACharacter(bool draftAccount)
 {
  var runtime = Runtime(); runtime.Settings.Characters = []; runtime.Settings.Save();
  var vm = new MainWindowViewModel(runtime);
  if (draftAccount) { vm.AddCharacterCommand.Execute(null); vm.SelectedCharacter!.Name = "Elara"; }
  Assert.True(vm.LaunchSelectedCommand.CanExecute(null));
  await vm.LaunchSelectedCommand.ExecuteAsync();
  Assert.Contains("Preview cannot launch game clients", vm.ErrorMessage);
  Assert.Empty(AppSettings.Load().Characters); Assert.False(runtime.LaunchedClientRunning);
  await runtime.ShutdownAsync();
 }
 [Fact]
 public async Task QueueToggleCanPauseWhileItsRunTaskIsStillActive()
 {
  var complete = new TaskCompletionSource(); bool running = false;
  var command = new AsyncCommand(async _ => { if (running) { complete.SetResult(); return; } running = true; await complete.Task; }, allowConcurrent: true);
  var run = command.ExecuteAsync(); Assert.True(running); Assert.True(command.CanExecute(null));
  await command.ExecuteAsync(); await run;
 }
 static DesktopRuntime Runtime(bool preview = true)
 {
  AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
  var settings = AppSettings.Load(); settings.Characters = ["Elara", "Wren"]; settings.ClientPath = ""; settings.Save(); return new(preview: preview);
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
 [Fact] public void InvalidDragBoundaryRejectsOperation() => Assert.Throws<ArgumentOutOfRangeException>(() => QueueReorder.InsertionTarget(0, 4, 3));
 [Fact]
 public async Task SwitchingCharacterDuringQueueSavePreservesOriginalOwner()
 {
  var runtime = Runtime(); var vm = new MainWindowViewModel(runtime); var birth = vm.Quests.Single(q => q.RecipeId == "mundane-birth");
  var save = vm.ToggleQuestAsync(birth); vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Wren"); await save;
  Assert.Equal(["mundane-birth"], runtime.Queue("Elara").Quests); Assert.Empty(runtime.Queue("Wren").Quests); Assert.Empty(vm.QueueItems);
  Assert.Equal(["mundane-birth"], new FileQuestQueueStore(Path.Combine(AppSettings.DataPath, "queues")).Load("elara").Quests);
  await runtime.ShutdownAsync();
 }
 [Fact]
 public async Task SavedObservedLevelAndLegendGatePlanningWithoutInventingConnection()
 {
  var runtime = Runtime(); var memory = new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), runtime.Settings.ServerProfile);
  await memory.ObserveAsync("Elara", new() { Name = "Elara", Id = 1, Connected = true, Generation = 1, Revision = 2, Level = 99, LegendsKnown = true, LegendsRevision = 2, Legends = [MuisirEvidence.Legend] });
  var vm = new MainWindowViewModel(runtime);
  Assert.Equal("Blocked", vm.Quests.Single(q => q.RecipeId == "spare-a-stick").Status);
  Assert.Equal("Completed", vm.Quests.Single(q => q.RecipeId == "entered-muisir-village").Status);
  Assert.Contains("last observed", vm.SelectedCharacter!.LevelText); Assert.False(runtime.Snapshot("Elara").Connected);
  await runtime.ShutdownAsync();
 }
 [Fact]
 public async Task NavigationRetainsSearchAndSelectedCharacterQueue()
 {
  var runtime = Runtime(); var vm = new MainWindowViewModel(runtime) { Search = "birth" };
  vm.Page = "Characters"; vm.Page = "Settings"; vm.Page = "Planner";
  Assert.Equal("birth", vm.Search); Assert.Single(vm.Quests); Assert.Equal("Elara", vm.CharacterName);
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
  await runtime.StartAsync(); Assert.False(runtime.Ready); await runtime.ShutdownAsync();
 }
 [Fact]
 public async Task ExplicitAccountRemovalStaysRemovedDuringBackgroundRefresh()
 {
  var runtime = Runtime(); var vm = new MainWindowViewModel(runtime);
  vm.Characters.Single(c => c.Name == "Wren").RemoveCommand.Execute(null); vm.Refresh();
  Assert.DoesNotContain(vm.Characters, c => c.Name == "Wren");
  await vm.SaveAccountsCommand.ExecuteAsync(); Assert.Equal(["Elara"], AppSettings.Load().Characters);
  await runtime.ShutdownAsync();
 }
}
