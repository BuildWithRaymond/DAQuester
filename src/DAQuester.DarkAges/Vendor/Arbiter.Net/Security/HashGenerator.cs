using System.Security.Cryptography;
using System.Text;

namespace Arbiter.Net.Security;

public static class HashGenerator
{
    public static string CalcMd5Hash(string input, Encoding? encoding = null)
    {
        encoding ??= Encoding.ASCII;

        Span<byte> hashBuffer = stackalloc byte[16];
        MD5.HashData(encoding.GetBytes(input), hashBuffer);

        return Convert.ToHexStringLower(hashBuffer);
    }
}