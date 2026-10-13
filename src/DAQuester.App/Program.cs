using System.Text.Json;
using Avalonia;

namespace DAQuester.App;

internal static class Program
{
    public static string[] Arguments { get; private set; } = [];
    public static bool IsScreenshots => Arguments.Contains("--screenshots");
    public static bool IsValidation => IsScreenshots;

    [STAThread]
    static int Main(string[] args)
    {
        Arguments = args;
        if (args.Contains("--smoke"))
        {
            Console.Error.WriteLine("The render test suite was removed. Run the xUnit projects; use --screenshots for documentation captures.");
            return 1;
        }

        if (args.Contains("--mcp") || args.Contains("--control"))
            return ControlMain.RunAsync(args).GetAwaiter().GetResult();
        if (args.Contains("--verify-local-update"))
            return Validation.UpdateCompatibility.RunAsync(args).GetAwaiter().GetResult();
        if (IsValidation)
        {
            Directory.CreateDirectory("artifacts");
            AppSettings.UseIsolatedData(Path.Combine(Environment.CurrentDirectory, "artifacts", "screenshot-data-" + Guid.NewGuid().ToString("N")));
            var seed = AppSettings.Load();
            if (seed.Characters.Length != 0)
                throw new InvalidOperationException("New profiles must not invent a default character.");
            seed.ClientPath = Environment.GetEnvironmentVariable("DAQUESTER_SCREENSHOT_CLIENT") ?? "";
            seed.AutomaticUpdateChecks = false;
            seed.Characters = [];
            seed.Save();
        }
        else
            Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        if (args.Contains("--check-updates"))
            return CheckUpdates();
        using var instance = new Mutex(true, IsValidation ? "Local\\DAQuester.Smoke." + Environment.ProcessId : "Local\\DAQuester", out bool ownsInstance);
        if (!ownsInstance)
            return 0;
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            if (IsValidation)
            {
                Directory.CreateDirectory("artifacts");
                File.WriteAllText("artifacts/screenshots.txt", error.ToString());
            }
            else
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "DAQuester-startup-error.txt"), error.ToString());
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
    static int CheckUpdates()
    {
        Directory.CreateDirectory("artifacts");
        try
        {
            var backend = new VelopackUpdates();
            if (!backend.IsInstalled)
                throw new InvalidOperationException("Use the installer or portable package for update checks.");
            var available = backend.CheckAsync().GetAwaiter().GetResult();
            File.WriteAllText("artifacts/update-check.json", JsonSerializer.Serialize(new
            {
                current = UpdateVersion.Current,
                available,
                pending = backend.PendingVersion,
                repository = Core.UpdateCoordinator.RepositoryUrl
            }));
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText("artifacts/update-check.json", JsonSerializer.Serialize(new
            {
                error = error.Message
            }));
            return 1;
        }
    }
}
