using Ncx.Core.Geometry;

namespace Ncx.Core.Tests.Geometry;

/// <summary>
/// The working planes of ARC and their direction convention (language 4.2, 4.3; virtual machine 3.2; D102): in every
/// plane ARC=CCW turns from the first plane axis toward the second, seen looking against the tool axis.
/// </summary>
public sealed class PlaneTests
{
    [Fact]
    public void XY_Axes_AreXThenYWithToolAxisZ()
    {
        AssertAxes("X", "Y", "Z", Plane.XY);
    }

    // WORKPLANE=ZX is the G18 orientation (language 4.2, virtual machine 3.2).
    [Fact]
    public void ZX_Axes_AreZThenXWithToolAxisY()
    {
        AssertAxes("Z", "X", "Y", Plane.ZX);
    }

    // WORKPLANE=YZ is the G19 orientation (virtual machine 3.2).
    [Fact]
    public void YZ_Axes_AreYThenZWithToolAxisX()
    {
        AssertAxes("Y", "Z", "X", Plane.YZ);
    }

    // G17, G18 and G19 keep X, Y and Z in their cyclic order, so that the first axis, the second axis and the tool
    // axis are right-handed in every plane and CCW means the same turn seen against each tool axis.
    [Fact]
    public void Workplanes_FirstSecondAndToolAxis_AreACyclicOrderOfXYZ()
    {
        string[] cyclicOrders = ["XYZ", "YZX", "ZXY"];

        foreach (Plane plane in new[] { Plane.XY, Plane.ZX, Plane.YZ })
        {
            Assert.Contains(plane.FirstAxis + plane.SecondAxis + plane.ToolAxis, cyclicOrders);
        }
    }

    // Under POLAR=ON: the face plane of the X word and the C word as a length, X first and C second (G12.1,
    // TRANSMIT), looking against the tool axis onto the face (virtual machine 3.2, D102).
    [Fact]
    public void Polar_Axes_AreXThenCWithToolAxisZ()
    {
        AssertAxes("X", "C", "Z", Plane.Polar);
    }

    // Under CYLINDER=n: the cylinder axis (Z on a lathe) first and C as a length on the circumference second (G7.1,
    // TRACYL), X staying a workpiece coordinate (virtual machine 3.2, 3.4, D102).
    [Fact]
    public void Cylinder_Axes_AreZThenCWithToolAxisX()
    {
        AssertAxes("Z", "C", "X", Plane.Cylinder);
    }

    // WORKPLANE=ZX, from Z=10 X=0 to Z=0 X=10 with R=10: counterclockwise seen from +Y is the quarter turn from +Z
    // toward +X around the origin (the G18 orientation, virtual machine 3.2). Plane coordinates are first Z, second X.
    [Fact]
    public void ZX_CounterclockwiseQuarterFromZTowardX_TurnsAroundTheOrigin()
    {
        var start = new Vec3(10, 0, 0);
        var end = new Vec3(0, 10, 0);

        Arc arc = GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(ArcDirection.Counterclockwise, start, end,
            10m, GeometryAssert.ArcToleranceMm));

        GeometryAssert.Point(0, 0, arc.Center);
        Assert.Equal(90, arc.Sweep, GeometryAssert.Precision);
    }

    private static void AssertAxes(string first, string second, string tool, Plane plane)
    {
        Assert.Equal(first, plane.FirstAxis);
        Assert.Equal(second, plane.SecondAxis);
        Assert.Equal(tool, plane.ToolAxis);
    }
}
