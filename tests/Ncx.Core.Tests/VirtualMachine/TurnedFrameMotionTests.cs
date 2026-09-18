using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// Motions in a frame that a ROTATE, TILT or TILT_AXIS turns against the machine frame, and the axes such a motion does
/// not name: "A motion that names some axes leaves the others as they were" (virtual machine 3.4). That holds for an
/// axis known in the MACHINE frame and for one whose setpos shift was recorded against its machine position alike,
/// because after SETPOS "the position store keeps its physical value" (3.4, D101). The rest of what the motions do to
/// the record of a setpos shift is in SetposMotionTests.
/// </summary>
public sealed class TurnedFrameMotionTests
{
    // Review of RR-P1-03 (VM 3.4): a motion of Y under ROTATE=30 names neither X nor Z, so both stay known in the
    // MACHINE frame where they were.
    [Fact]
    public void Rotate_AMotionOfTheOtherAxisOfThePlane_LeavesAnAxisKnownInTheMachineFrameAsItWas()
    {
        VmHarness vm = MillTurn().Execute("ROTATE=30", "LINE Y=5");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // VM 3.4: the same for a motion along the tool axis of the ROTATE, and for a motion of a rotary axis.
    [Theory]
    [InlineData("LINE Z=-5")]
    [InlineData("RAPID C=10")]
    public void Rotate_AMotionThatMovesNoAxisOfThePlane_KeepsTheAxisInTheMachineFrame(string motion)
    {
        VmHarness vm = MillTurn().Execute("SPINDLE_MODE:MAIN=AXIS", "ROTATE=30", motion);

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 1, 3.4: a target from an expression moves Y to a position the VM does not know; X, which the block does not
    // name, stays where it was in the MACHINE frame.
    [Fact]
    public void Rotate_AMotionToATargetFromAnExpression_LeavesTheOtherAxisOfThePlaneAsItWas()
    {
        VmHarness vm = MillTurn().Execute("ROTATE=30", "LINE Y={$Q1}");

        Assert.Equal(AxisPosition.Unknown, vm.Position("Y"));
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 3.4, D35: a FRAME=MACHINE block moves in machine coordinates, past the turned frame, so a motion of Y there
    // leaves X where it is.
    [Fact]
    public void Rotate_AMachineFrameMove_KeepsTheOtherAxisInTheMachineFrame()
    {
        VmHarness vm = MillTurn().Execute("ROTATE=30", "RAPID Y=5 FRAME=MACHINE");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // Review of RR-P1-03 (VM 3.4): a motion of Y under TILT or TILT_AXIS names none of X, Z, the sub spindle slide Z2
    // and the rotary axis B, so each stays known in the MACHINE frame where it was.
    [Theory]
    [InlineData("TILT B=45")]
    [InlineData("TILT_AXIS B=45")]
    public void Tilt_AMotionOfALinearAxis_LeavesTheAxesKnownInTheMachineFrameAsTheyWere(string tilt)
    {
        VmHarness vm = MillTurn().Execute(tilt, "LINE Y=5");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));
        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), vm.Position("Z2"));
        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), vm.Position("B"));
    }

    // VM 3.4: the same for a motion of a rotary axis under a TILT.
    [Fact]
    public void Tilt_AMotionOfARotaryAxis_KeepsTheLinearAxesInTheMachineFrame()
    {
        VmHarness vm = MillTurn().Execute("SPINDLE_MODE:MAIN=AXIS", "TILT B=45", "RAPID C=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // Language 4.2, VM 3.4: a MIRROR and a SHIFT act on each axis by itself, and a motion of Y leaves X where it is.
    [Theory]
    [InlineData("MIRROR=X")]
    [InlineData("SHIFT X=5")]
    public void MirrorAndShift_AMotionOfAnotherAxis_KeepsTheAxisInTheMachineFrame(string change)
    {
        VmHarness vm = MillTurn().Execute(change, "LINE Y=5");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // Review of RR-P1-03, probe 1 (VM 3.4, D35, D101): X stays known in the MACHINE frame through the motion of Y and
    // through the reset, which marks only positions outside the MACHINE frame unknown, so SETPOS X=0 records against
    // the machine position 300 without the ERROR VM050.
    [Fact]
    public void Rotate_AMotionOfYAndTheReset_SetposOfXRecordsAgainstItsMachinePosition()
    {
        VmHarness vm = MillTurn().Execute("ROTATE=30", "LINE Y=5", "ROTATE=RESET", "SETPOS X=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(300m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(0m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
    }

    // Review of RR-P1-03, probe 2 (VM 3.1, 3.4, D35): Z stays known in the MACHINE frame at 450 through a motion of X
    // under a TILT, so an incremental word of Z in a FRAME=MACHINE block adds to it without the ERROR VM202.
    [Fact]
    public void Tilt_AMotionOfXAndAnIncrementalMachineFrameMoveOfZ_AddsToTheMachinePosition()
    {
        VmHarness vm = MillTurn().Execute("TILT B=45", "LINE X=10", "RAPID IZ=5 FRAME=MACHINE");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(455m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // Review of RR-P1-03, probe 3 (VM 3.4, D102): the first motion under POLAR=ON puts X and C into the polar frame;
    // Z, which the block does not name, stays known in the MACHINE frame, also under a ROTATE of the ZX plane.
    [Fact]
    public void Polar_AFirstMotionUnderARotateOfTheZxPlane_LeavesZKnownInTheMachineFrame()
    {
        VmHarness vm = MillTurn()
            .Execute("SPINDLE_MODE:MAIN=AXIS", "WORKPLANE=ZX ROTATE=30", "WORKPLANE=XY", "POLAR=ON", "LINE C=5");

        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // Review of the RR-P1-03 review fixes, the record-path twin of probe 1 (VM 3.4, D35, D101): SETPOS X=100 under the
    // ROTATE records against the machine position 300, which the store keeps. The motion of Y does not name X and
    // leaves it as it was, so the reset returns X to 300 in the MACHINE frame; SETPOS X=0 records against it without
    // the ERROR VM050, and an incremental word of X in a FRAME=MACHINE block adds to it without the ERROR VM202.
    [Theory]
    [InlineData("SETPOS X=0", 300)]
    [InlineData("RAPID IX=5 FRAME=MACHINE", 305)]
    public void Rotate_AMotionOfYAfterSetposOfX_TheResetReturnsXToItsMachinePosition(string next, int machine)
    {
        VmHarness vm = MillTurn().Execute("HOME X", "ROTATE=30", "SETPOS X=100", "LINE Y=5", "ROTATE=RESET");

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute(next);

        vm.AssertNoDiagnostics();
        Assert.Equal((decimal?)machine, FrameRules.MachineCoordinate(vm.State, "X"));
    }

    // Review of the RR-P1-03 review fixes, the record-path twin of probe 2 (VM 3.4, D35, D101): SETPOS Z=100 under the
    // TILT records against the machine position 450; the motion of X does not name Z, so the reset returns Z to 450 in
    // the MACHINE frame, and SETPOS Z=0 or an incremental machine-frame move of Z uses it without an ERROR.
    [Theory]
    [InlineData("SETPOS Z=0", 450)]
    [InlineData("RAPID IZ=5 FRAME=MACHINE", 455)]
    public void Tilt_AMotionOfXAfterSetposOfZ_TheResetReturnsZToItsMachinePosition(string next, int machine)
    {
        VmHarness vm = MillTurn().Execute("HOME Z", "TILT B=45", "SETPOS Z=100", "LINE X=10", "TILT=RESET");

        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));

        vm.Execute(next);

        vm.AssertNoDiagnostics();
        Assert.Equal((decimal?)machine, FrameRules.MachineCoordinate(vm.State, "Z"));
    }

    // Review of the RR-P1-03 review fixes, the record-path twin of probe 3 (VM 3.4, D101, D102): SETPOS Z=100 under a
    // ROTATE of the ZX plane records against the machine position 450. The first motion under POLAR=ON does not name
    // Z, so Z keeps its workpiece coordinate and its machine position; after POLAR=OFF the reset returns Z to 450 in
    // the MACHINE frame, and SETPOS Z=0 records against it without the ERROR VM050.
    [Fact]
    public void Polar_AFirstMotionUnderARotateOfTheZxPlane_KeepsTheMachinePositionOfZThroughItsRecord()
    {
        VmHarness vm = MillTurn().Execute("SPINDLE_MODE:MAIN=AXIS", "HOME Z", "WORKPLANE=ZX ROTATE=30",
            "SETPOS Z=100", "WORKPLANE=XY", "POLAR=ON", "LINE C=5");

        Assert.Equal(100m, FrameRules.WorkpieceCoordinate(vm.State, "Z"));
        Assert.Equal(450m, FrameRules.MachineCoordinate(vm.State, "Z"));

        vm.Execute("POLAR=OFF", "ROTATE=RESET", "SETPOS Z=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(450m, vm.State.Frame.SetposShift["Z"]);
    }

    // The mill-turn machine file, whose X (home 300), Z (home 450), B (home 0), C (home 90) and Z2 (home 0) start known
    // in the MACHINE frame (VM 2.2, 3.4, D100), with the units, a feed and the tool spindle set.
    private static VmHarness MillTurn()
    {
        return new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM F=100 SPINDLE:TOOL=CW");
    }
}
