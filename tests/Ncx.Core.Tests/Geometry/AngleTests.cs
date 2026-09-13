using Ncx.Core.Geometry;

namespace Ncx.Core.Tests.Geometry;

/// <summary>
/// Angles are degrees (language 4.12), turned in the direction of the verb (language 4.3, virtual machine 3.2), and a
/// sweep is full turns plus a rest (D84).
/// </summary>
public sealed class AngleTests
{
    [Fact]
    public void ToRadians_HalfTurn_IsPi()
    {
        Assert.Equal(Math.PI, Angle.ToRadians(180), GeometryAssert.Precision);
    }

    [Fact]
    public void ToDegrees_Pi_IsHalfTurn()
    {
        Assert.Equal(180, Angle.ToDegrees(Math.PI), GeometryAssert.Precision);
    }

    // The direction of a vector in plane coordinates, from the first plane axis toward the second.
    [Theory]
    [InlineData(5, 0, 0)]
    [InlineData(0, 5, 90)]
    [InlineData(-5, 0, 180)]
    [InlineData(0, -5, -90)]
    public void Of_PlaneVector_IsMeasuredFromTheFirstPlaneAxisTowardTheSecond(double first, double second,
        double expected)
    {
        Assert.Equal(expected, Angle.Of(new Vec3(first, second, 7)), GeometryAssert.Precision);
    }

    // CCW turns from the first plane axis toward the second (language 4.3, virtual machine 3.2).
    [Fact]
    public void Turn_FirstToSecondPlaneAxis_IsAQuarterCounterclockwiseAndThreeQuartersClockwise()
    {
        Assert.Equal(90, Angle.Turn(0, 90, ArcDirection.Counterclockwise), GeometryAssert.Precision);
        Assert.Equal(270, Angle.Turn(0, 90, ArcDirection.Clockwise), GeometryAssert.Precision);
    }

    [Fact]
    public void Turn_AcrossTheHalfTurn_StaysWithinOneTurn()
    {
        Assert.Equal(20, Angle.Turn(170, -170, ArcDirection.Counterclockwise), GeometryAssert.Precision);
        Assert.Equal(20, Angle.Turn(-170, 170, ArcDirection.Clockwise), GeometryAssert.Precision);
    }

    [Fact]
    public void Turn_SameDirection_IsZero()
    {
        Assert.Equal(0, Angle.Turn(45, 45, ArcDirection.Counterclockwise));
        Assert.Equal(0, Angle.Turn(45, 45, ArcDirection.Clockwise));
    }

    // ANGLE=737.956 is two full turns and a rest of 17.956 degrees (language 6, D84).
    [Fact]
    public void FullTurnsAndRest_Sweep737956_AreTwoTurnsAndTheRest()
    {
        Assert.Equal(2, Angle.FullTurns(737.956));
        Assert.Equal(17.956, Angle.Rest(737.956), GeometryAssert.Precision);
    }

    [Fact]
    public void FullTurnsAndRest_ExactlyTwoTurns_HaveNoRest()
    {
        Assert.Equal(2, Angle.FullTurns(720));
        Assert.Equal(0, Angle.Rest(720));
    }

    [Fact]
    public void FullTurnsAndRest_LessThanOneTurn_AreNoTurnAndTheSweep()
    {
        Assert.Equal(0, Angle.FullTurns(90));
        Assert.Equal(90, Angle.Rest(90));
    }
}
