using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.App.Views;

namespace DAQuester.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var runtime = new DesktopRuntime(Program.IsValidation)
            {
                Dispatch = action => Dispatcher.UIThread.Post(action),
                // At most twenty display updates per second leave time for input and the
                // independent sprite animation timer during busy quest queues.
                DispatchPresentation = action => Dispatcher.UIThread.Post(
                    () => DispatcherTimer.RunOnce(action, TimeSpan.FromMilliseconds(50), DispatcherPriority.Background),
                    DispatcherPriority.Background)
            };
            var vm = new MainWindowViewModel(runtime);
            var window = new MainWindow(vm);
            desktop.MainWindow = window;
            runtime.ChooseQuestAsync = async (character, quest, job, error) =>
            {
                var result = await QuestChoiceDialog.ShowChoiceAsync(window, character, quest, job, error, DAQuester.Core.SickChildEvidence.For(quest.Id) is not null ? runtime.SickChildChoice(character, quest.Id) : null, runtime.CharacterClass(character));
                if (result?.OpenCharacters == true)
                    vm.Page = "Characters";
                return result;
            };
            vm.PickClientAsync = () => ClientPickerDialog.ShowAsync(window, vm.ClientPath);
            vm.ConfirmPaulinQuestAsync = (character, quest, replacing) => PaulinQueueDialog.ShowAsync(window, character, quest, replacing, vm.JourneyGameDirectory);
            vm.RequestClose += window.Close;
            bool closing = false, closed = false;
            window.Closing += async (_, eventArgs) =>
            {
                if (closed)
                    return;
                eventArgs.Cancel = true;
                if (closing)
                    return;
                closing = true;
                try
                {
                    await runtime.ShutdownAsync(vm.InstallRequested);
                    closed = true;
                    window.Close();
                }
                catch (Exception error)
                {
                    vm.ReportError(error);
                    closing = false;
                }
            };
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += async (_, _) =>
            {
                // Scan external saves off-thread. Normal movement reuses display
                // receipts; this scan never delays the scheduler or game actions.
                _ = runtime.RefreshPresentationAsync();
                await runtime.TickSchedulerAsync();
                await runtime.TickDanaanContinuationsAsync();
            };
            window.Closed += (_, _) => timer.Stop();
            window.Opened += async (_, _) =>
            {
                if (Program.IsValidation)
                {
                    Dispatcher.UIThread.Post(async () => await Validation.ScreenshotCapture.RunAsync(window, vm));
                    return;
                }

                timer.Start();
                try
                {
                    await runtime.StartAsync();
                }
                catch (Exception error)
                {
                    vm.ReportError(error);
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
