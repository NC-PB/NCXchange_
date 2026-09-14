using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The setpos shift recorded against the machine position (virtual machine 3.4, D101) and the motions: the record
/// stays while the axis moves, so ORIGIN and a change of the frame find the machine position the motions reached
/// (D35); and the two findings of the second review of P1-02, checked against the motions.
/// </summary>
public sealed class SetposMotionTests
{
    // VM 3.4, D101: a motion in the workpiece frame moves the machine position by as much; ORIGIN returns the axis to
    // the machine position the motion reached.
    [Fact]
    public void Origin_AfterSetposAgainstTheMachinePositionAndAMotion_ReturnsTheAxisWhereTheMotionLeftIt()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", "LINE X=50", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 3 step 5, 3.4, D101: HOME leaves the record alone, and the next motion with a known coordinate makes the axis
    // known in the workpiece frame again; its machine position follows from the record.
    [Fact]
    public void Origin_AfterHomeAndAMotionInTheWorkpieceFrame_FindsTheMachinePositionThroughTheRecord()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", "HOME X", "LINE X=50", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 3.4, D35, D101: the same after a FRAME=MACHINE move; a TILT marks the position unknown in the new frame, the
    // axis returns to its machine position, and the next SETPOS records against it.
    [Fact]
    public void Tilt_AfterAMachineFrameMoveAndAMotionInTheWorkpieceFrame_ReturnsTheAxisToItsMachinePosition()
    {
        VmHarness vm = MillTurn()
            .Execute("HOME X", "SETPOS X=100", "RAPID X=50 FRAME=MACHINE", "LINE X=80", "TILT B=45");

        Assert.Equal(new AxisPosition(280m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(270m, vm.State.Frame.SetposShift["X"]);
    }

    // Second review of P1-02, finding 2, settled by virtual machine 3.4, "a RESET that removes shifts folds them
    // back": SHIFT=RESET of a shift that stood before the SETPOS folds it back, as for every position known outside
    // the MACHINE frame, so X reads 105; nothing moved, and a motion and ORIGIN find the machine position.
    [Fact]
    public void ShiftReset_OfAShiftBeforeTheSetpos_FoldsItBackAndTheMachinePositionFollowsTheMotions()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SHIFT X=5", "SETPOS X=100", "SHIFT=RESET");

        Assert.Equal(105m, FrameRules.WorkpieceCoordinate(vm.State, "X"));

        vm.Execute("LINE X=100", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(295m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // Second review of P1-02, finding 1 (VM 3.4, D101): a change of the workpiece frame, and SETPOS or SHIFT from an
    // expression, after motions return the axis to its machine position, and a later SETPOS records against it
    // without the ERROR VM050.
    [Theory]
    [InlineData("ROTATE=30")]
    [InlineData("MIRROR=X")]
    [InlineData("TILT B=45")]
    [InlineData("WORKPIECE=SUB")]
    [InlineData("SHIFT X={$Q1}")]
    [InlineData("SETPOS X={$Q1}")]
    public void FrameChange_AfterSetposAndAMotion_KeepsTheMachinePositionForTheNextSetpos(string change)
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", "LINE X=50", change);

        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(240m, vm.State.Frame.SetposShift["X"]);
    }

    // D102, D101: a transformation is no change of the frame. After a motion under POLAR, POLAR=OFF leaves C unknown in
    // every frame, and the next motion with a known coordinate finds the machine position through the record again.
    [Fact]
    public void PolarOff_AfterSetposAgainstTheMachinePosition_TheNextMotionFindsTheMachinePositionAgain()
    {
        VmHarness vm = MillTurn().Execute(
            "SPINDLE_MODE:MAIN=AXIS", "HOME C", "SETPOS C=0", "POLAR=ON", "LINE X=20 C=5", "POLAR=OFF");

        Assert.Equal(AxisPosition.Unknown, vm.Position("C"));

        vm.Execute("RAPID C=10");

        Assert.Equal(10m, FrameRules.WorkpieceCoordinate(vm.State, "C"));

        vm.Execute("ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(100m, PositionFrame.Machine, Known: true), vm.Position("C"));
    }

    // Second review of P1-03, finding 2 (VM 3.4, D101): changes of the frame before the motion, each followed by the
    // change that reaches the axis after it. The record stays with the setpos shift, so the motion makes the axis known
    // in the workpiece frame through it again.
    public static TheoryData<string[], string> DetoursWithAChange => new()
    {
        { ["ROTATE=30", "ROTATE=RESET"], "TILT B=45" },
        { ["HOME X", "WORKPIECE=SUB", "WORKPIECE=MAIN"], "MIRROR=X" },
        { ["HOME X", "TILT B=0", "TILT=RESET"], "ROTATE=30" },
        { ["MIRROR=X", "MIRROR=OFF"], "WORKPIECE=SUB" },
    };

    // The detours of DetoursWithAChange, for ORIGIN.
    public static TheoryData<string[]> Detours => new()
    {
        { ["ROTATE=30", "ROTATE=RESET"] },
        { ["WORKPIECE=SUB", "WORKPIECE=MAIN"] },
        { ["HOME X", "TILT B=0", "TILT=RESET"] },
        { ["HOME X", "WORKPIECE=SUB", "WORKPIECE=MAIN"] },
    };

    // Second review of P1-03, finding 2 (VM 3.4, D101): a change of the frame before the motion keeps the record; the
    // change after the motion returns the axis to the machine position the motion reached, and the next SETPOS records
    // against it without the ERROR VM050.
    [Theory]
    [MemberData(nameof(DetoursWithAChange))]
    public void FrameChange_BeforeTheMotion_KeepsTheRecordForTheNextSetpos(string[] detour, string change)
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100").Execute(detour).Execute("LINE X=50", change);

        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(240m, vm.State.Frame.SetposShift["X"]);
    }

    // Second review of P1-03, finding 2 (VM 3.4, D35, D101): ORIGIN clears the setpos shift and its record, and the
    // axis is known in the MACHINE frame at its machine position and unknown in the workpiece frame, also after a
    // change of the frame before the motion.
    [Theory]
    [MemberData(nameof(Detours))]
    public void Origin_AfterAChangeOfTheFrameBeforeTheMotion_ReturnsTheAxisToTheMachineFrame(string[] detour)
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100").Execute(detour).Execute("LINE X=50", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));
        Assert.Null(FrameRules.WorkpieceCoordinate(vm.State, "X"));
        Assert.Empty(vm.State.Frame.SetposAgainstMachine);
    }

    // VM 3.4, 10, D101: a motion under ROTATE, MIRROR, TILT or TILT_AXIS runs in a frame turned against the machine
    // frame, which only the kinematics module converts; the machine position of the axis is not known afterwards, so
    // the reset leaves the axis unknown in every frame instead of returning it to a wrong machine position. The record
    // stays with the setpos shift.
    [Theory]
    [InlineData("ROTATE=30", "ROTATE=RESET")]
    [InlineData("MIRROR=X", "MIRROR=OFF")]
    [InlineData("TILT B=45", "TILT=RESET")]
    [InlineData("TILT_AXIS B=45", "TILT_AXIS=RESET")]
    public void Reset_AfterAMotionInATurnedFrame_LeavesTheAxisUnknown(string change, string reset)
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", change, "LINE X=50", reset);

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Contains("X", vm.State.Frame.SetposAgainstMachine);
    }

    // VM 3.4, D101: the setpos shift still stands after the reset, so the next motion with a known coordinate in a
    // frame that relates to the machine frame through shifts alone finds the machine position through the record.
    [Fact]
    public void Motion_AfterTheResetOfATurnedFrame_FindsTheMachinePositionThroughTheRecordAgain()
    {
        VmHarness vm = MillTurn()
            .Execute("HOME X", "SETPOS X=100", "ROTATE=30", "LINE X=50", "ROTATE=RESET", "LINE X=60", "TILT B=45");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(260m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 3.4, D101: SETPOS under a rotation records against the machine position, and while nothing moves the reset
    // returns the axis there.
    [Fact]
    public void Reset_AfterSetposInATurnedFrameWithoutAMotion_ReturnsTheAxisToItsMachinePosition()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "ROTATE=30", "SETPOS X=100", "ROTATE=RESET");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // The workaround of the TODO(question) above FrameRules.Couples (D101): the documents do not say whether a motion
    // of Y in a rotated plane moves X in the machine frame, so the machine position that the record gives for X is
    // taken as unknown, and the reset leaves X unknown. Its stored position stays as it was (VM 3.4).
    [Fact]
    public void Reset_AfterAMotionOfAnotherAxisInATurnedFrame_LeavesTheAxisUnknown()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "ROTATE=30", "SETPOS X=100", "LINE Y=5", "ROTATE=RESET");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // VM 3.4, D101: ORIGIN clears the setpos shift of an axis whose machine position a motion in a turned frame left
    // unknown; the axis is unknown in every frame afterwards.
    [Fact]
    public void Origin_AfterAMotionInATurnedFrame_LeavesTheAxisUnknown()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", "ROTATE=30", "LINE X=50", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Empty(vm.State.Frame.SetposAgainstMachine);
    }

    // VM 3.4, D101: SETPOS on an axis whose machine position is not known records the new shift against its workpiece
    // position; the record of the earlier shift goes with that shift, and the reset leaves the axis unknown.
    [Fact]
    public void Setpos_AfterAMotionInATurnedFrame_RecordsAgainstTheWorkpiecePositionAndDropsTheRecord()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", "ROTATE=30", "LINE X=50", "SETPOS X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(10m, FrameRules.WorkpieceCoordinate(vm.State, "X"));
        Assert.DoesNotContain("X", vm.State.Frame.SetposAgainstMachine);

        vm.Execute("ROTATE=RESET");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // VM 3.4, D57, D101: the frame of another workpiece holder relates to the machine frame through the machine's
    // mirror or datum convention, which the VM does not apply; a motion there leaves the machine position unknown, and
    // a motion under the default holder, which took the SETPOS, finds it through the record again.
    [Fact]
    public void Workpiece_AMotionUnderAnotherHolder_LeavesTheMachinePositionUnknownUntilOneUnderTheDefaultHolder()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", "WORKPIECE=SUB", "LINE X=50", "WORKPIECE=MAIN");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));

        vm.Execute("LINE X=60", "TILT B=45");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(260m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 1, 3.4, wave-1 question #100: a SHIFT from an expression on the axis is unknown in STATIC mode, so after a
    // motion under it the machine position is not known, and the change of the frame leaves the axis unknown.
    [Fact]
    public void Shift_FromAnExpressionOnTheAxis_AMotionUnderItLeavesTheMachinePositionUnknown()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", "SHIFT X={$Q1}", "LINE X=50", "TILT B=45");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Contains("X", vm.State.Frame.SetposAgainstMachine);
    }

    // VM 3.4, D101: once the SHIFT from an expression is reset, the next motion finds the machine position through the
    // record, and the next SETPOS records against it.
    [Fact]
    public void Shift_FromAnExpressionResetBeforeTheMotion_TheNextSetposRecordsAgainstTheMachinePosition()
    {
        VmHarness vm = MillTurn().Execute(
            "HOME X", "SETPOS X=100", "SHIFT X={$Q1}", "SHIFT=RESET", "LINE X=50", "TILT B=45", "SETPOS X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(240m, vm.State.Frame.SetposShift["X"]);
    }

    // Second review of P1-03, finding 1 (VM 3.4, D101, D102): the first motion under POLAR=ON that does not name X
    // leaves it unknown and keeps the record; after POLAR=OFF the next motion finds the machine position through it.
    [Fact]
    public void Polar_AFirstMotionThatDoesNotNameTheAxis_KeepsTheRecordForTheMotionAfterPolarOff()
    {
        VmHarness vm = MillTurn().Execute("SPINDLE_MODE:MAIN=AXIS", "HOME X", "SETPOS X=100", "POLAR=ON", "LINE C=5");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Contains("X", vm.State.Frame.SetposAgainstMachine);

        vm.Execute("POLAR=OFF", "LINE X=50", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // Third review of P1-03, finding 1 (VM 1, 3.4, D101; language 4.2): SETPOS declares the position in the active
    // workpiece frame, which holds a SHIFT from an expression. Once that shift is reset, a motion moves the machine to
    // a position off by the unknown shift, so ORIGIN and a change of the frame leave X unknown instead of returning it
    // to a wrong machine position.
    [Theory]
    [InlineData("ORIGIN=1")]
    [InlineData("TILT B=45")]
    public void Setpos_UnderAShiftFromAnExpressionResetBeforeAMotion_LeavesTheMachinePositionUnknown(string change)
    {
        VmHarness vm = MillTurn()
            .Execute("HOME X", "SHIFT X={$Q1}", "SETPOS X=100", "SHIFT=RESET", "LINE X=50", change);

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // VM 3.4, D101: nothing moves between the SETPOS and the reset of the SHIFT from an expression that stood before
    // it, so X returns to its machine position, and the next SETPOS records against it without the ERROR VM050.
    [Fact]
    public void Setpos_UnderAShiftFromAnExpressionResetWithoutAMotion_ReturnsTheAxisToItsMachinePosition()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SHIFT X={$Q1}", "SETPOS X=100", "SHIFT=RESET");

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(300m, vm.State.Frame.SetposShift["X"]);
    }

    // VM 3.4, D101: while the SHIFT from an expression of the SETPOS stands, the frame of the motion holds the unknown
    // shift as the frame of the SETPOS did, so the motion moves the machine by as much as the workpiece coordinate, and
    // ORIGIN returns X to the machine position the motion reached.
    [Fact]
    public void Setpos_UnderAShiftFromAnExpressionThatStillStands_AMotionKeepsTheMachinePosition()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SHIFT X={$Q1}", "SETPOS X=100", "LINE X=50", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // Third review of P1-03, finding 2 (VM 3.4, D57, D101): the frame of the sub spindle has +Z out of its own chuck,
    // and the machine reaches it through its mirror or datum convention, which readers and compilers apply outside the
    // VM. A motion under it leaves the machine position unknown, also when the SETPOS was taken under it.
    [Fact]
    public void Workpiece_AMotionUnderTheSubSpindleThatTookTheSetpos_LeavesTheMachinePositionUnknown()
    {
        VmHarness vm = MillTurn().Execute("HOME Z", "WORKPIECE=SUB", "SETPOS Z=0", "LINE Z=10", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 3.4, D57, D101: the setpos shift was declared in the frame of the sub spindle. Nothing moved, so the change
    // back to the default holder returns Z to its machine position; a motion under the default holder then moves in a
    // frame the SETPOS did not relate to the machine frame, and the machine position is unknown.
    [Fact]
    public void Workpiece_AMotionUnderTheDefaultHolderAfterASetposUnderTheSubSpindle_LeavesTheMachinePositionUnknown()
    {
        VmHarness vm = MillTurn().Execute("HOME Z", "WORKPIECE=SUB", "SETPOS Z=0", "WORKPIECE=MAIN");

        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));

        vm.Execute("LINE Z=10", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // Third review of P1-03, finding 3 (language 4.2, VM 3.4): ROTATE turns the working plane about the tool axis. A
    // motion of a rotary axis or along the tool axis moves neither axis of the plane, so the machine position of X
    // stays known through the record, the reset returns X to it, and the next SETPOS records against it without the
    // ERROR VM050.
    [Theory]
    [InlineData("RAPID C=10")]
    [InlineData("LINE Z=-5")]
    public void Rotate_AMotionThatMovesNoAxisOfThePlane_KeepsTheMachinePositionOfAPlaneAxis(string motion)
    {
        VmHarness vm = MillTurn().Execute(
            "SPINDLE_MODE:MAIN=AXIS", "HOME X", "ROTATE=30", "SETPOS X=100", motion, "ROTATE=RESET");

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(300m, vm.State.Frame.SetposShift["X"]);
    }

    // Third review of P1-03, finding 3 (language 4.2, VM 3.4): MIRROR=Y mirrors Y alone. A motion of X under it moves
    // the machine by as much as the workpiece coordinate, so MIRROR=OFF returns X to the machine position the motion
    // reached, and the next SETPOS records against it.
    [Fact]
    public void Mirror_OfAnotherAxis_AMotionOfTheAxisKeepsItsMachinePosition()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "SETPOS X=100", "MIRROR=Y", "LINE X=50", "MIRROR=OFF");

        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("SETPOS X=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(250m, vm.State.Frame.SetposShift["X"]);
    }

    // The workaround of the TODO(question) above FrameRules.Couples (D101): a TILT couples X, Y and Z, so after a
    // motion of Z under it the machine position that the record gives for X is taken as unknown, and the reset leaves
    // X unknown.
    [Fact]
    public void Tilt_AMotionOfAnotherLinearAxis_LeavesTheMachinePositionUnknown()
    {
        VmHarness vm = MillTurn().Execute("HOME X", "TILT B=45", "SETPOS X=100", "LINE Z=-5", "TILT=RESET");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // Language 4.2, VM 3.4: a motion of a rotary axis under a TILT moves no linear axis, so the reset returns X to its
    // machine position.
    [Fact]
    public void Tilt_AMotionOfARotaryAxis_KeepsTheMachinePositionOfALinearAxis()
    {
        VmHarness vm = MillTurn()
            .Execute("SPINDLE_MODE:MAIN=AXIS", "HOME X", "TILT B=45", "SETPOS X=100", "RAPID C=10", "TILT=RESET");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // Language 4.2, D31: ROTATE turns the working plane where it stands, the WORKPLANE of its own block included in
    // either order. Under WORKPLANE=ZX its plane is Z and X and its tool axis Y, so a motion of Y keeps the machine
    // position of X.
    [Theory]
    [InlineData("WORKPLANE=ZX ROTATE=30")]
    [InlineData("ROTATE=30 WORKPLANE=ZX")]
    public void Rotate_InTheZxPlane_AMotionAlongItsToolAxisKeepsTheMachinePositionOfX(string rotate)
    {
        VmHarness vm = MillTurn().Execute(rotate, "HOME X", "SETPOS X=100", "LINE Y=5");

        Assert.Equal(Workplane.ZX, Assert.Single(vm.State.Frame.Chain).Workplane);

        vm.Execute("ROTATE=RESET");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // Language 4.2, D31: a later WORKPLANE does not turn the rotation into another plane; ROTATE=30 under XY still
    // turns X and Y, so after WORKPLANE=ZX a motion of Y couples X, and the workaround of the TODO(question) above
    // FrameRules.Couples leaves the machine position of X unknown.
    [Fact]
    public void Rotate_AfterAChangeOfTheWorkplane_StillTurnsThePlaneWhereItStood()
    {
        VmHarness vm = MillTurn()
            .Execute("HOME X", "ROTATE=30", "SETPOS X=100", "WORKPLANE=ZX", "LINE Y=5", "ROTATE=RESET");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // Residual review of P1-03 (language 4.2, VM 3.4, 10, D101): SETPOS under a ROTATE, MIRROR, TILT or TILT_AXIS
    // declares the value in a frame that turns or mirrors X against the machine frame. Nothing moves, so the reset
    // returns X to its machine position; the declared value relates to the machine position only through a conversion
    // the kinematics module makes, so a motion of X after the reset leaves the machine position unknown, and ORIGIN
    // leaves X unknown instead of at a wrong machine position.
    [Theory]
    [InlineData("ROTATE=30", "ROTATE=RESET")]
    [InlineData("MIRROR=X", "MIRROR=OFF")]
    [InlineData("TILT B=45", "TILT=RESET")]
    [InlineData("TILT_AXIS B=45", "TILT_AXIS=RESET")]
    public void Setpos_InAFrameThatTurnsTheAxis_AMotionAfterTheResetLeavesTheMachinePositionUnknown(string change,
        string reset)
    {
        VmHarness vm = MillTurn().Execute("HOME X", change, "SETPOS X=100", reset);

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));

        vm.Execute("LINE X=50", "ORIGIN=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // Language 4.2, VM 3.4, D101: a ROTATE of the YZ plane turns Y and Z about X, and a MIRROR of Y mirrors Y alone, so
    // SETPOS X under them declares X through shifts alone; a motion of X keeps its machine position, and the reset
    // returns X to where the motion left it.
    [Theory]
    [InlineData("WORKPLANE=YZ ROTATE=30", "ROTATE=RESET")]
    [InlineData("MIRROR=Y", "MIRROR=OFF")]
    public void Setpos_InAFrameThatDoesNotTurnTheAxis_AMotionKeepsTheMachinePosition(string change, string reset)
    {
        VmHarness vm = MillTurn().Execute("HOME X", change, "SETPOS X=100", "LINE X=50", reset);

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(250m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // Language 4.2, VM 3.4: TILT tilts the frame about its axes X, Y and Z; the sub spindle slide Z2 is none of them,
    // so the workaround of the TODO(question) above FrameRules.Couples does not couple it with X, and the reset
    // returns Z2 to the machine position of its setpos shift.
    [Fact]
    public void Tilt_AMotionOfXUnderIt_KeepsTheMachinePositionOfAnAxisOutsideXYZ()
    {
        VmHarness vm = MillTurn().Execute("HOME Z2", "TILT B=45", "SETPOS Z2=10", "LINE X=50", "TILT=RESET");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), vm.Position("Z2"));
    }

    // The mill-turn machine file with reference points (X home 300, C home 90), the units and a feed set.
    private static VmHarness MillTurn()
    {
        return new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM F=100 SPINDLE:TOOL=CW");
    }
}
