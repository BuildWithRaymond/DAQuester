using System.Buffers.Binary;
using DAQuester.DarkAges.Launcher;
using Xunit;

namespace DAQuester.Tests;

public class WindowedLauncherTests
{
    sealed class SuspendedClient : ISuspendedClient
    {
        public int Id => 42;
        public List<string> Calls { get; } = [];
        public Dictionary<int, byte[]> Patches { get; } = [];
        public byte[]? Host;
        public string? Library;
        public string? FailAt;
        public uint Module = 0x50000000;
        public Action? Loaded;
        public nint Allocate(byte[] bytes) { Calls.Add("allocate"); Host = bytes; return 0x12340000; }
        public void Patch(int address, byte[] bytes)
        {
            Calls.Add("patch");
            if (FailAt == "patch") throw new IOException("Synthetic patch failure");
            Patches.Add(address, bytes);
        }
        public uint LoadWindowing(string library, CancellationToken cancellation = default)
        {
            Calls.Add("load"); Library = library;
            if (FailAt == "load") throw new TimeoutException("Synthetic loader timeout");
            Loaded?.Invoke();
            return Module;
        }
        public void Resume()
        {
            Calls.Add("resume");
            if (FailAt == "resume") throw new IOException("Synthetic resume failure");
        }
        public void Abort() => Calls.Add("abort");
        public void Dispose() => Calls.Add("dispose");
    }

    [Fact]
    public void Windowed_start_preserves_proxy_patches_and_loads_X_before_resume()
    {
        var client = new SuspendedClient();
        string library = Path.GetFullPath(Path.Combine("synthetic ü folder", "DAWindowerX.dll"));
        Assert.Equal(42, ClientLauncher.Start(client, 2910, library));
        Assert.Equal(library, client.Library);
        Assert.Equal("localhost\0", System.Text.Encoding.ASCII.GetString(client.Host!));
        Assert.Equal(new byte[] { 0xBA, 0x5E, 0x0B, 0, 0 }, client.Patches[0x4333E3]);
        Assert.Equal(BitConverter.GetBytes(0x12340000u), client.Patches[0x433392]);
        Assert.Equal(BitConverter.GetBytes(0x12340000u), client.Patches[0x565628]);
        Assert.Equal(new byte[] { 0x6A, 1, 0x6A, 0, 0x6A, 0, 0x6A, 127 }, client.Patches[0x4333C2]);
        Assert.Equal(new byte[] { 0x31, 0xC0, 0x90, 0x90, 0x90, 0x90 }, client.Patches[0x57A7CE]);
        Assert.Equal(new byte[] { 0xEB, 0x6C }, client.Patches[0x4B897C]);
        Assert.Equal(new byte[] { 0xEB, 0x6D }, client.Patches[0x4B8ACF]);
        Assert.True(client.Calls.LastIndexOf("patch") < client.Calls.IndexOf("load"));
        Assert.Equal(new[] { "load", "resume", "dispose" }, client.Calls.TakeLast(3));
        Assert.DoesNotContain("abort", client.Calls);
    }

    [Theory]
    [InlineData("patch")]
    [InlineData("load")]
    [InlineData("resume")]
    public void Failed_owned_start_aborts_before_disposing_its_suspended_resources(string failAt)
    {
        var client = new SuspendedClient { FailAt = failAt };
        Assert.ThrowsAny<Exception>(() => ClientLauncher.Start(client, 2910, "DAWindowerX.dll"));
        Assert.Equal(new[] { "abort", "dispose" }, client.Calls.TakeLast(2));
        if (failAt != "resume") Assert.DoesNotContain("resume", client.Calls);
        Assert.Single(client.Calls, c => c == "abort");
    }

    [Fact]
    public void Null_loader_result_never_resumes_the_client()
    {
        var client = new SuspendedClient { Module = 0 };
        Assert.Throws<InvalidOperationException>(() => ClientLauncher.Start(client, 2910, "DAWindowerX.dll"));
        Assert.DoesNotContain("resume", client.Calls);
        Assert.Equal(new[] { "abort", "dispose" }, client.Calls.TakeLast(2));
    }

    [Fact]
    public void Stop_during_library_loading_aborts_before_main_thread_resume()
    {
        using var stop = new CancellationTokenSource();
        var client = new SuspendedClient { Loaded = stop.Cancel };
        Assert.ThrowsAny<OperationCanceledException>(() => ClientLauncher.Start(client, 2910, "DAWindowerX.dll", stop.Token));
        Assert.DoesNotContain("resume", client.Calls);
        Assert.Equal(new[] { "abort", "dispose" }, client.Calls.TakeLast(2));
    }

    [Fact]
    public void Ownership_is_registered_after_loading_but_before_primary_thread_resume()
    {
        var client = new SuspendedClient();
        int registered = 0;
        ClientLauncher.Start(client, 2910, "DAWindowerX.dll", beforeResume: pid =>
        {
            Assert.Contains("load", client.Calls);
            Assert.DoesNotContain("resume", client.Calls);
            registered = pid;
        });
        Assert.Equal(client.Id, registered);
    }

    static string Image(bool x64 = false, bool dll = true)
    {
        // A minimal synthetic PE header: no runnable code or game content.
        var bytes = new byte[512];
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        U16(0, 0x5A4D); U32(0x3C, 0x80); U32(0x80, 0x4550);
        U16(0x84, x64 ? (ushort)0x8664 : (ushort)0x14C);
        U16(0x94, 224); U16(0x96, dll ? (ushort)0x2102 : (ushort)0x102);
        U16(0x98, 0x10B); U32(0xB4, 0x400000); U32(0xB8, 0x1000); U32(0xBC, 0x200);
        U32(0xD0, 0x1000); U32(0xD4, 0x200); U32(0xF4, 16);
        var path = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N"), "DAWindowerX.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void X86_DLL_header_is_validated_without_loading_any_library() => ClientLauncher.ValidateWindowing(Image());

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Wrong_architecture_or_executable_is_rejected_before_process_creation(bool x64, bool dll) =>
        Assert.Throws<InvalidOperationException>(() => ClientLauncher.ValidateWindowing(Image(x64, dll)));

    [Fact]
    public void Missing_windowing_is_rejected_before_process_creation() =>
        Assert.Throws<FileNotFoundException>(() => ClientLauncher.ValidateWindowing(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll")));
}
