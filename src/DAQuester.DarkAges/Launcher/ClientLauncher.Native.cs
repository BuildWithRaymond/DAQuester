using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DAQuester.DarkAges.Launcher;

public partial class ClientLauncher
{
    internal sealed class NativeSuspendedClient(ProcessInfo process) : ISuspendedClient
    {
        nint libraryArgument, loader;
        bool loaderFinished;
        public int Id => checked((int)process.ProcessId);

        public static NativeSuspendedClient Create(string file, string? launchPipe = null)
        {
            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
            nint environment = 0;
            try
            {
                if (launchPipe is not null)
                {
                    const string prefix = "DAQuester.NewClient.";
                    if (!launchPipe.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(launchPipe[prefix.Length..], "N", out _))
                        throw new ArgumentException("Invalid X launch endpoint.", nameof(launchPipe));
                    var entries = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                        .ToDictionary(e => (string)e.Key, e => (string?)e.Value ?? "", StringComparer.OrdinalIgnoreCase);
                    entries["DAQUESTER_LAUNCH_PIPE"] = launchPipe;
                    string block = string.Join('\0', entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(e => e.Key + "=" + e.Value)) + "\0\0";
                    environment = Marshal.StringToHGlobalUni(block);
                }
                if (!CreateProcess(file, null, 0, 0, false, 4u | (environment != 0 ? 0x400u : 0u), environment,
                    Path.GetDirectoryName(file), ref startup, out var created))
                    throw new Win32Exception();
                return new(created);
            }
            finally { if (environment != 0) Marshal.FreeHGlobal(environment); }
        }

        public nint Allocate(byte[] bytes)
        {
            var pointer = VirtualAllocEx(process.Process, 0, (nuint)bytes.Length, 0x3000, 4);
            if (pointer == 0) throw new Win32Exception();
            Write(process.Process, pointer, bytes);
            return pointer;
        }

        public void Patch(int address, byte[] bytes) => Write(process.Process, new(address), bytes);

        public uint LoadWindowing(string library, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            libraryArgument = Allocate(Encoding.Unicode.GetBytes(library + "\0"));
            // App and hash-validated client are both x86. This is the same
            // Windows loader route used by upstream X, including Unicode paths.
            var load = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
            if (load == 0) throw new Win32Exception();
            loader = CreateRemoteThread(process.Process, 0, 0, load, libraryArgument, 0, 0);
            if (loader == 0) throw new Win32Exception();
            var elapsed = Stopwatch.StartNew();
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                uint wait = WaitForSingleObject(loader, 50);
                if (wait == 0) break;
                if (wait != 258) throw new Win32Exception();
                if (elapsed.Elapsed >= TimeSpan.FromSeconds(15))
                    throw new TimeoutException("DAWindower X loading timed out before client startup.");
            }
            loaderFinished = true;
            if (!GetExitCodeThread(loader, out uint module)) throw new Win32Exception();
            if (module == 0 || module == 259) return 0;
            // A crashed loader thread can also return a nonzero value. Require
            // the actual module and full path before allowing the main thread.
            using var target = Process.GetProcessById(Id);
            return target.Modules.Cast<ProcessModule>().Any(m => unchecked((uint)m.BaseAddress.ToInt64()) == module &&
                Path.GetFullPath(m.FileName).Equals(library, StringComparison.OrdinalIgnoreCase)) ? module : 0;
        }

        public void Resume()
        {
            if (!FlushInstructionCache(process.Process, 0, 0)) throw new Win32Exception();
            if (ResumeThread(process.Thread) == uint.MaxValue) throw new Win32Exception();
        }

        public void Abort()
        {
            // The only handle here came from this launch's CREATE_SUSPENDED.
            TerminateProcess(process.Process, 1);
        }

        public void Dispose()
        {
            // After timeout the remote loader may still read this argument.
            // Abort runs first; process exit reclaims it instead of an early free.
            if (libraryArgument != 0 && (loader == 0 || loaderFinished))
                VirtualFreeEx(process.Process, libraryArgument, 0, 0x8000);
            if (loader != 0) CloseHandle(loader);
            CloseHandle(process.Thread);
            CloseHandle(process.Process);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetModuleHandleW")]
    static extern nint GetModuleHandle(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    static extern nint GetProcAddress(nint module, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern nint CreateRemoteThread(nint process, nint attributes, nuint stackSize, nint start, nint argument, uint flags, nint threadId);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetExitCodeThread(nint thread, out uint result);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualFreeEx(nint process, nint address, nuint size, uint kind);
}
