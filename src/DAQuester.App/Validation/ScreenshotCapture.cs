using System.Collections.Immutable;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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

            File.WriteAllText("artifacts/screenshots.json", JsonSerializer.Serialize(new
            {
                framework = "Avalonia",
                renderer = "native",
                isolated = true,
                synthetic = true,
                failed = 0,
                renders
            }, AppSettings.Json));
            File.WriteAllText("artifacts/screenshots.txt", "Four documentation screenshots captured with isolated sample data. No game clients, listeners, or updates.");
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
            Legends = ["Entered Muisir Village - Deoch 223, Spring"]
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
