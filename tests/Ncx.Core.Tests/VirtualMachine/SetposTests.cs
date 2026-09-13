using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// SETPOS (language 4.2, virtual machine 3.4, D55, D101): the setpos shift such that the current position reads as the
/// declared value, known in some frame, and directly after a HOME without a reference point.
/// </summary>
public sealed class SetposTests
{
    // VM 3.4: newSetposShift = oldPos - declared; the store keeps its physical value, read through the shift.
    [Fact]
    public void Setpos_AxisKnownInTheWorkpieceFrame_RecordsTheShiftSoThePositionReadsAsDeclared()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .At("X", 50m, PositionFrame.Workpiece)
            .Execute("SETPOS X=10");

        Assert.Equal(40m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(new AxisPosition(50m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(10m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
        vm.AssertNoDiagnostics();
    }

    // D101: HOME C then SETPOS C=0 with a C axis that has home records the shift against the machine position, and C
    // is known in the workpiece frame with the declared value.
    [Fact]
    public void Setpos_AfterHomeWithAReferencePoint_RecordsTheShiftAgainstTheMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS", "HOME C", "SETPOS C=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(90m, vm.State.Frame.SetposShift["C"]);
        Assert.Equal(PositionFrame.Workpiece, vm.Position("C").Frame);
        Assert.Equal(0m, FrameRules.WorkpieceCoordinate(vm.State, "C"));
        Assert.DoesNotContain("SETPOS:C", vm.State.Unknown);
    }

    // D100, D101: without a reference point the HOME WARNING is raised, the shift stays unknown and C is known in the
    // workpiece frame as 0, not an ERROR.
    [Fact]
    public void Setpos_DirectlyAfterHomeWithoutAReferencePoint_ShiftStaysUnknownAndTheAxisReadsAsDeclared()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS", "HOME C", "SETPOS C=0");

        Assert.Equal([DiagnosticCodes.HomeWithoutReferencePoint], vm.Codes());
        Assert.False(vm.Diagnostics.HasErrors);
        Assert.Contains("SETPOS:C", vm.State.Unknown);
        Assert.Equal(new AxisPosition(0m, PositionFrame.Workpiece, Known: true), vm.Position("C"));
        Assert.Equal(0m, FrameRules.WorkpieceCoordinate(vm.State, "C"));
    }

    // D101: the ERROR remains for every other axis unknown in every frame.
    [Fact]
    public void Setpos_AxisUnknownInEveryFrame_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("SETPOS X=0");

        Assert.Equal([DiagnosticCodes.SetposAxisUnknown], vm.Codes());
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // D101: "directly after" the HOME: a block that names the axis in between ends it.
    [Fact]
    public void Setpos_NotDirectlyAfterTheHome_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS", "HOME C", "SHIFT C=5", "SETPOS C=0");

        Assert.Equal(1, vm.Count(DiagnosticCodes.SetposAxisUnknown));
    }

    // VM 4: SETPOS on the same axis replaces the setpos shift.
    [Fact]
    public void Setpos_TheSameAxisAgain_TheSecondReplacesTheShift()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "SETPOS X=100", "SETPOS X=40");

        Assert.Equal(260m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(40m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
    }

    // D60, VM 3 steps 3 and 4: DIAMETER=ON in the block applies before its verb, and the X of SETPOS is a diameter.
    [Fact]
    public void Setpos_UnderDiameterOnInTheSameBlock_TheXWordIsADiameter()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", "DIAMETER=ON SETPOS X=100");

        Assert.Equal(250m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(50m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
    }

    // VM 3.4: ORIGIN clears the setpos shift; an axis that read through an unknown shift is unknown afterwards.
    [Fact]
    public void Origin_AfterSetposWithAnUnknownShift_LeavesTheAxisUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS", "HOME C", "SETPOS C=0", "ORIGIN=1");

        Assert.Equal(AxisPosition.Unknown, vm.Position("C"));
        Assert.DoesNotContain("SETPOS:C", vm.State.Unknown);
        Assert.Equal(0m, vm.State.Frame.SetposShift["C"]);
    }

    // VM 3.4, D35, D101: ORIGIN clears a setpos shift recorded against the machine position; the axis is known in the
    // MACHINE frame at that position again and unknown in the workpiece frame, not known there at its machine value.
    [Fact]
    public void Origin_AfterSetposAgainstTheMachinePosition_LeavesTheAxisKnownInTheMachineFrameOnly()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", "SETPOS X=100");
        Assert.Contains("X", vm.State.Frame.SetposAgainstMachine);

        vm.Execute("ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Null(FrameRules.WorkpieceCoordinate(vm.State, "X"));
        Assert.Equal(0m, vm.State.Frame.SetposShift["X"]);
        Assert.Empty(vm.State.Frame.SetposAgainstMachine);
    }

    // VM 3.4: a SHIFT after the SETPOS is folded back when ORIGIN empties the chain, so the axis returns to its machine
    // position.
    [Fact]
    public void Origin_AfterSetposAgainstTheMachinePositionAndAShift_LeavesTheAxisAtItsMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "SETPOS X=100", "SHIFT X=5", "ORIGIN=1");

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 3.4, D101: SETPOS again on an axis that reads through an unknown setpos shift: newSetposShift = oldPos -
    // declared with the physical position unknown, so the shift stays unknown and the axis reads as the new value.
    [Fact]
    public void Setpos_AgainAfterSetposWithAnUnknownShift_ShiftStaysUnknownAndTheAxisReadsAsDeclared()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS", "HOME C", "SETPOS C=0", "SETPOS C=10");

        Assert.False(vm.Diagnostics.HasErrors);
        Assert.Contains("SETPOS:C", vm.State.Unknown);
        Assert.DoesNotContain("C", vm.State.Frame.SetposAgainstMachine);
        Assert.Equal(new AxisPosition(10m, PositionFrame.Workpiece, Known: true), vm.Position("C"));
        Assert.Equal(10m, FrameRules.WorkpieceCoordinate(vm.State, "C"));
    }

    // D100, D101: the machine position of C was never known; ORIGIN clears the shift and C is unknown in every frame,
    // also after a second SETPOS on it.
    [Fact]
    public void Origin_AfterASecondSetposOnAnAxisWithAnUnknownShift_LeavesTheAxisUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS", "HOME C", "SETPOS C=0", "SETPOS C=10", "ORIGIN=1");

        Assert.Equal(AxisPosition.Unknown, vm.Position("C"));
        Assert.Null(FrameRules.WorkpieceCoordinate(vm.State, "C"));
        Assert.DoesNotContain("SETPOS:C", vm.State.Unknown);
        Assert.Equal(0m, vm.State.Frame.SetposShift["C"]);
    }

    // VM 3.4, D35: the machine frame does not move with the workpiece frame; an axis known in the MACHINE frame stays
    // known there when ORIGIN clears an unknown setpos shift.
    [Fact]
    public void Origin_AxisKnownInTheMachineFrameWithAnUnknownShift_KeepsTheMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "SETPOS X={$Q1}", "HOME X", "ORIGIN=1");

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.DoesNotContain("SETPOS:X", vm.State.Unknown);
    }

    // Code-guidelines 7: the snapshot keeps the axes whose setpos shift was recorded against the machine position
    // while ORIGIN clears them in the live state.
    [Fact]
    public void SetposAgainstMachine_ClearedAfterASnapshot_SnapshotKeepsTheAxis()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", "SETPOS X=100");

        ChannelSnapshot snapshot = vm.State.Snapshot();
        vm.Execute("ORIGIN=1");

        Assert.Equal(["X"], snapshot.Frame.SetposAgainstMachine.Keys);
        Assert.Empty(vm.State.Frame.SetposAgainstMachine);
    }

    // VM 1, 3.4, D101: a declared value from an expression is not evaluated in STATIC mode, so the shift is UNKNOWN;
    // SETPOS moves nothing, and an axis known in the MACHINE frame stays known there.
    [Fact]
    public void Setpos_FromAnExpression_LeavesTheShiftUnknownAndTheAxisAtItsMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", "SETPOS X={$Q1}");

        Assert.Contains("SETPOS:X", vm.State.Unknown);
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Null(FrameRules.WorkpieceCoordinate(vm.State, "X"));
    }

    // VM 3.4, D101: ORIGIN clears the unknown setpos shift; the axis is still known in the MACHINE frame, and the next
    // SETPOS records against its machine position.
    [Fact]
    public void Origin_AfterSetposFromAnExpression_KeepsTheMachinePositionForTheNextSetpos()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", "SETPOS X={$Q1}", "ORIGIN=1");

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=5");

        vm.AssertNoDiagnostics();
        Assert.Equal(295m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(5m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
    }

    // VM 1, 3.4, D101: SETPOS from an expression on an axis whose setpos shift was recorded against the machine
    // position: the shift is UNKNOWN and nothing moved, so the axis is back at its machine position, with the SHIFT
    // after the first SETPOS taken out of the store again.
    [Fact]
    public void Setpos_FromAnExpressionAfterSetposAgainstTheMachinePosition_ReturnsTheAxisToItsMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "SETPOS X=100", "SHIFT X=5", "SETPOS X={$Q1}");

        Assert.Contains("SETPOS:X", vm.State.Unknown);
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.DoesNotContain("X", vm.State.Frame.SetposAgainstMachine);
    }

    // VM 3.4, D35, D101: TILT, TILT_AXIS, ROTATE and MIRROR, and their RESET forms, mark the position unknown in the
    // new frame; the machine frame does not move with it, so an axis whose setpos shift was recorded against the
    // machine position is back at that position in the MACHINE frame, and the next SETPOS records against it again.
    // The record stays as long as the setpos shift does (second review of P1-03, finding 2).
    [Theory]
    [InlineData("ROTATE=30", "ROTATE=RESET")]
    [InlineData("MIRROR=X", "MIRROR=OFF")]
    [InlineData("TILT B=45", "TILT=RESET")]
    [InlineData("TILT_AXIS B=45", "TILT_AXIS=RESET")]
    public void FrameChange_AfterSetposAgainstTheMachinePosition_ReturnsTheAxisToItsMachinePosition(string change,
        string reset)
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", "SETPOS X=100", change);

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Contains("X", vm.State.Frame.SetposAgainstMachine);

        vm.Execute(reset);

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=50");

        vm.AssertNoDiagnostics();
        Assert.Equal(250m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(50m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
    }

    // VM 3.4, D101: the machine position of the axis is the store with the shifts the store took in since the SETPOS
    // taken out again: a SHIFT after the SETPOS is in the store, one before it never was.
    [Theory]
    [InlineData("SHIFT X=5", "SETPOS X=100")]
    [InlineData("SETPOS X=100", "SHIFT X=5")]
    public void Tilt_AfterSetposAgainstTheMachinePositionAndAShift_ReturnsTheAxisToItsMachinePosition(string first,
        string second)
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", first, second, "TILT B=45");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 3.4, D57: a WORKPIECE change marks the position unknown in the new holder's frame, not in the MACHINE frame.
    [Fact]
    public void Workpiece_AnotherHolderAfterSetposAgainstTheMachinePosition_ReturnsTheAxisToItsMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "SETPOS X=100", "WORKPIECE=SUB");

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=50");

        vm.AssertNoDiagnostics();
        Assert.Equal(250m, vm.State.Frame.SetposShift["X"]);
    }

    // VM 1, 3.4: a SHIFT from an expression leaves the workpiece coordinate unknown and moves nothing; the axis whose
    // setpos shift was recorded against the machine position is back at that position.
    [Fact]
    public void Shift_FromAnExpressionAfterSetposAgainstTheMachinePosition_ReturnsTheAxisToItsMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "SETPOS X=100", "SHIFT X={$Q1}");

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SHIFT=RESET", "SETPOS X=50");

        vm.AssertNoDiagnostics();
        Assert.Equal(250m, vm.State.Frame.SetposShift["X"]);
    }

    // D101, VM 2.9: a SETPOS after a WORKPIECE change is no ERROR, and the STATIC run goes on.
    [Fact]
    public void Setpos_AgainAfterAWorkpieceChangeInAStaticRun_IsNoErrorAndTheRunGoesOn()
    {
        string text = VmHarness.File("UNITS=MM", "HOME X Z", "SETPOS X=100 Z=0", "WORKPIECE=SUB", "SETPOS X=100 Z=0",
            "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        Assert.Equal(0, vm.Count(DiagnosticCodes.SetposAxisUnknown));
        Assert.False(vm.Diagnostics.HasErrors, vm.Diagnostics.ToText());
        Assert.False(vm.Result!.Stopped);
    }

    // D101: the shift is machinePos minus declared also with a SHIFT in the chain, and the record keeps the shifts of
    // the chain that stood on the axis at the SETPOS.
    [Fact]
    public void Setpos_AgainstTheMachinePositionAfterAShift_RecordsTheShiftsThatStoodBeforeIt()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", "SHIFT X=5", "SETPOS X=100");

        vm.AssertNoDiagnostics();
        Assert.Equal(200m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(100m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
        Assert.Equal(5m, vm.State.Frame.SetposAgainstMachine["X"].ChainShift);
    }

    // VM 3.4, D101: a SHIFT that stood in the chain before the SETPOS was never folded into the axis, which was known
    // in the MACHINE frame then; ORIGIN folds it back into the store, and the axis still returns to its machine
    // position, where nothing moved.
    [Fact]
    public void Origin_AfterAShiftThenSetposAgainstTheMachinePosition_LeavesTheAxisAtItsMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "SHIFT X=5", "SETPOS X=100", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 3.4: SHIFT=RESET folds the removed shift back into the position, so the workpiece coordinate read through
    // the setpos shift grows by it; the machine position stays, and ORIGIN returns the axis to it.
    [Fact]
    public void Origin_AfterAShiftThenSetposAgainstTheMachinePositionAndShiftReset_LeavesTheAxisAtItsMachinePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM", "HOME X", "SHIFT X=5", "SETPOS X=100", "SHIFT=RESET");

        Assert.Equal(105m, FrameRules.WorkpieceCoordinate(vm.State, "X"));

        vm.Execute("ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // D101: in a STATIC run the SETPOS after ORIGIN records its shift against the machine position the axis has.
    [Fact]
    public void Setpos_AfterAShiftASetposAndOriginInAStaticRun_RecordsAgainstTheMachinePosition()
    {
        string text = VmHarness.File(
            "UNITS=MM", "HOME X", "SHIFT X=5", "SETPOS X=100", "ORIGIN=1", "SETPOS X=0", "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        vm.AssertNoDiagnostics();
        Assert.Equal(300m, vm.State.Frame.SetposShift["X"]);
    }

    // VM 3.4: a coordinate of the workpiece frame goes into the store through the setpos shift; with the shift unknown
    // (D101) the store holds the coordinate itself.
    [Fact]
    public void WorkpiecePosition_Coordinate_IsStoredThroughTheSetposShift()
    {
        VmHarness known = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME X", "SETPOS X=100");
        VmHarness unknown = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:MAIN=AXIS", "HOME C", "SETPOS C=0");

        Assert.Equal(220m, FrameRules.WorkpiecePosition(known.State, "X", 20m).Value);
        Assert.Equal(20m, FrameRules.WorkpiecePosition(unknown.State, "C", 20m).Value);
    }
}
