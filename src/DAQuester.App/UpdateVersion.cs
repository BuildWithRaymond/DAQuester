using System.Reflection;

namespace DAQuester.App;

internal static class UpdateVersion
{
    internal static string Current => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
}
