using System.Text;

namespace Arbiter.Net.Security;

public static class KeyTableGenerator
{
    public static void GenerateKeyTable(string name, Span<byte> outputBuffer, Encoding? encoding = null)
    {
        encoding ??= Encoding.ASCII;

        var table = HashGenerator.CalcMd5Hash(name, encoding);
        table = HashGenerator.CalcMd5Hash(table, encoding);

        var tableBuilder = new StringBuilder(1024);
        tableBuilder.Append(table);

        for (var i = 0; i < 31; i++)
        {
            var hash = HashGenerator.CalcMd5Hash(tableBuilder.ToString());
            tableBuilder.Append(hash);
        }

        encoding.GetBytes(tableBuilder.ToString(), outputBuffer);
    }
}