using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The frame rows of virtual machine 2.1: their start values, and a snapshot that keeps the values it was taken with
/// while the live state changes.
/// </summary>
public sealed class FrameStateTests
{
    // VM 2.1: units start UNKNOWN.
    [Fact]
    public void Units_AtStart_AreUnknown()
    {
        Assert.Equal(Units.Unknown, Frame().Units);
    }

    // VM 2.1: UNITS sets them.
    [Fact]
    public void Units_ChangedAfterASnapshot_SnapshotKeepsMm()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.Units = Units.Mm;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.Units = Units.Inch;

        Assert.Equal(Units.Mm, snapshot.Frame.Units);
        Assert.Equal(Units.Inch, state.Frame.Units);
    }

    // VM 2.1: the workplane starts from TOML, XY.
    [Fact]
    public void Workplane_AtStart_IsXY()
    {
        Assert.Equal(Workplane.XY, Frame().Workplane);
    }

    // VM 2.1: WORKPLANE sets it.
    [Fact]
    public void Workplane_ChangedAfterASnapshot_SnapshotKeepsZX()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.Workplane = Workplane.ZX;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.Workplane = Workplane.YZ;

        Assert.Equal(Workplane.ZX, snapshot.Frame.Workplane);
        Assert.Equal(Workplane.YZ, state.Frame.Workplane);
    }

    // VM 2.1: the origin starts from TOML, 0.
    [Fact]
    public void Origin_AtStart_IsZero()
    {
        Assert.Equal(0, Frame().Origin);
    }

    // VM 2.1: ORIGIN sets it.
    [Fact]
    public void Origin_ChangedAfterASnapshot_SnapshotKeepsOne()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.Origin = 1;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.Origin = 2;

        Assert.Equal(1, snapshot.Frame.Origin);
        Assert.Equal(2, state.Frame.Origin);
    }

    // VM 2.1, D31: the transform chain starts empty.
    [Fact]
    public void TransformChain_AtStart_IsEmpty()
    {
        Assert.Empty(Frame().Chain);
    }

    // VM 2.1, D31: SHIFT, ROTATE, MIRROR, TILT and TILT_AXIS append an entry in program order.
    [Fact]
    public void TransformChain_AppendedAfterASnapshot_SnapshotKeepsTheEntriesItHad()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        TransformEntry shift = new()
        {
            Kind = TransformKind.Shift,
            Shift = new Dictionary<string, decimal?> { ["Z"] = -5m },
        };
        state.Frame.Chain.Add(shift);

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.Chain.Add(new TransformEntry
        {
            Kind = TransformKind.Tilt,
            Angles = new Dictionary<string, decimal?> { ["A"] = 0m, ["B"] = 45m, ["C"] = 0m },
        });

        Assert.Equal(shift, Assert.Single(snapshot.Frame.Chain));
        Assert.Equal(2, state.Frame.Chain.Count);
    }

    // VM 2.1, D31: ORIGIN empties the chain; the snapshot taken before keeps it.
    [Fact]
    public void TransformChain_EmptiedAfterASnapshot_SnapshotKeepsTheEntries()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.Chain.Add(new TransformEntry { Kind = TransformKind.Rotate, Angle = 30m });

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.Chain.Clear();

        Assert.Equal(30m, Assert.Single(snapshot.Frame.Chain).Angle);
        Assert.Empty(state.Frame.Chain);
    }

    // VM 2.1, 3.4: the setpos shift of every axis starts at 0.
    [Fact]
    public void SetposShift_AtStart_IsZeroOnEveryAxis()
    {
        FrameState frame = Frame();
        string[] axes = ["X", "Y", "Z", "C", "Z2", "C2"];

        Assert.Equal(axes, frame.SetposShift.Keys);
        Assert.All(frame.SetposShift.Values, shift => Assert.Equal(0m, shift));
    }

    // VM 2.1, 3.4: SETPOS records a shift per axis; ORIGIN clears it.
    [Fact]
    public void SetposShift_ChangedAfterASnapshot_SnapshotKeepsTheShift()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.SetposShift["C"] = 12.5m;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.SetposShift["C"] = 0m;

        Assert.Equal(12.5m, snapshot.Frame.SetposShift["C"]);
        Assert.Equal(0m, state.Frame.SetposShift["C"]);
    }

    // VM 2.1: cylinder, polar and tcpm start OFF.
    [Fact]
    public void CylinderPolarTcpm_AtStart_AreOff()
    {
        FrameState frame = Frame();

        Assert.Null(frame.Cylinder);
        Assert.False(frame.Polar);
        Assert.False(frame.Tcpm);
    }

    // VM 2.1, D96: CYLINDER=30 carries the reference radius; POLAR and TCPM switch on.
    [Fact]
    public void CylinderPolarTcpm_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.Cylinder = 30m;
        state.Frame.Polar = true;
        state.Frame.Tcpm = true;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.Cylinder = null;
        state.Frame.Polar = false;
        state.Frame.Tcpm = false;

        Assert.Equal(30m, snapshot.Frame.Cylinder);
        Assert.True(snapshot.Frame.Polar);
        Assert.True(snapshot.Frame.Tcpm);
        Assert.Null(state.Frame.Cylinder);
    }

    // VM 2.1, D86: rotary path and rotary feed start FULL and DEG_MIN.
    [Fact]
    public void RotaryPathAndFeed_AtStart_AreFullAndDegMin()
    {
        FrameState frame = Frame();

        Assert.Equal(RotaryPath.Full, frame.RotaryPath);
        Assert.Equal(RotaryFeed.DegMin, frame.RotaryFeed);
    }

    // VM 2.1, D86: ROTARY_PATH and ROTARY_FEED set them.
    [Fact]
    public void RotaryPathAndFeed_ChangedAfterASnapshot_SnapshotKeepsShortestAndMmMin()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.RotaryPath = RotaryPath.Shortest;
        state.Frame.RotaryFeed = RotaryFeed.MmMin;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.RotaryPath = RotaryPath.Full;
        state.Frame.RotaryFeed = RotaryFeed.DegMin;

        Assert.Equal(RotaryPath.Shortest, snapshot.Frame.RotaryPath);
        Assert.Equal(RotaryFeed.MmMin, snapshot.Frame.RotaryFeed);
    }

    // VM 2.1, D85: tolerance, rotary tolerance and tolerance mode start OFF, none and FINISH.
    [Fact]
    public void Tolerance_AtStart_IsOffWithoutARotaryToleranceInFinishMode()
    {
        ToleranceState tolerance = Frame().Tolerance;

        Assert.Null(tolerance.Value);
        Assert.Null(tolerance.Rotary);
        Assert.Equal(ToleranceMode.Finish, tolerance.Mode);
    }

    // VM 2.1, D85: TOLERANCE, TOLERANCE:ROTARY and TOLERANCE_MODE set them.
    [Fact]
    public void Tolerance_ChangedAfterASnapshot_SnapshotKeepsIt()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.Tolerance = new ToleranceState { Value = 0.02m, Rotary = 0.05m, Mode = ToleranceMode.Rough };

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.Tolerance = new ToleranceState { Value = null, Rotary = null, Mode = ToleranceMode.Finish };

        Assert.Equal(0.02m, snapshot.Frame.Tolerance.Value);
        Assert.Equal(0.05m, snapshot.Frame.Tolerance.Rotary);
        Assert.Equal(ToleranceMode.Rough, snapshot.Frame.Tolerance.Mode);
        Assert.Null(state.Frame.Tolerance.Value);
    }

    // VM 2.1: the frame of the block starts WORKPIECE.
    [Fact]
    public void FrameOfTheBlock_AtStart_IsTheWorkpieceFrame()
    {
        Assert.False(Frame().MachineFrameBlock);
    }

    // VM 2.1, D35: FRAME=MACHINE for one block.
    [Fact]
    public void FrameOfTheBlock_ChangedAfterASnapshot_SnapshotKeepsMachine()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.MachineFrameBlock = true;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.MachineFrameBlock = false;

        Assert.True(snapshot.Frame.MachineFrameBlock);
        Assert.False(state.Frame.MachineFrameBlock);
    }

    // VM 2.1: diameter starts OFF.
    [Fact]
    public void Diameter_AtStart_IsOff()
    {
        Assert.False(Frame().Diameter);
    }

    // VM 2.1, D60: DIAMETER sets it.
    [Fact]
    public void Diameter_ChangedAfterASnapshot_SnapshotKeepsOn()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.Diameter = true;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.Diameter = false;

        Assert.True(snapshot.Frame.Diameter);
        Assert.False(state.Frame.Diameter);
    }

    // VM 2.1: the workpiece holder starts at default_workpiece from TOML.
    [Fact]
    public void WorkpieceHolder_AtStart_IsTheDefaultWorkpiece()
    {
        Assert.Equal("S1", Frame().WorkpieceHolder);
    }

    // VM 2.1, D57: WORKPIECE sets it.
    [Fact]
    public void WorkpieceHolder_ChangedAfterASnapshot_SnapshotKeepsTheSubSpindle()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Frame.WorkpieceHolder = "S2";

        ChannelSnapshot snapshot = state.Snapshot();
        state.Frame.WorkpieceHolder = "S1";

        Assert.Equal("S2", snapshot.Frame.WorkpieceHolder);
        Assert.Equal("S1", state.Frame.WorkpieceHolder);
    }

    private static FrameState Frame()
    {
        return new ChannelState(StateMachines.MillTurn()).Frame;
    }
}
