using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel;
using System.Reflection.PortableExecutable;

namespace DAQuester.DarkAges.Launcher;

public partial class ClientLauncher
{
    public const string WindowingFile = "DAWindowerX.dll";
    public static string WindowingPath => Path.Combine(AppContext.BaseDirectory, WindowingFile);

    public static void ValidateWindowing(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException("DAWindower X is missing. Restore the application files before launching clients.", file);
        try
        {
            using var stream = File.OpenRead(file);
            using var image = new PEReader(stream);
            if (image.PEHeaders.CoffHeader.Machine != Machine.I386 || image.PEHeaders.PEHeader?.Magic != PEMagic.PE32 || image.PEHeaders.CorHeader is not null ||
                !image.PEHeaders.CoffHeader.Characteristics.HasFlag(Characteristics.Dll))
                throw new BadImageFormatException();
        }
        catch (BadImageFormatException error)
        {
            throw new InvalidOperationException("DAWindower X must be the packaged native x86 DLL. Restore the application files.", error);
        }
    }

    internal static int Start(ISuspendedClient client, int port, string windowing, CancellationToken cancellation = default, Action<int>? beforeResume = null)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (port < 1024 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            var pointer = client.Allocate(Encoding.ASCII.GetBytes("localhost\0"));
            foreach (int address in new[] { 0x433392, 0x565628 })
                client.Patch(address, BitConverter.GetBytes(unchecked((uint)pointer.ToInt64())));
            client.Patch(0x4333E3, [0xBA, (byte)port, (byte)(port >> 8), 0, 0]);
            // Preserve whole PUSH instructions, starting at the opcode at C2.
            client.Patch(0x4333C2, [0x6A, 1, 0x6A, 0, 0x6A, 0, 0x6A, 127]);
            // Keep the existing supported-client multi-instance bypass.
            client.Patch(0x57A7CE, [0x31, 0xC0, 0x90, 0x90, 0x90, 0x90]);
            client.Patch(0x42E61F, [0x83, 0xFA, 0, 0x90, 0x90, 0x90]);
            ApplyAgreementBypass(client.Patch);
            if (client.LoadWindowing(windowing, cancellation) == 0)
                throw new InvalidOperationException("DAWindower X could not load. The new client was not started.");
            cancellation.ThrowIfCancellationRequested();
            beforeResume?.Invoke(client.Id);
            cancellation.ThrowIfCancellationRequested();
            client.Resume();
            return client.Id;
        }
        catch
        {
            client.Abort();
            throw;
        }
        finally
        {
            client.Dispose();
        }
    }
    // DAOrganizer's supported-7.41 agreement bypass. Keep both jump destinations.
    public static void ApplyAgreementBypass(Action<int, byte[]> patch)
    {
        patch(0x4B897C, [0xEB, 0x6C]);
        patch(0x4B8ACF, [0xEB, 0x6D]);
    }

    public static readonly string[] SupportedHashes = [// Single instance: stock 7.41.
    "054A5D6ADC56099C6BFD9D2A58675AFF62DC788B63209A3D906492F5B89E96C6", // Multi instance: stock 7.41 with jnz->jmp (75->EB) at 0x57A7D9; the 0x57A7CE patch below has the same effect.
    "B168856A22A5315D2EB0FC942CA1CB6EF38E81035A445BC2CC9710D3D1A5715F", ];
    public static void Validate(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException("DATester client not found.", file);
        if (Array.IndexOf(SupportedHashes, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))) < 0)
            throw new InvalidOperationException("Unsupported client image. Configure the validated DATester 7.41 binary.");
    }

    public static int Launch(string file, int port, string? windowing = null, CancellationToken cancellation = default,
        string? launchPipe = null, Action<int>? beforeResume = null)
    {
        cancellation.ThrowIfCancellationRequested();
        Validate(file);
        if (port < 1024 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        windowing = Path.GetFullPath(windowing ?? WindowingPath);
        ValidateWindowing(windowing);
        if (IntPtr.Size != 4)
            throw new PlatformNotSupportedException("Client startup and DAWindower X require the x86 DAQuester application.");
        cancellation.ThrowIfCancellationRequested();
        return Start(NativeSuspendedClient.Create(Path.GetFullPath(file), launchPipe), port, windowing, cancellation, beforeResume);
    }

    static void Write(IntPtr process, IntPtr address, byte[] bytes)
    {
        if (!VirtualProtectEx(process, address, (nuint)bytes.Length, 0x40, out uint old))
            throw new Win32Exception();
        try
        {
            if (!WriteProcessMemory(process, address, bytes, (nuint)bytes.Length, out var written) || written != (nuint)bytes.Length)
                throw new Win32Exception();
            var actual = new byte[bytes.Length];
            if (!ReadProcessMemory(process, address, actual, (nuint)actual.Length, out var read) || read != (nuint)actual.Length || !actual.SequenceEqual(bytes))
                throw new IOException("Client patch verification failed.");
        }
        finally
        {
            VirtualProtectEx(process, address, (nuint)bytes.Length, old, out _);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public uint X, Y, Width, Height, Columns, Rows, Fill, Flags;
        public ushort Show, ReservedSize;
        public IntPtr ReservedPointer, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInfo
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateProcessW")]
    static extern bool CreateProcess(string file, string? command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string? directory, ref StartupInfo si, out ProcessInfo pi);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr p, IntPtr address, byte[] bytes, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr p, IntPtr address, byte[] bytes, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtectEx(IntPtr p, IntPtr address, nuint size, uint protection, out uint old);
    [DllImport("kernel32.dll")]
    static extern bool FlushInstructionCache(IntPtr p, IntPtr address, nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")]
    static extern bool TerminateProcess(IntPtr handle, uint code);
}
