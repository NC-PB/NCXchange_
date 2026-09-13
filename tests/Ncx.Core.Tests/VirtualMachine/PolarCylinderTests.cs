using Ncx.Core.Geometry;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Tests.Geometry;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The polar and the cylinder plane (virtual machine 3.1, 3.2, 3.4, D102): under POLAR=ON the X word (halved under
/// DIAMETER=ON, D60) and the C word as a length, under CYLINDER=n the cylinder axis Z and the C word as a length on the
/// circumference; the position known in that frame from the first motion under the transformation, and the arc
/// direction fixed, independent of WORKPLANE.
/// </summary>
public sealed class PolarCylinderTests
{
    // A hexagon of 30 across the flats has its corners at 15 / cos 30 = 17.32 (D102).
    private const double CornerRadius = 17.32;

    // D102, D60: the hexagon H30 of POLAR_FACE through the virtual machine: under POLAR=ON its lines and arcs run in
    // the face plane of X halved and C, both arcs turn around X=27 C=0, the six corners lie at radius 17.32 within the
    // arc tolerance, the contour closes where the approach arc ended, and nothing is an ERROR.
    [Fact]
    public void PolarFace_HexagonThroughTheVirtualMachine_ChecksWithoutErrorAndCloses()
    {
        NcxProgram program = Parser.Parse(Fixture.ReadText("POLAR_FACE.ncx"), "POLAR_FACE.ncx", new ParserOptions());
        var vm = new VmHarness(VmMachines.Default());
        var arcs = new List<PlaneArc>();
        var lineTargetsBetweenTheArcs = new List<Vec3>();
        Section section = program.Programs[0];
        for (int index = section.FirstBlock; index <= section.LastBlock; index++)
        {
            Block block = program.Blocks[index];
            vm.Vm.Execute(block);
            if (!vm.State.Frame.Polar)
            {
                continue;
            }

            if (block.Verb?.Key == "ARC")
            {
                Assert.NotNull(vm.Vm.LastArc);
                arcs.Add(vm.Vm.LastArc);
            }
            else if (block.Verb?.Key == "LINE" && arcs.Count == 1)
            {
                lineTargetsBetweenTheArcs.Add(FacePoint(vm));
            }
        }

        Assert.False(vm.Diagnostics.HasErrors, vm.Diagnostics.ToText());
        Assert.Equal(2, arcs.Count);
        foreach (PlaneArc arc in arcs)
        {
            Assert.Equal("POLAR", arc.Plane.Name);
            GeometryAssert.Point(27, 0, arc.Arc.Center);
            Assert.Equal(12, arc.Arc.Radius, GeometryAssert.Precision);
        }

        Assert.Equal(7, lineTargetsBetweenTheArcs.Count);
        for (int corner = 0; corner < 6; corner++)
        {
            Vec3 point = lineTargetsBetweenTheArcs[corner];
            Assert.InRange(new Vec3(point.X, point.Y, 0).Length, CornerRadius - 0.01, CornerRadius + 0.01);
        }

        GeometryAssert.Point(arcs[0].Arc.End.X, arcs[0].Arc.End.Y, lineTargetsBetweenTheArcs[6]);
        GeometryAssert.Point(15, 0, arcs[1].Arc.Start);
    }

    // VM 3.1, 3.4, D102: under POLAR=ON X and C are known in the polar frame from the first motion, X halved under
    // DIAMETER=ON, while Z stays a workpiece coordinate; POLAR=OFF leaves X and C unknown until the next motion with
    // known coordinates.
    [Fact]
    public void Polar_FirstMotion_KnowsXAndCInThePolarFrameUntilPolarOff()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=MM DIAMETER=ON SPINDLE_MODE:MAIN=AXIS", "RAPID X=54 Z=2 C=0", "POLAR=ON", "LINE X=54 C=-12 F=4000");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(27m, PositionFrame.Polar, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(-12m, PositionFrame.Polar, Known: true), vm.Position("C"));
        Assert.Equal(new AxisPosition(2m, PositionFrame.Workpiece, Known: true), vm.Position("Z"));

        vm.Execute("POLAR=OFF");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("C"));
        Assert.Equal(new AxisPosition(2m, PositionFrame.Workpiece, Known: true), vm.Position("Z"));

        vm.Execute("RAPID X=200 C=0");

        Assert.Equal(new AxisPosition(100m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(0m, PositionFrame.Workpiece, Known: true), vm.Position("C"));
    }

    // Second review of P1-03, finding 1 (VM 2.2, 3.1, 3.4, D102): from the first motion under POLAR=ON the position
    // frame of X and C is the polar frame. An axis of the plane that the block does not name and that is not known in
    // the polar frame yet is unknown, because the VM does not convert a workpiece coordinate into the face plane (D54);
    // it stays unknown after POLAR=OFF, so an incremental word on it is IX from an unknown position.
    [Theory]
    [InlineData("LINE C=5", "X", "LINE IX=5")]
    [InlineData("LINE X=20", "C", "LINE IC=5")]
    [InlineData("LINE Z=-1", "X", "LINE IX=5")]
    [InlineData("LINE Z=-1", "C", "LINE IC=5")]
    public void Polar_FirstMotionThatDoesNotNameAnAxisOfThePlane_LeavesItUnknownAfterPolarOff(string motion,
        string axis, string incremental)
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS F=100", "RAPID X=40 Z=2 C=0", "POLAR=ON", motion);

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position(axis));

        vm.Execute("POLAR=OFF", incremental);

        Assert.Equal([DiagnosticCodes.IncrementalFromUnknownPosition], vm.Codes());
    }

    // VM 2.2, 3.4, D102: an axis known in the MACHINE frame only (after HOME) is not known in the polar frame either,
    // and the first motion under POLAR=ON that does not name it leaves it unknown.
    [Fact]
    public void Polar_FirstMotionThatDoesNotNameAnAxisKnownInTheMachineFrame_LeavesItUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS F=100", "HOME X", "POLAR=ON", "LINE C=5");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Equal(new AxisPosition(5m, PositionFrame.Polar, Known: true), vm.Position("C"));
    }

    // VM 3.4, D102: a motion that names some axes leaves the others as they were; an axis of the plane that is known in
    // the polar frame already keeps its value there.
    [Fact]
    public void Polar_MotionThatDoesNotNameAnAxisKnownInThePolarFrame_KeepsItThere()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=MM SPINDLE_MODE:MAIN=AXIS F=100", "POLAR=ON", "LINE X=10 C=0", "LINE C=5", "LINE Z=-1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(10m, PositionFrame.Polar, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(5m, PositionFrame.Polar, Known: true), vm.Position("C"));
    }

    // Second review of P1-03, finding 1 (VM 2.2, 3.1, 3.4, D102): the same under CYLINDER=n for the cylinder axis Z
    // and C, while X stays a coordinate of the workpiece frame.
    [Theory]
    [InlineData("LINE C=5", "Z", "LINE IZ=5")]
    [InlineData("LINE Z=5", "C", "LINE IC=5")]
    [InlineData("LINE X=20", "Z", "LINE IZ=5")]
    [InlineData("LINE X=20", "C", "LINE IC=5")]
    public void Cylinder_FirstMotionThatDoesNotNameAnAxisOfThePlane_LeavesItUnknownAfterCylinderOff(string motion,
        string axis, string incremental)
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS F=100", "RAPID X=30 Z=0 C=0", "CYLINDER=30", motion);

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position(axis));
        Assert.Equal(PositionFrame.Workpiece, vm.Position("X").Frame);

        vm.Execute("CYLINDER=OFF", incremental);

        Assert.Equal([DiagnosticCodes.IncrementalFromUnknownPosition], vm.Codes());
    }

    // VM 3.1, D102: before the first motion under POLAR=ON the position is not known in the polar frame, so an
    // incremental C there is IX from an unknown position.
    [Fact]
    public void Polar_IncrementalWordBeforeTheFirstMotionUnderIt_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS F=100", "RAPID X=10 C=0", "POLAR=ON", "LINE IC=5");

        Assert.Equal([DiagnosticCodes.IncrementalFromUnknownPosition], vm.Codes());
    }

    // VM 3.1, D102: after a motion under POLAR=ON an incremental C adds in the polar frame.
    [Fact]
    public void Polar_IncrementalWordAfterAMotionUnderIt_AddsInThePolarFrame()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS F=100", "POLAR=ON", "LINE X=10 C=0", "LINE IC=5");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(5m, PositionFrame.Polar, Known: true), vm.Position("C"));
    }

    // VM 3.2, D102: in the polar plane the arc turns from X toward C looking against the tool axis onto the face (the
    // G12.1 and TRANSMIT convention), independent of WORKPLANE.
    [Theory]
    [InlineData("ARC=CCW", 90)]
    [InlineData("ARC=CW", 270)]
    public void Polar_Arc_TurnsFromXTowardCIndependentOfTheWorkplane(string verb, double sweep)
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=MM WORKPLANE=ZX SPINDLE_MODE:MAIN=AXIS F=100",
            "POLAR=ON",
            "LINE X=5 C=0",
            verb + " X=0 C=5 CENTER:X=0 CENTER:C=0");

        vm.AssertNoDiagnostics();
        Assert.NotNull(vm.Vm.LastArc);
        Assert.Equal("POLAR", vm.Vm.LastArc.Plane.Name);
        Assert.Equal(sweep, vm.Vm.LastArc.Arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(new AxisPosition(5m, PositionFrame.Polar, Known: true), vm.Position("C"));
    }

    // VM 3.1, 3.2, 3.4, D102: under CYLINDER=n the arc turns in the plane of the cylinder axis Z and the C word as a
    // length on the circumference, Z first and C second (G7.1, TRACYL); X stays a workpiece coordinate; CYLINDER=OFF
    // leaves Z and C unknown.
    [Fact]
    public void Cylinder_Arc_TurnsFromZTowardCAndXStaysInTheWorkpieceFrame()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=MM SPINDLE_MODE:MAIN=AXIS F=100",
            "RAPID X=30 Z=0 C=0",
            "CYLINDER=30",
            "LINE Z=10 C=0",
            "ARC=CCW Z=0 C=10 CENTER:Z=0 CENTER:C=0");

        vm.AssertNoDiagnostics();
        Assert.NotNull(vm.Vm.LastArc);
        Assert.Equal("CYLINDER", vm.Vm.LastArc.Plane.Name);
        Assert.Equal(90, vm.Vm.LastArc.Arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(new AxisPosition(0m, PositionFrame.Cylinder, Known: true), vm.Position("Z"));
        Assert.Equal(new AxisPosition(10m, PositionFrame.Cylinder, Known: true), vm.Position("C"));
        Assert.Equal(new AxisPosition(30m, PositionFrame.Workpiece, Known: true), vm.Position("X"));

        vm.Execute("CYLINDER=OFF");

        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("C"));
        Assert.Equal(new AxisPosition(30m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
    }

    // X and C as a point of the face plane; both known in the polar frame (D102).
    private static Vec3 FacePoint(VmHarness vm)
    {
        AxisPosition x = vm.Position("X");
        AxisPosition c = vm.Position("C");
        Assert.Equal(PositionFrame.Polar, x.Frame);
        Assert.Equal(PositionFrame.Polar, c.Frame);
        return Vec3.FromDecimals(x.Value, c.Value, 0m);
    }
}
