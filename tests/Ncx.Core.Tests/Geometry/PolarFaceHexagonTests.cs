using System.Globalization;
using Ncx.Core.Geometry;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Geometry;

/// <summary>
/// The hexagon H30 of POLAR_FACE.ncx under POLAR=ON, as far as it is geometry: the lines and arcs run in the face
/// plane of X and C, X first and C second (virtual machine 3.1, 3.2, D102); X is a diameter under DIAMETER=ON and
/// reaches the plane halved (D60, applied here by hand, by the virtual machine later); the hexagon closes and its
/// corners lie at radius 17.32.
/// </summary>
public sealed class PolarFaceHexagonTests
{
    // A hexagon of 30 across the flats has its corners at 15 / cos 30 = 17.32 (D102).
    private const double CornerRadius = 17.32;

    [Fact]
    public void Hexagon_ArcsInThePolarPlane_ResolveWithoutError()
    {
        HexagonWalk walk = Walk();

        Assert.Equal(2, walk.Arcs.Count);
        foreach (Arc arc in walk.Arcs)
        {
            Assert.Equal(12, arc.Radius, GeometryAssert.Precision);
            Assert.Equal(90, arc.Sweep, GeometryAssert.Precision);
        }
    }

    // With X first and C second (G12.1, TRANSMIT) both arcs turn around X=27 C=0 and meet the flat X=15 tangentially:
    // at the joint X=15 C=0 the radius runs along X. With C first they would turn around X=15 C=-12 and meet the flat
    // at a right angle.
    [Fact]
    public void Hexagon_ArcsWithXThenC_TurnAroundX27C0TangentToTheFlat()
    {
        HexagonWalk walk = Walk();

        foreach (Arc arc in walk.Arcs)
        {
            GeometryAssert.Point(27, 0, arc.Center);
        }

        Arc approach = walk.Arcs[0];
        GeometryAssert.Point(15, 0, approach.End);
        Assert.Equal(0, (approach.End - approach.Center).Y, GeometryAssert.Precision);
    }

    // The corners lie at radius 17.32 within the arc tolerance (D102, D36).
    [Fact]
    public void Hexagon_SixCorners_LieAtRadius1732()
    {
        List<Vec3> lineTargets = Walk().LineTargetsBetweenTheArcs;

        for (int corner = 0; corner < 6; corner++)
        {
            Vec3 point = lineTargets[corner];
            double radius = new Vec3(point.X, point.Y, 0).Length;
            Assert.InRange(radius, CornerRadius - 0.01, CornerRadius + 0.01);
        }
    }

    // After the six sides the contour is back where the approach arc ended, and the departure arc starts there.
    [Fact]
    public void Hexagon_Contour_ClosesWhereTheApproachArcEnds()
    {
        HexagonWalk walk = Walk();

        Assert.Equal(7, walk.LineTargetsBetweenTheArcs.Count);
        Assert.Equal(walk.Arcs[0].End, walk.LineTargetsBetweenTheArcs[6]);
        Assert.Equal(walk.Arcs[0].End, walk.Arcs[1].Start);
    }

    // Walks the motion blocks between POLAR=ON and POLAR=OFF of the example in the polar plane: the target of every
    // block is the position with its words applied (virtual machine 3.1), and an ARC=CW with R is resolved from the
    // position to that target (virtual machine 3.2).
    private static HexagonWalk Walk()
    {
        var walk = new HexagonWalk();

        // Z=2 from RAPID X=54 Z=2 before POLAR=ON; the first motion under POLAR=ON names both plane axes.
        var position = new Vec3(0, 0, 2);
        foreach (string block in PolarMotionBlocks())
        {
            Vec3 target = Target(position, block);
            if (block.StartsWith("ARC=CW ", StringComparison.Ordinal))
            {
                decimal radius = decimal.Parse(WordValue(block, "R") ?? "", CultureInfo.InvariantCulture);
                walk.Arcs.Add(GeometryAssert.Resolved(ArcResolver.ResolveRadiusForm(ArcDirection.Clockwise, position,
                    target, radius, GeometryAssert.ArcToleranceMm)));
            }
            else if (walk.Arcs.Count == 1)
            {
                walk.LineTargetsBetweenTheArcs.Add(target);
            }

            position = target;
        }

        return walk;
    }

    // The target of a block in the polar plane: the X word halved (a diameter under DIAMETER=ON, D60) on the first
    // plane axis, the C word as a length on the second, Z on the tool axis; an axis the block does not name keeps its
    // value (virtual machine 3.1, D102).
    private static Vec3 Target(Vec3 position, string block)
    {
        double first = position.X;
        double second = position.Y;
        double tool = position.Z;

        string? diameter = WordValue(block, Plane.Polar.FirstAxis);
        if (diameter is not null)
        {
            first = double.Parse(diameter, CultureInfo.InvariantCulture) / 2;
        }

        string? length = WordValue(block, Plane.Polar.SecondAxis);
        if (length is not null)
        {
            second = double.Parse(length, CultureInfo.InvariantCulture);
        }

        string? toolAxis = WordValue(block, Plane.Polar.ToolAxis);
        if (toolAxis is not null)
        {
            tool = double.Parse(toolAxis, CultureInfo.InvariantCulture);
        }

        return new Vec3(first, second, tool);
    }

    // The value of the word KEY=VALUE in a block, null when the block has no such word (language 3).
    private static string? WordValue(string block, string key)
    {
        string prefix = key + "=";
        foreach (string word in block.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith(prefix, StringComparison.Ordinal))
            {
                return word.Substring(prefix.Length);
            }
        }

        return null;
    }

    // The LINE and ARC blocks of POLAR_FACE.ncx from POLAR=ON to POLAR=OFF, their comments stripped (language 3).
    private static List<string> PolarMotionBlocks()
    {
        var blocks = new List<string>();
        bool polar = false;
        foreach (string line in Fixture.ReadText("POLAR_FACE.ncx").Split('\n'))
        {
            int semicolon = line.IndexOf(';');
            string block = (semicolon < 0 ? line : line.Substring(0, semicolon)).Trim();
            if (block == "POLAR=ON")
            {
                polar = true;
            }
            else if (block == "POLAR=OFF")
            {
                polar = false;
            }
            else if (polar && (block.StartsWith("LINE ", StringComparison.Ordinal)
                || block.StartsWith("ARC=", StringComparison.Ordinal)))
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    private sealed class HexagonWalk
    {
        public List<Arc> Arcs { get; } = [];

        // The targets of the seven LINE blocks between the approach and the departure arc: six corners, then the
        // point where the approach arc ended.
        public List<Vec3> LineTargetsBetweenTheArcs { get; } = [];
    }
}
