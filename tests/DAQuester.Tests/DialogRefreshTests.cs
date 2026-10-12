using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class DialogRefreshTests
{
    static StateTracker Tracker()
    {
        var t = new StateTracker(7);
        t.Identity("Swallow", 42);
        t.Observe(0x15, [1, 244, 5, 5, 0, 0, 0, 0, 0, 0]);
        t.Observe(4, [0, 1, 0, 1, 0, 0, 0, 0]);
        return t;
    }

    static void OpenPopup(StateTracker t)
    {
        var menu = new ServerScreenMenuMessage
        {
            EntityId = 22002,
            EntityType = Arbiter.Net.Types.EntityTypeFlags.Creature,
            Name = "",
            Content = "For a moment, you doubted yourself.",
            MenuType = Arbiter.Net.Types.DialogMenuType.Menu
        };
        var builder = new NetworkPacketBuilder(ServerCommand.ScreenMenu);
        try
        {
            menu.Serialize(ref builder);
            t.Observe((byte)ServerCommand.ScreenMenu, ((ServerPacket)builder.ToPacket()).Data);
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void Same_map_refresh_keeps_the_open_dialog()
    {
        var t = Tracker();
        OpenPopup(t);
        Assert.NotNull(t.Snapshot.Dialog);
        t.Observe(0x15, [1, 244, 5, 5, 0, 0, 0, 0, 0, 0]);
        t.Observe(4, [0, 1, 0, 1, 0, 0, 0, 0]);
        Assert.NotNull(t.Snapshot.Dialog);
        Assert.Equal(500, t.Snapshot.Map);
        Assert.Equal(new DAQuester.Core.Tile(1, 1), t.Snapshot.Position);
    }

    [Fact]
    public void Map_change_still_clears_the_dialog()
    {
        var t = Tracker();
        OpenPopup(t);
        Assert.NotNull(t.Snapshot.Dialog);
        t.Observe(0x15, [1, 245, 5, 5, 0, 0, 0, 0, 0, 0]);
        Assert.Null(t.Snapshot.Dialog);
        Assert.Equal(501, t.Snapshot.Map);
    }
}
