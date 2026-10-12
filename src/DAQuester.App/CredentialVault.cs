using System.Security.Cryptography;
using System.Text;

namespace DAQuester.App;

public static class CredentialVault
{
    public static bool Exists(string character) => File.Exists(Path.Combine(AppSettings.DataPath, "logins", character.ToUpperInvariant() + ".bin"));
    public static void Save(string character, string password)
    {
        string file = Path.Combine(AppSettings.DataPath, "logins", character.ToUpperInvariant() + ".bin");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var bytes = Encoding.UTF8.GetBytes(password);
        try
        {
            File.WriteAllBytes(file, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static string? Load(string character)
    {
        string file = Path.Combine(AppSettings.DataPath, "logins", character.ToUpperInvariant() + ".bin");
        if (!File.Exists(file))
            return null;
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
