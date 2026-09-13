using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// Linear motion (virtual machine 3.1): the targets of absolute and incremental words in the frame the words program
/// in, UNITS before the first motion, a feed for LINE, one form per axis (language 4.3), the suppression inside a
/// subprogram that no program calls (3.9, D99); and HOME Z with and without a reference point (3 step 5, D100).
/// </summary>
public sealed class MotionTargetTests
{
    // VM 3.1: X= replaces; the target is known in the workpiece frame, and an axis the block does not name stays.
    [Fact]
    public void Rapid_AbsoluteWords_MoveTheAxesToTheirTargetsInTheWorkpieceFrame()
    {
        VmHarness vm = Mill().Execute("RAPID X=50.4 Y=-7.025");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(50.4m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(-7.025m, PositionFrame.Workpiece, Known: true), vm.Position("Y"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 3.1: IX= adds to the current value; axes not mentioned keep their value.
    [Fact]
    public void Line_IncrementalWord_AddsToTheCurrentValueAndTheOtherAxesKeepTheirs()
    {
        VmHarness vm = Mill().Execute("RAPID X=0 Y=0 Z=2", "LINE IX=30 F=800");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(30m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(0m, PositionFrame.Workpiece, Known: true), vm.Position("Y"));
        Assert.Equal(new AxisPosition(2m, PositionFrame.Workpiece, Known: true), vm.Position("Z"));
    }

    // VM 3.1, 5: IX from an unknown position is an ERROR, and the axis stays unknown.
    [Fact]
    public void Line_IncrementalWordFromAnUnknownPosition_IsAnError()
    {
        VmHarness vm = Mill().Execute("F=100", "LINE IX=30");

        Assert.Equal([DiagnosticCodes.IncrementalFromUnknownPosition], vm.Codes());
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // VM 3.1, D35: after HOME the axis is known in the MACHINE frame only, and the current value in the workpiece
    // frame the block programs in is unknown.
    [Fact]
    public void Line_IncrementalWordAfterHome_IsAnErrorInTheWorkpieceFrame()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM F=100", "HOME X", "LINE IX=5");

        Assert.Equal([DiagnosticCodes.IncrementalFromUnknownPosition], vm.Codes());
    }

    // VM 3.4, D35: a FRAME=MACHINE block moves in machine coordinates, an incremental word adds to the position in the
    // MACHINE frame, and the moved axes are known in the MACHINE frame afterwards.
    [Fact]
    public void Rapid_FrameMachine_MovesInMachineCoordinates()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "RAPID IX=-10 Z=0 FRAME=MACHINE");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(290m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // VM 3.1, 5: LINE with feed.value none is an ERROR; RAPID needs no feed.
    [Fact]
    public void Line_WithoutAFeed_IsAnError()
    {
        VmHarness vm = Mill().Execute("RAPID X=0", "LINE X=5");

        Assert.Equal([DiagnosticCodes.LineWithoutFeed], vm.Codes());
    }

    // VM 1, 3.1: a feed from an expression is set with its value unknown, so LINE has a feed.
    [Fact]
    public void Line_FeedFromAnExpression_CountsAsAFeed()
    {
        VmHarness vm = Mill().Execute("F={$Q1}", "RAPID X=0", "LINE X=5");

        vm.AssertNoDiagnostics();
    }

    // VM 3.1, 5, language 4.1: a motion before UNITS is an ERROR; HOME moves as well (language 2 rule 2).
    [Theory]
    [InlineData("RAPID X=0")]
    [InlineData("LINE X=0 F=100")]
    [InlineData("HOME Z")]
    [InlineData("RETRACT")]
    public void Motion_BeforeUnits_IsAnError(string motion)
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute(motion);

        Assert.Equal([DiagnosticCodes.MotionBeforeUnits], vm.Codes());
    }

    // VM 1, 3.1: a word from an expression is not evaluated in STATIC mode, and the axis is unknown afterwards.
    [Fact]
    public void Rapid_WordFromAnExpression_LeavesTheAxisUnknown()
    {
        VmHarness vm = Mill().Execute("RAPID X=5", "RAPID X={$Q1}");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // D60: under DIAMETER=ON X and IX are diameters, halved on the way into the position store; Z is a length.
    [Fact]
    public void Line_UnderDiameterOn_HalvesXAndIX()
    {
        VmHarness vm = Mill().Execute("WORKPLANE=ZX DIAMETER=ON", "RAPID X=40 Z=2", "LINE IX=-10 Z=-60 F=0.2");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(15m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(-60m, PositionFrame.Workpiece, Known: true), vm.Position("Z"));
    }

    // Language 4.3: absolute and incremental words may be mixed in one block, one form per axis.
    [Fact]
    public void Rapid_TwoFormsOfOneAxis_IsAnError()
    {
        VmHarness vm = Mill().Execute("RAPID X=0", "RAPID X=5 IX=5");

        Assert.Equal([DiagnosticCodes.TwoFormsOfOneAxis], vm.Codes());
    }

    // Language 4.3: the forms of different axes mix in one block.
    [Fact]
    public void Rapid_AbsoluteAndIncrementalWordsOnDifferentAxes_AreAllowed()
    {
        VmHarness vm = Mill().Execute("RAPID X=0 Y=0", "RAPID X=5 IY=5");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(5m, PositionFrame.Workpiece, Known: true), vm.Position("Y"));
    }

    // VM 3.4, D101: a coordinate of the workpiece frame goes into the store through the setpos shift.
    [Fact]
    public void Line_AfterSetposAgainstTheMachinePosition_StoresTheCoordinateThroughTheShift()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM F=100", "HOME X", "SETPOS X=100", "LINE X=50");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(250m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(50m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
    }

    // VM 3.1, 3.9, 5, D99: inside a subprogram that no program calls, motion before UNITS, LINE without feed and IX
    // from an unknown position are suppressed, and the incremental word leaves the position unknown, as the SETPOS
    // after it shows: SETPOS on an axis unknown in every frame is a rule that stays.
    [Fact]
    public void UncalledSub_TheCallerRulesOfMotion_AreSuppressedAndIXLeavesThePositionUnknown()
    {
        string text = VmHarness.File("PROGRAM=END", "SUB=BEGIN NAME=10", "LINE IX=30", "SETPOS X=0", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal([DiagnosticCodes.SetposAxisUnknown], vm.Codes());
    }

    // VM 3.1, D99: at a CALL the subprogram runs with the caller's state, and the rules apply there as anywhere else.
    [Fact]
    public void CalledSub_IncrementalWordFromAnUnknownPosition_IsAnError()
    {
        string text = VmHarness.File(
            "UNITS=MM", "CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "LINE IX=30 F=100", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal([DiagnosticCodes.IncrementalFromUnknownPosition], vm.Codes());
        Assert.True(vm.Result!.Stopped);
    }

    // INCREMENTAL_SUB note 4, D99: the subprogram is walked at each of the four passes of its CALL with the state the
    // pass before left, so its IX words resolve from the known position and the row ends at X=0 Y=60.
    [Fact]
    public void IncrementalSub_CallWithTimes4_EndsTheRowAtX0Y60()
    {
        string text = VmHarness.File(
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "RAPID X=0 Y=0",
            "RAPID Z=2",
            "CALL=100 TIMES=4",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "LINE Z=-3 F=200",
            "LINE IX=30 F=800",
            "RAPID Z=2",
            "RAPID IX=-30 IY=15",
            "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(0m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(60m, PositionFrame.Workpiece, Known: true), vm.Position("Y"));
        Assert.Equal(new AxisPosition(2m, PositionFrame.Workpiece, Known: true), vm.Position("Z"));
    }

    // D100: HOME Z on an axis without a reference point is a WARNING, and Z is unknown in every frame afterwards.
    [Fact]
    public void HomeZ_WithoutAReferencePoint_WarnsAndLeavesZUnknown()
    {
        VmHarness vm = Mill().Execute("RAPID Z=5", "HOME Z");

        Assert.Equal([DiagnosticCodes.HomeWithoutReferencePoint], vm.Codes());
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 3 step 5, D100: with home in the configuration Z is known in the MACHINE frame at the reference point.
    [Fact]
    public void HomeZ_WithAReferencePoint_IsKnownInTheMachineFrameAtIt()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "RAPID Z=5", "HOME Z");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // The default machine of D103 with the units set, the start of most tests here.
    private static VmHarness Mill()
    {
        return new VmHarness(VmMachines.Default()).Execute("UNITS=MM");
    }
}
