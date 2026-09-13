using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The start position rule (virtual machine 2.2, 3.4, D35, D100): an axis whose [[axis]] entry has home starts known
/// in the MACHINE frame at that reference point and unknown in the workpiece frame; an axis without home starts
/// unknown in every frame.
/// </summary>
public sealed class StartPositionTests
{
    // VM 2.2, 3.4, D100: home is the reference point in machine coordinates, the G53 / M91 frame.
    [Fact]
    public void StartPosition_AxisWithHome_IsKnownInTheMachineFrameAtItsReferencePoint()
    {
        MotionState motion = new ChannelState(StateMachines.MillTurn()).Motion;

        Assert.Equal(new AxisPosition(300m, PositionFrame.Machine, Known: true), motion.Position["X"]);
        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), motion.Position["Z"]);
        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), motion.Position["C"]);
        Assert.Equal(new AxisPosition(-20.5m, PositionFrame.Machine, Known: true), motion.Position["Z2"]);
    }

    // VM 2.2, 3.4, D35: known in the MACHINE frame, the axis is unknown in the workpiece frame.
    [Fact]
    public void StartPosition_AxisWithHome_IsUnknownInTheWorkpieceFrame()
    {
        MotionState motion = new ChannelState(StateMachines.MillTurn()).Motion;

        foreach (string axis in new[] { "X", "Z", "C", "Z2" })
        {
            Assert.NotEqual(PositionFrame.Workpiece, motion.Position[axis].Frame);
        }
    }

    // VM 2.2, 3.4: an axis without home starts unknown in every frame, its position frame UNKNOWN.
    [Fact]
    public void StartPosition_AxisWithoutHome_IsUnknownInEveryFrame()
    {
        MotionState motion = new ChannelState(StateMachines.MillTurn()).Motion;

        Assert.Equal(AxisPosition.Unknown, motion.Position["Y"]);
        Assert.Equal(AxisPosition.Unknown, motion.Position["C2"]);
        Assert.False(motion.Position["Y"].Known);
        Assert.Equal(PositionFrame.Unknown, motion.Position["C2"].Frame);
    }

    // D100: home is reference point 1; home2 is only for HOME with POINT=2.
    [Fact]
    public void StartPosition_AxisWithASecondReferencePoint_StartsAtTheFirst()
    {
        MotionState motion = new ChannelState(StateMachines.MillTurn()).Motion;

        Assert.Equal(300m, motion.Position["X"].Value);
    }

    // D103: the default machine has no reference points, so every axis starts unknown in every frame.
    [Fact]
    public void StartPosition_DefaultMachineOfD103_EveryAxisIsUnknownInEveryFrame()
    {
        MotionState motion = new ChannelState(StateMachines.Default()).Motion;

        Assert.Equal(6, motion.Position.Count);
        Assert.All(motion.Position.Values, position => Assert.Equal(AxisPosition.Unknown, position));
    }

    // Machine-config 4: every axis of [[axis]] has a position, by the NCX name the program writes and home belongs to.
    [Fact]
    public void StartPosition_EveryAxis_IsKeyedByItsNcxName()
    {
        MotionState motion = new ChannelState(StateMachines.MillTurn()).Motion;
        string[] axes = ["X", "Y", "Z", "C", "Z2", "C2"];

        Assert.Equal(axes, motion.Position.Keys);
    }

    // The start positions are part of the snapshot taken before the first block.
    [Fact]
    public void StartPosition_SnapshotBeforeTheFirstBlock_HoldsTheStartPositions()
    {
        ChannelSnapshot snapshot = new ChannelState(StateMachines.MillTurn()).Snapshot();

        Assert.Equal(new AxisPosition(450m, PositionFrame.Machine, Known: true), snapshot.Motion.Position["Z"]);
        Assert.Equal(AxisPosition.Unknown, snapshot.Motion.Position["Y"]);
    }
}
