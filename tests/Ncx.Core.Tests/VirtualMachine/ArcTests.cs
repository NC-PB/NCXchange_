using Ncx.Core.Model;
using Ncx.Core.Tests.Geometry;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// ARC through the virtual machine (virtual machine 3.2): the CENTER form with the tolerance check, the R form with the
/// recomputed center, the ANGLE form of D84, the helix, the orientation of the planes of WORKPLANE, the words an ARC
/// block takes, the start known in the plane, the arc tolerance of D36 and the diameters of D60.
/// </summary>
public sealed class ArcTests
{
    // Language 6: G3 X70. Y50. I-.534 J-19.993 from 50.534/69.993 is ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50; the
    // start lies at radius 20.00013, the end at 20, within the arc tolerance of 0.01 mm (VM 3.2, D36).
    [Fact]
    public void Arc_CenterFormOfTheLanguageExample_ResolvesWithinTheArcTolerance()
    {
        VmHarness vm = Mill().Execute("RAPID X=50.534 Y=69.993", "ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50");

        vm.AssertNoDiagnostics();
        Assert.Equal(Workpiece(70m), vm.Position("X"));
        Assert.Equal(Workpiece(50m), vm.Position("Y"));
        PlaneArc arc = LastArc(vm);
        Assert.Equal("XY", arc.Plane.Name);
        GeometryAssert.Point(50, 50, arc.Arc.Center);
        Assert.Equal(20.00013, arc.Arc.Radius, 5);
    }

    // 2.5D_FRAESEN H16, language 6: ARC=CW X=2 Y=7 R=5 from 7/2; the VM computes the center 7/7, and the CENTER form
    // with that center reproduces the arc (VM 3.2: the VM keeps the computed center, so either form can be written).
    [Fact]
    public void Arc_RadiusFormCornerArc_RecomputesTheCenterThatTheCenterFormReproduces()
    {
        VmHarness radius = Mill().Execute("RAPID X=7 Y=2", "ARC=CW X=2 Y=7 R=5");
        VmHarness center = Mill().Execute("RAPID X=7 Y=2", "ARC=CW X=2 Y=7 CENTER:X=7 CENTER:Y=7");

        radius.AssertNoDiagnostics();
        center.AssertNoDiagnostics();
        PlaneArc computed = LastArc(radius);
        PlaneArc given = LastArc(center);
        GeometryAssert.Point(7, 7, computed.Arc.Center);
        Assert.Equal(90, computed.Arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(given.Arc.Sweep, computed.Arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(center.Position("X"), radius.Position("X"));
        Assert.Equal(center.Position("Y"), radius.Position("Y"));
    }

    // 2.5D_FRAESEN H36: ARC=CCW X=49.466 Y=69.993 R=20 from 70/50 turns around 50/50, within the rounding of the
    // example's three decimals (VM 3.2).
    [Fact]
    public void Arc_RadiusFormOfThePocket_RecomputesTheKnownCenter()
    {
        VmHarness vm = Mill().Execute("RAPID X=70 Y=50", "ARC=CCW X=49.466 Y=69.993 R=20");

        vm.AssertNoDiagnostics();
        PlaneArc arc = LastArc(vm);
        Assert.Equal(50, arc.Arc.Center.X, 3);
        Assert.Equal(50, arc.Arc.Center.Y, 3);
    }

    // Language 4.3: a negative R is the arc of more than 180 degrees, around the other center.
    [Fact]
    public void Arc_NegativeRadius_IsTheArcOfMoreThan180Degrees()
    {
        VmHarness vm = Mill().Execute("RAPID X=7 Y=2", "ARC=CW X=2 Y=7 R=-5");

        vm.AssertNoDiagnostics();
        PlaneArc arc = LastArc(vm);
        GeometryAssert.Point(2, 2, arc.Arc.Center);
        Assert.Equal(270, arc.Arc.Sweep, GeometryAssert.Precision);
    }

    // VM 3.2, 5: d > 2|R| plus the tolerance is an ERROR, radius too small; the arc does not resolve.
    [Fact]
    public void Arc_ChordLongerThanTheDiameter_IsRadiusTooSmall()
    {
        VmHarness vm = Mill().Execute("RAPID X=0 Y=0", "ARC=CW X=20 Y=0 R=5");

        Assert.Equal([DiagnosticCodes.ArcRadiusTooSmall], vm.Codes());
        Assert.Null(vm.Vm.LastArc);
    }

    // VM 3.2, 5: start = end with R is an ERROR, a full circle needs CENTER.
    [Fact]
    public void Arc_RadiusFormFromTheStartToTheStart_IsAFullCircleWithR()
    {
        VmHarness vm = Mill().Execute("RAPID X=0 Y=0", "ARC=CW X=0 Y=0 R=5");

        Assert.Equal([DiagnosticCodes.ArcFullCircleWithRadius], vm.Codes());
    }

    // Language 4.3: R is a number, not 0.
    [Fact]
    public void Arc_RadiusZero_IsAnError()
    {
        VmHarness vm = Mill().Execute("RAPID X=0 Y=0", "ARC=CW X=5 Y=5 R=0");

        Assert.Equal([DiagnosticCodes.ArcRadiusZero], vm.Codes());
    }

    // VM 3.2, 5: |start - center| against |end - center| beyond the tolerance is an inconsistent center.
    [Fact]
    public void Arc_CenterAtDifferentRadii_IsAnInconsistentCenter()
    {
        VmHarness vm = Mill().Execute("RAPID X=0 Y=10", "ARC=CW X=10 Y=0 CENTER:X=0 CENTER:Y=0.5");

        Assert.Equal([DiagnosticCodes.ArcInconsistentCenter], vm.Codes());
    }

    // VM 3.2: in the CENTER form start = end is a full circle.
    [Fact]
    public void Arc_CenterFormFromTheStartToTheStart_IsAFullCircle()
    {
        VmHarness vm = Mill().Execute("RAPID X=10 Y=0", "ARC=CCW CENTER:X=0 CENTER:Y=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(360, LastArc(vm).Arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(0m), vm.Position("Y"));
    }

    // Language 6, D84: the TopSolid helix CP IPA+737.956 IZ-5.4 DR+ around CC X50 Y50 from 70/50: two full turns bring
    // the tool back to 70/50, the rest of 17.956 degrees ends it at 69.026/56.166, 5.4 lower; the VM keeps the sweep.
    [Fact]
    public void Arc_AngleFormHelixOfTheLanguageExample_EndsAfterTwoFullTurnsAndTheRest()
    {
        VmHarness vm = Mill().Execute("RAPID X=70 Y=50 Z=0", "ARC=CCW IZ=-5.4 CENTER:X=50 CENTER:Y=50 ANGLE=737.956");

        vm.AssertNoDiagnostics();
        Assert.Equal(Workpiece(69.026m), vm.Position("X"));
        Assert.Equal(Workpiece(56.166m), vm.Position("Y"));
        Assert.Equal(Workpiece(-5.4m), vm.Position("Z"));
        PlaneArc arc = LastArc(vm);
        Assert.Equal(2, arc.Arc.FullTurns);
        Assert.Equal(17.956, arc.Arc.Rest, GeometryAssert.Precision);
        Assert.Equal(737.956, arc.Arc.Sweep, GeometryAssert.Precision);
        Assert.True(arc.Arc.IsHelix);
    }

    // VM 3.2, 5, D84: the ANGLE form needs CENTER and takes no R, no plane end-point word, no ANGLE of 0 or less.
    [Theory]
    [InlineData("ARC=CCW CENTER:X=50 CENTER:Y=50 ANGLE=90 R=5", DiagnosticCodes.ArcAngleWithRadius)]
    [InlineData("ARC=CCW X=60 CENTER:X=50 CENTER:Y=50 ANGLE=90", DiagnosticCodes.ArcAngleWithPlaneEndPoint)]
    [InlineData("ARC=CCW ANGLE=90", DiagnosticCodes.ArcAngleWithoutCenter)]
    [InlineData("ARC=CCW CENTER:X=50 CENTER:Y=50 ANGLE=0", DiagnosticCodes.ArcAngleNotGreaterThanZero)]
    public void Arc_WordsOfTheAngleForm_AreChecked(string arc, string code)
    {
        VmHarness vm = Mill().Execute("RAPID X=70 Y=50", arc);

        Assert.Equal([code], vm.Codes());
    }

    // VM 3.2, 5, language 4.3: an ARC needs CENTER, R or ANGLE; CENTER on both plane axes, on no other axis, and not
    // together with R.
    [Theory]
    [InlineData("ARC=CW X=5 Y=5", DiagnosticCodes.ArcWithoutCenterRadiusOrAngle)]
    [InlineData("ARC=CW X=5 Y=5 CENTER:X=0", DiagnosticCodes.ArcCenterWithoutBothPlaneAxes)]
    [InlineData("ARC=CW X=5 Y=5 CENTER:X=0 CENTER:Y=0 CENTER:Z=0", DiagnosticCodes.ArcCenterOutsideThePlane)]
    [InlineData("ARC=CW X=5 Y=5 CENTER:X=5 CENTER:Y=0 R=5", DiagnosticCodes.ArcCenterWithRadius)]
    public void Arc_WordsOfTheCenterAndRadiusForms_AreChecked(string arc, string code)
    {
        VmHarness vm = Mill().Execute("RAPID X=0 Y=0", arc);

        Assert.Equal([code], vm.Codes());
    }

    // VM 3.2: the start is the current position and must be known in the plane.
    [Fact]
    public void Arc_StartNotKnownInThePlane_IsAnError()
    {
        VmHarness vm = Mill().Execute("F=100", "RAPID X=5", "ARC=CW X=5 Y=5 R=5");

        Assert.Equal([DiagnosticCodes.ArcStartUnknownInThePlane], vm.Codes());
        Assert.Null(vm.Vm.LastArc);
    }

    // VM 3.9, D99: inside a subprogram that no program calls the unknown start is suppressed like an incremental word
    // from an unknown position; the absolute end is known afterwards, an axis the arc moved from the unknown start is
    // not, as the SETPOS words after it show.
    [Fact]
    public void UncalledSub_ArcFromAnUnknownStart_IsSuppressedAndTheAbsoluteEndIsKnown()
    {
        string text = VmHarness.File(
            "PROGRAM=END", "SUB=BEGIN NAME=10", "ARC=CW X=5 R=5", "SETPOS X=0", "SETPOS Y=0", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal([DiagnosticCodes.SetposAxisUnknown], vm.Codes());
        Assert.Contains("SETPOS Y", vm.Messages(DiagnosticCodes.SetposAxisUnknown)[0], StringComparison.Ordinal);
    }

    // VM 3.2: WORKPLANE=ZX uses the G18 orientation, CCW from Z toward X looking against the tool axis Y.
    [Theory]
    [InlineData("ARC=CCW", 90)]
    [InlineData("ARC=CW", 270)]
    public void Arc_WorkplaneZX_TurnsFromZTowardX(string verb, double sweep)
    {
        VmHarness vm = Mill()
            .Execute("WORKPLANE=ZX F=100", "RAPID X=0 Y=0 Z=10", verb + " X=10 Z=0 CENTER:X=0 CENTER:Z=0");

        vm.AssertNoDiagnostics();
        PlaneArc arc = LastArc(vm);
        Assert.Equal("ZX", arc.Plane.Name);
        Assert.Equal(sweep, arc.Arc.Sweep, GeometryAssert.Precision);
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(0m), vm.Position("Z"));
    }

    // VM 3.2: WORKPLANE=YZ uses the G19 orientation, CCW from Y toward Z looking against the tool axis X.
    [Fact]
    public void Arc_WorkplaneYZ_TurnsCounterclockwiseFromYTowardZ()
    {
        VmHarness vm = Mill()
            .Execute("WORKPLANE=YZ F=100", "RAPID X=0 Y=10 Z=0", "ARC=CCW Y=0 Z=10 CENTER:Y=0 CENTER:Z=0");

        vm.AssertNoDiagnostics();
        Assert.Equal("YZ", LastArc(vm).Plane.Name);
        Assert.Equal(90, LastArc(vm).Arc.Sweep, GeometryAssert.Precision);
    }

    // VM 3.2: a tool-axis word makes a helix.
    [Fact]
    public void Arc_ToolAxisWord_MakesAHelix()
    {
        VmHarness vm = Mill().Execute("F=100", "RAPID X=10 Y=0 Z=0", "ARC=CCW X=0 Y=10 Z=-2 CENTER:X=0 CENTER:Y=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(Workpiece(-2m), vm.Position("Z"));
        Assert.True(LastArc(vm).Arc.IsHelix);
        Assert.Equal(90, LastArc(vm).Arc.Sweep, GeometryAssert.Precision);
    }

    // D60, VM 3.1: under DIAMETER=ON X and the absolute CENTER:X are diameters and halved, CENTER:IX and R are radius
    // values; the three forms give the same arc around Z=0 X=10 in the ZX plane.
    [Theory]
    [InlineData("ARC=CCW X=20 Z=-10 CENTER:X=20 CENTER:Z=0")]
    [InlineData("ARC=CCW X=20 Z=-10 CENTER:IX=-10 CENTER:IZ=0")]
    [InlineData("ARC=CCW X=20 Z=-10 R=10")]
    public void Arc_UnderDiameterOn_HalvesXAndTheAbsoluteCenterXButNotCenterIXOrR(string arc)
    {
        VmHarness vm = Mill().Execute("WORKPLANE=ZX DIAMETER=ON F=0.2", "RAPID X=40 Z=0", arc);

        vm.AssertNoDiagnostics();
        GeometryAssert.Point(0, 10, LastArc(vm).Arc.Center);
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(-10m), vm.Position("Z"));
    }

    // D36: the arc tolerance is 0.0005 in under UNITS=INCH; a difference of 0.005 is beyond it.
    [Fact]
    public void Arc_UnderInchUnits_TakesTheToleranceOf00005Inch()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=INCH F=10", "RAPID X=0 Y=10.005", "ARC=CW X=10 Y=0 CENTER:X=0 CENTER:Y=0");

        Assert.Equal([DiagnosticCodes.ArcInconsistentCenter], vm.Codes());
    }

    // D36: the arc tolerance is 0.01 mm under UNITS=MM; the same difference of 0.005 is within it.
    [Fact]
    public void Arc_UnderMmUnits_TakesTheToleranceOf001Mm()
    {
        VmHarness vm = Mill().Execute("F=10", "RAPID X=0 Y=10.005", "ARC=CW X=10 Y=0 CENTER:X=0 CENTER:Y=0");

        vm.AssertNoDiagnostics();
    }

    // VM 3.2, D36: the tolerance of the configuration, carried by the options, replaces the default.
    [Fact]
    public void Arc_ToleranceOfTheOptions_ReplacesTheDefault()
    {
        VmHarness vm = new VmHarness(VmMachines.Default(), new VmOptions { ArcTolerance = 0.001m })
            .Execute("UNITS=MM F=10", "RAPID X=0 Y=10.005", "ARC=CW X=10 Y=0 CENTER:X=0 CENTER:Y=0");

        Assert.Equal([DiagnosticCodes.ArcInconsistentCenter], vm.Codes());
    }

    // VM 1, 3.2: a center from an expression is not evaluated in STATIC mode; the arc stays unresolved and its end is
    // known where the words give it.
    [Fact]
    public void Arc_CenterFromAnExpression_EndsAtTheWordsWithoutAnArc()
    {
        VmHarness vm = Mill().Execute("F=100", "RAPID X=10 Y=0", "ARC=CCW X=0 Y=10 CENTER:X={$Q1} CENTER:Y=0");

        vm.AssertNoDiagnostics();
        Assert.Null(vm.Vm.LastArc);
        Assert.Equal(Workpiece(0m), vm.Position("X"));
        Assert.Equal(Workpiece(10m), vm.Position("Y"));
    }

    // The default machine of D103 with the units and a feed set.
    private static VmHarness Mill()
    {
        return new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=1000");
    }

    private static AxisPosition Workpiece(decimal value)
    {
        return new AxisPosition(value, PositionFrame.Workpiece, Known: true);
    }

    private static PlaneArc LastArc(VmHarness vm)
    {
        Assert.NotNull(vm.Vm.LastArc);
        return vm.Vm.LastArc;
    }
}
