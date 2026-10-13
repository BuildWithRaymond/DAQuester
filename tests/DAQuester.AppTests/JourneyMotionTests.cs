using DAQuester.App.Controls;
using Xunit;

namespace DAQuester.AppTests;

public class JourneyMotionTests
{
    [Fact]
    public void Reversing_mid_fold_preserves_position_and_reaches_the_latest_target()
    {
        var motion = new JourneyMotion();
        motion.Target(true, 0);
        double middle = motion.Position(160);
        Assert.InRange(middle, .05, .95);
        motion.Target(false, 160);
        Assert.Equal(middle, motion.Position(160), 8);
        Assert.Equal(0, motion.Position(1000));
        Assert.False(motion.Active(1000));
    }

    [Fact]
    public void Reduced_motion_settles_immediately_in_both_directions()
    {
        var motion = new JourneyMotion();
        motion.Target(true, 0, false);
        Assert.Equal(1, motion.Position(0));
        Assert.False(motion.Active(0));
        motion.Target(false, 1, false);
        Assert.Equal(0, motion.Position(1));
    }

    [Fact]
    public void Repeated_target_does_not_restart_the_fold()
    {
        var motion = new JourneyMotion();
        motion.Target(true, 0);
        double middle = motion.Position(200);
        motion.Target(true, 200);
        Assert.Equal(middle, motion.Position(200), 8);
        Assert.Equal(1, motion.Position(600));
    }
}
