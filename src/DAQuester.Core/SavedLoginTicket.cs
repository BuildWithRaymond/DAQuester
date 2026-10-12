using System.Security.Cryptography;
using System.Text;

namespace DAQuester.Core;

public record SavedLoginRewrite(bool Blocked, byte[]? Payload);
public sealed class SavedLoginTicket(string character, Func<string?> password)
{
    public string Character { get; } = character;
    public string Ticket { get; } = RandomNumberGenerator.GetInt32(10000000, 99999999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    readonly DateTimeOffset expires = DateTimeOffset.UtcNow.AddMinutes(2);
    int cancelled, claimed, submitted;
    public bool WasSubmitted => Volatile.Read(ref submitted) != 0;

    public void Cancel() => Interlocked.Exchange(ref cancelled, 1);
    public SavedLoginRewrite Rewrite(byte[] original, DateTimeOffset now)
    {
        try
        {
            if (original.Length < 2)
                return new(true, null);
            int nameLength = original[0], offset = 1 + nameLength;
            if (offset >= original.Length)
                return new(true, null);
            int length = original[offset], suffix = offset + 1 + length;
            if (suffix > original.Length || original.Length - suffix < 8)
                return new(true, null);
            string entered = Encoding.ASCII.GetString(original, offset + 1, length);
            if (entered != Ticket)
                return new(false, original);
            if (Volatile.Read(ref cancelled) != 0 || now > expires || !Encoding.ASCII.GetString(original, 1, nameLength).Equals(Character, StringComparison.OrdinalIgnoreCase) || Interlocked.CompareExchange(ref claimed, 1, 0) != 0)
                return new(true, null);
            string secret = password() ?? throw new InvalidOperationException("Saved password unavailable.");
            if (secret.Length is < 1 or > 255 || secret.Any(c => c > 127) || Volatile.Read(ref cancelled) != 0)
                return new(true, null);
            byte[] bytes = Encoding.ASCII.GetBytes(secret);
            try
            {
                byte[] output = new byte[offset + 1 + bytes.Length + original.Length - suffix];
                original.AsSpan(0, offset).CopyTo(output);
                output[offset] = (byte)bytes.Length;
                bytes.CopyTo(output, offset + 1);
                original.AsSpan(suffix).CopyTo(output.AsSpan(offset + 1 + bytes.Length));
                Volatile.Write(ref submitted, 1);
                return new(false, output);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch
        {
            return new(true, null);
        }
    }
}
