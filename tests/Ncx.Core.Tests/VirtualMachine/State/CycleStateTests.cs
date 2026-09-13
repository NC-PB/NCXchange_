using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The cycle row of virtual machine 2.6: its start value, and a snapshot that keeps the cycle it was taken with while
/// the live state changes.
/// </summary>
public sealed class CycleStateTests
{
    // VM 2.6: cycle.name, axis and parameters start OFF, the tool axis of the workplane (Z for XY) and none.
    [Fact]
    public void Cycle_AtStart_IsOffAlongTheToolAxisWithoutParameters()
    {
        CycleState cycle = new ChannelState(StateMachines.MillTurn()).Cycle;

        Assert.Null(cycle.Name);
        Assert.False(cycle.Active);
        Assert.Null(cycle.Controller);
        Assert.Equal("Z", cycle.Axis);
        Assert.Empty(cycle.Parameters);
    }

    // VM 2.6: CYCLE with its parameter words and AXIS sets it; a new CYCLE replaces all parameters.
    [Fact]
    public void Cycle_ChangedAfterASnapshot_SnapshotKeepsNameAxisAndParameters()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        Word depth = new() { Key = "DEPTH", Value = new DecimalValue(-21.732m, "-21.732") };
        state.Cycle.Name = "DRILL";
        state.Cycle.Axis = "X";
        state.Cycle.Parameters.Add(depth);

        ChannelSnapshot snapshot = state.Snapshot();
        state.Cycle.Name = null;
        state.Cycle.Axis = "Z";
        state.Cycle.Parameters.Clear();

        Assert.Equal("DRILL", snapshot.Cycle.Name);
        Assert.True(snapshot.Cycle.Active);
        Assert.Equal("X", snapshot.Cycle.Axis);
        Assert.Equal(depth, Assert.Single(snapshot.Cycle.Parameters));
        Assert.False(state.Cycle.Active);
    }

    // VM 2.6, D94: CYCLE:HEIDENHAIN=251 sets the native cycle of that family with its parameters unresolved.
    [Fact]
    public void Cycle_NativeCycle_KeepsItsControllerAndNumber()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Cycle.Name = "251";
        state.Cycle.Controller = "HEIDENHAIN";
        state.Cycle.Parameters.Add(new Word { Key = "Q215", Value = new IntegerValue(0, "0") });

        CycleSnapshot cycle = state.Snapshot().Cycle;

        Assert.Equal("251", cycle.Name);
        Assert.Equal("HEIDENHAIN", cycle.Controller);
        Assert.Equal("Q215", Assert.Single(cycle.Parameters).Key);
    }

    // Language 4.2, VM 3.3: the tool axis of a workplane is the axis perpendicular to it.
    [Theory]
    [InlineData(Workplane.XY, "Z")]
    [InlineData(Workplane.ZX, "Y")]
    [InlineData(Workplane.YZ, "X")]
    public void ToolAxisOf_Workplane_IsTheAxisPerpendicularToIt(Workplane workplane, string toolAxis)
    {
        Assert.Equal(toolAxis, CycleState.ToolAxisOf(workplane));
    }
}
