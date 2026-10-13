using System.Text.Json;

namespace DAQuester.App;

public class AppSettings
{
    public string ClientPath { get;
        set; } = "C:\\Program Files (x86)\\KRU\\DATester\\Darkages.exe";
    public string RemoteAddress { get;
        set; } = "52.88.55.94";
    public int[] RemotePorts { get;
        set; } = [2610, 2611, 2612];
    public int[] LocalPorts { get;
        set; } = [2910, 2911, 2912];
    public string ServerProfile { get;
        set; } = "datester-7.41";
    public string[] Characters { get;
        set; } = [];
    public bool AutomaticUpdateChecks { get;
        set; } = false;
    public int MovementMilliseconds { get;
        set; } = 400;
    public string[] FastModeCharacters { get;
        set; } = [];
    // Characters whose stat points the hunting engine spends; remembered from the control bridge.
    public string[] SpendStatsCharacters { get; set; } = [];
    // Fast mode is on unless switched off for that character.
    public string[] FastModeOffCharacters { get; set; } = [];

    public bool FastModeEnabled(string character) => !FastModeOffCharacters.Any(name => name.Equals(character, StringComparison.OrdinalIgnoreCase));
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    static string? isolatedData;
    public static string DataPath => isolatedData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DAQuester", "Data");

    internal static void UseIsolatedData(string directory) => isolatedData = Path.GetFullPath(directory);
    public static string Content => Path.Combine(AppContext.BaseDirectory, "content");

    public static AppSettings Load()
    {
        if (isolatedData is null)
            Core.UserDataStorage.Initialize(AppContext.BaseDirectory, DataPath);
        Directory.CreateDirectory(DataPath);
        var file = Path.Combine(DataPath, "profiles.json");
        if (!File.Exists(file))
            File.Copy(Path.Combine(Content, "profiles.json"), file);
        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file), Json) ?? new();
    }

    public void Save() => File.WriteAllText(Path.Combine(DataPath, "profiles.json"), JsonSerializer.Serialize(this, Json));
}
