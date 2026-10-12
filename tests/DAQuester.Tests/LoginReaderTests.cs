using System.Buffers.Binary;
using System.Text;
using DAQuester.DarkAges.Launcher;
using Xunit;

namespace DAQuester.Tests;

public class LoginReaderTests
{
    [Fact]
    public void ReadsLoginFocusAndExactFieldsWithoutReturningCredentials()
    {
        var memory = Fixture();
        var state = NativeLoginForm.ReadState(memory.Read, "Example", "12345678");
        Assert.True(state.Visible);
        Assert.False(state.Blocked);
        Assert.Equal(3, state.Focus);
        Assert.Equal(7, state.NameLength);
        Assert.Equal(8, state.PasswordLength);
        Assert.True(state.NameMatches);
        Assert.True(state.PasswordMatches);
        Assert.DoesNotContain("12345678", state.ToString());
        Assert.False(NativeLoginForm.ReadState(memory.Read, "Example", "different").PasswordMatches);
    }

    [Fact]
    public void DetectsInputBlockAndIgnoresHiddenOrUnregisteredDialogs()
    {
        var memory = Fixture();
        memory.U32(0x6DA3A8, 0x200000);
        Assert.True(NativeLoginForm.ReadState(memory.Read, "", "").Blocked);
        memory.U32(0x120130, 0);
        Assert.False(NativeLoginForm.ReadState(memory.Read, "", "").Visible);
        memory.U32(0x120130, 1);
        memory.U32(0x120188, 0);
        Assert.False(NativeLoginForm.ReadState(memory.Read, "", "").Visible);
    }

    [Fact]
    public void RejectsChangedEventTreeAndOversizedText()
    {
        var memory = Fixture();
        var reads = 0;
        byte[] Unstable(uint address, int count)
        {
            if (address == 0x100068 && ++reads == 2)
                return BitConverter.GetBytes(2u);
            return memory.Read(address, count);
        }

        Assert.False(NativeLoginForm.ReadState(Unstable, "", "").Visible);
        memory.U32(0x182014, 100000);
        Assert.Throws<InvalidDataException>(() => NativeLoginForm.ReadState(memory.Read, "", ""));
    }

    private static Memory Fixture()
    {
        var m = new Memory();
        m.U32(0x73D944, 0x100000);
        m.U32(0x100064, 0x110000);
        m.U32(0x100068, 1);
        m.U32(0x110000, 0x120000);
        m.U32(0x120000, 0x67896C);
        m.U32(0x120130, 1);
        m.U32(0x120188, 2);
        m.U32(0x120594, 0x130000);
        m.U32(0x1205AC, 3);
        m.U32(0x13000C, 4);
        m.U32(0x130014, 4);
        m.U32(0x130018, 0x140000);
        for (uint i = 2; i <= 3; i++)
        {
            var control = 0x150000 + i * 0x1000;
            var canvas = 0x160000 + i * 0x1000;
            var list = 0x180000 + i * 0x1000;
            var buffer = 0x190000 + i * 0x1000;
            var text = Encoding.ASCII.GetBytes(i == 2 ? "Example" : "12345678");
            m.U32(0x140000 + i * 4, control);
            m.U32(control + 0x19C, canvas);
            m.U32(canvas + 0x1BC, list);
            m.U32(list + 0x0C, 1);
            m.U32(list + 0x14, (uint)text.Length);
            m.U32(list + 0x18, buffer);
            m.Write(buffer, text);
        }

        return m;
    }

    private sealed class Memory
    {
        private readonly Dictionary<uint, byte> _bytes = [];
        public void U32(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
        public void Write(uint address, byte[] value)
        {
            for (uint i = 0; i < value.Length; i++)
                _bytes[address + i] = value[i];
        }

        public byte[] Read(uint address, int count) => Enumerable.Range(0, count).Select(i => _bytes.GetValueOrDefault(address + (uint)i)).ToArray();
    }
}
