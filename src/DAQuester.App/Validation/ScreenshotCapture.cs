using System.Collections.Immutable;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.ViewModels;
using DAQuester.Core;

namespace DAQuester.App.Validation;
// Optional documentation captures, not a UI test suite. The preview runtime
// never opens listeners or launches clients, and all data stays in artifacts.
internal static class ScreenshotCapture
{
    public static async Task RunAsync(Window window, MainWindowViewModel viewModel)
    {
        Directory.CreateDirectory("artifacts/screenshots");
        int exitCode = 0;
        try
        {
            if (!Program.IsScreenshots || !viewModel.Runtime.Preview)
                throw new InvalidOperationException("Screenshots require an isolated preview runtime.");
            await SeedWorkspaceAsync(viewModel);
            window.Width = 1450;
            window.Height = 930;
            var renders = new List<string>();
            foreach (string page in new[]
            {
                "Planner",
                "Characters",
                "History",
                "Settings"
            }

            )
            {
                viewModel.Page = page;
                await Task.Delay(100);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var size = window.ClientSize;
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)size.Width, (int)size.Height), new Vector(96, 96));
                bitmap.Render(window);
                string path = "artifacts/screenshots/" + page.ToLowerInvariant() + ".png";
                bitmap.Save(path);
                renders.Add(path);
            }

            // Inspect the same card with a paused checkpoint and another library selection.
            viewModel.Page = "Planner";
            viewModel.JourneyCollapsed = true;
            await Task.Delay(650);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
            {
                bitmap.Render(window);
                string path = "artifacts/screenshots/planner-collapsed.png";
                bitmap.Save(path);
                renders.Add(path);
            }
            viewModel.JourneyCollapsed = false;
            await Task.Delay(650);
            var plannedQueue = viewModel.Runtime.Queue("Agent");
            viewModel.Runtime.SetPreviewQueue(plannedQueue with { State = "Paused", ActiveQuest = "perfect-hairstyle", Reason = "Paused by player" });
            viewModel.SelectedQuest = viewModel.Quests.Single(q => q.RecipeId == "emerald-dawn");
            window.FindControl<Expander>("questDetails")!.IsExpanded = true;
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
            {
                bitmap.Render(window);
                string path = "artifacts/screenshots/planner-paused.png";
                bitmap.Save(path);
                renders.Add(path);
            }
            // Manually review reopening and selecting details after scrolling to the bottom.
            var details = window.FindControl<Expander>("questDetails")!;
            var detailsScroll = window.FindControl<ScrollViewer>("questDetailsScroll")!;
            detailsScroll.Offset = new Vector(0, 10000);
            window.UpdateLayout();
            double scrolledDetails = detailsScroll.Offset.Y;
            details.IsExpanded = false;
            details.IsExpanded = true;
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            double reopenedDetails = detailsScroll.Offset.Y;
            detailsScroll.Offset = new Vector(0, 10000);
            viewModel.Filter = "All";
            viewModel.SelectedQuest = viewModel.Quests.Single(q => q.RecipeId == "spare-a-stick");
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            double selectedDetails = detailsScroll.Offset.Y;
            viewModel.Filter = "Available";

            await viewModel.Runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
            await viewModel.Runtime.SetSchedulerEnabledAsync(true, true);
            // Presentation-only ownership; preview cannot launch or operate characters.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            void SetScheduler(string field, object? value) => typeof(DAQuester.App.Services.DesktopRuntime).GetField(field, flags)!.SetValue(viewModel.Runtime, value);
            SetScheduler("schedulerCharacter", "Agent");
            SetScheduler("schedulerQuest", "spare-a-stick");
            SetScheduler("schedulerPhase", "Running");
            viewModel.Refresh();
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
            {
                bitmap.Render(window);
                string path = "artifacts/screenshots/planner-scheduled.png";
                bitmap.Save(path);
                renders.Add(path);
            }
            SetScheduler("schedulerCharacter", null);
            SetScheduler("schedulerQuest", null);
            SetScheduler("schedulerPhase", null);
            // Minimum-size idle planner: selection and the planned queue stay present.
            viewModel.Runtime.SetPreviewQueue(plannedQueue);
            viewModel.Refresh();
            window.Width = 900;
            window.Height = 560;
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
            {
                bitmap.Render(window);
                string path = "artifacts/screenshots/planner-compact.png";
                bitmap.Save(path);
                renders.Add(path);
            }
            viewModel.JourneyCollapsed = true;
            await Task.Delay(650);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
            {
                bitmap.Render(window);
                string path = "artifacts/screenshots/planner-compact-collapsed.png";
                bitmap.Save(path);
                renders.Add(path);
            }
            viewModel.JourneyCollapsed = false;
            await Task.Delay(650);
            viewModel.Page = "Characters";
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
            {
                bitmap.Render(window);
                string path = "artifacts/screenshots/characters-compact.png";
                bitmap.Save(path);
                renders.Add(path);
            }
            window.Width = 1450;
            window.Height = 930;

            // Optional scheduler reference, still using only the isolated preview runtime.
            foreach (var entry in viewModel.Runtime.SchedulerState.Entries)
                await viewModel.Runtime.RemoveScheduleAsync(entry.Id);
            await viewModel.Runtime.SaveScheduleAsync("Elara", "spare-a-stick", DateTimeOffset.UtcNow.AddHours(1), true, true);
            await viewModel.Runtime.SaveScheduleAsync("Wren", "perfect-hairstyle", DateTimeOffset.UtcNow.AddHours(2), false, true);
            await viewModel.Runtime.PauseScheduleAsync(viewModel.Runtime.SchedulerState.Entries.Single(e => e.Character == "Wren").Id);
            viewModel.Page = "Scheduler";
            await Task.Delay(700);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var scheduler = (DAQuester.App.Views.SchedulerPage)window.FindControl<ContentControl>("schedulerPage")!.Content!;
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                bitmap.Render(window);
                string path = "artifacts/screenshots/scheduler.png";
                bitmap.Save(path);
                renders.Add(path);
                var warnings = scheduler.GetVisualDescendants().OfType<Expander>().Single();
                warnings.IsExpanded = true;
                await Task.Delay(100);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                using (var warningBitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
                {
                    warningBitmap.Render(window);
                    string warningPath = "artifacts/screenshots/scheduler-warnings.png";
                    warningBitmap.Save(warningPath);
                    renders.Add(warningPath);
                }
                warnings.IsExpanded = false;
                window.Width = 900;
                window.Height = 560;
                await Task.Delay(100);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                using (var compactBitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
                {
                    compactBitmap.Render(window);
                    string compactPath = "artifacts/screenshots/scheduler-compact.png";
                    compactBitmap.Save(compactPath);
                    renders.Add(compactPath);
                }
                scheduler.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "schedulerBody").Offset = new Vector(0, 10000);
                window.UpdateLayout();
                using var footerBitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                footerBitmap.Render(window);
                string footerPath = "artifacts/screenshots/scheduler-compact-saved.png";
                footerBitmap.Save(footerPath);
                renders.Add(footerPath);
            }

            // A starter reference makes the app medallions and legend reward shapes reviewable.
            window.Width = 1450;
            window.Height = 930;
            viewModel.Runtime.SetPreviewSnapshot(new() { Name = "Agent", Id = 42, Connected = true,
                Generation = 1, Revision = 20, Map = 500, Position = new(10, 10), Level = 3, Hp = 40 });
            viewModel.Page = "Planner";
            viewModel.Filter = "All";
            viewModel.Search = "";
            viewModel.SelectedQuest = viewModel.Quests.Single(q => q.RecipeId == "awakening");
            await Task.Delay(700);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using (var starterBitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
            {
                starterBitmap.Render(window);
                string starterPath = "artifacts/screenshots/planner-starter.png";
                starterBitmap.Save(starterPath);
                renders.Add(starterPath);
            }

            // Optional native comparison captures; isolated data only, never queue or run a character.
            foreach (bool replace in new[] { false, true })
            {
                var dialog = new Views.PaulinQueueDialog("Agent", SickChildEvidence.Id,
                    replace ? SickChildEvidence.AnamanId : null, viewModel.JourneyGameDirectory);
                var closed = dialog.ShowDialog<bool>(window);
                await Task.Delay(700);
                Dispatcher.UIThread.RunJobs();
                dialog.UpdateLayout();
                using var dialogBitmap = new RenderTargetBitmap(new PixelSize((int)dialog.ClientSize.Width, (int)dialog.ClientSize.Height), new Vector(96, 96));
                dialogBitmap.Render(dialog);
                string dialogPath = "artifacts/screenshots/paulin-" + (replace ? "replace" : "choice") + ".png";
                dialogBitmap.Save(dialogPath);
                renders.Add(dialogPath);
                dialog.Close(false);
                await closed;
            }

            var huntingSnapshot = viewModel.Runtime.Snapshot("Agent") with { Level = 5, ToNextLevel = 1234, Map = 1, MapName = "Crypt 1-1", Position = new(5, 5),
                Entities = ImmutableDictionary<uint, Entity>.Empty.Add(55, new(55, "", new(6, 5), true) { Sprite = 53, Kind = 0 }) };
            viewModel.Runtime.SetPreviewSnapshot(huntingSnapshot);
            viewModel.Runtime.SetPreviewQueue(new() { Character = "Agent", State = "Running", Quests = [NewCharacterPath.FirstStepsId], ActiveQuest = NewCharacterPath.FirstStepsId });
            viewModel.Runtime.SetPreviewRunning("Agent", true);
            viewModel.Runtime.RecordHuntingActivity("Agent", NewCharacterPath.FirstStepsId, huntingSnapshot.Generation, new("Attacking", huntingSnapshot.Entities[55]));
            viewModel.JourneyCollapsed = false;
            window.FindControl<Expander>("questDetails")!.IsExpanded = false;
            foreach (bool compact in new[] { false, true })
            {
                window.Width = compact ? 900 : 1450;
                window.Height = compact ? 560 : 930;
                viewModel.Refresh();
                await Task.Delay(700);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                using var huntingBitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                huntingBitmap.Render(window);
                string huntingPath = "artifacts/screenshots/first-steps" + (compact ? "-compact" : "") + ".png";
                huntingBitmap.Save(huntingPath);
                renders.Add(huntingPath);
            }

            File.WriteAllText("artifacts/screenshots.json", JsonSerializer.Serialize(new
            {
                framework = "Avalonia",
                renderer = "native",
                isolated = true,
                synthetic = true,
                failed = 0,
                detailsScroll = new { scrolledDetails, reopenedDetails, selectedDetails },
                renders
            }, AppSettings.Json));
            File.WriteAllText("artifacts/screenshots.txt", "Documentation screenshots captured with isolated sample data. No game clients, listeners, or updates.");
        }
        catch (Exception error)
        {
            exitCode = 1;
            File.WriteAllText("artifacts/screenshots.txt", error.ToString());
        }
        finally
        {
            await viewModel.Runtime.ShutdownAsync();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown(exitCode);
        }
    }

    private static async Task SeedWorkspaceAsync(MainWindowViewModel viewModel)
    {
        var runtime = viewModel.Runtime;
        runtime.Settings.Characters = ["Agent", "Elara", "Wren"];
        runtime.Settings.Save();
        viewModel.JourneyCollapsed = false;
        var sample = new CharacterSnapshot
        {
            Name = "Agent",
            Id = 42,
            Connected = true,
            Generation = 1,
            Revision = 12,
            Level = 99,
            Hp = 400,
            Mana = 200,
            Gold = 999,
            Experience = 250000,
            Class = "Wizard",
            IsMaster = true,
            IsPure = true,
            Weight = 7,
            MaxWeight = 100,
            InventoryKnown = true,
            SpellsKnown = true,
            LegendsKnown = true,
            LegendsRevision = 12,
            Map = 3082,
            MapName = "Rucesion Threshold",
            Position = new(12, 18),
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Rucesion Song", 3)),
            Legends = ["Entered Muisir Village - Deoch 223, Spring", "Walked the northern forest - Deoch 223, Winter",
                "Studied the ancient manuscripts - Deoch 222, Fall", "Found the hidden grove - Deoch 222, Summer",
                "Served the community of Mileth - Deoch 221, Spring", "Remembered a friend - Deoch 220, Winter",
                "Travelled the northern road - Deoch 219, Fall", "Visited the coastal village - Deoch 218, Summer",
                "Completed a very long remembered journey with friends from across the kingdom - Deoch 217, Spring"]
        };
        var memory = new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), runtime.Settings.ServerProfile);
        foreach (var snapshot in new[]
        {
            sample,
            sample with
            {
                Name = "Elara",
                Id = 43,
                Level = 11,
                Legends = []
            },
            sample with
            {
                Name = "Wren",
                Id = 44,
                Level = 10,
                Legends = []
            }
        }

        )
        {
            await memory.ObserveAsync(snapshot.Name!, snapshot);
            runtime.SetPreviewSnapshot(snapshot with
            {
                Connected = snapshot.Name != "Wren"
            });
        }

        var queue = new QuestQueueState
        {
            Character = "Agent",
            Quests = ["perfect-hairstyle", "emerald-dawn", "shinewood-faerie"],
            History = [new("entered-muisir-village", "Completed", new(2026, 10, 11, 14, 30, 0, TimeSpan.Zero), "Muisir legend and four Shrinking Potions verified")]
        };
        await new FileQuestQueueStore(Path.Combine(AppSettings.DataPath, "queues")).SaveAsync(queue);
        runtime.SetPreviewQueue(queue);
        viewModel.Refresh();
        viewModel.SelectedCharacter = viewModel.Characters.Single(c => c.Name == "Agent");
        viewModel.SelectedQuest = viewModel.Quests.Single(q => q.RecipeId == "perfect-hairstyle");
    }
}
