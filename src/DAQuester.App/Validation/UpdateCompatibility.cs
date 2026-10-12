using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace DAQuester.App.Validation;
// Read a local candidate with the updater shipped in the previous package. Never applies an update.
internal static class UpdateCompatibility
{
    public static async Task<int> RunAsync(string[] args)
    {
        Directory.CreateDirectory("artifacts");
        try
        {
            string Argument(string name) => args.ElementAtOrDefault(Array.IndexOf(args, name) + 1) is { } value && Array.IndexOf(args, name) >= 0 ? Path.GetFullPath(value) : throw new ArgumentException("Missing " + name);
            string feed = Argument("--feed"), previous = Argument("--previous-package");
            var old = VelopackAsset.FromNupkgNoChecksum(previous);
            if (old.PackageId != "BuildWithRaymond.DAQuester")
                throw new InvalidDataException("The previous package uses a different application identity.");
            using var archive = ZipFile.OpenRead(previous);
            var updater = archive.Entries.Single(e => e.FullName.Replace('\\', '/').EndsWith("/Velopack.dll", StringComparison.OrdinalIgnoreCase));
            using var oldStream = updater.Open();
            var oldHash = Convert.ToHexString(await SHA256.HashDataAsync(oldStream));
            using var currentStream = File.OpenRead(typeof(UpdateManager).Assembly.Location);
            var currentHash = Convert.ToHexString(await SHA256.HashDataAsync(currentStream));
            if (oldHash != currentHash)
                throw new InvalidDataException("The previous updater differs. Run a compatibility probe using that updater before release.");
            string probe = Path.GetFullPath(Path.Combine("artifacts", "update-compatibility-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(probe);
            var locator = new TestVelopackLocator(old.PackageId, old.Version.ToString(), probe, probe, probe, null, "win", localPackage: old);
            var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(feed)), locator: locator);
            var update = await manager.CheckForUpdatesAsync() ?? throw new InvalidDataException("The previous version did not discover the candidate.");
            if (update.TargetFullRelease.Version.ToString() != UpdateVersion.Current)
                throw new InvalidDataException("The previous version discovered an unexpected candidate.");
            if (update.TargetFullRelease.PackageId != old.PackageId)
                throw new InvalidDataException("Application identity changed in the update feed.");
            // This copies a local package into a unique synthetic cache and verifies the updater's checksums.
            await manager.DownloadUpdatesAsync(update);
            var candidate = Path.Combine(probe, update.TargetFullRelease.FileName);
            if (!File.Exists(candidate) || new FileInfo(candidate).Length != update.TargetFullRelease.Size)
                throw new InvalidDataException("The previous updater could not prepare the local candidate.");
            File.WriteAllText("artifacts/update-compatibility.json", JsonSerializer.Serialize(new
            {
                previous = old.Version.ToString(),
                candidate = UpdateVersion.Current,
                packageId = old.PackageId,
                updaterIdentical = true,
                localDownloadVerified = true,
                applied = false,
                isolated = true
            }, AppSettings.Json));
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText("artifacts/update-compatibility.json", JsonSerializer.Serialize(new
            {
                error = error.ToString(),
                applied = false,
                isolated = true
            }, AppSettings.Json));
            return 1;
        }
    }
}
