using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The motion rows of virtual machine 2.2: their start values, and a snapshot that keeps the values it was taken with
/// while the live state changes. The start position of the axes is <see cref="StartPositionTests"/>.
/// </summary>
public sealed class MotionStateTests
{
    // VM 2.2: the verb of the block starts none.
    [Fact]
    public void BlockVerb_AtStart_IsNone()
    {
        Assert.Null(Motion().BlockVerb);
    }

    // VM 2.2: RAPID, LINE, ARC, RETRACT, HOME, CYCLE_CALL, SHIFT, TILT, TILT_AXIS and SETPOS set it.
    [Fact]
    public void BlockVerb_ChangedAfterASnapshot_SnapshotKeepsLine()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Motion.BlockVerb = Verb.Line;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Motion.BlockVerb = Verb.TiltAxis;

        Assert.Equal(Verb.Line, snapshot.Motion.BlockVerb);
        Assert.Equal(Verb.TiltAxis, state.Motion.BlockVerb);
    }

    // VM 2.2, D81: the tool vector and the surface normal start unknown.
    [Fact]
    public void ToolVectorAndSurfaceNormal_AtStart_AreUnknown()
    {
        MotionState motion = Motion();

        Assert.Null(motion.ToolVector);
        Assert.Null(motion.SurfaceNormal);
    }

    // VM 2.2, D81: TX TY TZ and NX NY NZ on a LINE under TCPM=ON set them; a rotary word makes them unknown again.
    [Fact]
    public void ToolVectorAndSurfaceNormal_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Motion.ToolVector = [0m, 0.5m, 0.866m];
        state.Motion.SurfaceNormal = [0m, 0m, 1m];

        ChannelSnapshot snapshot = state.Snapshot();
        state.Motion.ToolVector = null;
        state.Motion.SurfaceNormal = null;

        decimal[] toolVector = [0m, 0.5m, 0.866m];
        decimal[] surfaceNormal = [0m, 0m, 1m];
        Assert.Equal(toolVector, snapshot.Motion.ToolVector);
        Assert.Equal(surfaceNormal, snapshot.Motion.SurfaceNormal);
        Assert.Null(state.Motion.ToolVector);
    }

    // The snapshot copies the vector, so a list the live state still holds cannot change it.
    [Fact]
    public void ToolVector_ListChangedAfterASnapshot_SnapshotKeepsItsCopy()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        List<decimal> vector = [0m, 0m, 1m];
        state.Motion.ToolVector = vector;

        ChannelSnapshot snapshot = state.Snapshot();
        vector[2] = -1m;

        Assert.Equal(1m, snapshot.Motion.ToolVector![2]);
    }

    // VM 2.2, D53: the skip of the block starts none.
    [Fact]
    public void Skip_AtStart_IsNone()
    {
        MotionState motion = Motion();

        Assert.False(motion.Skip);
        Assert.Null(motion.SkipNumber);
    }

    // VM 2.2, D53: SKIP and SKIP=n set it.
    [Fact]
    public void Skip_ChangedAfterASnapshot_SnapshotKeepsSkipTwo()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Motion.Skip = true;
        state.Motion.SkipNumber = 2;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Motion.Skip = false;
        state.Motion.SkipNumber = null;

        Assert.True(snapshot.Motion.Skip);
        Assert.Equal(2, snapshot.Motion.SkipNumber);
        Assert.False(state.Motion.Skip);
    }

    // VM 2.2: feed.value starts none, feed.mode PER_MIN.
    [Fact]
    public void Feed_AtStart_IsNoneInPerMin()
    {
        MotionState motion = Motion();

        Assert.Null(motion.Feed);
        Assert.Equal(FeedMode.PerMin, motion.FeedMode);
    }

    // VM 2.2: F and FEED_MODE set them.
    [Fact]
    public void Feed_ChangedAfterASnapshot_SnapshotKeepsTheFeedAndItsMode()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Motion.Feed = 0.15m;
        state.Motion.FeedMode = FeedMode.PerRev;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Motion.Feed = 800m;
        state.Motion.FeedMode = FeedMode.PerMin;

        Assert.Equal(0.15m, snapshot.Motion.Feed);
        Assert.Equal(FeedMode.PerRev, snapshot.Motion.FeedMode);
        Assert.Equal(800m, state.Motion.Feed);
    }

    // VM 2.2: compensation starts OFF.
    [Fact]
    public void Compensation_AtStart_IsOff()
    {
        Assert.Equal(Compensation.Off, Motion().Comp);
    }

    // VM 2.2: COMP sets it.
    [Fact]
    public void Compensation_ChangedAfterASnapshot_SnapshotKeepsLeft()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Motion.Comp = Compensation.Left;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Motion.Comp = Compensation.Right;

        Assert.Equal(Compensation.Left, snapshot.Motion.Comp);
        Assert.Equal(Compensation.Right, state.Motion.Comp);
    }

    // VM 2.2: every executed motion sets the position of the axes it moves.
    [Fact]
    public void Position_ChangedAfterASnapshot_SnapshotKeepsThePosition()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Motion.Position["X"] = new AxisPosition(33.22m, PositionFrame.Workpiece, Known: true);

        ChannelSnapshot snapshot = state.Snapshot();
        state.Motion.Position["X"] = new AxisPosition(55.44m, PositionFrame.Workpiece, Known: true);

        Assert.Equal(new AxisPosition(33.22m, PositionFrame.Workpiece, Known: true), snapshot.Motion.Position["X"]);
        Assert.Equal(55.44m, state.Motion.Position["X"].Value);
    }

    // VM 2.2, 3.4: motions set the frame of each position, the polar frame under POLAR=ON (D102).
    [Fact]
    public void PositionFrame_ChangedAfterASnapshot_SnapshotKeepsThePolarFrame()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Motion.Position["C"] = new AxisPosition(17.32m, PositionFrame.Polar, Known: true);

        ChannelSnapshot snapshot = state.Snapshot();
        state.Motion.Position["C"] = new AxisPosition(0m, PositionFrame.Workpiece, Known: false);

        Assert.Equal(PositionFrame.Polar, snapshot.Motion.Position["C"].Frame);
        Assert.Equal(PositionFrame.Workpiece, state.Motion.Position["C"].Frame);
    }

    private static MotionState Motion()
    {
        return new ChannelState(StateMachines.MillTurn()).Motion;
    }
}
