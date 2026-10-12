namespace DAQuester.Core;

public static class UserDataStorage
{
    /// <summary>Migrate only into a missing destination. Never delete or merge the legacy folder.</summary>
    public static void Initialize(string installationDirectory, string userDataDirectory)
    {
        string destination = Path.GetFullPath(userDataDirectory);
        if (Directory.Exists(destination))
            return;
        string legacy = Path.Combine(Path.GetFullPath(installationDirectory), "Data");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string staging = destination + ".migration-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        if (Directory.Exists(legacy))
            Copy(legacy, staging);
        // Failed copies deliberately retain their staging folder for recovery; no partial Data is committed.
        Directory.Move(staging, destination);
    }

    static void Copy(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Legacy data contains a linked folder. Copy it manually after reviewing its location.");
        foreach (string file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Legacy data contains a linked file. Copy it manually after reviewing its location.");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string folder in Directory.GetDirectories(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(folder));
            Directory.CreateDirectory(target);
            Copy(folder, target);
        }
    }
}
