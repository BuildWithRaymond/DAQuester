using System.Text.Json;

namespace DAQuester.Core;
// Stores serialize their own writers. This helper commits a complete checkpoint
// without exposing partial JSON or replacing the previous checkpoint on failure.
internal static class AtomicJsonFile
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true
    };
    internal static async Task WriteAsync<T>(string path, T value, string failureMessage, CancellationToken cancellation)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellation);
                stream.Flush(true);
            }

            for (int attempt = 0; ; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    File.Move(temporary, path, true);
                    return;
                }
                // Windows access denied / sharing / lock violations can be transient.
                catch (Exception error) when ((error is IOException or UnauthorizedAccessException) && (error.HResult & 0xFFFF) is 5 or 32 or 33)
                {
                    if (attempt >= 5)
                        throw new IOException(failureMessage, error);
                    await Task.Delay(100 << Math.Min(attempt, 2), cancellation);
                }
            }
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
