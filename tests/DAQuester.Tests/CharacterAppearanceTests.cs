using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Serialization;
using Arbiter.Net.Types;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;
public class CharacterAppearanceTests
{
    static void Observe(StateTracker tracker, ServerMessage message, ServerCommand command)
    {
        var builder = new NetworkPacketBuilder(command);
        try
        {
            message.Serialize(ref builder);
            tracker.Observe((byte)command, builder.ToPacket().Data);
        }
        finally
        {
            builder.Dispose();
        }
    }

    static ServerDrawHumanObjectsMessage Look(uint id) => new()
    {
        EntityId = id,
        Name = "Preview",
        BodySprite = BodySprite.Male,
        HeadSprite = 5,
        ArmorSprite = 7,
        ArmsSprite = 8,
        WeaponSprite = 9,
        Direction = WorldDirection.Left
    };
    [Fact]
    public void OwnAppearanceSurvivesDecodeWithoutUsingAnotherPlayersOutfit()
    {
        var tracker = new StateTracker(8);
        tracker.Identity("Preview", 42);
        Observe(tracker, Look(42), ServerCommand.DrawHumanObjects);
        Assert.NotNull(tracker.Snapshot.Appearance);
        Assert.Equal(7, tracker.Snapshot.Appearance.Armor);
        var other = Look(99);
        other.ArmorSprite = 99;
        Observe(tracker, other, ServerCommand.DrawHumanObjects);
        Assert.Equal(7, tracker.Snapshot.Appearance.Armor);
        Assert.Equal(3, tracker.Snapshot.Motion!.Direction);
        tracker.Disconnect();
        Assert.Null(tracker.Snapshot.Appearance);
        Assert.Null(tracker.Snapshot.Motion);
        Assert.Null(new StateTracker(9).Snapshot.Appearance);
    }

    [Fact]
    public void NewIdentityCannotInheritThePreviousCharactersAppearance()
    {
        var tracker = new StateTracker(8);
        tracker.Identity("Preview", 42);
        Observe(tracker, Look(42), ServerCommand.DrawHumanObjects);
        tracker.Identity("Other", 43);
        Assert.Null(tracker.Snapshot.Appearance);
        Assert.Null(tracker.Snapshot.Motion);
    }

    [Fact]
    public void ObservedWalkingRetainsFacingWithoutInventingPositionEvidence()
    {
        var tracker = new StateTracker(8);
        tracker.Identity("Preview", 42);
        Observe(tracker, new ServerMoveMessage { PreviousX = 5, PreviousY = 5, Direction = WorldDirection.Right }, ServerCommand.Move);
        Assert.Equal(1, tracker.Snapshot.Motion!.Direction);
        Assert.True(tracker.Snapshot.Motion.Walking);
        Assert.Equal(new DAQuester.Core.Tile(6, 5), tracker.Snapshot.Position);
        long movementRevision = tracker.LastMovementRevision;
        Observe(tracker, new ServerChangeDirectionMessage { EntityId = 42, Direction = WorldDirection.Down }, ServerCommand.ChangeDirection);
        Assert.Equal(movementRevision, tracker.LastMovementRevision);
        Assert.Equal(new DAQuester.Core.Tile(6, 5), tracker.Snapshot.Position);
        Assert.False(tracker.Snapshot.Motion!.Walking);
    }

    [Fact]
    public void TurnsAndActionsAffectOnlyOwnCharacter()
    {
        var tracker = new StateTracker(8);
        tracker.Identity("Preview", 42);
        Observe(tracker, Look(42), ServerCommand.DrawHumanObjects);
        Observe(tracker, new ServerChangeDirectionMessage { EntityId = 99, Direction = WorldDirection.Up }, ServerCommand.ChangeDirection);
        Assert.Equal(3, tracker.Snapshot.Motion!.Direction);
        Observe(tracker, new ServerChangeDirectionMessage { EntityId = 42, Direction = WorldDirection.Down }, ServerCommand.ChangeDirection);
        Assert.Equal(2, tracker.Snapshot.Motion!.Direction);
        Observe(tracker, new ServerMotionMessage { EntityId = 42, Animation = BodyAnimation.Wave, Duration = 80 }, ServerCommand.Motion);
        Assert.Equal(22, tracker.Snapshot.Motion!.Animation);
        Assert.False(tracker.Snapshot.Motion.Walking);
    }
}
