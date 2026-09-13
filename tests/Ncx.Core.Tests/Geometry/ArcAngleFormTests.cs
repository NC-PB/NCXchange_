using Ncx.Core.Geometry;

namespace Ncx.Core.Tests.Geometry;

/// <summary>
/// The ANGLE form of ARC: the start rotated about CENTER by the sweep in the direction of the verb, more than 360
/// degrees for several turns, a tool-axis word spreading its travel over the whole sweep (virtual machine 3.2, D84).
/// </summary>
public sealed class ArcAngleFormTests
{
    // Language 6, the TopSolid helix CP IPA+737.956 IZ-5.4 DR+ around CC X50 Y50: ARC=CCW IZ=-5.4 CENTER:X=50
    // CENTER:Y=50 ANGLE=737.956, here from 70/50 at Z=0. Two full turns bring the tool back to 70/50, the rest of
    // 17.956 degrees ends it at 69.026/56.166, 5.4 lower.
    [Fact]
    public void ResolveAngleForm_Sweep737956WithIZ_EndsAfterTwoFullTurnsAndTheRest()
    {
        Vec3 start = Vec3.FromDecimals(70m, 50m, 0m);
        Vec3 center = Vec3.FromDecimals(50m, 50m, 0m);
        // IZ=-5.4 applied to the start: the helix end on the tool axis (virtual machine 3.1, 3.2).
        double toolAxisEnd = (double)(0m + -5.4m);

        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveAngleForm(ArcDirection.Counterclockwise, start,
            toolAxisEnd, center, 737.956m));

        GeometryAssert.Point(69.025870876999, 56.165730887068, -5.4, arc.End);
        Assert.Equal(2, arc.FullTurns);
        Assert.Equal(17.956, arc.Rest, GeometryAssert.Precision);
        Assert.Equal(737.956, arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(20, arc.Radius, GeometryAssert.Precision);
        GeometryAssert.Point(50, 50, 0, arc.Center);
        Assert.True(arc.IsHelix);
    }

    // The verb carries the direction, ANGLE is unsigned (language 4.3): CW turns the rest the other way.
    [Fact]
    public void ResolveAngleForm_Clockwise_TurnsTheRestTheOtherWay()
    {
        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveAngleForm(ArcDirection.Clockwise, new Vec3(70, 50, 0),
            -5.4, new Vec3(50, 50, 0), 737.956m));

        GeometryAssert.Point(69.025870876999, 43.834269112932, -5.4, arc.End);
        Assert.Equal(2, arc.FullTurns);
    }

    // A sweep of whole turns ends where it started, with no rest.
    [Fact]
    public void ResolveAngleForm_WholeTurns_EndsAtTheStartWithNoRest()
    {
        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveAngleForm(ArcDirection.Counterclockwise,
            new Vec3(70, 50, 0), 0, new Vec3(50, 50, 0), 720m));

        GeometryAssert.Point(70, 50, 0, arc.End);
        Assert.Equal(2, arc.FullTurns);
        Assert.Equal(0, arc.Rest);
        Assert.False(arc.IsHelix);
    }

    // ANGLE is a number of degrees greater than 0 (language 4.3, virtual machine 5).
    [Theory]
    [InlineData(0.0)]
    [InlineData(-90.0)]
    public void ResolveAngleForm_AngleNotGreaterThanZero_IsAnError(double angle)
    {
        ArcResult result = ArcResolver.ResolveAngleForm(ArcDirection.Counterclockwise, new Vec3(70, 50, 0), 0,
            new Vec3(50, 50, 0), (decimal)angle);

        Assert.Null(result.Arc);
        Assert.Equal(ArcError.AngleNotGreaterThanZero, result.Error);
    }
}
