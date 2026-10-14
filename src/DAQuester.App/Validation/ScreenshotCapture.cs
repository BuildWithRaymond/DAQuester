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
internal static partial class ScreenshotCapture
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
            await CaptureDanaanChoiceAsync(window, viewModel, renders);
            await CaptureDanaanLibraryAsync(window, viewModel, renders);
            await CaptureLongTrailAsync(window, viewModel, renders);
            await CaptureFiskWaitAsync(window, viewModel, renders);
            await CaptureDashboardAsync(window, viewModel, renders);
            await CaptureCharacterDisclosuresAsync(window, viewModel, renders);
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
            viewModel.Runtime.SetPreviewQueue(plannedQueue with
            {
                State = "Paused",
                ActiveQuest = "perfect-hairstyle",
                Reason = "Paused by player"
            });
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
            viewModel.Runtime.SetPreviewSnapshot(new()
            {
                Name = "Agent",
                Id = 42,
                Connected = true,
                Generation = 1,
                Revision = 20,
                Map = 500,
                Position = new(10, 10),
                Level = 3,
                Hp = 40
            });
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

            var huntingSnapshot = viewModel.Runtime.Snapshot("Agent") with
            {
                Level = 5,
                ToNextLevel = 1234,
                Map = 1,
                MapName = "Crypt 1-1",
                Position = new(5, 5),
                Entities = ImmutableDictionary<uint, Entity>.Empty.Add(55, new(55, "", new(6, 5), true)
                {
                    Sprite = 53,
                    Kind = 0
                })
            };
            viewModel.Runtime.SetPreviewSnapshot(huntingSnapshot);
            viewModel.Runtime.SetPreviewQueue(new()
            {
                Character = "Agent",
                State = "Running",
                Quests = [NewCharacterPath.FirstStepsId],
                ActiveQuest = NewCharacterPath.FirstStepsId
            });
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

            var journeyReview = await CaptureJourneysAsync(window, viewModel, renders);
            File.WriteAllText("artifacts/screenshots.json", JsonSerializer.Serialize(new
            {
                framework = "Avalonia",
                renderer = "native",
                isolated = true,
                synthetic = true,
                failed = 0,
                detailsScroll = new
                {
                    scrolledDetails,
                    reopenedDetails,
                    selectedDetails
                },
                journeyReview,
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

    static async Task CaptureCharacterDisclosuresAsync(Window window, MainWindowViewModel vm, List<string> renders)
    {
        vm.Page = "Characters";
        foreach (bool compact in new[] { false, true })
        {
            window.Width = compact ? 900 : 1450;
            window.Height = compact ? 560 : 930;
            foreach (bool expanded in new[] { false, true })
            {
                vm.Characters[0].IsExpanded = expanded;
                await Task.Delay(200);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                string path = "artifacts/screenshots/characters-" + (expanded ? "expanded" : "collapsed") + (compact ? "-compact" : "") + ".png";
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                bitmap.Render(window);
                bitmap.Save(path);
                renders.Add(path);
            }
        }
        vm.Characters[0].IsExpanded = false;
        var selected = vm.SelectedCharacter;
        string accountStatus = vm.AccountStatus;
        vm.AddCharacterCommand.Execute(null);
        var draft = vm.SelectedCharacter!;
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/characters-new.png");
            renders.Add("artifacts/screenshots/characters-new.png");
        }
        draft.RemoveCommand.Execute(null);
        vm.SelectedCharacter = selected;
        vm.AccountStatus = accountStatus;
        window.Width = 1450;
        window.Height = 930;
    }

    static async Task CaptureFiskWaitAsync(Window window, MainWindowViewModel vm, List<string> renders)
    {
        if (!vm.Runtime.Preview)
            throw new InvalidOperationException("Wait capture requires isolated preview data.");
        var original = vm.Runtime.Queue("Agent");
        var journal = new FileQuestJournal(Path.Combine(AppSettings.DataPath, "journal"));
        var at = DateTimeOffset.UtcNow;
        var acceptance = new Dialog(1, 10, 55, 1, "Viveka", FiskEvidence.BouquetAccepted, true, 0, 1, 0, []) { ObservedAt = at };
        await journal.SaveAsync(new()
        {
            Character = "Agent",
            Quest = FiskEvidence.Id,
            Step = "run",
            Status = "Deferred",
            FiskWait = new(at, acceptance, 50000, 30000, true),
            NextRun = at.AddHours(1),
            Route = new()
            {
                Task = 12
            },
            Reason = "Waiting in Suomi Tavern; the paid bouquet wait is saved"
        });
        vm.Runtime.SetPreviewQueue(new()
        {
            Character = "Agent",
            State = "Running",
            Quests = [FiskEvidence.Id],
            Deferred = [new(FiskEvidence.Id, at.AddHours(1))]
        });
        vm.Runtime.SetPreviewQueueRunning("Agent", true);
        vm.Page = "Planner";
        vm.Filter = "All";
        vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Agent");
        vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == FiskEvidence.Id);
        vm.Refresh();
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/fisk-wait.png");
            renders.Add("artifacts/screenshots/fisk-wait.png");
        }
        vm.Runtime.SetPreviewQueueRunning("Agent", false);
        vm.Runtime.SetPreviewQueue(original);
        vm.Filter = "Available";
        vm.Refresh();
    }

    static async Task CaptureDanaanLibraryAsync(Window window, MainWindowViewModel vm, List<string> renders)
    {
        if (!vm.Runtime.Preview)
            throw new InvalidOperationException("Library capture requires isolated preview data.");
        var snapshot = vm.Runtime.Snapshot("Agent");
        var selected = vm.SelectedQuest;
        var look = Controls.CharacterPreviewAppearance.From(null).Look;
        vm.Page = "Planner";
        vm.Filter = "All";
        vm.Search = "Danaan";
        foreach (byte body in new byte[] { 0x10, 0x20 })
        {
            vm.Runtime.SetPreviewSnapshot(snapshot with
            {
                Appearance = look with
                {
                    Body = body
                }
            });
            vm.Refresh();
            vm.SelectedQuest = vm.Quests.Single(q => q.RecipeId == DanaanEvidence.Id);
            window.FindControl<Expander>("questDetails")!.IsExpanded = true;
            await Task.Delay(200);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(window);
            string path = "artifacts/screenshots/danaan-" + (body == 0x10 ? "male" : "female") + ".png";
            bitmap.Save(path);
            renders.Add(path);
        }
        vm.Runtime.SetPreviewSnapshot(snapshot);
        vm.Search = "";
        vm.SelectedQuest = selected;
        window.FindControl<Expander>("questDetails")!.IsExpanded = false;
    }

    static async Task CaptureDanaanChoiceAsync(Window owner, MainWindowViewModel vm, List<string> renders)
    {
        if (!vm.Runtime.Preview)
            throw new InvalidOperationException("Choice capture requires isolated preview data.");
        var dialog = new Views.QuestChoiceDialog("Elara", vm.Runtime.Recipes[DanaanEvidence.Id], new()
        {
            Character = "Elara",
            Quest = DanaanEvidence.Id
        }, null);
        var closed = dialog.ShowDialog<Services.QuestChoiceResult?>(owner);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
        dialog.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)dialog.ClientSize.Width, (int)dialog.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(dialog);
            string path = "artifacts/screenshots/danaan-choices.png";
            bitmap.Save(path);
            renders.Add(path);
        }
        dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "danaanAvatar").SelectedIndex = 0;
        dialog.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "danaanRiteAccepted").IsChecked = true;
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();
        dialog.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)dialog.ClientSize.Width, (int)dialog.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(dialog);
            string path = "artifacts/screenshots/danaan-choices-selected.png";
            bitmap.Save(path);
            renders.Add(path);
        }
        dialog.Close();
        await closed;
    }

    static async Task CaptureDashboardAsync(Window window, MainWindowViewModel vm, List<string> renders)
    {
        var runtime = vm.Runtime;
        var originalQueue = runtime.Queue("Agent");
        var originalElara = runtime.Snapshot("Elara");
        runtime.SetPreviewQueue(originalQueue with
        {
            State = "Running",
            ActiveQuest = "perfect-hairstyle"
        });
        runtime.SetPreviewRunning("Agent", true);
        runtime.SetPreviewQueue(new()
        {
            Character = "Elara",
            Quests = [NewCharacterPath.ClimbId],
            State = "Paused",
            ActiveQuest = NewCharacterPath.ClimbId,
            Reason = "Free an inventory slot, then resume."
        });
        vm.Page = "Dashboard";
        foreach (bool compact in new[] { false, true })
        {
            window.Width = compact ? 900 : 1450;
            window.Height = compact ? 560 : 930;
            foreach (bool list in new[] { false, true })
            {
                vm.DashboardListView = list;
                vm.SelectedDashboardCharacter = vm.DashboardCharacters.First(c => c.Name == "Agent");
                await Task.Delay(400);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                string path = "artifacts/screenshots/dashboard-" + (list ? "list" : "cards") + (compact ? "-compact" : "") + ".png";
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                bitmap.Render(window);
                bitmap.Save(path);
                renders.Add(path);
            }
        }
        var attentionQueue = runtime.Queue("Elara");
        runtime.SetPreviewQueue(attentionQueue with
        {
            Quests = [DanaanEvidence.Id],
            ActiveQuest = DanaanEvidence.Id,
            Reason = "Free an inventory slot before returning to your guide. Open the planner to review the saved journey, then resume when you are ready to continue."
        });
        vm.SelectedDashboardCharacter = vm.DashboardCharacters.First(c => c.Name == "Elara");
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/dashboard-list-long-text.png");
            renders.Add("artifacts/screenshots/dashboard-list-long-text.png");
        }
        runtime.SetPreviewQueue(attentionQueue);
        vm.SelectedDashboardCharacter = vm.DashboardCharacters.First(c => c.Name == "Agent");
        window.Width = 1450;
        window.Height = 930;
        vm.DashboardListView = false;
        await Task.Delay(250);
        var levelFeedback = new CharacterFeedback(1, 13, "Level", 12, DateTimeOffset.UtcNow);
        runtime.SetPreviewSnapshot(originalElara with
        {
            Revision = 13,
            Level = 12,
            Feedback = [levelFeedback]
        });
        await Task.Delay(150);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/dashboard-feedback.png");
            renders.Add("artifacts/screenshots/dashboard-feedback.png");
        }
        vm.Page = "Planner";
        vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Elara");
        await Task.Delay(200);
        runtime.SetPreviewSnapshot(originalElara with
        {
            Revision = 14,
            Feedback = [new(1, 14, "Kill", 123, DateTimeOffset.UtcNow)]
        });
        await Task.Delay(150);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/journey-kill-feedback.png");
            renders.Add("artifacts/screenshots/journey-kill-feedback.png");
        }
        vm.SelectedCharacter = vm.Characters.Single(c => c.Name == "Agent");
        vm.Page = "Dashboard";
        window.Width = 900;
        window.Height = 560;
        vm.DashboardListView = true;
        vm.DashboardSearch = "no matching character";
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/dashboard-empty.png");
            renders.Add("artifacts/screenshots/dashboard-empty.png");
        }
        vm.DashboardSearch = "";
        await Task.Delay(200);
        var originalAgent = runtime.Snapshot("Agent");
        runtime.SetPreviewSnapshot(originalAgent with
        {
            Revision = originalAgent.Revision + 1,
            Feedback = [new(originalAgent.Generation, originalAgent.Revision + 1, "Experience", 12345, DateTimeOffset.UtcNow)]
        });
        await Task.Delay(150);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
        {
            bitmap.Render(window);
            bitmap.Save("artifacts/screenshots/dashboard-list-feedback.png");
            renders.Add("artifacts/screenshots/dashboard-list-feedback.png");
        }
        runtime.SetPreviewSnapshot(originalAgent);
        vm.DashboardListView = false;
        window.Width = 1450;
        window.Height = 930;
        runtime.SetPreviewRunning("Agent", false);
        runtime.SetPreviewQueue(originalQueue);
        runtime.SetPreviewQueue(new()
        {
            Character = "Elara"
        });
        runtime.SetPreviewSnapshot(originalElara);
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
