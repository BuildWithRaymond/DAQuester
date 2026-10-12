using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class UserDataTests
{
    static (string Install, string Data) Folders()
    {
        string root = Path.Combine(Path.GetTempPath(), "DAQuester-migration-" + Guid.NewGuid());
        return (Path.Combine(root, "app"), Path.Combine(root, "user", "Data"));
    }

    [Fact]
    public void Legacy_settings_journals_and_encrypted_credentials_are_copied_byte_for_byte()
    {
        var f = Folders();
        var legacy = Path.Combine(f.Install, "Data");
        Directory.CreateDirectory(Path.Combine(legacy, "logins"));
        Directory.CreateDirectory(Path.Combine(legacy, "journal"));
        File.WriteAllText(Path.Combine(legacy, "profiles.json"), "{\"AutomaticUpdateChecks\":false}");
        File.WriteAllText(Path.Combine(legacy, "journal", "quest.json"), "saved progress");
        File.WriteAllBytes(Path.Combine(legacy, "logins", "AISLING.bin"), [0, 254, 42, 255]);
        UserDataStorage.Initialize(f.Install, f.Data);
        foreach (var file in Directory.GetFiles(legacy, "*", SearchOption.AllDirectories))
            Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(Path.Combine(f.Data, Path.GetRelativePath(legacy, file))));
        Assert.True(Directory.Exists(legacy));
    }

    [Fact]
    public void Existing_destination_is_never_overwritten_or_merged()
    {
        var f = Folders();
        Directory.CreateDirectory(f.Data);
        Directory.CreateDirectory(Path.Combine(f.Install, "Data"));
        File.WriteAllText(Path.Combine(f.Data, "profiles.json"), "current");
        File.WriteAllText(Path.Combine(f.Install, "Data", "profiles.json"), "old");
        File.WriteAllText(Path.Combine(f.Install, "Data", "secret.bin"), "old");
        UserDataStorage.Initialize(f.Install, f.Data);
        Assert.Equal("current", File.ReadAllText(Path.Combine(f.Data, "profiles.json")));
        Assert.False(File.Exists(Path.Combine(f.Data, "secret.bin")));
    }

    [Fact]
    public void Fresh_install_creates_an_empty_user_folder()
    {
        var f = Folders();
        UserDataStorage.Initialize(f.Install, f.Data);
        Assert.True(Directory.Exists(f.Data));
        Assert.Empty(Directory.GetFileSystemEntries(f.Data));
    }

    [Fact]
    public void A_locked_legacy_file_does_not_leave_a_partial_destination()
    {
        var f = Folders();
        var legacy = Path.Combine(f.Install, "Data");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "profiles.json"), "original");
        using (var locked = new FileStream(Path.Combine(legacy, "profiles.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(() => UserDataStorage.Initialize(f.Install, f.Data));
            Assert.False(Directory.Exists(f.Data));
        }

        UserDataStorage.Initialize(f.Install, f.Data);
        Assert.Equal("original", File.ReadAllText(Path.Combine(f.Data, "profiles.json")));
    }
}
