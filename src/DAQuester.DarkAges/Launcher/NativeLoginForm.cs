// Adapted from DAOrganizer (MIT), pinned DATester 7.41 read-only login observation.
using System.Buffers.Binary;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;
using DAQuester.Core;

namespace DAQuester.DarkAges.Launcher;
// Supported 7.41 only. Read-only observation; the game still edits and submits its own form.
public sealed class NativeLoginForm(int pid) : ILoginForm, IDisposable
{
    private readonly LoginWindowInput _input = new(pid);
    private readonly ReadOnlyProcessMemory _memory = new ReadOnlyProcessMemory(pid);
    public LoginFormState Read(string name, string ticket)
    {
        _input.Validate();
        return ReadState(ReadBytes, name, ticket);
    }

    private byte[] ReadBytes(uint address, int count)
    {
        if (address < 0x10000 || count < 0 || count > 65536)
            throw new InvalidDataException("Login dialog memory is unavailable.");
        var bytes = new byte[count];
        _memory.Position = address;
        _memory.ReadExactly(bytes);
        return bytes;
    }

    public static LoginFormState ReadState(Func<uint, int, byte[]> read, string name, string ticket)
    {
        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(read(address, 4));
        bool Byte(uint address) => read(address, 1)[0] != 0;
        var waiting = new LoginFormState(false, false, true, -1, 0, 0, false, false);
        var dispatcher = U32(0x73D944);
        if (dispatcher == 0)
            return waiting;
        var entries = U32(dispatcher + 0x64);
        var count = U32(dispatcher + 0x68);
        if (entries == 0 || count > 4096)
            return waiting;
        var blocked = U32(0x6DA3A8) != 0;
        var menu = false;
        uint dialog = 0;
        for (uint i = 0; i < count; i++)
        {
            var pane = U32(entries + i * 12);
            if (pane == 0)
                continue;
            var type = U32(pane);
            if (type is not (0x67896C or 0x6788EC) || !Byte(pane + 0x130) || (read(pane + 0x188, 1)[0] & 2) == 0)
                continue;
            if (type == 0x67896C)
                dialog = pane;
            else
                menu = U32(pane + 0x500) == 0;
        }

        if (dialog == 0)
            return waiting with
            {
                MenuReady = menu,
                Blocked = blocked
            };
        var controls = U32(dialog + 0x594);
        if (controls == 0 || U32(controls + 0x14) != 4 || U32(controls + 0x0C) != 4)
            return waiting;
        var array = U32(controls + 0x18);
        if (array == 0)
            return waiting;
        (int Length, bool Matches) Field(uint index, string expected)
        {
            var control = U32(array + index * 4);
            if (control == 0)
                throw new InvalidDataException("Login control missing.");
            var canvas = U32(control + 0x19C);
            if (canvas == 0)
                throw new InvalidDataException("Login text canvas missing.");
            var list = U32(canvas + 0x1BC);
            if (list == 0 || U32(list + 0x0C) != 1)
                throw new InvalidDataException("Unsupported login text layout.");
            var length = U32(list + 0x14);
            if (length > 255)
                throw new InvalidDataException("Unexpected login text length.");
            if (length == 0)
                return (0, expected.Length == 0);
            var text = read(U32(list + 0x18), (int)length);
            try
            {
                return ((int)length, text.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(expected)));
            }
            finally
            {
                Array.Clear(text);
            }
        }

        var n = Field(2, name);
        var p = Field(3, ticket);
        var focus = unchecked((int)U32(dialog + 0x5AC));
        // Pane arrays can change during an external read. Discard an unstable snapshot.
        if (U32(0x73D944) != dispatcher || U32(dispatcher + 0x64) != entries || U32(dispatcher + 0x68) != count || U32(dialog) != 0x67896C || !Byte(dialog + 0x130))
            return waiting;
        return new(menu, true, blocked, focus, n.Length, p.Length, n.Matches, p.Matches);
    }

    public void Open() => _input.Click(112, 320);
    public void Tab() => _input.Key(9);
    public void End() => _input.Key(0x23);
    public void Backspace() => _input.Key(8);
    public void Type(char character) => _input.Character(character);
    public void TypePassword(char character) => _input.PasswordDigit(character);
    public void AcceptName() => _input.Key(13);
    public void Submit() => _input.Key(13);
    public Task Pause(CancellationToken token) => Task.Delay(50, token);
    public void Dispose() => _memory.Dispose();
}

internal sealed class ReadOnlyProcessMemory : IDisposable
{
    readonly IntPtr handle;
    public long Position
    {
        get;
        set;
    }

    public ReadOnlyProcessMemory(int pid)
    {
        handle = OpenProcess(0x10, false, pid);
        if (handle == IntPtr.Zero)
            throw new Win32Exception();
    }

    public void ReadExactly(byte[] bytes)
    {
        if (!ReadProcessMemory(handle, new IntPtr(Position), bytes, (nuint)bytes.Length, out var read) || read != (nuint)bytes.Length)
            throw new Win32Exception();
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero)
            CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] bytes, nuint length, out nuint read);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);
}
