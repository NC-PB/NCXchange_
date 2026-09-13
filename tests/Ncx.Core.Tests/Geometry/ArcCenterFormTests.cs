using Ncx.Core.Geometry;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Geometry;

/// <summary>
/// The CENTER form of ARC: the virtual machine validates |start - center| against |end - center| with the arc
/// tolerance of the configuration, and start = end is a full circle (virtual machine 3.2, D36).
/// </summary>
public sealed class ArcCenterFormTests
{
    // Language 6 and H34 of 2.5D_FRAESEN: ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50 from 50.534/69.993 (Fanuc G3 X70.
    // Y50. I-.534 J-19.993). The start lies 20.00013 from the center, the end 20: radius 20 within 0.01 mm.
    [Fact]
    public void ResolveCenterForm_LanguageExample_IsRadiusTwentyWithinTolerance()
    {
        Assert.Contains("ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50", Fixture.ReadText("2.5D_FRAESEN.ncx"),
            StringComparison.Ordinal);
        Vec3 start = Vec3.FromDecimals(50.534m, 69.993m, -10m);
        Vec3 end = Vec3.FromDecimals(70m, 50m, -10m);
        Vec3 center = Vec3.FromDecimals(50m, 50m, 0m);

        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveCenterForm(ArcDirection.Counterclockwise, start, end,
            center, GeometryAssert.ArcToleranceMm));

        Assert.Equal(20.000130124576685, arc.Radius, GeometryAssert.Precision);
        GeometryAssert.Point(70, 50, -10, arc.End);
        // The center is a point of the plane; the arc keeps it at the tool-axis coordinate of its start.
        GeometryAssert.Point(50, 50, -10, arc.Center);
        // Counterclockwise from the top of the circle round through 180 and 270 degrees to 0.
        Assert.Equal(271.529969177505, arc.Sweep, GeometryAssert.Precision);
        Assert.False(arc.IsHelix);
    }

    [Fact]
    public void ResolveCenterForm_LanguageExampleClockwise_TakesTheOtherWayRound()
    {
        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveCenterForm(ArcDirection.Clockwise,
            Vec3.FromDecimals(50.534m, 69.993m, -10m), Vec3.FromDecimals(70m, 50m, -10m),
            Vec3.FromDecimals(50m, 50m, 0m), GeometryAssert.ArcToleranceMm));

        Assert.Equal(360 - 271.529969177505, arc.Sweep, GeometryAssert.Precision);
    }

    // Around 50/50 from 70/50 (radius 20): an end 20.009 from the center is within 0.01 mm, one 20.011 away is not.
    [Theory]
    [InlineData(70.009, true)]
    [InlineData(70.011, false)]
    public void ResolveCenterForm_EndRadiusAgainstStartRadius_IsCheckedWithTheArcTolerance(double endY, bool resolves)
    {
        ArcResult result = ArcResolver.ResolveCenterForm(ArcDirection.Counterclockwise, new Vec3(70, 50, 0),
            new Vec3(50, endY, 0), new Vec3(50, 50, 0), GeometryAssert.ArcToleranceMm);

        Assert.Equal(resolves, result.Arc is not null);
        Assert.Equal(resolves ? null : ArcError.InconsistentCenter, result.Error);
    }

    // The tolerance is the configuration's, 0.01 mm and 0.0005 in by default (D36): a radius that differs by 0.0008
    // passes in MM and fails in INCH.
    [Fact]
    public void ResolveCenterForm_ToleranceOfTheConfiguration_DecidesTheCheck()
    {
        var start = new Vec3(1, 0, 0);
        var end = new Vec3(0, 1.0008, 0);
        var center = new Vec3(0, 0, 0);

        ArcResult inMillimetres = ArcResolver.ResolveCenterForm(ArcDirection.Counterclockwise, start, end, center,
            0.01m);
        ArcResult inInches = ArcResolver.ResolveCenterForm(ArcDirection.Counterclockwise, start, end, center,
            0.0005m);

        Assert.NotNull(inMillimetres.Arc);
        Assert.Equal(ArcError.InconsistentCenter, inInches.Error);
    }

    // Start = end is a full circle (virtual machine 3.2), in both directions.
    [Fact]
    public void ResolveCenterForm_StartEqualsEnd_IsAFullCircle()
    {
        foreach (ArcDirection direction in new[] { ArcDirection.Clockwise, ArcDirection.Counterclockwise })
        {
            Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveCenterForm(direction, new Vec3(70, 50, -10),
                new Vec3(70, 50, -10), new Vec3(50, 50, -10), GeometryAssert.ArcToleranceMm));

            Assert.Equal(360, arc.Sweep);
            Assert.Equal(1, arc.FullTurns);
            Assert.Equal(0, arc.Rest);
        }
    }

    // A tool-axis word makes a helix (virtual machine 3.2): start = end in the plane with a different tool-axis
    // coordinate is one full turn of a helix.
    [Fact]
    public void ResolveCenterForm_StartEqualsEndWithAToolAxisWord_IsAFullTurnOfAHelix()
    {
        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveCenterForm(ArcDirection.Counterclockwise,
            new Vec3(70, 50, 0), new Vec3(70, 50, -2), new Vec3(50, 50, 0), GeometryAssert.ArcToleranceMm));

        Assert.Equal(360, arc.Sweep);
        Assert.True(arc.IsHelix);
        Assert.Equal(-2, arc.End.Z);
    }
}
