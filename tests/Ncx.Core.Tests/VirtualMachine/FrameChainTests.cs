using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The frame chain (language 4.2, virtual machine 2.1, 3.4, D31): ORIGIN followed by the transform words in program
/// order, RESET cutting from the end, and what a change of the frame does to the position.
/// </summary>
public sealed class FrameChainTests
{
    // D31: ORIGIN=1, SHIFT Z=-5, TILT B=45 and ORIGIN=1, TILT B=45, SHIFT Z=-5 are different programs.
    [Fact]
    public void Chain_OriginShiftTiltAgainstOriginTiltShift_GivesDifferentChains()
    {
        FrameState shiftFirst = new VmHarness(VmMachines.Default())
            .Execute("ORIGIN=1", "SHIFT Z=-5", "TILT B=45").State.Frame;
        FrameState tiltFirst = new VmHarness(VmMachines.Default())
            .Execute("ORIGIN=1", "TILT B=45", "SHIFT Z=-5").State.Frame;

        Assert.Equal(new[] { TransformKind.Shift, TransformKind.Tilt }, Kinds(shiftFirst.Chain));
        Assert.Equal(new[] { TransformKind.Tilt, TransformKind.Shift }, Kinds(tiltFirst.Chain));
        Assert.NotEqual(Kinds(shiftFirst.Chain), Kinds(tiltFirst.Chain));
        Assert.Equal(-5m, shiftFirst.Chain[0].Shift["Z"]);
        Assert.Equal(45m, shiftFirst.Chain[1].Angles["B"]);
    }

    // VM 1, 3.4, wave-1 question #100: the chain entry of a SHIFT from an expression names the axes whose shift is
    // unknown; removing it folds an unknown shift back, so an axis known in the workpiece frame is unknown afterwards,
    // as appending it left it.
    [Fact]
    public void ShiftReset_OfAShiftFromAnExpression_LeavesTheShiftedAxisUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "SHIFT X={$Q1} Y=5", "RAPID X=50 Y=7");

        Assert.Equal(["X"], vm.State.Frame.Chain[0].UnknownShift);

        vm.Execute("SHIFT=RESET");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Equal(new AxisPosition(12m, PositionFrame.Workpiece, Known: true), vm.Position("Y"));
    }

    // Language 4.2, VM 2.1: SHIFT=RESET after a tilt removes the shift and the tilt appended after it.
    [Fact]
    public void ShiftReset_AfterATilt_RemovesTheShiftAndTheTiltAfterIt()
    {
        FrameState frame = new VmHarness(VmMachines.Default())
            .Execute("ORIGIN=1", "SHIFT Z=-5", "TILT B=45", "SHIFT=RESET").State.Frame;

        Assert.Empty(frame.Chain);
    }

    // VM 2.1: a RESET cuts the chain at the last entry of its kind; what stands before it stays.
    [Fact]
    public void Reset_TwoEntriesOfTheKind_CutsAtTheLastAndKeepsWhatStandsBefore()
    {
        FrameState frame = new VmHarness(VmMachines.Default())
            .Execute("SHIFT X=1", "TILT B=45", "SHIFT Z=2", "SHIFT=RESET").State.Frame;

        Assert.Equal(new[] { TransformKind.Shift, TransformKind.Tilt }, Kinds(frame.Chain));
        Assert.Equal(1m, frame.Chain[0].Shift["X"]);
    }

    // VM 2.1: a RESET of a kind the chain does not hold changes nothing.
    [Fact]
    public void Reset_NoEntryOfTheKind_LeavesTheChain()
    {
        FrameState frame = new VmHarness(VmMachines.Default()).Execute("SHIFT X=1", "TILT=RESET").State.Frame;

        Assert.Equal(new[] { TransformKind.Shift }, Kinds(frame.Chain));
    }

    // Language 4.2: TILT=RESET removes the tilt and what follows it.
    [Fact]
    public void TiltReset_RemovesTheTiltAndWhatFollows()
    {
        FrameState frame = new VmHarness(VmMachines.Default())
            .Execute("SHIFT X=1", "TILT B=45", "SHIFT Z=2", "TILT=RESET").State.Frame;

        Assert.Equal(new[] { TransformKind.Shift }, Kinds(frame.Chain));
        Assert.Equal(1m, frame.Chain[0].Shift["X"]);
    }

    // Language 4.2, D82: TILT_AXIS sits in the chain like TILT; TILT_AXIS=RESET removes it and what follows.
    [Fact]
    public void TiltAxisReset_RemovesTheTiltAxisAndWhatFollows()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TILT_AXIS A=-90 C=180", "SHIFT Z=2");
        Assert.Equal(new[] { TransformKind.TiltAxis, TransformKind.Shift }, Kinds(vm.State.Frame.Chain));

        vm.Execute("TILT_AXIS=RESET");

        Assert.Empty(vm.State.Frame.Chain);
        vm.AssertNoDiagnostics();
    }

    // Language 4.2: ROTATE and MIRROR are appended to the chain; ROTATE=RESET and MIRROR=OFF remove them and what
    // follows.
    [Fact]
    public void RotateAndMirror_AppendToTheChain_TheirResetFormsCutIt()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("ROTATE=30", "MIRROR=X,Y");
        FrameState frame = vm.State.Frame;

        string[] mirrored = ["X", "Y"];
        Assert.Equal(new[] { TransformKind.Rotate, TransformKind.Mirror }, Kinds(frame.Chain));
        Assert.Equal(30m, frame.Chain[0].Angle);
        Assert.Equal(mirrored, frame.Chain[1].Mirrored);

        vm.Execute("MIRROR=OFF");
        Assert.Equal(new[] { TransformKind.Rotate }, Kinds(frame.Chain));

        vm.Execute("ROTATE=RESET");
        Assert.Empty(frame.Chain);
    }

    // VM 2.1, 3.4: ORIGIN empties the chain and clears the setpos shifts.
    [Fact]
    public void Origin_AfterAChainAndASetpos_EmptiesTheChainAndClearsTheSetposShifts()
    {
        FrameState frame = new VmHarness(VmMachines.MillTurn())
            .Execute("HOME X", "SETPOS X=100", "SHIFT Z=1", "ORIGIN=2").State.Frame;

        Assert.Equal(2, frame.Origin);
        Assert.Empty(frame.Chain);
        Assert.Equal(0m, frame.SetposShift["X"]);
    }

    // VM 3.4: a SHIFT is folded into the position, newPos = oldPos - shift; omitted axes are 0, and the machine frame
    // does not move.
    [Fact]
    public void Shift_KnownPositions_AreFoldedIntoThePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .At("X", 50m, PositionFrame.Workpiece)
            .At("Y", 20m, PositionFrame.Workpiece)
            .At("Z", 100m, PositionFrame.Machine)
            .Execute("SHIFT X=10 Z=5");

        Assert.Equal(new AxisPosition(40m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
        Assert.Equal(new AxisPosition(20m, PositionFrame.Workpiece, Known: true), vm.Position("Y"));
        Assert.Equal(new AxisPosition(100m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // VM 3.4: a RESET that removes shifts folds them back.
    [Fact]
    public void ShiftReset_RemovingAShift_FoldsItBack()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .At("X", 50m, PositionFrame.Workpiece)
            .Execute("SHIFT X=10", "SHIFT=RESET");

        Assert.Equal(50m, vm.Position("X").Value);
    }

    // VM 3.4: TILT, TILT_AXIS, ROTATE and MIRROR mark the position unknown in the new frame without the kinematics
    // module; an axis known in the MACHINE frame stays known there.
    [Fact]
    public void Tilt_KnownPositions_BecomeUnknownOutsideTheMachineFrame()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .At("X", 50m, PositionFrame.Workpiece)
            .At("Z", 100m, PositionFrame.Machine)
            .Execute("TILT B=45");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Equal(new AxisPosition(100m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // VM 3.4: removing a tilt is a change of the frame as well.
    [Fact]
    public void TiltReset_KnownPositions_BecomeUnknownOutsideTheMachineFrame()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TILT B=45").At("X", 5m, PositionFrame.Workpiece);

        vm.Execute("TILT=RESET");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
    }

    // Language 4.2, D82: MOVE defaults to STAY and ROT to TABLE; both are kept on the entry.
    [Fact]
    public void Tilt_MoveAndRot_AreKeptOnTheEntryWithTheirDefaults()
    {
        FrameState frame = new VmHarness(VmMachines.Default())
            .Execute("TILT B=45", "TILT_AXIS B=30 MOVE=TURN ROT=COORD").State.Frame;

        Assert.Equal(TiltMove.Stay, frame.Chain[0].Move);
        Assert.Equal(TiltRot.Table, frame.Chain[0].Rot);
        Assert.Equal(TiltMove.Turn, frame.Chain[1].Move);
        Assert.Equal(TiltRot.Coord, frame.Chain[1].Rot);
    }

    // VM 3.4: MOVE=TURN on a spatial TILT moves the rotary axes to the plane; without the kinematics module they are
    // unknown.
    [Fact]
    public void TiltWithMoveTurn_RotaryAxes_BecomeUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("HOME B X", "TILT B=45 MOVE=TURN");

        Assert.Equal(AxisPosition.Unknown, vm.Position("B"));
        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), vm.Position("X"));
    }

    // VM 3.4: MOVE=STAY leaves the rotary axes where they are.
    [Fact]
    public void TiltWithMoveStay_RotaryAxes_Stay()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("HOME B", "TILT B=45 MOVE=STAY");

        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), vm.Position("B"));
    }

    // VM 3.4: after MOVE=TURN on a TILT_AXIS the rotary axes are known from its words.
    [Fact]
    public void TiltAxisWithMoveTurn_NamedRotaryAxes_AreKnownAtTheirAngles()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TILT_AXIS A=-90 C=180 MOVE=TURN");

        Assert.Equal(new AxisPosition(-90m, PositionFrame.Machine, Known: true), vm.Position("A"));
        Assert.Equal(new AxisPosition(180m, PositionFrame.Machine, Known: true), vm.Position("C"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("B"));
    }

    // VM 3.4, D57: a WORKPIECE change marks the position unknown in the new holder's frame.
    [Fact]
    public void Workpiece_AnotherHolder_MarksThePositionUnknownInItsFrame()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .At("X", 20m, PositionFrame.Workpiece)
            .Execute("WORKPIECE=SUB");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), vm.Position("Z"));
    }

    // VM 3.4: selecting the holder that already holds the part is no change.
    [Fact]
    public void Workpiece_TheSameHolder_KeepsThePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .At("X", 20m, PositionFrame.Workpiece)
            .Execute("WORKPIECE=MAIN");

        Assert.Equal(new AxisPosition(20m, PositionFrame.Workpiece, Known: true), vm.Position("X"));
    }

    // VM 3.4, D102: POLAR=OFF leaves X and C unknown in the workpiece frame until the next motion with known
    // coordinates.
    [Fact]
    public void PolarOff_PositionsInThePolarFrame_BecomeUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("POLAR=ON")
            .At("X", 27m, PositionFrame.Polar)
            .At("Z", 2m, PositionFrame.Workpiece)
            .Execute("POLAR=OFF");

        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Equal(new AxisPosition(2m, PositionFrame.Workpiece, Known: true), vm.Position("Z"));
    }

    // VM 3.4, D102: CYLINDER=OFF does the same for the cylinder frame.
    [Fact]
    public void CylinderOff_PositionsInTheCylinderFrame_BecomeUnknown()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("CYLINDER=30")
            .At("Z", -10m, PositionFrame.Cylinder)
            .Execute("CYLINDER=OFF");

        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
        Assert.Null(vm.State.Frame.Cylinder);
    }

    private static List<TransformKind> Kinds(IEnumerable<TransformEntry> chain)
    {
        var kinds = new List<TransformKind>();
        foreach (TransformEntry entry in chain)
        {
            kinds.Add(entry.Kind);
        }

        return kinds;
    }
}
