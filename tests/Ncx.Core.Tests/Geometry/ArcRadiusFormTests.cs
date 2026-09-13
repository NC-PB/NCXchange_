using Ncx.Core.Geometry;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Geometry;

/// <summary>
/// The R form of ARC: a signed radius, the center computed by the formula of virtual machine 3.2, d > 2|R| plus the
/// arc tolerance an ERROR, start = end with R an ERROR (language 4.3, virtual machine 3.2, 5).
/// </summary>
public sealed class ArcRadiusFormTests
{
    // The four corner arcs of the contour of 2.5D_FRAESEN.ncx (H16, H18, H20, H22): R=5, CW, from the end of one side
    // to the start of the next; the center lies 5 inside the corner.
    public static TheoryData<string, double, double, double, double, double, double> CornerArcs => new()
    {
        { "ARC=CW X=2 Y=7 R=5", 7, 2, 2, 7, 7, 7 },
        { "ARC=CW X=7 Y=98 R=5", 2, 93, 7, 98, 7, 93 },
        { "ARC=CW X=98 Y=93 R=5", 93, 98, 98, 93, 93, 93 },
        { "ARC=CW X=93 Y=2 R=5", 98, 7, 93, 2, 93, 7 },
    };

    [Theory]
    [MemberData(nameof(CornerArcs))]
    public void ResolveRadiusForm_CornerArcOfTheContour_CentersFiveInsideTheCorner(string block, double startX,
        double startY, double endX, double endY, double centerX, double centerY)
    {
        Assert.Contains(block, Fixture.ReadText("2.5D_FRAESEN.ncx"), StringComparison.Ordinal);

        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(ArcDirection.Clockwise,
            new Vec3(startX, startY, -10), new Vec3(endX, endY, -10), 5m, GeometryAssert.ArcToleranceMm));

        GeometryAssert.Point(centerX, centerY, arc.Center);
        Assert.Equal(5, arc.Radius, GeometryAssert.Precision);
        Assert.Equal(90, arc.Sweep, GeometryAssert.Precision);
    }

    // The VM keeps the computed center, so the compiler can write either form (virtual machine 3.2): the CENTER form
    // around the computed center, and around the known corner center, is the same arc.
    [Theory]
    [MemberData(nameof(CornerArcs))]
    public void ResolveRadiusForm_ComputedCenter_IsReproducedByTheCenterForm(string block, double startX,
        double startY, double endX, double endY, double centerX, double centerY)
    {
        Assert.Contains(block, Fixture.ReadText("2.5D_FRAESEN.ncx"), StringComparison.Ordinal);
        var start = new Vec3(startX, startY, -10);
        var end = new Vec3(endX, endY, -10);

        Arc byRadius = GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(ArcDirection.Clockwise, start, end, 5m,
            GeometryAssert.ArcToleranceMm));
        Arc byComputedCenter = GeometryAssert.Resolved(ArcResolver.ResolveCenterForm(ArcDirection.Clockwise, start,
            end, byRadius.Center, GeometryAssert.ArcToleranceMm));
        Arc byKnownCenter = GeometryAssert.Resolved(ArcResolver.ResolveCenterForm(ArcDirection.Clockwise, start, end,
            new Vec3(centerX, centerY, -10), GeometryAssert.ArcToleranceMm));

        Assert.Equal(byRadius.Center, byComputedCenter.Center);
        Assert.Equal(byRadius.Sweep, byComputedCenter.Sweep, GeometryAssert.Precision);
        Assert.Equal(byRadius.Radius, byComputedCenter.Radius, GeometryAssert.Precision);
        Assert.Equal(byRadius.Sweep, byKnownCenter.Sweep, GeometryAssert.Precision);
    }

    // H36 of 2.5D_FRAESEN, ARC=CCW X=49.466 Y=69.993 R=20 from 70/50, continues the circle that H34 draws around
    // CENTER 50/50: the computed center lies within the arc tolerance of 50/50.
    [Fact]
    public void ResolveRadiusForm_PocketArcH36_RecomputesTheCenterOfH34WithinTolerance()
    {
        Assert.Contains("ARC=CCW X=49.466 Y=69.993 R=20", Fixture.ReadText("2.5D_FRAESEN.ncx"),
            StringComparison.Ordinal);

        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(ArcDirection.Counterclockwise,
            Vec3.FromDecimals(70m, 50m, -10m), Vec3.FromDecimals(49.466m, 69.993m, -10m), 20m,
            GeometryAssert.ArcToleranceMm));

        GeometryAssert.Point(50.000000000424, 50.000130170995, -10, arc.Center);
        Assert.InRange((arc.Center - new Vec3(50, 50, -10)).Length, 0, 0.01);
        Assert.Equal(91.530352047846, arc.Sweep, GeometryAssert.Precision);
    }

    // H33 of 2.5D_FRAESEN, ARC=CCW X=50.534 Y=69.993 R=5.525 from 55.243/67.104, enters the pocket circle of radius 20
    // around 50/50 tangentially: its center lies 20 - 5.525 = 14.475 from 50/50, within the arc tolerance.
    [Fact]
    public void ResolveRadiusForm_PocketEntryArcH33_IsTangentToThePocketCircle()
    {
        Assert.Contains("ARC=CCW X=50.534 Y=69.993 R=5.525", Fixture.ReadText("2.5D_FRAESEN.ncx"),
            StringComparison.Ordinal);

        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(ArcDirection.Counterclockwise,
            Vec3.FromDecimals(55.243m, 67.104m, -10m), Vec3.FromDecimals(50.534m, 69.993m, -10m), 5.525m,
            GeometryAssert.ArcToleranceMm));

        GeometryAssert.Point(50.386299975458, 64.469974587896, arc.Center);
        Assert.InRange((arc.Center - new Vec3(50, 50, -10)).Length, 14.475 - 0.01, 14.475 + 0.01);
    }

    // R positive: an arc of 180 degrees or less; negative: more than 180 (language 4.3). The center lies on the left
    // of the chord for (CCW, R > 0) and (CW, R < 0), on the right otherwise (virtual machine 3.2): from 7/2 to 2/7 the
    // center is the corner 7/7 or the opposite corner 2/2.
    [Theory]
    [InlineData("CW", 5, 7.0, 7.0, 90.0)]
    [InlineData("CW", -5, 2.0, 2.0, 270.0)]
    [InlineData("CCW", 5, 2.0, 2.0, 90.0)]
    [InlineData("CCW", -5, 7.0, 7.0, 270.0)]
    public void ResolveRadiusForm_SignOfRAndTheVerb_PickTheSideOfTheCenter(string verb, int radius, double centerX,
        double centerY, double sweep)
    {
        ArcDirection direction = verb == "CCW" ? ArcDirection.Counterclockwise : ArcDirection.Clockwise;

        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(direction, new Vec3(7, 2, 0),
            new Vec3(2, 7, 0), radius, GeometryAssert.ArcToleranceMm));

        GeometryAssert.Point(centerX, centerY, arc.Center);
        Assert.Equal(sweep, arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(5, arc.Radius, GeometryAssert.Precision);
    }

    // d > 2|R| plus tolerance is an ERROR (virtual machine 3.2, 5 "radius too small"): a chord of 10.02 for R=5.
    [Theory]
    [InlineData(5)]
    [InlineData(-5)]
    public void ResolveRadiusForm_ChordLongerThanTwiceTheRadiusPlusTolerance_IsRadiusTooSmall(int radius)
    {
        ArcResult result = ArcResolver.ResolveRadiusForm(ArcDirection.Clockwise, new Vec3(0, 0, 0),
            new Vec3(10.02, 0, 0), radius, GeometryAssert.ArcToleranceMm);

        Assert.Null(result.Arc);
        Assert.Equal(ArcError.RadiusTooSmall, result.Error);
    }

    // A chord of 10.005 for R=5 is within the tolerance: nothing is left under the root of h, and the arc is the half
    // circle around the midpoint of the chord.
    [Fact]
    public void ResolveRadiusForm_ChordLongerThanTwiceTheRadiusWithinTolerance_IsAHalfCircleAroundTheMidpoint()
    {
        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(ArcDirection.Clockwise, new Vec3(0, 0, 0),
            new Vec3(10.005, 0, 0), 5m, GeometryAssert.ArcToleranceMm));

        GeometryAssert.Point(5.0025, 0, arc.Center);
        Assert.Equal(180, arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(5.0025, arc.Radius, GeometryAssert.Precision);
    }

    // Start = end with R: ERROR; full circles need CENTER (virtual machine 3.2, language 4.3).
    [Fact]
    public void ResolveRadiusForm_StartEqualsEnd_IsFullCircleWithRadius()
    {
        ArcResult result = ArcResolver.ResolveRadiusForm(ArcDirection.Counterclockwise, new Vec3(70, 50, 0),
            new Vec3(70, 50, -2), 20m, GeometryAssert.ArcToleranceMm);

        Assert.Null(result.Arc);
        Assert.Equal(ArcError.FullCircleWithRadius, result.Error);
    }

    // R is a number, not 0 (language 4.3).
    [Fact]
    public void ResolveRadiusForm_RadiusZero_IsRadiusZero()
    {
        ArcResult result = ArcResolver.ResolveRadiusForm(ArcDirection.Clockwise, new Vec3(7, 2, 0),
            new Vec3(2, 7, 0), 0m, GeometryAssert.ArcToleranceMm);

        Assert.Equal(ArcError.RadiusZero, result.Error);
    }

    // A tool-axis word makes a helix (virtual machine 3.2); the plane arc is the one without it.
    [Fact]
    public void ResolveRadiusForm_ToolAxisWord_MakesAHelixOverTheSamePlaneArc()
    {
        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(ArcDirection.Clockwise, new Vec3(7, 2, -10),
            new Vec3(2, 7, -11), 5m, GeometryAssert.ArcToleranceMm));

        GeometryAssert.Point(7, 7, -10, arc.Center);
        GeometryAssert.Point(2, 7, -11, arc.End);
        Assert.Equal(90, arc.Sweep, GeometryAssert.Precision);
        Assert.True(arc.IsHelix);
    }
}
