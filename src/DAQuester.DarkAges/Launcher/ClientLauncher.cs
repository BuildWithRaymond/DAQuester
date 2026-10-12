using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel;

namespace DAQuester.DarkAges.Launcher;

public class ClientLauncher
{
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

    public static int Launch(string file, int port)
    {
        Validate(file);
        if (port < 1024 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        var si = new StartupInfo
        {
            Size = Marshal.SizeOf<StartupInfo>()
        };
        if (!CreateProcess(file, null, IntPtr.Zero, IntPtr.Zero, false, 4, IntPtr.Zero, Path.GetDirectoryName(file), ref si, out var pi))
            throw new Win32Exception();
        try
        {
            var host = Encoding.ASCII.GetBytes("localhost\0");
            var pointer = VirtualAllocEx(pi.Process, IntPtr.Zero, (nuint)host.Length, 0x3000, 4);
            if (pointer == IntPtr.Zero)
                throw new Win32Exception();
            Write(pi.Process, pointer, host);
            foreach (int address in new[]
            {
                0x433392,
                0x565628
            }

            )
                Write(pi.Process, new(address), BitConverter.GetBytes(unchecked((uint)pointer.ToInt64())));
            Write(pi.Process, new(0x4333E3), [0xBA, (byte)port, (byte)(port >> 8), 0, 0]);
            // Installed image has PUSH's opcode at C2 and its first operand at C3.
            // Patch whole instructions starting at C2, correcting the legacy C3 offset.
            Write(pi.Process, new(0x4333C2), [0x6A, 1, 0x6A, 0, 0x6A, 0, 0x6A, 127]);
            // Multi instance: GetLastError -> xor eax,eax, so the Nexon.SingleInstance check never exits.
            Write(pi.Process, new(0x57A7CE), [0x31, 0xC0, 0x90, 0x90, 0x90, 0x90]);
            Write(pi.Process, new(0x42E61F), [0x83, 0xFA, 0, 0x90, 0x90, 0x90]);
            // Only this newly created, hash-validated, still-suspended client is patched.
            ApplyAgreementBypass((address, bytes) => Write(pi.Process, new(address), bytes));
            FlushInstructionCache(pi.Process, IntPtr.Zero, 0);
            if (ResumeThread(pi.Thread) == uint.MaxValue)
                throw new Win32Exception();
            return (int)pi.ProcessId;
        }
        catch
        {
            // Only this freshly created, still-suspended DATester process is owned here.
            TerminateProcess(pi.Process, 1);
            throw;
        }
        finally
        {
            CloseHandle(pi.Thread);
            CloseHandle(pi.Process);
        }
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
    struct ProcessInfo
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
