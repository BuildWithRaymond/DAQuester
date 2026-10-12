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
                Dispatch = action => Dispatcher.UIThread.Post(action)
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
            timer.Tick += (_, _) => vm.Refresh();
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
