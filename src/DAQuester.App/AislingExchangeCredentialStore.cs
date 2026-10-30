using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App;

public sealed class AislingExchangeCredentialStore : IAislingExchangeCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DAQuester/AislingExchange/quest-supplies/v1");
    private readonly string directory;
    private readonly TimeProvider time;

    public AislingExchangeCredentialStore(string dataPath, TimeProvider? time = null)
    {
        directory = Path.Combine(dataPath, "aisling-exchange", "credentials");
        this.time = time ?? TimeProvider.System;
    }

    public async Task<AislingExchangeCredential?> LoadAsync(string character, string serverProfile, CancellationToken cancellation = default)
    {
        byte[]? clear = null;
        try
        {
            string path = CredentialPath(character, serverProfile);
            if (!File.Exists(path))
                return null;
            if (new FileInfo(path).Length is < 1 or > 16_384)
                throw StorageFailure();
            byte[] encrypted = await File.ReadAllBytesAsync(path, cancellation);
            clear = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var credential = JsonSerializer.Deserialize<AislingExchangeCredential>(clear, AislingExchangeClient.Json);
            if (!AislingExchangeClient.ValidCredential(credential, character, serverProfile, time.GetUtcNow()))
                return null;
            return credential;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException or JsonException or AislingExchangeException)
        {
            throw StorageFailure();
        }
        finally { if (clear is not null) CryptographicOperations.ZeroMemory(clear); }
    }

    public async Task SaveAsync(AislingExchangeCredential credential, CancellationToken cancellation = default)
    {
        if (credential is null || !AislingExchangeClient.ValidCredential(credential, credential.Character, credential.ServerProfile, time.GetUtcNow()))
            throw StorageFailure();
        byte[]? clear = null;
        string? temporary = null;
        try
        {
            string path = CredentialPath(credential.Character, credential.ServerProfile);
            Directory.CreateDirectory(directory);
            clear = JsonSerializer.SerializeToUtf8Bytes(credential, AislingExchangeClient.Json);
            byte[] encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
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
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException or JsonException or AislingExchangeException)
        {
            throw StorageFailure();
        }
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

    public Task ClearAsync(string character, string serverProfile, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        try { File.Delete(CredentialPath(character, serverProfile)); }
        catch (DirectoryNotFoundException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or AislingExchangeException) { throw StorageFailure(); }
        return Task.CompletedTask;
    }

    private string CredentialPath(string character, string serverProfile)
    {
        if (string.IsNullOrEmpty(character) || character.Length > 30 || !character.All(char.IsAsciiLetter)
            || string.IsNullOrEmpty(serverProfile) || serverProfile.Length > 128
            || !serverProfile.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            throw StorageFailure();
        string subject = character.ToUpperInvariant() + "\n" + serverProfile;
        string id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(subject)));
        return Path.Combine(directory, id + ".bin");
    }

    private static AislingExchangeException StorageFailure() => new("AislingExchange credential storage is unavailable.");
}
