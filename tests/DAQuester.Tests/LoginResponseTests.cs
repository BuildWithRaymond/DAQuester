using System.Text;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Serialization;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class LoginResponseTests
{
    [Theory]
    [InlineData(0, "", false)]
    [InlineData(15, "Incorrect password.", true)]
    [InlineData(99, "Account is already logged in.", true)]
    [InlineData(0, "You are already logged on.", true)]
    public void Login_response_preserves_server_denial_without_guessing_result_codes(byte code, string message, bool denied)
    {
        var bytes = new byte[] { code, (byte)message.Length }.Concat(Encoding.ASCII.GetBytes(message)).ToArray();
        var response = LoginResponse.Parse(bytes);
        Assert.Equal(message, response.Message);
        Assert.Equal(denied, response.Denied);
    }

    [Fact]
    public void Truncated_response_cannot_prove_success()
    {
        Assert.True(LoginResponse.Parse([0, 5, 65]).Denied);
        Assert.True(LoginResponse.Parse([]).Denied);
    }

    [Theory]
    [InlineData(0, "", false)]
    [InlineData(15, "Incorrect password.", true)]
    [InlineData(0, "Account is already logged in.", true)]
    public void Complete_result_and_message_accept_trailing_protocol_data(byte code, string message, bool denied)
    {
        // The bundled protocol reader consumes the result and String8 message,
        // leaving any following fields for the caller. This is a synthetic packet.
        var payload = new byte[] { code, (byte)message.Length }
            .Concat(Encoding.ASCII.GetBytes(message)).Concat(new byte[] { 0 }).ToArray();
        var protocol = new ServerLoginCheckMessage();
        protocol.Deserialize(new NetworkPacketReader(new ServerPacket(2, payload)));
        var response = LoginResponse.Parse(payload);
        Assert.Equal((byte)protocol.Result, response.Result);
        Assert.Equal(protocol.Message, response.Message);
        Assert.Equal(denied, response.Denied);
    }
}
