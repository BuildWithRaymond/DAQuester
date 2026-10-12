using Xunit;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;

namespace DAQuester.Tests;

public class DialogResponseTests
{
    [Fact]
    public void Closing_pursuit_keeps_step_and_sends_no_choice()
    {
        var d = new Dialog(1, 4, 77, 1, "Callough", "Run along", true, 177, 41, 0, []);
        var packet = DialogPackets.Close(FoundationTests.Ready() with
        {
            Dialog = d
        }, d);
        var m = new ClientPursuitMessage();
        m.Deserialize(new NetworkPacketReader(packet));
        Assert.Equal((ushort)177, m.PursuitId);
        Assert.Equal((ushort)41, m.StepId);
        Assert.Equal(Arbiter.Net.Types.DialogArgsType.None, m.ArgsType);
        Assert.Throws<QuestBlockedException>(() => DialogPackets.Close(FoundationTests.Ready() with { Dialog = d with { Revision = 5 } }, d));
    }

    [Fact]
    public void Next_advances_current_pursuit_without_inventing_option()
    {
        var d = new Dialog(1, 4, 77, 1, "Callough", "Hello", true, 264, 5, 0, [], HasNext: true);
        var packet = DialogPackets.Continue(FoundationTests.Ready() with
        {
            Dialog = d
        }, d);
        var m = new ClientPursuitMessage();
        m.Deserialize(new NetworkPacketReader(packet));
        Assert.Equal((ushort)6, m.StepId);
        Assert.Equal(Arbiter.Net.Types.DialogArgsType.None, m.ArgsType);
    }

    [Fact]
    public void Text_input_preserves_merchant_pursuit_and_rejects_stale_prompt()
    {
        var d = new Dialog(1, 4, 77, 1, "Cassidy", "Amount?", false, 82, 0, 2, [], "How much?");
        var s = FoundationTests.Ready() with
        {
            Dialog = d
        };
        var m = new ClientMerchantMessage();
        m.Deserialize(new NetworkPacketReader(DialogPackets.Continue(s, d, "1001")));
        Assert.Equal((ushort)82, m.PursuitId);
        Assert.Equal("1001", Assert.Single(m.Arguments));
        Assert.Throws<QuestBlockedException>(() => DialogPackets.Continue(s with { Dialog = d with { Revision = 5 } }, d, "1001"));
    }

    [Fact]
    public void Pursuit_response_advances_received_step()
    {
        var d = new Dialog(1, 4, 77, 1, "Callough", "", true, 264, 5, 2, [new("I'll gather them now", 264, 2)]);
        var s = FoundationTests.Ready() with
        {
            Dialog = d
        };
        var packet = DialogPackets.Choice(s, d, "I'll gather them now");
        var m = new ClientPursuitMessage();
        m.Deserialize(new NetworkPacketReader(packet));
        Assert.Equal((ushort)264, m.PursuitId);
        Assert.Equal((ushort)6, m.StepId);
        Assert.Equal((byte)2, m.MenuChoice);
    }

    [Fact]
    public void Merchant_response_uses_live_pursuit_id()
    {
        var d = new Dialog(1, 4, 77, 1, "Callough", "", false, 0, 0, 0, [new("Spare a Stick", 999, 1)]);
        var packet = DialogPackets.Choice(FoundationTests.Ready() with
        {
            Dialog = d
        }, d, "Spare a Stick");
        var m = new ClientMerchantMessage();
        m.Deserialize(new NetworkPacketReader(packet));
        Assert.Equal(77u, m.EntityId);
        Assert.Equal((ushort)999, m.PursuitId);
    }
}
