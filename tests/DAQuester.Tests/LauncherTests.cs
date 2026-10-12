using Xunit;
using DAQuester.DarkAges.Launcher;

namespace DAQuester.Tests;

public class LauncherTests
{
    [Fact]
    public void Unknown_client_is_rejected_without_launch()
    {
        var p = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString() + ".exe");
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllBytes(p, [0, 1, 2]);
        Assert.Throws<InvalidOperationException>(() => ClientLauncher.Validate(p));
    }

    [Fact]
    public void Agreement_bypass_skips_both_display_branches_and_preserves_surrounding_bytes()
    {
        const int start = 0x4B8900;
        var memory = Enumerable.Repeat((byte)0xCC, 0x300).ToArray();
        memory[0x4B897C - start] = 0x75;
        memory[0x4B897D - start] = 0x6C;
        memory[0x4B8ACF - start] = 0x75;
        memory[0x4B8AD0 - start] = 0x6D;
        var expected = (byte[])memory.Clone();
        expected[0x4B897C - start] = 0xEB;
        expected[0x4B8ACF - start] = 0xEB;
        var writes = new List<int>();
        ClientLauncher.ApplyAgreementBypass((address, bytes) =>
        {
            writes.Add(address);
            Array.Copy(bytes, 0, memory, address - start, bytes.Length);
        });
        Assert.Equal(new[] { 0x4B897C, 0x4B8ACF }, writes);
        Assert.Equal(expected, memory);
    }
}
