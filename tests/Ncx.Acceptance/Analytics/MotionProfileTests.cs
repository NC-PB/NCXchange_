using Ncx.Analytics.Runtime;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The trapezoidal velocity profile of one motion (virtual machine 8, D64: deceleration equal to acceleration), in mm,
/// mm/s and mm/s^2, the times computed by hand.
/// </summary>
public sealed class MotionProfileTests
{
    // 100 mm at 100 mm/s with 1000 mm/s^2 from rest to rest: 0.1 s and 5 mm to reach the speed, 0.1 s and 5 mm to stop,
    // 90 mm at 100 mm/s in 0.9 s; 1.1 s in all.
    [Fact]
    public void TrapezoidalProfile_FromRestToRest_IsAccelerationTravelAndDeceleration()
    {
        Assert.Equal(1.1, MotionProfile.Seconds(100, 100, 0, 0, 1000), 9);
    }

    // 4 mm are too short to reach 100 mm/s (10 mm to speed up and slow down): the motion reaches sqrt(1000 * 4) mm/s at
    // half way and brakes at once, 2 * sqrt(4000) / 1000 s.
    [Fact]
    public void TrapezoidalProfile_TooShortForTheSpeed_IsATriangle()
    {
        Assert.Equal(2 * Math.Sqrt(4000) / 1000, MotionProfile.Seconds(4, 100, 0, 0, 1000), 9);
    }

    // Entering and leaving at 50 mm/s: 0.05 s and 3.75 mm to reach 100 mm/s, the same to slow down, 92.5 mm at
    // 100 mm/s; 1.025 s.
    [Fact]
    public void TrapezoidalProfile_EntryAndExitAtTheCornerSpeed_AcceleratesFromThemOnly()
    {
        Assert.Equal(1.025, MotionProfile.Seconds(100, 100, 50, 50, 1000), 9);
    }

    // A machine file without acceleration: the motion travels at its speed from start to end.
    [Fact]
    public void TrapezoidalProfile_WithoutAcceleration_IsLengthOverSpeed()
    {
        Assert.Equal(1.0, MotionProfile.Seconds(100, 100, 0, 0, null), 9);
    }

    // 1 mm cannot brake from 100 mm/s to rest at 1000 mm/s^2 (that takes 5 mm): the speed falls evenly over the length,
    // 2 * 1 / (100 + 0) s.
    [Fact]
    public void TrapezoidalProfile_TooShortToBrakeFromEntryToExit_ChangesItsSpeedEvenly()
    {
        Assert.Equal(0.02, MotionProfile.Seconds(1, 100, 100, 0, 1000), 9);
    }

    // A motion that moves nothing takes no time of its own; the block time of the control is added by the estimate.
    [Fact]
    public void TrapezoidalProfile_ZeroLength_TakesNoTime()
    {
        Assert.Equal(0, MotionProfile.Seconds(0, 100, 0, 0, 1000), 9);
    }
}
