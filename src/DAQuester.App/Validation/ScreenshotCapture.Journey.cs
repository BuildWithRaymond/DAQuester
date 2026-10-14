using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.ViewModels;
using DAQuester.Core;

namespace DAQuester.App.Validation;

internal static partial class ScreenshotCapture
{
    // Exercises the real native window with isolated sample state. No client is started.
    static async Task<object> CaptureJourneysAsync(Window window, MainWindowViewModel vm, List<string> renders)
    {
        var runtime = vm.Runtime;
        runtime.SetPreviewRunning("Agent", false);
        runtime.SetPreviewQueue(new()
        {
            Character = "Agent"
        });
        var observed = new CharacterSnapshot
        {
            Name = "Elara",
            Id = 43,
            Connected = true,
            Generation = 1,
            Revision = 30,
            Level = 1,
            Hp = 100,
            MaxHp = 100,
            Gold = 50_000,
            Class = "Priest",
            InventoryKnown = true,
            LegendsKnown = true,
            LegendsRevision = 30,
            Weight = 0,
            MaxWeight = 100,
            Experience = 0
        };
        runtime.SetPreviewSnapshot(observed);
        runtime.SetPreviewQueue(new()
        {
            Character = "Elara",
            Quests = [NewCharacterPath.FirstStepsId, NewCharacterPath.ClimbId, "emerald-dawn"]
        });
        vm.Refresh();
        vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
        vm.Page = "Planner";
        vm.Filter = "All";
        vm.Search = "";
        window.Width = 1450;
        window.Height = 930;
        await SettleAsync(window);
        await CaptureJourneyAsync(window, "journey-dependencies", renders);
        var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
        await journal.SaveAsync(new()
        {
            Character = "Elara",
            Quest = "emerald-dawn",
            EmeraldOptions = new("emerald-knights")
        });
        vm.Refresh();
        await CaptureJourneyAsync(window, "journey-dependencies-chosen", renders);

        var firstRow = window.FindControl<ItemsControl>("queueList")!.GetVisualDescendants().OfType<Border>()
            .Single(b => b.Name == "queueRow" && b.DataContext is QueueItemVM { Id: NewCharacterPath.FirstStepsId });
        bool focused = firstRow.Focus();
        var reorder = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down, KeyModifiers = KeyModifiers.Control };
        firstRow.RaiseEvent(reorder);
        await SettleAsync(window);
        bool reordered = runtime.Queue("Elara").Quests[0] == NewCharacterPath.ClimbId;
        await CaptureJourneyAsync(window, "journey-keyboard-reordered", renders);
        var movedRow = window.FindControl<ItemsControl>("queueList")!.GetVisualDescendants().OfType<Border>()
            .Single(b => b.Name == "queueRow" && b.DataContext is QueueItemVM { Id: NewCharacterPath.FirstStepsId });
        var remove = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Delete };
        movedRow.RaiseEvent(remove);
        await SettleAsync(window);
        bool removed = !runtime.Queue("Elara").Quests.Contains(NewCharacterPath.FirstStepsId);
        await CaptureJourneyAsync(window, "journey-prerequisite-removed", renders);

        await journal.SaveAsync(new()
        {
            Character = "Elara",
            Quest = SickChildEvidence.Id,
            SickChildOptions = new(SickChildOptions.Daily, [], 0)
        });
        observed = observed with
        {
            Level = 11,
            Gold = 25_000,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, SickChildEvidence.Blossom, 5))
        };
        runtime.SetPreviewSnapshot(observed);
        runtime.SetPreviewQueue(new()
        {
            Character = "Elara",
            Quests = [SickChildEvidence.Id, AltarWorshipEvidence.Id]
        });
        vm.Refresh();
        // Review queue controls without exposing the temporarily hidden whole-journey forecast.
        await CaptureJourneyAsync(window, "journey-planned-queue", renders);
        window.Width = 900;
        window.Height = 560;
        await CaptureJourneyAsync(window, "journey-minimum", renders);

        window.Width = 1450;
        window.Height = 930;
        vm.Page = "Dashboard";
        vm.DashboardListView = false;
        vm.SelectedDashboardCharacter = vm.DashboardCharacters.Single(c => c.Name == "Elara");
        await CaptureJourneyAsync(window, "dashboard-integrated-cards", renders);
        vm.DashboardListView = true;
        await CaptureJourneyAsync(window, "dashboard-integrated-list", renders);

        runtime.SetPreviewQueue(new()
        {
            Character = "Elara",
            Quests = [NewCharacterPath.FirstStepsId, SickChildEvidence.Id],
            State = "Paused",
            ActiveQuest = NewCharacterPath.FirstStepsId,
            Reason = "The last submitted action has no verified result. Resume must reconcile the current generation, equipment ownership and observed level before another quest can run. Long explanations remain available in the planner and never authorize a projected handoff."
        });
        vm.Refresh();
        await CaptureJourneyAsync(window, "dashboard-long-explanation", renders);
        window.Width = 900;
        window.Height = 560;
        await CaptureJourneyAsync(window, "dashboard-integrated-minimum", renders);

        runtime.SetPreviewQueue(new()
        {
            Character = "Elara",
            Quests = [SickChildEvidence.Id, AltarWorshipEvidence.Id]
        });
        runtime.SetPreviewSnapshot(observed with
        {
            Connected = false,
            Revision = 0,
            Gold = null,
            InventoryKnown = false,
            Level = null
        });
        vm.Refresh();
        vm.Page = "Planner";
        await CaptureJourneyAsync(window, "journey-unknown-minimum", renders);
        bool disabled = !window.FindControl<Button>("queueStart")!.IsEffectivelyEnabled;
        window.Width = 1450;
        window.Height = 930;
        await CaptureJourneyAsync(window, "journey-unknown", renders);

        runtime.SetPreviewSnapshot(observed with
        {
            Inventory = ImmutableDictionary<byte, Item>.Empty
            .Add(1, new(1, SickChildEvidence.Blossom, 1)).Add(2, new(2, AltarWorshipEvidence.Shield, 3))
        });
        var now = DateTimeOffset.UtcNow;
        await journal.SaveAsync(new()
        {
            Character = "Elara",
            Quest = SickChildEvidence.Id,
            SickChildOptions = new(SickChildOptions.Daily, [], 1),
            SickChildTurnIns = [new(now.AddDays(-3), 1, 25_000, 20_000, "", null), new(now.AddDays(-2), 1, 20_000, 15_000, "", null)]
        });
        await journal.SaveAsync(new()
        {
            Character = "Elara",
            Quest = AltarWorshipEvidence.Id,
            AltarOfferings = [new(now.AddHours(-9), 1, 750, "", 0), new(now.AddHours(-6), 1, 750, "", 0)]
        });
        vm.Refresh();
        vm.Page = "Characters";
        foreach (var character in vm.Characters)
            character.IsExpanded = false;
        await CaptureJourneyAsync(window, "characters-integrated-collapsed", renders);
        var elara = vm.Characters.Single(c => c.Name == "Elara");
        var disclosure = window.GetVisualDescendants().OfType<ToggleButton>()
            .Single(control => control.Name == "characterDetails" && ReferenceEquals(control.DataContext, elara));
        bool disclosureFocused = disclosure.Focus();
        // Exercise the standard keyboard toggle and its two-way binding in isolated sample data.
        void ToggleDisclosure()
        {
            disclosure.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            disclosure.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        }
        ToggleDisclosure();
        bool disclosureOpened = elara.IsExpanded;
        ToggleDisclosure();
        bool disclosureClosed = !elara.IsExpanded;
        ToggleDisclosure();
        await CaptureJourneyAsync(window, "characters-partial-supplies", renders);
        window.Width = 900;
        window.Height = 560;
        await CaptureJourneyAsync(window, "characters-partial-supplies-minimum", renders);
        return new
        {
            keyboard = new
            {
                frameworkRoutedEvents = true,
                physicalKeys = "unrun",
                focused,
                reorder.Handled,
                reordered,
                deleteHandled = remove.Handled,
                removed
            },
            unknownStartDisabled = disabled,
            characterDisclosure = new
            {
                frameworkRoutedEvents = true,
                physicalKeys = "unrun",
                focused = disclosureFocused,
                opened = disclosureOpened,
                closed = disclosureClosed
            },
            clientsStarted = 0,
            observedStateUsedAsForecastReceipt = false
        };
    }

    static async Task SettleAsync(Window window)
    {
        await Task.Delay(700);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    static async Task CaptureJourneyAsync(Window window, string name, List<string> renders)
    {
        await SettleAsync(window);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
        bitmap.Render(window);
        string path = "artifacts/screenshots/" + name + ".png";
        bitmap.Save(path);
        renders.Add(path);
    }
}
