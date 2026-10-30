using DAQuester.App.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace DAQuester.App;

public sealed class AislingExchangePairingStore : IAislingExchangePairingStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DAQuester/AislingExchange/pairing/v1");
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InstallationGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly string directory;
    private readonly SemaphoreSlim installationGate;

    public AislingExchangePairingStore(string dataPath)
    {
        directory = Path.GetFullPath(Path.Combine(dataPath, "aisling-exchange", "pairings"));
        installationGate = InstallationGates.GetOrAdd(directory, _ => new(1, 1));
    }

    public async Task<Guid> GetInstallationIdAsync(CancellationToken cancellation = default)
    {
        await installationGate.WaitAsync(cancellation);
        try
        {
            string file = Path.Combine(directory, "installation.bin");
            var stored = await ReadAsync<Installation>(file, cancellation);
            if (stored is not null)
            {
                if (stored.Id == Guid.Empty) throw Failure();
                return stored.Id;
            }
            Guid id = Guid.NewGuid();
            await WriteAsync(file, new Installation(id), cancellation);
            return id;
        }
        finally { installationGate.Release(); }
    }

    public Task<AislingExchangePairingIntent?> LoadAsync(string character, string serverProfile, CancellationToken cancellation = default)
        => ReadAsync<AislingExchangePairingIntent>(SubjectPath(character, serverProfile), cancellation);

    public Task SaveAsync(AislingExchangePairingIntent intent, CancellationToken cancellation = default)
        => WriteAsync(SubjectPath(intent.Character, intent.ServerProfile), intent, cancellation);

    public Task ClearAsync(string character, string serverProfile, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        try { File.Delete(SubjectPath(character, serverProfile)); }
        catch (DirectoryNotFoundException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { throw Failure(); }
        return Task.CompletedTask;
    }

    private async Task<T?> ReadAsync<T>(string path, CancellationToken cancellation) where T : class
    {
        byte[]? clear = null;
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is < 1 or > 16_384) throw Failure();
            byte[] bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellation);
            clear = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<T>(clear, AislingExchangeClient.Json) ?? throw Failure();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException) { throw Failure(); }
        finally { if (clear is not null) CryptographicOperations.ZeroMemory(clear); }
    }

    private async Task WriteAsync<T>(string path, T value, CancellationToken cancellation)
    {
        byte[]? clear = null;
        string? temporary = null;
        try
        {
            clear = JsonSerializer.SerializeToUtf8Bytes(value, AislingExchangeClient.Json);
            if (clear.Length > 8_192) throw Failure();
            byte[] encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(directory);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await stream.WriteAsync(encrypted, cancellation);
                stream.Flush(true);
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException) { throw Failure(); }
        finally
        {
            if (clear is not null) CryptographicOperations.ZeroMemory(clear);
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    private string SubjectPath(string character, string serverProfile)
    {
        if (string.IsNullOrEmpty(character) || character.Length > 30 || !character.All(char.IsAsciiLetter)
            || string.IsNullOrEmpty(serverProfile) || serverProfile.Length > 128
            || !serverProfile.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            throw Failure();
        string id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(character.ToUpperInvariant() + "\n" + serverProfile)));
        return Path.Combine(directory, id + ".bin");
    }

    private static AislingExchangeException Failure() => new("AislingExchange pairing storage is unavailable.");
    private sealed record Installation(Guid Id);
}
